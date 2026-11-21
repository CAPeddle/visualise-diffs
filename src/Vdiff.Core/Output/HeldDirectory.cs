using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Vdiff.Core.Cli;

namespace Vdiff.Core.Output;

/// <summary>
/// The output directory, opened and held while the output files are written. On Windows the
/// directory is opened without delete sharing, so the operating system refuses to rename or
/// delete it or any of its ancestors until <see cref="Dispose"/>; its final path is read from the
/// open handle, judged by the caller's function, and is the only path any file operation uses.
/// Check-to-write interval (T-01-43, T-01-46). On Windows the output directory is opened without delete
/// sharing before the first file is created and held until the last file is in place, so the operating
/// system refuses to rename or delete it or any of its ancestors; its final path is read from the open
/// handle and judged against the protected roots, and every file is created and moved by that final path,
/// never by the path the operator typed, because a link entry in that path can still be retargeted while
/// the directory is held. On Linux and macOS no handle is held: the resolved path is judged before and
/// after the directories are created and the files are written by the resolved path, so the interval
/// between that judgement and each write stays open for a process that is able to rename a directory.
/// </summary>
internal sealed class HeldDirectory : IDisposable
{
    // Win32 values, kept here so the call below reads without a table lookup:
    // GENERIC_READ 0x80000000, FILE_SHARE_READ | FILE_SHARE_WRITE 3 (no delete sharing),
    // OPEN_EXISTING 3, FILE_FLAG_BACKUP_SEMANTICS 0x02000000 (needed to open a directory).
    // The flag that opens a link itself instead of following it is deliberately not set, so a link
    // at the last component is followed and the place the files would really land is what is judged.
    const uint GenericRead = 0x80000000;
    const uint ShareReadWrite = 3;
    const uint OpenExisting = 3;
    const uint BackupSemantics = 0x02000000;

    const string ExtendedPrefix = @"\\?\";
    const string ExtendedUncPrefix = @"\\?\UNC\";

    readonly SafeFileHandle? _handle;

    HeldDirectory(string path, SafeFileHandle? handle)
    {
        Path = path;
        _handle = handle;
    }

    /// <summary>The directory every file operation uses: the final path read from the held handle.</summary>
    public string Path { get; }

    /// <summary>
    /// Creates the missing directories of <paramref name="resolvedDirectory"/> and holds the last one.
    /// <paramref name="findRefusal"/> returns the fixed refusal text for a path that must not be written to,
    /// or null; it is applied to the final path of the held directory.
    /// Order (H-2): the deepest existing ancestor (the anchor) is opened and judged first, so a refused
    /// anchor creates nothing; the missing directories are then created under the anchor's final path; the
    /// leaf is opened and judged; only then is the anchor released. A refusal or a failure after a directory
    /// was created removes the directories this call created, bottom-up and non-recursively. Nothing is
    /// written to any directory that was refused; a directory created through a swap before the leaf is
    /// judged is removed afterwards, not prevented.
    /// </summary>
    public static HeldDirectory Create(string resolvedDirectory, Func<string, string?> findRefusal)
    {
        var (anchor, remainder) = SplitAtDeepestExisting(resolvedDirectory);
        return OperatingSystem.IsWindows()
            ? CreateHeld(anchor, remainder, findRefusal)
            : CreateNarrowed(resolvedDirectory, anchor, remainder, findRefusal);
    }

    /// <summary>
    /// The final-path forms Windows reports, reduced to a path files can be written to. A drive-letter
    /// path loses its extended-length prefix and is judged. A UNC path (the form a mapped drive letter
    /// or a share reports) becomes <c>\\server\share\...</c> and is accepted without being judged: the
    /// guard has never judged those spellings, and behavior for them is unchanged; this method makes no
    /// claim about them. Every other shape is refused with <see cref="FinalPath.UnsupportedRootMessage"/>.
    /// </summary>
    internal static (string Path, bool Judged) Normalize(string finalPath)
    {
        if (finalPath.StartsWith(ExtendedUncPrefix, StringComparison.OrdinalIgnoreCase)
            && finalPath.Length > ExtendedUncPrefix.Length)
        {
            return (@"\\" + finalPath[ExtendedUncPrefix.Length..], false);
        }

        if (finalPath.StartsWith(ExtendedPrefix, StringComparison.Ordinal))
        {
            var rest = finalPath[ExtendedPrefix.Length..];
            if (FinalPath.StartsWithDriveLetter(rest))
            {
                return (rest, true);
            }
        }

        throw new VdiffException(FinalPath.UnsupportedRootMessage);
    }

    public void Dispose() => _handle?.Dispose();

