using Vdiff.Core.Process;

namespace Vdiff.Tests.Support;

/// <summary>
/// Spells one directory in the Windows path namespaces the <c>--out</c> guard must refuse
/// (CR-01, T-01-41). These spellings reach the same files as the drive-letter path but compare
/// unequal as text, which is exactly why a lexical containment test misses them. Windows only:
/// callers return before using them on any other host. Every volume used is the sandbox's own
/// or a fixed all-zero GUID that names no real volume.
/// </summary>
public static class PathSpellings
{
    /// <summary>
    /// The same location as <paramref name="absoluteDrivePath"/> in another namespace:
    /// <c>extended</c> (backslash backslash question mark backslash path), <c>device</c> (dot
    /// instead of the question mark), <c>loopback-unc</c> (localhost, drive letter and a dollar
    /// sign), <c>extended-unc</c> (the extended prefix with UNC, localhost and the share) and
    /// <c>volume-guid-path</c> (a fixed all-zero volume GUID that names no real volume).
    /// </summary>
    public static string NonDriveNamespace(string kind, string absoluteDrivePath)
    {
        var full = Path.GetFullPath(absoluteDrivePath);
        var drive = full[0];
        var rest = full[2..];
        return kind switch
        {
            "extended" => @"\\?\" + full,
            "device" => @"\\.\" + full,
            "loopback-unc" => @"\\localhost\" + drive + "$" + rest,
            "extended-unc" => @"\\?\UNC\localhost\" + drive + "$" + rest,
            "volume-guid-path" => @"\\?\Volume{00000000-0000-0000-0000-000000000000}" + rest,
            _ => throw new ArgumentException("unknown namespace kind", nameof(kind)),
        };
    }

    /// <summary>
    /// The path <paramref name="absoluteDrivePath"/> spelled through the real volume GUID of its
    /// drive, as <c>mountvol</c> reports it.
    /// </summary>
    public static string VolumeGuidPath(string absoluteDrivePath)
    {
        var full = Path.GetFullPath(absoluteDrivePath);
        var limits = new ProcessLimits(TimeSpan.FromSeconds(30), 64 * 1024, 64 * 1024);
        var result = BoundedProcess.Run("mountvol", [full[..3], "/L"], limits);
        var text = result.StdoutText.Trim();
        if (result.ExitCode != 0
            || !text.StartsWith(@"\\?\Volume{", StringComparison.Ordinal)
            || !text.EndsWith('\\'))
        {
            throw new InvalidOperationException($"mountvol exited {result.ExitCode}");
        }

        return text + full[3..];
    }
}
