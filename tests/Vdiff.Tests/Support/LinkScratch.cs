using Vdiff.Core.Process;

namespace Vdiff.Tests.Support;

/// <summary>
/// A scratch directory for directory links used by the aliased --out tests (WR-01, T-01-36).
/// Hygiene rule: every link lives inside <see cref="Root"/>, which is a fresh directory under the
/// system temp path and never inside a sandbox, never points at the repository checkout or at an
/// ancestor of a sandbox (<c>RepoSnapshot</c> and <c>SyntheticRepo.Dispose</c> enumerate
/// recursively and recursion enters junctions), and is removed first, as a link, never
/// recursively. Only after every link is gone does <see cref="Dispose"/> delete <see cref="Root"/>
/// with the real directories a test created as link targets.
/// </summary>
public sealed class LinkScratch : IDisposable
{
    readonly List<string> _links = [];

    public string Root { get; }

    public LinkScratch()
    {
        Root = Path.Combine(Path.GetTempPath(), "vdiff-links-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>
    /// Creates <c>Root/name</c> pointing at <paramref name="target"/> (an absolute path that need
    /// not exist) and returns the link path. On Windows this is a junction made by
    /// <c>cmd.exe /c mklink /J</c>, which needs no privilege, and it always holds an absolute
    /// target, so <paramref name="relativeTarget"/> is ignored there. Elsewhere it is a symbolic
    /// link, with the target made relative to <see cref="Root"/> when the flag is set.
    /// </summary>
    public string Link(string name, string target, bool relativeTarget = false)
    {
        var link = Path.Combine(Root, name);
        if (OperatingSystem.IsWindows())
        {
            var limits = new ProcessLimits(TimeSpan.FromSeconds(30), 1024 * 1024, 1024 * 1024);
            var result = BoundedProcess.Run("cmd.exe", ["/c", "mklink", "/J", link, target], limits);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"mklink /J exited {result.ExitCode}: {result.StderrTail}");
            }
        }
        else
        {
            Directory.CreateSymbolicLink(link, relativeTarget ? Path.GetRelativePath(Root, target) : target);
        }

        _links.Add(link);
        return link;
    }

    public void Dispose()
    {
        for (var i = _links.Count - 1; i >= 0; i--)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Not recursive: removes only the junction, never what it points at.
                    Directory.Delete(_links[i]);
                }
                else
                {
                    File.Delete(_links[i]);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // already gone
            }
        }

        if (Directory.Exists(Root))
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
