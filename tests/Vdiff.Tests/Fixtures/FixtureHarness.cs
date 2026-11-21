using Vdiff.Core;
using Vdiff.Core.Cli;
using Vdiff.Core.Process;
using Vdiff.Tests.Contract;
using Vdiff.Tests.Support;
using Xunit.Sdk;

namespace Vdiff.Tests.Fixtures;

/// <summary>What one CLI run did: exit code, both text streams and the directory it wrote into.</summary>
public sealed record FixtureRun(int ExitCode, string Stdout, string Stderr, string OutDir);

/// <summary>One fixture output's verdict: its report row, whether it passes, and the unified diff of a mismatch.</summary>
public sealed record FixtureOutcome(ReportRow Row, bool Passed, string? MismatchDiff);

/// <summary>
/// The outcomes of one synthetic fixture and the expected files accept mode rewrote. <see cref="Run"/> is the
/// CLI run behind the outcomes, so a caller can log why a run failed; it is an addition to the two members the
/// other plans use.
/// </summary>
public sealed record SyntheticResult(IReadOnlyList<FixtureOutcome> Outcomes, IReadOnlyList<string> Accepted, FixtureRun? Run = null);

/// <summary>
/// The golden-fixture harness (D-05, D-08, D-10, D-18). It builds the two-commit repository of a synthetic
/// fixture, drives <see cref="CliApp.Run"/> end to end, compares the two goldens with the files the run wrote
/// byte for byte, and judges each comparison with <see cref="Ratchet.Evaluate"/>. Private mode (plan 02-09)
/// reuses <see cref="CliArguments"/>, <see cref="RunCli"/> and <see cref="Evaluate"/>, so a private run
/// executes exactly the comparison code CI runs.
/// </summary>
public static class FixtureHarness
{
    public const string DiffOutput = "diff";
    public const string SummaryOutput = "summary";

