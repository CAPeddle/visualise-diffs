using System.Globalization;
using System.Text;
using Vdiff.Core;
using Vdiff.Core.Cli;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Fixtures;

/// <summary>
/// What a private run reports: the console lines (one per shape, or one refusal) and whether the run passed.
/// A line never carries more than a shape name, a verdict and a date (D-14).
/// </summary>
public sealed record PrivateRunResult(IReadOnlyList<string> Lines, bool Passed);

/// <summary>
/// Private mode (D-12, D-13, D-14, ACC-02): the golden-fixture harness run over fixture directories outside this
/// repository. A private fixture names a local clone and two commits instead of carrying source, and the CLI reads
/// the clone's git objects read-only, so nothing from the clone is checked out or copied. The comparison is the
/// harness's own <see cref="FixtureHarness.CliArguments"/>, <see cref="FixtureHarness.RunCli"/> and
/// <see cref="FixtureHarness.Evaluate"/>, so a private run tests exactly what CI tests. Everything that could carry
/// target content (diffs, CLI errors, exception text) goes to files under the private output directory; the console
/// gets the shape, PASS, FAIL or PENDING, and a date.
/// </summary>
public static class PrivateFixtures
{
    public const string FixturesDirVariable = "VDIFF_FIXTURES_DIR";
    public const string OutVariable = "VDIFF_PRIVATE_OUT";

    const string Pass = "PASS";
    const string Pending = "PENDING";
    const string Fail = "FAIL";

    /// <summary>FAIL when any output failed the ratchet, else PENDING when any output is RED, else PASS.</summary>
    public static string Aggregate(IReadOnlyList<FixtureOutcome> outcomes)
    {
        if (outcomes.Any(o => !o.Passed))
        {
            return Fail;
        }

        return outcomes.Any(o => string.Equals(o.Row.Status, "RED", StringComparison.Ordinal)) ? Pending : Pass;
    }

    /// <summary>
    /// Runs the CLI on one private fixture with this repository's root protected, so the CLI itself refuses an
    /// <c>--out</c> inside it, and judges the outputs. The caller must not log the outcomes' mismatch diffs.
    /// </summary>
    public static (IReadOnlyList<FixtureOutcome> Outcomes, FixtureRun Run) RunShape(LoadedFixture f, string outDir, string repoRoot)
    {
        var args = FixtureHarness.CliArguments(f, f.File.Repo!, f.File.Base!, f.File.Head!, outDir);
        FixtureRun run;
        using (new EnvironmentScope(new Dictionary<string, string?> { [CliApp.ProtectedDirectoriesVariable] = repoRoot }))
        {
            run = FixtureHarness.RunCli(args, outDir);
        }

        return (FixtureHarness.Evaluate(f, run), run);
    }

    /// <summary>
    /// Null when the run may start; otherwise the first applicable fixed reason it must not (D-06, D-14). Nothing
    /// from a private fixture is echoed: not a directory name, not a path.
    /// </summary>
    public static string? Refusal(
        string fixturesDir, string privateOut, string repoRoot, Func<string, string?> getEnvironmentVariable)
    {
        if (!string.IsNullOrEmpty(getEnvironmentVariable(AcceptMode.Variable)))
        {
            return "accept mode is synthetic-only; unset VDIFF_FIXTURE_ACCEPT for a private run";
        }

        if (string.IsNullOrEmpty(fixturesDir) || !Directory.Exists(fixturesDir))
        {
            return "VDIFF_FIXTURES_DIR must name an existing directory";
        }

        if (IsInside(fixturesDir, repoRoot))
        {
            return "VDIFF_FIXTURES_DIR must be outside this repository";
        }

        if (string.IsNullOrEmpty(privateOut) || IsInside(privateOut, repoRoot))
        {
            return "VDIFF_PRIVATE_OUT must name a directory outside this repository";
        }

        List<string> names;
        try
        {
            names = Directory.EnumerateDirectories(fixturesDir).Select(d => Path.GetFileName(d)).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "VDIFF_FIXTURES_DIR must name an existing directory";
        }

        if (names.Any(name => !Shapes.IsShape(name)))
        {
            return "VDIFF_FIXTURES_DIR may hold only directories named after the five shapes";
        }

        return names.Count == 0 ? "VDIFF_FIXTURES_DIR holds no shape directory" : null;
    }

