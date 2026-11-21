using System.Text;
using System.Text.RegularExpressions;
using Vdiff.Core.Process;
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

    // ---- refusals and leak checks ----

    const string OutRefusal = "VDIFF_PRIVATE_OUT must name a directory outside this repository";
    const string FixturesRefusal = "VDIFF_FIXTURES_DIR must be outside this repository";
    const string MissingRefusal = "VDIFF_FIXTURES_DIR must name an existing directory";
    const string NonShapeRefusal = "VDIFF_FIXTURES_DIR may hold only directories named after the five shapes";
    const string NoShapeRefusal = "VDIFF_FIXTURES_DIR holds no shape directory";
    const string AcceptRefusal = "accept mode is synthetic-only; unset VDIFF_FIXTURE_ACCEPT for a private run";

    static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_private_out_inside_this_repository_is_refused_naming_only_the_variable_and_is_not_created()
    {
        using var standIn = new StandIn();
        var inside = Path.Combine(RepoRoot.Find(), "out", "private-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunPrivate(standIn, inside);

            Assert.False(result.Passed);
            Assert.Equal([OutRefusal], result.Lines);
            Assert.False(Directory.Exists(inside));
        }
        finally
        {
            DeleteIfExists(inside);
        }
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void The_CLI_itself_refuses_an_out_inside_this_repository_when_the_runner_is_bypassed()
    {
        using var standIn = new StandIn();
        var inside = Path.Combine(RepoRoot.Find(), "out", "private-cli-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (outcomes, run) = PrivateFixtures.RunShape(
                FixtureLoader.Load(standIn.ShapeDir, FixtureKind.Private), inside, RepoRoot.Find());

            Assert.NotEqual(0, run.ExitCode);
            Assert.Equal(2, outcomes.Count);
            Assert.All(outcomes, o =>
            {
                Assert.Equal("FAIL", o.Row.Status);
                Assert.Equal("the tool run failed", o.Row.Detail);
            });
            Assert.False(Directory.Exists(inside));
        }
        finally
        {
            DeleteIfExists(inside);
        }
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_fixtures_directory_inside_this_repository_is_refused()
    {
        using var standIn = new StandIn();
        var inside = Path.Combine(RepoRoot.Find(), "tests", "fixtures");
        var privateOut = standIn.NewOut();

        var result = PrivateFixtures.Run(inside, privateOut, RepoRoot.Find(), RunDate, _ => null);

        Assert.False(result.Passed);
        Assert.Equal([FixturesRefusal], result.Lines);
        Assert.False(Directory.Exists(privateOut));
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_fixtures_directory_that_does_not_exist_is_refused()
    {
        using var standIn = new StandIn();
        var privateOut = standIn.NewOut();

        var result = PrivateFixtures.Run(Path.Combine(standIn.Root, "absent"), privateOut, RepoRoot.Find(), RunDate, _ => null);

        Assert.False(result.Passed);
        Assert.Equal([MissingRefusal], result.Lines);
        Assert.False(Directory.Exists(privateOut));
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_directory_that_is_not_a_shape_is_refused_without_echoing_its_name_and_no_shape_runs()
    {
        using var standIn = new StandIn();
        Directory.CreateDirectory(Path.Combine(standIn.FixturesDir, "SecretTypeName"));
        var privateOut = standIn.NewOut();

        var result = RunPrivate(standIn, privateOut);

        Assert.False(result.Passed);
        Assert.Equal([NonShapeRefusal], result.Lines);
        Assert.DoesNotContain("SecretTypeName", string.Join("\n", result.Lines), StringComparison.Ordinal);
        Assert.False(Directory.Exists(privateOut));
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_fixtures_directory_with_no_shape_directory_is_refused()
    {
        using var standIn = new StandIn();
        var empty = Path.Combine(standIn.Root, "empty");
        Directory.CreateDirectory(empty);
        var privateOut = standIn.NewOut();

        var result = PrivateFixtures.Run(empty, privateOut, RepoRoot.Find(), RunDate, _ => null);

        Assert.False(result.Passed);
        Assert.Equal([NoShapeRefusal], result.Lines);
        Assert.False(Directory.Exists(privateOut));
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void Accept_mode_is_refused_for_a_private_run()
    {
        using var standIn = new StandIn();
        var privateOut = standIn.NewOut();
        var goldenBefore = File.ReadAllBytes(Path.Combine(standIn.ShapeDir, "expected", "diff.json"));

        var result = PrivateFixtures.Run(
            standIn.FixturesDir, privateOut, RepoRoot.Find(), RunDate, name => name == AcceptMode.Variable ? "small-cpp" : null);

        Assert.False(result.Passed);
        Assert.Equal([AcceptRefusal], result.Lines);
        Assert.False(Directory.Exists(privateOut));
        Assert.Equal(goldenBefore, File.ReadAllBytes(Path.Combine(standIn.ShapeDir, "expected", "diff.json")));
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void An_unknown_key_in_a_private_fixture_is_a_FAIL_line_that_echoes_nothing_and_a_file_holds_the_error()
    {
        using var standIn = new StandIn();
        standIn.WritePrivate("pending", "pending", WrongDiff, WrongSummary, extraTop: "secret_widget: x\n");
        var privateOut = standIn.NewOut();

        var result = RunPrivate(standIn, privateOut);

        Assert.False(result.Passed);
        Assert.Equal(["small-cpp: FAIL 2026-11-20"], result.Lines);
        Assert.DoesNotContain("secret_widget", string.Join("\n", result.Lines), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(privateOut, "small-cpp", "error.txt")));
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void Every_console_line_is_a_shape_a_verdict_and_a_date_and_nothing_else()
    {
        using var standIn = new StandIn();

        var result = RunPrivate(standIn, standIn.NewOut());

        Assert.NotEmpty(result.Lines);
        foreach (var line in result.Lines)
        {
            Assert.Matches(@"^(small-cpp|csharp|cross-stack|extract-class|rename): (PASS|FAIL|PENDING) \d{4}-\d{2}-\d{2}$", line);
            Assert.DoesNotMatch("[0-9a-fA-F]{7,}", line);
            foreach (var secret in new[] { standIn.BaseId, standIn.HeadId, standIn.Root, standIn.RepoDir })
            {
                Assert.DoesNotContain(secret, line, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void Without_VDIFF_FIXTURES_DIR_the_environment_run_is_null_and_with_it_but_no_out_it_is_refused()
    {
        using var standIn = new StandIn();

        Assert.Null(PrivateFixtures.RunFromEnvironment(_ => null, RepoRoot.Find(), RunDate));

        var refused = PrivateFixtures.RunFromEnvironment(
            name => name == PrivateFixtures.FixturesDirVariable ? standIn.FixturesDir : null, RepoRoot.Find(), RunDate);
        Assert.NotNull(refused);
        Assert.False(refused.Passed);
        Assert.Equal([OutRefusal], refused.Lines);
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_private_out_that_cannot_be_created_gives_one_fixed_line_with_no_path()
    {
        using var standIn = new StandIn();
        var blocker = Path.Combine(standIn.Root, "a-file");
        File.WriteAllText(blocker, "x");

        var result = RunPrivate(standIn, Path.Combine(blocker, "results"));

        Assert.False(result.Passed);
        Assert.Equal(["the private run could not read or write its files; check VDIFF_FIXTURES_DIR and VDIFF_PRIVATE_OUT"], result.Lines);
        Assert.DoesNotContain(standIn.Root, string.Join('\n', result.Lines), StringComparison.OrdinalIgnoreCase);
    }

    static string RepositoryStatus()
    {
        var limits = new ProcessLimits(TimeSpan.FromSeconds(60), 16L * 1024 * 1024, 1024 * 1024);
        var result = GitCommand.Run(RepoRoot.Find(), ["status", "--porcelain", "--ignored"], limits);
        Assert.Equal(0, result.ExitCode);
        return result.StdoutText;
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void A_private_run_leaves_this_repository_status_unchanged()
    {
        using var standIn = new StandIn();
        var before = RepositoryStatus();

        RunPrivate(standIn, standIn.NewOut());

        Assert.Equal(before, RepositoryStatus());
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

    // ---- the recipe and the public record ----

    const string RecordHeader = "| shape | status | date |";
    const string RecordSeparator = "| --- | --- | --- |";

    static readonly Regex RecordRow = new(
        @"^\| (small-cpp|csharp|cross-stack|extract-class|rename) \| (PASS|FAIL|PENDING) \| [0-9]{4}-[0-9]{2}-[0-9]{2} \|$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// What is wrong with the text of <c>docs/trials/private-fixtures.md</c>, as fixed messages that never echo the
    /// text. Valid is: the header line and the separator line once each, every other table row a shape, a status
    /// and a date, at most one row per shape, no run of seven or more hex digits (a commit id) and no backslash (a
    /// path). Zero rows is valid.
    /// </summary>
    public static IReadOnlyList<string> RecordProblems(string text)
    {
        var problems = new List<string>();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Count(l => l == RecordHeader) != 1)
        {
            problems.Add("the header line must appear exactly once");
        }

        if (lines.Count(l => l == RecordSeparator) != 1)
        {
            problems.Add("the separator line must appear exactly once");
        }

        var shapes = new List<string>();
        foreach (var line in lines.Where(l => l.StartsWith('|') && l != RecordHeader && l != RecordSeparator))
        {
            var match = RecordRow.Match(line);
            if (match.Success)
            {
                shapes.Add(match.Groups[1].Value);
            }
            else
            {
                problems.Add("a table row is not a shape, a status and a date");
            }
        }

        if (shapes.GroupBy(s => s, StringComparer.Ordinal).Any(g => g.Count() > 1))
        {
            problems.Add("a shape has more than one row");
        }

        if (Regex.IsMatch(text, "[0-9a-fA-F]{7,}", RegexOptions.CultureInvariant))
        {
            problems.Add("the text holds a run of seven or more hex digits");
        }

        if (text.Contains('\\', StringComparison.Ordinal))
        {
            problems.Add("the text holds a backslash");
        }

        return problems;
    }

    static string ReadDocument(params string[] relative) =>
        File.ReadAllText(Path.Combine([RepoRoot.Find(), .. relative])).Replace("\r\n", "\n", StringComparison.Ordinal);

    static string Table(params string[] rows) =>
        "# Private fixtures\n\n" + RecordHeader + "\n" + RecordSeparator + "\n" + string.Concat(rows.Select(r => r + "\n"));

    [Fact]
    [Trait("Category", "Fixtures")]
    public void The_private_fixture_recipe_names_its_variables_and_record()
    {
        var text = ReadDocument("docs", "how-to", "private-fixtures.md");

        foreach (var required in new[]
        {
            PrivateFixtures.FixturesDirVariable,
            PrivateFixtures.OutVariable,
            "Category=PrivateFixtures",
            "docs/trials/private-fixtures.md",
        })
        {
            Assert.Contains(required, text, StringComparison.Ordinal);
        }

        // Built by concatenation so that this file does not itself hold a remote address (RepoHygieneTests).
        Assert.DoesNotContain("git" + "@", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ssh:" + "//", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Fixtures")]
    public void The_private_record_holds_only_shape_status_and_date()
    {
        var problems = RecordProblems(ReadDocument("docs", "trials", "private-fixtures.md"));

        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    public static TheoryData<bool, string> RecordCases() => new()
    {
        { true, Table() },
        { true, Table("| rename | PASS | 2026-11-20 |") },
        { false, Table("| rename | TBD | 2026-11-20 |") },
        { false, Table("| rename | PASS | - |") },
        { false, Table("| widget | PASS | 2026-11-20 |") },
        { false, Table("| rename | PASS | 2026-11-20 |", "| rename | FAIL | 2026-11-21 |") },
        { false, Table("| rename | PASS | 2026-11-20 | " + new string('a', 40) + " |") },
        { false, Table("| rename | PASS | 2026-11-20 |").Replace("# Private", "# D:\\Private", StringComparison.Ordinal) },
    };

    [Theory]
    [Trait("Category", "Fixtures")]
    [MemberData(nameof(RecordCases))]
    public void The_record_check_accepts_header_only_and_one_row_per_shape_and_rejects_the_rest(bool valid, string text)
    {
        Assert.Equal(valid, RecordProblems(text).Count == 0);
    }
}
