using Vdiff.Core.Extraction;
using Vdiff.Core.Git;
using Vdiff.Core.Output;
using Vdiff.Core.Pipeline;
using Vdiff.Core.Scope;

namespace Vdiff.Core.Cli;

/// <summary>
/// The whole tool behind <c>Main</c>: returns 0 on success and 1 on any failure (R27 contract)
/// and prints <c>vdiff: message</c> to stderr on failure, never file content. Order of work:
/// check the arguments, the repository and the git version, resolve both commits, refuse an
/// <c>--out</c> inside the repository, run the pipeline in memory, and only then create
/// <c>--out</c> and write the five files, so a failure before the write leaves it absent or
/// untouched (T-01-28).
/// </summary>
public static class CliApp
{
    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
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
            RefuseUnsafeOut(outPath, location);

            var command = CppSidecar.Locate();
            var result = TracerPipeline.Run(
                reader, baseId, headId, new ScopeFilter(options.Include), options.Blank, command, SidecarLimits.Default);

            var mermaid = MermaidWriter.Render(result.Diff);
            var html = HtmlWriter.Render(result.Diff, mermaid);
            OutputWriter.WriteAll(outPath, result.Base, result.Head, result.Diff, mermaid, html);

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

    /// <summary>Refuses an <c>--out</c> that lies inside the work tree or a git directory, or that is a file (T-01-28).</summary>
    static void RefuseUnsafeOut(string outPath, RepoLocation location)
    {
        if (IsSameOrUnder(outPath, location.Root) || IsSameOrUnder(outPath, location.GitDir) || IsSameOrUnder(outPath, location.CommonDir))
        {
            throw new VdiffException("--out must not lie inside the analysed repository");
        }

        if (File.Exists(outPath))
        {
            throw new VdiffException("--out exists and is a file");
        }
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
