using System.Text;

namespace Vdiff.Tests.Fixtures;

/// <summary>
/// The expected-failure ratchet, the report table and the shape names (D-10, D-18). Pure functions:
/// nothing here touches process state, so these tests share no collection. The vacuity rows are the
/// point of the file: a crashed run must read FAIL for every state, never RED (T-02-10).
/// </summary>
[Trait("Category", "Fixtures")]
public class RatchetTests
{
    static OutputCheck AllGood(bool matched) => new(true, true, true, true, matched);

    // ---- the four-case truth table (all checks true) ----

    [Theory]
    [InlineData("pending", false, true, "RED")]
    [InlineData("pending", true, false, "FAIL")]
    [InlineData("green", true, true, "GREEN")]
    [InlineData("green", false, false, "FAIL")]
    public void The_truth_table_has_four_cases(string state, bool matched, bool passed, string status)
    {
        var outcome = Ratchet.Evaluate(state, null, AllGood(matched));

        Assert.Equal(passed, outcome.Passed);
        Assert.Equal(status, outcome.Status);
    }

    [Fact]
    public void Pending_with_a_match_says_it_unexpectedly_matches()
    {
        var outcome = Ratchet.Evaluate(Ratchet.Pending, 5, AllGood(matched: true));

        Assert.StartsWith("unexpectedly matches", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Green_details_are_fixed_text()
    {
        Assert.Equal("matches", Ratchet.Evaluate(Ratchet.Green, null, AllGood(true)).Detail);
        Assert.Equal("does not match the golden", Ratchet.Evaluate(Ratchet.Green, null, AllGood(false)).Detail);
    }

    // ---- vacuity: a broken run is FAIL for every state, whatever Matched says ----

    public static TheoryData<string, int, bool> VacuityRows()
    {
        var rows = new TheoryData<string, int, bool>();
        foreach (var state in new[] { "pending", "green" })
        {
            for (var broken = 0; broken < 4; broken++)
            {
                foreach (var matched in new[] { true, false })
                {
                    rows.Add(state, broken, matched);
                }
            }
        }

        return rows;
    }

    static OutputCheck WithOneBroken(int broken, bool matched) => new(
        ToolSucceeded: broken != 0,
        GoldenPresent: broken != 1,
        OutputPresent: broken != 2,
        SchemaValid: broken != 3,
        Matched: matched);

    [Theory]
    [MemberData(nameof(VacuityRows))]
    public void A_failed_run_missing_golden_missing_output_or_invalid_output_is_FAIL_never_RED(
        string state, int broken, bool matched)
    {
        var outcome = Ratchet.Evaluate(state, 3, WithOneBroken(broken, matched));

        Assert.False(outcome.Passed);
        Assert.Equal("FAIL", outcome.Status);
        Assert.NotEqual("RED", outcome.Status);
    }

    [Theory]
    [InlineData(0, "the tool run failed")]
    [InlineData(1, "the expected file is missing")]
    [InlineData(2, "the tool wrote no such output")]
    [InlineData(3, "the output does not validate against its schema")]
    public void Each_broken_check_has_its_own_fixed_detail(int broken, string detail)
    {
        Assert.Equal(detail, Ratchet.Evaluate(Ratchet.Pending, null, WithOneBroken(broken, false)).Detail);
    }

    [Fact]
    public void With_several_checks_false_the_first_in_tool_golden_output_schema_order_is_named()
    {
        Assert.Equal(
            "the tool run failed",
            Ratchet.Evaluate(Ratchet.Pending, null, new OutputCheck(false, false, false, false, false)).Detail);
        Assert.Equal(
            "the expected file is missing",
            Ratchet.Evaluate(Ratchet.Green, null, new OutputCheck(true, false, false, false, true)).Detail);
        Assert.Equal(
            "the tool wrote no such output",
            Ratchet.Evaluate(Ratchet.Green, null, new OutputCheck(true, true, false, false, true)).Detail);
    }

    // ---- unknown state, due text ----

    [Theory]
    [InlineData("due")]
    [InlineData("")]
    [InlineData("Pending")]
    [InlineData("GREEN")]
    public void An_unknown_state_is_FAIL_with_a_detail_that_does_not_echo_it(string state)
    {
        var outcome = Ratchet.Evaluate(state, 5, AllGood(false));

        Assert.False(outcome.Passed);
        Assert.Equal("FAIL", outcome.Status);
        Assert.Equal("unknown state in fixture.yml", outcome.Detail);
    }

    [Fact]
    public void The_RED_detail_shows_the_due_phase_and_never_compares_it()
    {
        Assert.Equal("pending, due phase 5", Ratchet.Evaluate(Ratchet.Pending, 5, AllGood(false)).Detail);
        Assert.Equal("pending", Ratchet.Evaluate(Ratchet.Pending, null, AllGood(false)).Detail);

        // due is display only: a due of 0 or of a far-off phase gives the same status.
        Assert.Equal("RED", Ratchet.Evaluate(Ratchet.Pending, 0, AllGood(false)).Status);
        Assert.Equal("RED", Ratchet.Evaluate(Ratchet.Pending, 9, AllGood(false)).Status);
    }

    // ---- the report table ----

    [Fact]
    public void Render_gives_a_header_a_separator_and_rows_sorted_by_fixture_then_output()
    {
        var rows = new[]
        {
            new ReportRow("rename", "diff.json", "GREEN", null, "matches"),
            new ReportRow("csharp", "head.model.json", "RED", 6, "pending, due phase 6"),
            new ReportRow("csharp", "base.model.json", "FAIL", null, "the tool run failed"),
        };

        var text = FixtureReport.Render(rows);

        Assert.Equal(
            "| fixture | output | status | due | detail |\n" +
            "| --- | --- | --- | --- | --- |\n" +
            "| csharp | base.model.json | FAIL | - | the tool run failed |\n" +
            "| csharp | head.model.json | RED | 6 | pending, due phase 6 |\n" +
            "| rename | diff.json | GREEN | - | matches |\n",
            text);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public void Render_sorts_ordinally()
    {
        var rows = new[]
        {
            new ReportRow("b", "x", "GREEN", null, "matches"),
            new ReportRow("B", "x", "GREEN", null, "matches"),
        };

        var lines = FixtureReport.Render(rows).Split('\n');

        Assert.StartsWith("| B |", lines[2], StringComparison.Ordinal);
        Assert.StartsWith("| b |", lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void Render_with_no_rows_is_just_the_header_and_separator()
    {
        Assert.Equal(
            "| fixture | output | status | due | detail |\n| --- | --- | --- | --- | --- |\n",
            FixtureReport.Render([]));
    }

    [Fact]
    public void WriteIfRequested_writes_nothing_when_the_variable_is_unset_or_empty()
    {
        var asked = new List<string>();
        var row = new[] { new ReportRow("a", "b", "GREEN", null, "matches") };

        FixtureReport.WriteIfRequested(row, name => { asked.Add(name); return null; });
        FixtureReport.WriteIfRequested(row, name => "");

        Assert.Equal(["VDIFF_FIXTURE_REPORT"], asked);
        Assert.Equal("VDIFF_FIXTURE_REPORT", FixtureReport.Variable);
    }

    [Fact]
    public void WriteIfRequested_writes_the_rendered_table_as_UTF8_without_a_BOM_creating_the_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "vdiff-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(root, "nested", "report.md");
            var rows = new[]
            {
                new ReportRow("small-cpp", "diff.json", "RED", 5, "pending, due phase 5"),
                new ReportRow("csharp", "diff.json", "GREEN", null, "matches"),
            };

            FixtureReport.WriteIfRequested(rows, name => name == FixtureReport.Variable ? path : null);

            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new UTF8Encoding(false).GetBytes(FixtureReport.Render(rows)), bytes);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "BOM written");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // ---- shape names ----

    [Fact]
    public void Shapes_All_is_the_five_names_in_order()
    {
        Assert.Equal(["small-cpp", "csharp", "cross-stack", "extract-class", "rename"], Shapes.All);
    }

    [Theory]
    [InlineData("small-cpp", true)]
    [InlineData("csharp", true)]
    [InlineData("cross-stack", true)]
    [InlineData("extract-class", true)]
    [InlineData("rename", true)]
    [InlineData("Rename", false)]
    [InlineData("", false)]
    [InlineData("tracer", false)]
    public void IsShape_is_ordinal(string name, bool expected)
    {
        Assert.Equal(expected, Shapes.IsShape(name));
    }
}