    /// <summary>
    /// True when <paramref name="path"/> is <paramref name="root"/> or below it after both are taken through
    /// <see cref="FinalPath.Resolve"/>, so a link into the repository counts. The nearest existing ancestor is
    /// resolved and the missing rest appended, so an output directory that does not exist yet can be judged. A
    /// path that cannot be resolved counts as inside (fail closed).
    /// </summary>
    static bool IsInside(string path, string root)
    {
        try
        {
            return CliApp.IsSameOrUnder(FinalPath.Resolve(path), FinalPath.Resolve(root));
        }
        catch (VdiffException)
        {
            return true;
        }
    }

    /// <summary>
    /// Runs every shape directory under <paramref name="fixturesDir"/> in ordinal order and writes the details
    /// under <paramref name="privateOut"/>. A refusal is the only line and writes nothing.
    /// </summary>
    public static PrivateRunResult Run(
        string fixturesDir, string privateOut, string repoRoot, DateOnly date, Func<string, string?> getEnvironmentVariable)
    {
        if (Refusal(fixturesDir, privateOut, repoRoot, getEnvironmentVariable) is { } refusal)
        {
            return new PrivateRunResult([refusal], false);
        }

        Directory.CreateDirectory(privateOut);
        var stamp = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lines = new List<string>();
        var rows = new List<ReportRow>();
        var failed = false;

        foreach (var directory in Directory.EnumerateDirectories(fixturesDir)
            .Where(d => Shapes.IsShape(Path.GetFileName(d)))
            .OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal))
        {
            var shape = Path.GetFileName(directory);
            var shapeOut = Path.Combine(privateOut, shape);
            Directory.CreateDirectory(shapeOut);

            string verdict;
            try
            {
                var loaded = FixtureLoader.Load(directory, FixtureKind.Private);
                var (outcomes, run) = RunShape(loaded, Path.Combine(shapeOut, "out"), repoRoot);
                foreach (var outcome in outcomes.Where(o => o.MismatchDiff is not null))
                {
                    WriteText(Path.Combine(shapeOut, outcome.Row.Output + ".diff"), outcome.MismatchDiff!);
                }

                if (run.Stderr.Length > 0)
                {
                    WriteText(Path.Combine(shapeOut, "cli-stderr.txt"), run.Stderr);
                }

                rows.AddRange(outcomes.Select(o => o.Row));
                verdict = Aggregate(outcomes);
            }
            catch (Exception e)
            {
                // The text may hold a path or a name from the target: it goes to a file, never to a line.
                WriteText(Path.Combine(shapeOut, "error.txt"), e.ToString());
                rows.Add(new ReportRow(shape, "-", Fail, null, "the shape could not be run"));
                verdict = Fail;
            }

            failed |= string.Equals(verdict, Fail, StringComparison.Ordinal);
            lines.Add($"{shape}: {verdict} {stamp}");
        }

        WriteText(Path.Combine(privateOut, "fixture-report.md"), FixtureReport.Render(rows));
        WriteText(Path.Combine(privateOut, "private-run.txt"), string.Concat(lines.Select(l => l + "\n")));
        return new PrivateRunResult(lines, !failed);
    }

    /// <summary>
    /// The run the environment asks for, or null when <see cref="FixturesDirVariable"/> is unset or empty: an
    /// ordinary run, and CI, have no private fixtures and pass by design.
    /// </summary>
    public static PrivateRunResult? RunFromEnvironment(Func<string, string?> getEnvironmentVariable, string repoRoot, DateOnly date)
    {
        var fixturesDir = getEnvironmentVariable(FixturesDirVariable);
        if (string.IsNullOrEmpty(fixturesDir))
        {
            return null;
        }

        return Run(fixturesDir, getEnvironmentVariable(OutVariable) ?? "", repoRoot, date, getEnvironmentVariable);
    }

    static void WriteText(string path, string text) => File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
}
