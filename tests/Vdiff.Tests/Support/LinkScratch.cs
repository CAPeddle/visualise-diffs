using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
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
/// <see cref="RawJunction"/> needs no privilege either and is registered in the same list, so it
/// too is removed first, as a link, before <see cref="Root"/>.
/// File links and hard links (<see cref="HardLink"/>, <see cref="TryFileLink"/>) are plain entries
/// and may live in a sandbox's output directory, never in the checkout; <see cref="Dispose"/>
/// removes them by name with <c>File.Delete</c>, which never touches the file they point to.
/// </summary>
public sealed class LinkScratch : IDisposable
{
    readonly List<string> _links = [];
    readonly List<string> _fileEntries = [];

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

    /// <summary>
    /// Windows only (elsewhere it throws <see cref="PlatformNotSupportedException"/>): creates
    /// <c>Root/name</c> as a directory junction whose substitute name is exactly
    /// <paramref name="substituteName"/>, written with <c>FSCTL_SET_REPARSE_POINT</c>. Needs no
    /// privilege. <c>mklink /J</c> can only write the drive-letter spelling; this writes any
    /// spelling (for example an NT namespace one) and an optional print name, which is empty for a
    /// plain junction. The junction is registered before the control call, so a failed write still
    /// removes the plain empty directory it left. Returns the link path.
    /// </summary>
    public string RawJunction(string name, string substituteName, string printName = "")
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        var link = Path.Combine(Root, name);
        Directory.CreateDirectory(link);
        _links.Add(link);

        var substitute = Encoding.Unicode.GetBytes(substituteName);
        var print = Encoding.Unicode.GetBytes(printName);
        var dataLength = 8 + substitute.Length + 2 + print.Length + 2;
        var buffer = new byte[8 + dataLength];
        BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), 0xA0000003u);
        BitConverter.TryWriteBytes(buffer.AsSpan(4, 2), (ushort)dataLength);
        BitConverter.TryWriteBytes(buffer.AsSpan(8, 2), (ushort)0);
        BitConverter.TryWriteBytes(buffer.AsSpan(10, 2), (ushort)substitute.Length);
        BitConverter.TryWriteBytes(buffer.AsSpan(12, 2), (ushort)(substitute.Length + 2));
        BitConverter.TryWriteBytes(buffer.AsSpan(14, 2), (ushort)print.Length);
        substitute.CopyTo(buffer, 16);
        print.CopyTo(buffer, 16 + substitute.Length + 2);

        const uint GenericWrite = 0x40000000;
        const uint ShareAll = 7;
        const uint OpenExisting = 3;
        const uint BackupSemanticsAndOpenReparsePoint = 0x02000000 | 0x00200000;
        const uint FsctlSetReparsePoint = 0x000900A4;

        using var handle = CreateFileW(
            link, GenericWrite, ShareAll, IntPtr.Zero, OpenExisting, BackupSemanticsAndOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new InvalidOperationException($"CreateFileW failed with Win32 error {Marshal.GetLastWin32Error()}");
        }

        if (!DeviceIoControl(handle, FsctlSetReparsePoint, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            throw new InvalidOperationException($"FSCTL_SET_REPARSE_POINT failed with Win32 error {Marshal.GetLastWin32Error()}");
        }

        return link;
    }

    /// <summary>
    /// Windows only: the substitute name for a junction to <paramref name="absoluteDrivePath"/> in
    /// one of four spellings. <c>globalroot</c>: the NT root alias, the device and the remainder.
    /// <c>device-no-prefix</c>: the device and the remainder, without any NT prefix.
    /// <c>unc</c>: the NT UNC prefix, localhost, the drive letter and a dollar sign, and the
    /// remainder. <c>drive</c>: the NT prefix and the drive-letter path (what <c>mklink /J</c>
    /// writes). The device name is asked of the operating system at run time, so no machine value
    /// is committed. A drive that is not a real volume (a subst drive) is refused.
    /// </summary>
    public static string NtSubstituteName(string kind, string absoluteDrivePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        var full = Path.GetFullPath(absoluteDrivePath);
        var letter = full[0];
        var drive = full[..2];
        var remainder = full[2..];

        var buffer = new char[1024];
        var length = QueryDosDeviceW(drive, buffer, (uint)buffer.Length);
        var device = length == 0 ? "" : new string(buffer, 0, (int)length).Split('\0')[0];
        if (!device.StartsWith(@"\Device\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("the drive of the scratch directory does not map to a device name");
        }

        return kind switch
        {
            "globalroot" => @"\??\GLOBALROOT" + device + remainder,
            "device-no-prefix" => device + remainder,
            "unc" => @"\??\UNC\localhost\" + letter + "$" + remainder,
            "drive" => @"\??\" + full,
            _ => throw new ArgumentException("unknown substitute name kind", nameof(kind)),
        };
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeviceIoControl(
        SafeFileHandle device, uint controlCode, byte[] inBuffer, int inBufferSize,
        IntPtr outBuffer, int outBufferSize, out int bytesReturned, IntPtr overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint QueryDosDeviceW(string deviceName, [Out] char[] targetPath, uint maxChars);

    /// <summary>
    /// Creates a hard link at <paramref name="linkPath"/> (any absolute path, for example in a
    /// sandbox's output directory) to the existing file <paramref name="target"/> on the same
    /// volume and returns the link path. Windows: <c>cmd.exe /c mklink /H</c>, no privilege
    /// needed. Elsewhere: <c>ln</c>.
    /// </summary>
    public string HardLink(string linkPath, string target)
    {
        var limits = new ProcessLimits(TimeSpan.FromSeconds(30), 1024 * 1024, 1024 * 1024);
        var result = OperatingSystem.IsWindows()
            ? BoundedProcess.Run("cmd.exe", ["/c", "mklink", "/H", linkPath, target], limits)
            : BoundedProcess.Run("ln", [target, linkPath], limits);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"hard link exited {result.ExitCode}: {result.StderrTail}");
        }

        _fileEntries.Add(linkPath);
        return linkPath;
    }

    /// <summary>
    /// Creates a file symbolic link at <paramref name="linkPath"/>. Returns false when the host
    /// refuses it (on Windows this needs a privilege the account may not hold), true otherwise.
    /// </summary>
    public bool TryFileLink(string linkPath, string target)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, target);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return false;
        }

        _fileEntries.Add(linkPath);
        return true;
    }

    public void Dispose()
    {
        foreach (var entry in _fileEntries)
        {
            try
            {
                // Removes only the directory entry, never the file it points to.
                File.Delete(entry);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // already gone
            }
        }

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
