using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Vdiff.Core.Cli;

namespace Vdiff.Core.Output;

/// <summary>
/// The output directory, opened and held while the five files are written. On Windows the
/// directory is opened without delete sharing, so the operating system refuses to rename or
/// delete it or any of its ancestors until <see cref="Dispose"/>; its final path is read from the
/// open handle, judged by the caller's function, and is the only path any file operation uses.
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

    readonly SafeFileHandle? _handle;

    HeldDirectory(string path, SafeFileHandle? handle)
    {
        Path = path;
        _handle = handle;
    }

    /// <summary>The directory every file operation uses: the final path read from the held handle.</summary>
    public string Path { get; }

    /// <summary>
    /// Creates <paramref name="resolvedDirectory"/> when it is missing and holds it. <paramref name="findRefusal"/>
    /// returns the fixed refusal text for a path that must not be written to, or null; it is applied to the
    /// final path of the held directory.
    /// </summary>
    public static HeldDirectory Create(string resolvedDirectory, Func<string, string?> findRefusal)
    {
        Directory.CreateDirectory(resolvedDirectory);
        if (!OperatingSystem.IsWindows())
        {
            return new HeldDirectory(resolvedDirectory, null);
        }

        var handle = Open(resolvedDirectory);
        try
        {
            var (path, judged) = Normalize(ReadFinalPath(handle));
            if (judged)
            {
                var refusal = findRefusal(path);
                if (refusal is not null)
                {
                    throw new VdiffException(refusal);
                }
            }

            return new HeldDirectory(path, handle);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The final-path forms Windows reports, reduced to a path files can be written to. A drive-letter
    /// path loses its extended-length prefix and is judged; every other shape is refused with
    /// <see cref="FinalPath.UnsupportedRootMessage"/>.
    /// </summary>
    internal static (string Path, bool Judged) Normalize(string finalPath)
    {
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
