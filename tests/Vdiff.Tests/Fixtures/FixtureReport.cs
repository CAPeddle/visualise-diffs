namespace Vdiff.Tests.Fixtures;

/// <summary>One line of the fixture report.</summary>
public sealed record ReportRow(string Fixture, string Output, string Status, int? Due, string Detail);

public static class FixtureReport
{
    public const string Variable = "VDIFF_FIXTURE_REPORT";

    public static string Render(IEnumerable<ReportRow> rows) => throw new NotImplementedException();

    public static void WriteIfRequested(IEnumerable<ReportRow> rows, Func<string, string?> getEnvironmentVariable) =>
        throw new NotImplementedException();
}
