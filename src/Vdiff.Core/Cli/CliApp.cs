using Vdiff.Core.Extraction;
using Vdiff.Core.Git;
using Vdiff.Core.Output;
using Vdiff.Core.Pipeline;
using Vdiff.Core.Scope;

namespace Vdiff.Core.Cli;

/// <summary>
/// Test seam for the interval between the checks and the write (H-5). Every member is null in
/// production. A hook records what happened and never asserts: <see cref="CliApp"/> catches every
/// exception and reports exit 1, so an assertion inside a hook would be swallowed.
/// </summary>
/// <param name="AfterPipeline">Runs after the pipeline, before the second containment check.</param>
/// <param name="AfterSecondCheck">Runs immediately after the second containment check.</param>
/// <param name="AfterHeld">Runs immediately before the first output file is written, with the directory path the files go to.</param>
internal sealed record CliHooks(
    Action? AfterPipeline = null,
    Action? AfterSecondCheck = null,
    Action<string>? AfterHeld = null);

/// <summary>
/// The whole tool behind <c>Main</c>: returns 0 on success and 1 on any failure (R27 contract)
/// and prints <c>vdiff: message</c> to stderr on failure, never file content. Order of work:
/// check the arguments, the repository and the git version, resolve both commits, refuse an
/// <c>--out</c> inside the repository, run the pipeline in memory, refuse an <c>--out</c> inside
/// the repository a second time (a link may have been swapped in while the pipeline ran), and only
/// then create <c>--out</c> and write the five files, so a failure before the write leaves it
/// absent or untouched (T-01-28).
/// </summary>
public static class CliApp
{
    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr) =>
        Run(args, stdout, stderr, (CliHooks?)null);

    internal static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr, Action? afterPipeline) =>
        Run(args, stdout, stderr, new CliHooks(AfterPipeline: afterPipeline));

    internal static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr, CliHooks? hooks)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options.Help)
            {
                stdout.Write(CliOptions.Usage);
                return 0;
            }

            var repoPath = Path.GetFullPath(options.Repo);
            if (!Directory.Exists(repoPath))
            {
                throw new VdiffException("--repo is not an existing directory");
            }

            // The constructor reads `git --version` once and refuses git older than 2.45 (D-11).
            var reader = new GitObjectReader(repoPath);
            var location = reader.Locate();
            var baseId = reader.ResolveCommit(options.Base);
            var headId = reader.ResolveCommit(options.Head);

            var outPath = Path.GetFullPath(options.Out);
            _ = RefuseUnsafeOut(outPath, location);

            var command = CppSidecar.Locate();
            var result = TracerPipeline.Run(
                reader, baseId, headId, new ScopeFilter(options.Include), options.Blank, command, SidecarLimits.Default);

            var mermaid = MermaidWriter.Render(result.Diff);
            var html = HtmlWriter.Render(result.Diff, mermaid);

            // Test seam for the swap between the check and the write (WR-02); null in production.
            hooks?.AfterPipeline?.Invoke();

            // Second containment check, immediately before the write (T-01-40, tracked as T-01-43).
            var resolvedOut = RefuseUnsafeOut(outPath, location);
            hooks?.AfterSecondCheck?.Invoke();

            using var held = HeldDirectory.Create(resolvedOut, p => FindRefusal(p, location));
            hooks?.AfterHeld?.Invoke(held.Path);
            OutputWriter.WriteAll(held, result.Base, result.Head, result.Diff, mermaid, html);

            stdout.Write($"vdiff: wrote 5 files to {outPath}\n");
            return 0;
        }
        catch (VdiffException e)
        {
            return Fail(stderr, e.Message);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Fail(stderr, "could not write the output: " + e.Message);
        }
        catch (Exception e)
        {
            return Fail(stderr, $"unexpected failure ({e.GetType().Name})");
        }
    }

    static int Fail(TextWriter stderr, string message)
    {
        stderr.Write($"vdiff: {message}\n");
        return 1;
    }

    /// <summary>
    /// Refuses an <c>--out</c> that lies inside the work tree or a git directory, or that is a file
    /// (T-01-28). The containment test runs twice: on the lexical full paths, and again on the paths
    /// after <see cref="FinalPath.Resolve"/>, so an <c>--out</c> reached through a symbolic link or
    /// a directory junction (T-01-36), or a repository reached through one, is refused too.
    /// Bound (T-01-39): the guard compares drive-letter paths after resolving symbolic links and
    /// directory junctions. On Windows a link is followed only when the target the runtime reports
    /// for it is a drive-letter path, and every other reported target is refused with
    /// <see cref="FinalPath.UnsupportedRootMessage"/>, not resolved (T-01-41). That covers a target
    /// in an NT namespace (GLOBALROOT, bare device paths, UNC and volume GUID paths), a relative
    /// target, a target written without a drive letter and a drive-relative target. The runtime
    /// strips the NT prefix before it reports a target, so the test is on the reported text and not
    /// on a prefix, and raw reparse data is not read. Paths in other Windows namespaces
    /// (extended-length, device, UNC including loopback) are refused too, which includes a work
    /// tree or git directory that git reports in such a namespace. Still unresolved: subst and
    /// mapped drive letters, DOS 8.3 short names, and review finding IN-04 (case-insensitive file
    /// systems other than Windows compared case-sensitively).
    /// The check runs before the pipeline for a fast failure and again immediately before the write.
    /// The second call mitigates T-01-40, a link swapped in between check and write: plan 01-12
    /// accepted that threat, plan 01-13 mitigates it and tracks it as T-01-43. The interval between
    /// the second check and the first write is not closed, and a hostile concurrent process able to
    /// use it could edit the repository directly.
    /// </summary>
    internal static string RefuseUnsafeOut(string outPath, RepoLocation location)
    {
        if (IsSameOrUnder(outPath, location.Root) || IsSameOrUnder(outPath, location.GitDir) || IsSameOrUnder(outPath, location.CommonDir))
        {
            throw new VdiffException("--out must not lie inside the analysed repository");
        }

        var resolvedOut = FinalPath.Resolve(outPath);
        var refusal = FindRefusal(resolvedOut, location);
        if (refusal is not null)
        {
            throw new VdiffException(refusal);
        }

        if (File.Exists(outPath))
        {
            throw new VdiffException("--out exists and is a file");
        }

        return resolvedOut;
    }

    /// <summary>
    /// The refusal text when <paramref name="physicalPath"/> (already resolved) lies in the work tree,
    /// the git directory or the common git directory, each taken through <see cref="FinalPath.Resolve"/>,
    /// otherwise null. The pre-write check and the judgement of the held directory both use it, so
    /// the two cannot drift.
    /// </summary>
    internal static string? FindRefusal(string physicalPath, RepoLocation location) =>
        IsSameOrUnder(physicalPath, FinalPath.Resolve(location.Root))
            || IsSameOrUnder(physicalPath, FinalPath.Resolve(location.GitDir))
            || IsSameOrUnder(physicalPath, FinalPath.Resolve(location.CommonDir))
            ? "--out must not lie inside the analysed repository"
            : null;

    /// <summary>
    /// True when <paramref name="path"/> is <paramref name="directory"/> or below it, compared on
    /// directory boundaries (so <c>repo-out</c> is not inside <c>repo</c>): OrdinalIgnoreCase on
    /// Windows, Ordinal elsewhere.
    /// </summary>
    public static bool IsSameOrUnder(string path, string directory)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var trimmed = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(path);
        return string.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), trimmed, comparison)
            || full.StartsWith(trimmed + Path.DirectorySeparatorChar, comparison);
    }
}
