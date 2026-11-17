namespace Vdiff.Core.Cli;

/// <summary>
/// Resolves a path to the physical location a write to it would reach, so the <c>--out</c>
/// containment guard can compare where files would really land (WR-01, T-01-36).
/// </summary>
public static class FinalPath
{
    /// <summary>
    /// The one message every resolver failure carries. It holds no path and no .NET exception
    /// text, because the CLI prints a caught <see cref="IOException"/> with its own message,
    /// which can contain a full path (T-01-38).
    /// </summary>
    public const string UnresolvableMessage = "a path could not be resolved to its final target";

    const int MaxLinks = 40;

    /// <summary>
    /// The final physical path of the deepest existing ancestor of <paramref name="path"/> plus
    /// the missing remainder, unchanged. The path is walked from its root one segment at a time;
    /// a segment that is a symbolic link or a directory junction replaces everything so far by
    /// the link's final target, which is resolved by the same rule (its own ancestors can be
    /// links); the walk stops at the first segment that does not exist. At most 40 links are
    /// followed in one resolution. A loop, an unreadable link or no access fails closed with a
    /// <see cref="VdiffException"/> carrying <see cref="UnresolvableMessage"/>, never with the
    /// lexical path. Nothing is created, written or deleted.
    /// <para>
    /// Bound (T-01-39): this resolves symbolic links and directory junctions only. It does not
    /// resolve subst drive letters or DOS 8.3 short names; they are an accepted limit of the
    /// closure of verification gap 2.
    /// </para>
    /// </summary>
    public static string Resolve(string path)
    {
        try
        {
            var links = 0;
            return Step(path, ref links);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or TooManyLinksException)
        {
            throw new VdiffException(UnresolvableMessage);
        }
    }

    static string Step(string path, ref int links)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? throw new TooManyLinksException();
        var segments = full[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        var prefix = root;
        for (var i = 0; i < segments.Length; i++)
        {
            var candidate = Path.Combine(prefix, segments[i]);
            var info = new DirectoryInfo(candidate);
            if (info.LinkTarget is not null)
            {
                if (++links > MaxLinks)
                {
                    throw new TooManyLinksException();
                }

                var target = info.ResolveLinkTarget(returnFinalTarget: true) ?? throw new TooManyLinksException();
                prefix = Step(target.FullName, ref links);
            }
            else if (Directory.Exists(candidate) || File.Exists(candidate))
            {
                prefix = candidate;
            }
            else
            {
                return Path.Combine([prefix, .. segments[i..]]);
            }
        }

        return prefix;
    }

    /// <summary>Internal signal for a link budget overrun; converted to the fixed message by <see cref="Resolve"/>.</summary>
    sealed class TooManyLinksException : Exception;
}
