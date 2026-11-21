using Vdiff.Core.Cli;
using Vdiff.Tests.Contract;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Fixtures;

/// <summary>What one CLI run did: exit code, both text streams and the directory it wrote into.</summary>
public sealed record FixtureRun(int ExitCode, string Stdout, string Stderr, string OutDir);

/// <summary>One fixture output's verdict: its report row, whether it passes, and the unified diff of a mismatch.</summary>
public sealed record FixtureOutcome(ReportRow Row, bool Passed, string? MismatchDiff);

/// <summary>The outcomes of one synthetic fixture and the expected files accept mode rewrote.</summary>
public sealed record SyntheticResult(IReadOnlyList<FixtureOutcome> Outcomes, IReadOnlyList<string> Accepted);

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
            var matched = goldenPresent && outputPresent
                && File.ReadAllBytes(goldenPath).AsSpan().SequenceEqual(File.ReadAllBytes(actualPath));
            var check = new OutputCheck(toolSucceeded, goldenPresent, outputPresent, schemaValid, matched);
            var verdict = Ratchet.Evaluate(output.State, output.Due, check);
            var row = new ReportRow(f.Shape, output.Name, verdict.Status, output.Due, verdict.Detail);
            outcomes.Add(new FixtureOutcome(row, verdict.Passed, null));
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
            return new SyntheticResult(Evaluate(f, run), []);
        }
    }
}
