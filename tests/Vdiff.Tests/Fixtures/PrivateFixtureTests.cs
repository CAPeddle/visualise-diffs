using System.Text;
using Vdiff.Tests.Support;
using Xunit.Abstractions;

namespace Vdiff.Tests.Fixtures;

/// <summary>
/// Private mode (D-12, D-13, D-14, ACC-02). The entry test reads the environment and runs the private fixtures a
/// maintainer in the target organisation points it at; every other test proves a behaviour on a synthetic
/// stand-in built under the temporary directory, outside this checkout, because no private fixture may ever
/// exist in this repository. The class runs the real CLI and sets environment variables, so it joins the
/// sidecar collection.
/// </summary>
[Collection("sidecar")]
public class PrivateFixtureTests(ITestOutputHelper output)
{
    static readonly DateOnly RunDate = new(2026, 11, 20);

    const string BaseSource = "namespace synthetic { struct Alpha { int x; }; }\n";
    const string HeadSource = "namespace synthetic { struct Alpha { int x; int y; }; }\n";
    const string WrongDiff = "deliberately wrong diff\n";
    const string WrongSummary = "deliberately wrong summary\n";

    static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
    }

    /// <summary>A fixture.yml with both outputs in the given states, and optional extra top-level lines.</summary>
    static string Yaml(string diffState, string summaryState, string top) =>
        "shape: small-cpp\n" +
        "description: one struct gains a field\n" +
        top +
        "expected:\n" +
        "  diff:\n" +
        "    state: " + diffState + "\n" +
        "    due: 5\n" +
        "  summary:\n" +
        "    state: " + summaryState + "\n" +
        "    due: 5\n";

    /// <summary>
    /// A stand-in for a private clone and its fixture: a small repository with two commits, built by the same
    /// code as a synthetic fixture, and a private fixtures directory beside it that names that repository and
    /// both commits. All of it lives under one temporary root outside the checkout.
    /// </summary>
    sealed class StandIn : IDisposable
    {
        readonly SyntheticRepo _repo;
        int _twins;

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "vdiff-pv-" + Guid.NewGuid().ToString("N"));
        public string BaseId { get; }
        public string HeadId { get; }
        public LoadedFixture Twin { get; }
        public string FixturesDir => Path.Combine(Root, "private");
        public string ShapeDir => Path.Combine(FixturesDir, "small-cpp");
        public string RepoDir => _repo.RepoDir;

        public StandIn()
        {
            Directory.CreateDirectory(Root);
            Twin = WriteTwin(WrongDiff, WrongSummary, "pending", "pending");
            (_repo, BaseId, HeadId) = FixtureHarness.BuildSyntheticRepo(Twin);
            WritePrivate("pending", "pending", WrongDiff, WrongSummary);
        }

        /// <summary>A path for a private output directory. It is not created: the runner creates it.</summary>
        public string NewOut() => Path.Combine(Root, "out-" + Guid.NewGuid().ToString("N"));

        /// <summary>The synthetic twin of the private fixture: the same trees, goldens and states, carried as source.</summary>
        public LoadedFixture WriteTwin(string diffGolden, string summaryGolden, string diffState, string summaryState)
        {
            var dir = Path.Combine(Root, "twin-" + _twins++, "small-cpp");
            WriteText(Path.Combine(dir, "fixture.yml"), Yaml(diffState, summaryState, ""));
            WriteText(Path.Combine(dir, "base", "include", "a.h"), BaseSource);
            WriteText(Path.Combine(dir, "head", "include", "a.h"), HeadSource);
            WriteText(Path.Combine(dir, "expected", "diff.json"), diffGolden);
            WriteText(Path.Combine(dir, "expected", "summary.mmd"), summaryGolden);
            return FixtureLoader.Load(dir, FixtureKind.Synthetic);
        }

        public string Identity() =>
            "repo: '" + RepoDir + "'\n" +
            "base: " + BaseId + "\n" +
            "head: " + HeadId + "\n";

        /// <summary>Writes (or rewrites) the private small-cpp fixture: its fixture.yml and its expected/ files.</summary>
        public void WritePrivate(string diffState, string summaryState, string diffGolden, string summaryGolden, string extraTop = "")
        {
            WriteText(Path.Combine(ShapeDir, "fixture.yml"), Yaml(diffState, summaryState, Identity() + extraTop));
            WriteText(Path.Combine(ShapeDir, "expected", "diff.json"), diffGolden);
            WriteText(Path.Combine(ShapeDir, "expected", "summary.mmd"), summaryGolden);
        }

        public void Dispose()
        {
            _repo.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    PrivateRunResult RunPrivate(StandIn standIn, string privateOut)
    {
        var result = PrivateFixtures.Run(standIn.FixturesDir, privateOut, RepoRoot.Find(), RunDate, _ => null);
        foreach (var line in result.Lines)
        {
            output.WriteLine(line);
        }

        return result;
    }

    [Fact]
    [Trait("Category", "PrivateFixtures")]
    public void Private_fixtures_from_the_environment()
    {
        var result = PrivateFixtures.RunFromEnvironment(
            Environment.GetEnvironmentVariable, RepoRoot.Find(), DateOnly.FromDateTime(DateTime.UtcNow));
        if (result is null)
        {
            return;
        }

        foreach (var line in result.Lines)
        {
            output.WriteLine(line);
        }

        Assert.True(
            result.Passed,
            "one or more private fixtures failed or were refused; read the files under VDIFF_PRIVATE_OUT");
    }

    // ---- the run over a stand-in clone ----

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_private_run_over_a_stand_in_clone_prints_one_pending_line()
    {
        using var standIn = new StandIn();

        var result = RunPrivate(standIn, standIn.NewOut());

        Assert.True(result.Passed);
        Assert.Equal(["small-cpp: PENDING 2026-11-20"], result.Lines);
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_private_run_judges_exactly_as_the_synthetic_harness_does()
    {
        using var standIn = new StandIn();
        var privateOut = standIn.NewOut();

        var (privateOutcomes, _) = PrivateFixtures.RunShape(
            FixtureLoader.Load(standIn.ShapeDir, FixtureKind.Private), Path.Combine(privateOut, "out"), RepoRoot.Find());
        var synthetic = FixtureHarness.RunSynthetic(standIn.Twin, accept: false);

        Assert.Equal(2, privateOutcomes.Count);
        Assert.Equal(
            synthetic.Outcomes.Select(o => (o.Row.Output, o.Row.Status, o.Row.Detail, o.Passed)),
            privateOutcomes.Select(o => (o.Row.Output, o.Row.Status, o.Row.Detail, o.Passed)));
        Assert.All(privateOutcomes, o => Assert.Equal("RED", o.Row.Status));
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void Goldens_equal_to_the_real_output_with_green_states_give_a_PASS_line()
    {
        using var standIn = new StandIn();
        var first = standIn.NewOut();
        RunPrivate(standIn, first);
        var diff = File.ReadAllBytes(Path.Combine(first, "small-cpp", "out", "diff.json"));
        var summary = File.ReadAllBytes(Path.Combine(first, "small-cpp", "out", "summary.mmd"));
        var utf8 = new UTF8Encoding(false, true);
        standIn.WritePrivate("green", "green", utf8.GetString(diff), utf8.GetString(summary));

        var result = RunPrivate(standIn, standIn.NewOut());

        Assert.True(result.Passed);
        Assert.Equal(["small-cpp: PASS 2026-11-20"], result.Lines);
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_private_run_writes_its_details_only_under_the_private_out_directory()
    {
        using var standIn = new StandIn();
        var privateOut = standIn.NewOut();

        RunPrivate(standIn, privateOut);

        Assert.Equal("small-cpp: PENDING 2026-11-20\n", File.ReadAllText(Path.Combine(privateOut, "private-run.txt")));
        Assert.True(File.Exists(Path.Combine(privateOut, "fixture-report.md")));
        Assert.True(File.Exists(Path.Combine(privateOut, "small-cpp", "out", "diff.json")));
        Assert.True(File.Exists(Path.Combine(privateOut, "small-cpp", "diff.diff")));
    }

    // ---- the aggregation rule ----

    static FixtureOutcome Outcome(string status) =>
        new(new ReportRow("small-cpp", "diff", status, null, ""), status != "FAIL", null);

    [Theory]
    [Trait("Category", "Fixtures")]
    [InlineData("PASS", "GREEN", "GREEN")]
    [InlineData("PENDING", "RED", "GREEN")]
    [InlineData("PENDING", "RED", "RED")]
    [InlineData("FAIL", "FAIL", "GREEN")]
    [InlineData("FAIL", "RED", "FAIL")]
    [InlineData("FAIL", "FAIL", "FAIL")]
    public void Aggregate_is_FAIL_over_PENDING_over_PASS(string expected, string first, string second)
    {
        Assert.Equal(expected, PrivateFixtures.Aggregate([Outcome(first), Outcome(second)]));
    }
}