    /// <summary>
    /// Builds the fixture repository: every base file committed as <c>base</c>, then every base file absent
    /// from head deleted and every head file written, committed as <c>head</c>. The bytes are copied exactly
    /// as stored and the identity and dates are fixed by <see cref="SyntheticRepo"/>, so commit ids are
    /// deterministic. The caller disposes the repository.
    /// </summary>
    public static (SyntheticRepo Repo, string BaseId, string HeadId) BuildSyntheticRepo(LoadedFixture f)
    {
        var repo = new SyntheticRepo();
        try
        {
            foreach (var file in f.BaseTree)
            {
                repo.Write(file.RelativePath, file.Bytes);
            }

            var baseId = repo.Commit("base");

            var headPaths = f.HeadTree.Select(t => t.RelativePath).ToHashSet(StringComparer.Ordinal);
            foreach (var file in f.BaseTree.Where(t => !headPaths.Contains(t.RelativePath)))
            {
                repo.Delete(file.RelativePath);
            }

            foreach (var file in f.HeadTree)
            {
                repo.Write(file.RelativePath, file.Bytes);
            }

            var headId = repo.Commit("head");
            return (repo, baseId, headId);
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>The CLI arguments for a fixture: the four required ones, then every include and blank entry in file order.</summary>
    public static IReadOnlyList<string> CliArguments(LoadedFixture f, string repoDir, string baseRef, string headRef, string outDir)
    {
        var args = new List<string> { "--repo", repoDir, "--base", baseRef, "--head", headRef, "--out", outDir };
        foreach (var glob in f.File.Config?.Include ?? [])
        {
            args.Add("--include");
            args.Add(glob);
        }

        foreach (var name in f.File.Config?.Blank ?? [])
        {
            args.Add("--blank");
            args.Add(name);
        }

        return args;
    }

    /// <summary>Runs the real CLI in this process, capturing both streams.</summary>
    public static FixtureRun RunCli(IReadOnlyList<string> args, string outDir)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = CliApp.Run(args, stdout, stderr);
        return new FixtureRun(exit, stdout.ToString(), stderr.ToString(), outDir);
    }

    /// <summary>True when every output file of the run that has a schema is present and validates.</summary>
    public static bool SchemaGate(string outDir) => SchemaSupport.ValidateOutputs(outDir).Count == 0;

    /// <summary>
    /// One outcome per output, diff first. A run that failed, a missing golden, a missing output or an
    /// output that fails its schema is FAIL in every state; only a sound run is judged on its bytes.
    /// </summary>
    public static IReadOnlyList<FixtureOutcome> Evaluate(LoadedFixture f, FixtureRun run)
    {
        var toolSucceeded = run.ExitCode == 0 && run.Stderr.Length == 0;
        var schemaValid = SchemaGate(run.OutDir);
        var outcomes = new List<FixtureOutcome>();
        foreach (var output in f.Outputs)
        {
            var goldenPath = Path.Combine(f.Directory, "expected", output.FileName);
            var actualPath = Path.Combine(run.OutDir, output.FileName);
            var goldenPresent = File.Exists(goldenPath);
            var outputPresent = File.Exists(actualPath);
            var golden = goldenPresent ? File.ReadAllBytes(goldenPath) : null;
            var actual = outputPresent ? File.ReadAllBytes(actualPath) : null;
            var matched = golden is not null && actual is not null && golden.AsSpan().SequenceEqual(actual);
            var check = new OutputCheck(toolSucceeded, goldenPresent, outputPresent, schemaValid, matched);
            var verdict = Ratchet.Evaluate(output.State, output.Due, check);
            var row = new ReportRow(f.Shape, output.Name, verdict.Status, output.Due, verdict.Detail);

            // The diff is target content in private mode: a private caller must never log it (D-13).
            var mismatch = golden is not null && actual is not null && !matched
                ? UnifiedDiff(golden, actual, output.FileName)
                : null;
            outcomes.Add(new FixtureOutcome(row, verdict.Passed, mismatch));
        }

        return outcomes;
    }

    /// <summary>
    /// Builds the repository, runs the CLI into a fresh directory next to it, and judges the outputs before
    /// the repository is disposed.
    /// </summary>
    public static SyntheticResult RunSynthetic(LoadedFixture f, bool accept)
    {
        var (repo, baseId, headId) = BuildSyntheticRepo(f);
        using (repo)
        {
            var outDir = Path.Combine(repo.Root, "fixture-out");
            var run = RunCli(CliArguments(f, repo.RepoDir, baseId, headId, outDir), outDir);
            var outcomes = Evaluate(f, run);

            IReadOnlyList<string> accepted = [];
            if (accept)
            {
                // Accept mode copies only from a run that proved something: the tool succeeded, both goldens'
                // outputs exist and every schema-bearing output validates.
                var sound = run.ExitCode == 0
                    && run.Stderr.Length == 0
                    && f.Outputs.All(o => File.Exists(Path.Combine(run.OutDir, o.FileName)))
                    && SchemaGate(run.OutDir);
                if (!sound)
                {
                    throw new InvalidOperationException("accept mode writes only from a successful, schema-valid run");
                }

                accepted = AcceptMode.Apply(f.Directory, run.OutDir);
            }

            return new SyntheticResult(outcomes, accepted, run);
        }
    }

    const int MaxDiffLines = 200;

    /// <summary>
    /// The unified diff of two files, made by <c>git diff --no-index</c> on copies under fixed names in a
    /// temporary directory, so no absolute path reaches the header. Git runs with the isolated configuration
    /// of <see cref="SyntheticRepo"/>, so nobody's diff or colour settings change the bytes. Empty when the
    /// files are equal; cut to 200 lines and a final count when longer.
    /// </summary>
    public static string UnifiedDiff(byte[] expected, byte[] actual, string fileName)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vdiff-udiff-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(temp, "expected"));
            Directory.CreateDirectory(Path.Combine(temp, "actual"));
            File.WriteAllBytes(Path.Combine(temp, "expected", fileName), expected);
            File.WriteAllBytes(Path.Combine(temp, "actual", fileName), actual);
            var emptyConfig = Path.Combine(temp, "empty.gitconfig");
            File.WriteAllBytes(emptyConfig, []);

