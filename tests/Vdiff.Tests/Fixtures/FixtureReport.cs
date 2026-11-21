using System.Text;

namespace Vdiff.Tests.Fixtures;

/// <summary>One line of the fixture report.</summary>
public sealed record ReportRow(string Fixture, string Output, string Status, int? Due, string Detail);

/// <summary>
/// The fixture report: a markdown table, one row per fixture and output, that the CI job publishes so a
/// reader sees which outputs are red by design and which are green. Written only when
/// <see cref="Variable"/> names a path.
/// </summary>
public static class FixtureReport
{
    public const string Variable = "VDIFF_FIXTURE_REPORT";

    public static string Render(IEnumerable<ReportRow> rows)
    {
        var text = new StringBuilder();
        text.Append("| fixture | output | status | due | detail |\n");
        text.Append("| --- | --- | --- | --- | --- |\n");
        foreach (var row in rows
            .OrderBy(r => r.Fixture, StringComparer.Ordinal)
            .ThenBy(r => r.Output, StringComparer.Ordinal))
        {
            text.Append("| ").Append(row.Fixture)
                .Append(" | ").Append(row.Output)
                .Append(" | ").Append(row.Status)
                .Append(" | ").Append(row.Due is { } due ? due.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-")
                .Append(" | ").Append(row.Detail)
                .Append(" |\n");
        }

        return text.ToString();
    }

    /// <summary>Writes the table to the path in <see cref="Variable"/>; does nothing when it is unset or empty.</summary>
    public static void WriteIfRequested(IEnumerable<ReportRow> rows, Func<string, string?> getEnvironmentVariable)
    {
        var path = getEnvironmentVariable(Variable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(Render(rows)));
    }
}
