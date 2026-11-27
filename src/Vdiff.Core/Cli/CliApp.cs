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
/// <param name="GetEnvironmentVariable">
/// Reads a variable instead of the process environment, so a test supplies <see cref="CliApp.ProtectedDirectoriesVariable"/>
/// without changing the process-wide environment.
/// </param>
internal sealed record CliHooks(
    Action? AfterPipeline = null,
    Action? AfterSecondCheck = null,
    Action<string>? AfterHeld = null,
    Func<string, string?>? GetEnvironmentVariable = null);

/// <summary>
/// The whole tool behind <c>Main</c>: returns 0 on success and 1 on any failure (R27 contract)
/// and prints <c>vdiff: message</c> to stderr on failure, never file content. Order of work:
/// check the arguments, the repository and the git version, resolve both commits, refuse an
/// <c>--out</c> inside the repository, run the pipeline in memory, refuse an <c>--out</c> inside
/// the repository a second time (a link may have been swapped in while the pipeline ran), and only
/// then create and hold <c>--out</c> and write the five files, so a failure before the write leaves
/// it absent or untouched (T-01-28).
/// </summary>
public static class CliApp
{
    /// <summary>
    /// The environment variable that adds protected directories: a list of at most
    /// <see cref="MaxProtectedDirectories"/> absolute directories separated by the platform's path separator.
    /// The trial script passes the vdiff repository root in it, which the CLI cannot know from its arguments.
    /// </summary>
    public const string ProtectedDirectoriesVariable = "VDIFF_PROTECTED_DIRS";

    /// <summary>The refusal for an <c>--out</c> inside a directory named by <see cref="ProtectedDirectoriesVariable"/>.</summary>
    public const string ProtectedOutRefusal = "--out must not lie inside a protected directory";

    /// <summary>The refusal for a malformed <see cref="ProtectedDirectoriesVariable"/>: names the variable, never its value.</summary>
    public const string ProtectedDirectoriesMalformed =
        "VDIFF_PROTECTED_DIRS must list at most 8 absolute directories separated by the path separator";

    const string RepositoryRefusal = "--out must not lie inside the analysed repository";

    /// <summary>The most directories <see cref="ProtectedDirectoriesVariable"/> may list.</summary>
    public const int MaxProtectedDirectories = 8;

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

            // Read before any git work, so a malformed value fails fast and creates nothing (T-01-58).
            var protectedDirectories = ParseProtectedDirectories(hooks?.GetEnvironmentVariable ?? Environment.GetEnvironmentVariable);

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
            _ = RefuseUnsafeOut(outPath, location, protectedDirectories);

            var command = CppSidecar.Locate();
            var result = TracerPipeline.Run(
                reader, baseId, headId, new ScopeFilter(options.Include), options.Blank, command, SidecarLimits.Default);

            var mermaid = MermaidWriter.Render(result.Diff);
            var html = HtmlWriter.Render(result.Diff, mermaid, result.Meta);

            // Test seam for the swap between the check and the write (WR-02); null in production.
            hooks?.AfterPipeline?.Invoke();

            // Second containment check, then the output directory is held (T-01-43).
            var resolvedOut = RefuseUnsafeOut(outPath, location, protectedDirectories);
            hooks?.AfterSecondCheck?.Invoke();

            using var held = HeldDirectory.Create(resolvedOut, p => FindRefusal(p, location, protectedDirectories));
            hooks?.AfterHeld?.Invoke(held.Path);
            OutputWriter.WriteAll(held, result.Base, result.Head, result.Diff, mermaid, html, result.Meta);

            stdout.Write($"vdiff: wrote {OutputWriter.FileNames.Count} files to {outPath}\n");
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
    /// systems other than Windows compared case-sensitively). The check-to-write interval is a
    /// separate bound (T-01-43, T-01-46), described where the output is written.
    /// The check runs before the pipeline for a fast failure and again after it; the second call
    /// returns the resolved path that feeds <c>HeldDirectory.Create</c>.
    /// The optional <paramref name="protectedDirectories"/> (from <see cref="ProtectedDirectoriesVariable"/>) only adds
    /// directories an <c>--out</c> must stay out of, at both checks: the trial script passes the vdiff repository
    /// root there, which the CLI cannot know from its arguments, and an unset or hostile environment cannot weaken the guard.
    /// Check-to-write interval (T-01-43, T-01-46). On Windows the output directory is opened without delete
    /// sharing before the first file is created and held until the last file is in place, so the operating
    /// system refuses to rename or delete it or any of its ancestors; its final path is read from the open
    /// handle and judged against the protected roots, and every file is created and moved by that final path,
    /// never by the path the operator typed, because a link entry in that path can still be retargeted while
    /// the directory is held. On Linux and macOS no handle is held: the resolved path is judged before and
    /// after the directories are created and the files are written by the resolved path, so the interval
    /// between that judgement and each write stays open for a process that is able to rename a directory.
    /// </summary>
    internal static string RefuseUnsafeOut(string outPath, RepoLocation location, IReadOnlyList<string>? protectedDirectories = null)
    {
        if (IsSameOrUnder(outPath, location.Root) || IsSameOrUnder(outPath, location.GitDir) || IsSameOrUnder(outPath, location.CommonDir))
        {
            throw new VdiffException(RepositoryRefusal);
        }

        if (protectedDirectories?.Any(dir => IsSameOrUnder(outPath, dir)) == true)
        {
            throw new VdiffException(ProtectedOutRefusal);
        }

        var resolvedOut = FinalPath.Resolve(outPath);
        var refusal = FindRefusal(resolvedOut, location, protectedDirectories);
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
    /// otherwise null; or, when it lies in one of the extra <paramref name="protectedDirectories"/> (each taken through
    /// <see cref="FinalPath.Resolve"/> too), the protected-directory text. The pre-write check and the judgement of
    /// the held directory both use it, so the two cannot drift.
    /// </summary>
    internal static string? FindRefusal(string physicalPath, RepoLocation location, IReadOnlyList<string>? protectedDirectories = null)
    {
        if (IsSameOrUnder(physicalPath, FinalPath.Resolve(location.Root))
            || IsSameOrUnder(physicalPath, FinalPath.Resolve(location.GitDir))
            || IsSameOrUnder(physicalPath, FinalPath.Resolve(location.CommonDir)))
        {
            return RepositoryRefusal;
        }

        if (protectedDirectories is not null
            && protectedDirectories.Any(dir => IsSameOrUnder(physicalPath, FinalPath.Resolve(dir))))
        {
            return ProtectedOutRefusal;
        }

        return null;
    }

    /// <summary>
    /// The directories named by <see cref="ProtectedDirectoriesVariable"/>: unset or empty gives none; otherwise the value
    /// is split on <see cref="Path.PathSeparator"/> and an empty entry, a relative entry or more than
    /// <see cref="MaxProtectedDirectories"/> entries is refused with <see cref="ProtectedDirectoriesMalformed"/>,
    /// which names the variable and never the value. The variable only adds protected directories (T-01-58).
    /// </summary>
    internal static IReadOnlyList<string> ParseProtectedDirectories(Func<string, string?> getEnvironmentVariable)
    {
        var value = getEnvironmentVariable(ProtectedDirectoriesVariable);
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        var entries = value.Split(Path.PathSeparator);
        if (entries.Length > MaxProtectedDirectories
            || entries.Any(entry => entry.Length == 0 || !Path.IsPathFullyQualified(entry)))
        {
            throw new VdiffException(ProtectedDirectoriesMalformed);
        }

        return entries;
    }

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