    /// <summary>Windows: hold the anchor, create under its final path, hold the leaf, release the anchor.</summary>
    static HeldDirectory CreateHeld(string anchor, IReadOnlyList<string> remainder, Func<string, string?> findRefusal)
    {
        var created = new List<string>();
        SafeFileHandle? anchorHandle = null;
        SafeFileHandle? leafHandle = null;
        try
        {
            anchorHandle = Open(anchor);
            var anchorPath = JudgedFinalPath(anchorHandle, findRefusal);

            var leafDirectory = anchorPath;
            foreach (var segment in remainder)
            {
                leafDirectory = System.IO.Path.Combine(leafDirectory, segment);
                if (!Directory.Exists(leafDirectory))
                {
                    Directory.CreateDirectory(leafDirectory);
                    created.Add(leafDirectory);
                }
            }

            leafHandle = Open(leafDirectory);
            var leafPath = JudgedFinalPath(leafHandle, findRefusal);

            anchorHandle.Dispose();
            return new HeldDirectory(leafPath, leafHandle);
        }
        catch
        {
            leafHandle?.Dispose();
            anchorHandle?.Dispose();
            RemoveCreated(created);
            throw;
        }
    }

    /// <summary>
    /// Linux and macOS: no handle is held. The resolved path is judged before and after the missing
    /// directories are created and the files are written by the resolved path, so the interval between
    /// that judgement and each write stays open for a process that is able to rename a directory.
    /// </summary>
    static HeldDirectory CreateNarrowed(
        string resolvedDirectory, string anchor, IReadOnlyList<string> remainder, Func<string, string?> findRefusal)
    {
        var created = new List<string>();
        try
        {
            Judge(FinalPath.Resolve(anchor), findRefusal);
            var current = anchor;
            foreach (var segment in remainder)
            {
                current = System.IO.Path.Combine(current, segment);
                Directory.CreateDirectory(current);
                created.Add(current);
            }

            Judge(FinalPath.Resolve(resolvedDirectory), findRefusal);
            return new HeldDirectory(resolvedDirectory, null);
        }
        catch
        {
            RemoveCreated(created);
            throw;
        }
    }

    static string JudgedFinalPath(SafeFileHandle handle, Func<string, string?> findRefusal)
    {
        var (path, judged) = Normalize(ReadFinalPath(handle));
        if (judged)
        {
            Judge(path, findRefusal);
        }

        return path;
    }

    static void Judge(string path, Func<string, string?> findRefusal)
    {
        var refusal = findRefusal(path);
        if (refusal is not null)
        {
            throw new VdiffException(refusal);
        }
    }

    /// <summary>
    /// The deepest existing directory at or above the path, and the missing names below it, outermost first.
    /// Trailing separators are trimmed, but never below the root of the path: a drive-relative <c>C:</c>
    /// names the current directory on that drive, not the root, and the slash root would become the empty
    /// string, so a root (with any number of trailing separators) is held as the root.
    /// </summary>
    static (string Anchor, IReadOnlyList<string> Remainder) SplitAtDeepestExisting(string directory)
    {
        var remainder = new List<string>();
        var root = System.IO.Path.GetPathRoot(directory) ?? string.Empty;
        var current = directory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        if (current.Length < root.Length)
        {
            current = root;
        }

        while (!Directory.Exists(current))
        {
            var parent = System.IO.Path.GetDirectoryName(current)
                ?? throw new VdiffException(FinalPath.UnresolvableMessage);
            remainder.Insert(0, System.IO.Path.GetFileName(current));
            current = parent;
        }

        return (current, remainder);
    }

    /// <summary>Removes the directories this call created, bottom-up, never recursively, ignoring a directory that cannot go.</summary>
    static void RemoveCreated(List<string> created)
    {
        for (var i = created.Count - 1; i >= 0; i--)
        {
            try
            {
                Directory.Delete(created[i]);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // best effort: the refusal that got us here is the one to report
            }
        }
    }

    static SafeFileHandle Open(string directory)
    {
        var handle = CreateFileW(directory, GenericRead, ShareReadWrite, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            // Fixed text: no path and no Win32 error text (T-01-38).
            handle.Dispose();
            throw new VdiffException(FinalPath.UnresolvableMessage);
        }

        return handle;
    }

    static string ReadFinalPath(SafeFileHandle handle)
    {
        var buffer = new char[1024];
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        if (length >= buffer.Length)
        {
            // Too small: the call returned the size it needs, including the terminator.
            buffer = new char[length];
            length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        }

        if (length == 0 || length >= buffer.Length)
        {
            throw new VdiffException(FinalPath.UnresolvableMessage);
        }

        return new string(buffer, 0, (int)length);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file, [Out] char[] filePath, uint filePathLength, uint flags);
}
