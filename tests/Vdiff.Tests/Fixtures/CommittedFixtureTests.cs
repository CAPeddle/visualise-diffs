using Vdiff.Tests.Support;
using Xunit.Abstractions;

namespace Vdiff.Tests.Fixtures;

/// <summary>
/// Runs every committed fixture through the harness (D-05, D-10, success criterion 3). While every output is
/// pending the run passes and the report lists each fixture and output as RED: red by design, visible, and
/// never silently skipped. The class drives the real CLI, so it joins the sidecar collection.
/// </summary>
[Collection("sidecar")]
[Trait("Category", "Fixtures")]
public class CommittedFixtureTests(ITestOutputHelper output)
{
    [Fact]
    public void Committed_fixtures_pass_the_expected_failure_ratchet()
    {
        var root = Path.Combine(RepoRoot.Find(), "tests", "fixtures");

        var outcomes = FixtureHarness.RunAll(root, output.WriteLine, Environment.GetEnvironmentVariable);

        output.WriteLine(FixtureReport.Render(outcomes.Select(o => o.Row)));
        Assert.NotEmpty(outcomes);
        var failing = outcomes.Where(o => !o.Passed).ToList();
        Assert.True(
            failing.Count == 0,
            "fixture outputs that broke the ratchet:\n"
            + string.Join('\n', failing.Select(o => $"{o.Row.Fixture} {o.Row.Output}: {o.Row.Status}: {o.Row.Detail}")));
    }

    [Fact]
    public void Every_fixture_directory_is_a_shape()
    {
        var root = Path.Combine(RepoRoot.Find(), "tests", "fixtures");
        Assert.True(Directory.Exists(root), "tests/fixtures is missing");

        var strays = Directory.EnumerateDirectories(root)
            .Select(d => Path.GetFileName(d))
            .Where(n => !Shapes.IsShape(n))
            .ToList();

        Assert.True(strays.Count == 0, "directories under tests/fixtures that are not one of the five shape names: " + string.Join(", ", strays));
    }

    [Fact]
    public void All_five_shapes_have_a_fixture()
    {
        var root = Path.Combine(RepoRoot.Find(), "tests", "fixtures");
        Assert.True(Directory.Exists(root), "tests/fixtures is missing");

        var present = Directory.EnumerateDirectories(root)
            .Select(d => Path.GetFileName(d))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var expected = Shapes.All.OrderBy(n => n, StringComparer.Ordinal).ToList();

        var missing = expected.Except(present, StringComparer.Ordinal).ToList();
        var extra = present.Except(expected, StringComparer.Ordinal).ToList();
        Assert.True(
            missing.Count == 0 && extra.Count == 0,
            "tests/fixtures must hold exactly the five shape directories; missing: ["
            + string.Join(", ", missing) + "], extra: [" + string.Join(", ", extra) + "]");
        Assert.Equal(expected, present);
    }
}
