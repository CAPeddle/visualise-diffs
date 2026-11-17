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

    /// <summary>
    /// The one message for a path in a Windows namespace other than the drive-letter one
    /// (T-01-41). Fixed text: no path and no exception text (T-01-38).
    /// </summary>
    public const string UnsupportedRootMessage =
        "a path must begin with a drive letter on Windows; extended-length, device, UNC and volume paths are not accepted";

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
    /// On Windows the full path must begin with an ASCII letter, a colon and a backslash, or the
    /// call fails with <see cref="UnsupportedRootMessage"/> (T-01-41). The accept-list is applied
    /// to the full path text, not to <see cref="Path.GetPathRoot"/>: the root reported for a
    /// volume GUID path or a device path differs between hosts, and a text rule does not. The
    /// same rule applies to every link target, and a link whose raw target is a volume GUID path
    /// is refused too, so the result does not depend on how a runtime reports such a target.
    /// </para>
    /// <para>
    /// Bound (T-01-39): the guard compares drive-letter paths after resolving symbolic links and
    /// directory junctions. It does not resolve subst or mapped drive letters or DOS 8.3 short
    /// names, and review findings IN-03 (link targets written without a drive letter) and IN-04
    /// (case-insensitive file systems other than Windows compared case-sensitively) remain open.
    /// A path in another Windows namespace (extended-length, device, UNC including loopback) and
    /// a link whose target is a volume GUID path are refused, not resolved (T-01-41).
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
        if (OperatingSystem.IsWindows() && !StartsWithDriveLetter(full))
        {
            throw new VdiffException(UnsupportedRootMessage);
        }

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

                if (OperatingSystem.IsWindows() && IsVolumeGuidTarget(info.LinkTarget))
                {
                    throw new VdiffException(UnsupportedRootMessage);
                }

                // One hop at a time: each hop's raw target is checked, and the next hop is
                // followed by the same walk, which counts it against the link budget.
                var target = info.ResolveLinkTarget(returnFinalTarget: false) ?? throw new TooManyLinksException();
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

    static bool StartsWithDriveLetter(string full) =>
        full.Length >= 3 && char.IsAsciiLetter(full[0]) && full[1] == ':' && full[2] == '\\';

    static bool IsVolumeGuidTarget(string rawTarget) =>
        rawTarget.TrimStart('\\', '?', '.').StartsWith("Volume{", StringComparison.OrdinalIgnoreCase);

    /// <summary>Internal signal for a link budget overrun; converted to the fixed message by <see cref="Resolve"/>.</summary>
    sealed class TooManyLinksException : Exception;
}