            var env = GitCommand.BuildEnvironment();
            env["GIT_CONFIG_NOSYSTEM"] = "1";
            env["GIT_CONFIG_GLOBAL"] = emptyConfig;
            var args = new List<string>(GitCommand.Prefix)
            {
                "diff", "--no-index", "--no-color", "--no-ext-diff", "--no-textconv", "--",
                "expected/" + fileName, "actual/" + fileName,
            };
            var limits = new ProcessLimits(TimeSpan.FromSeconds(30), 4L * 1024 * 1024, 1024 * 1024);
            var result = BoundedProcess.Run("git", args, limits, null, env, temp);

            return result.ExitCode switch
            {
                0 => "",
                1 => CutLines(result.StdoutText),
                _ => $"unified diff unavailable (git exit {result.ExitCode})",
            };
        }
        catch (VdiffException)
        {
            return "unified diff unavailable (git could not be run within its limits)";
        }
        finally
        {
            try
            {
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // a leftover temporary directory is not worth failing a comparison for
            }
        }
    }

    static string CutLines(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        return lines.Length <= MaxDiffLines
            ? string.Join('\n', lines) + "\n"
            : string.Join('\n', lines.Take(MaxDiffLines)) + $"\n... ({lines.Length - MaxDiffLines} more lines)\n";
    }

    static FixtureOutcome Failure(string fixture, string output, string detail) =>
        new(new ReportRow(fixture, output, "FAIL", null, detail), false, null);

    /// <summary>
    /// Runs every fixture directory under <paramref name="fixturesRoot"/> in name order and writes the report.
    /// A missing root fails the test; it never skips. A mismatch is logged with its unified diff (D-08), a
    /// failed CLI run with its exit code and stderr, both of which are synthetic public content here.
    /// </summary>
    public static IReadOnlyList<FixtureOutcome> RunAll(
        string fixturesRoot, Action<string> log, Func<string, string?> getEnvironmentVariable)
    {
        // Accept mode is judged first, so a refusal means nothing runs and nothing is written.
        if (AcceptMode.Refusal(getEnvironmentVariable) is { } refusal)
        {
            throw new XunitException(refusal);
        }

        var acceptShape = AcceptMode.RequestedShape(getEnvironmentVariable);

        if (!Directory.Exists(fixturesRoot))
        {
            throw new XunitException("fixtures root not found");
        }

        var outcomes = new List<FixtureOutcome>();
        var accepted = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(fixturesRoot)
            .OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal))
        {
            var name = Path.GetFileName(directory);
            if (!Shapes.IsShape(name))
            {
                outcomes.Add(Failure(name, "-", "not one of the five shape names"));
                continue;
            }

            LoadedFixture loaded;
            try
            {
                loaded = FixtureLoader.Load(directory, FixtureKind.Synthetic);
            }
            catch (FixtureException e)
            {
                if (e.Detail is not null)
                {
                    log($"{name}: {e.Message}: {e.Detail}");
                }

                outcomes.Add(Failure(name, DiffOutput, e.Message));
                outcomes.Add(Failure(name, SummaryOutput, e.Message));
                continue;
            }

            var result = RunSynthetic(loaded, accept: acceptShape is not null && string.Equals(acceptShape, name, StringComparison.Ordinal));
            accepted.AddRange(result.Accepted.Select(path => name + "/" + path));
            if (result.Run is { } run && (run.ExitCode != 0 || run.Stderr.Length > 0))
            {
                log($"{name}: the CLI exited with exit code {run.ExitCode}");
                if (run.Stderr.Length > 0)
                {
                    log(run.Stderr.TrimEnd('\n'));
                }
            }

            foreach (var outcome in result.Outcomes)
            {
                if (outcome.MismatchDiff is { } diff)
                {
                    log($"--- {outcome.Row.Fixture}/{outcome.Row.Output} ({outcome.Row.Status}) ---");
                    log(diff);
                }
            }

            outcomes.AddRange(result.Outcomes);
        }

        FixtureReport.WriteIfRequested(outcomes.Select(o => o.Row), getEnvironmentVariable);

        // A run with accept mode on never passes: the rewritten goldens must be reviewed first (D-06).
        if (acceptShape is not null)
        {
            throw new XunitException(accepted.Count == 0
                ? "accept mode named a fixture that was not found or did not run; nothing was rewritten"
                : $"accept mode rewrote {string.Join(", ", accepted)}; review them with git diff, then rerun without {AcceptMode.Variable}");
        }

        return outcomes;
    }
}
