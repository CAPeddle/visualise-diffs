using Vdiff.Core;
using Vdiff.Core.Cli;
using Vdiff.Core.Output;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Safety;

/// <summary>
/// The held output directory in isolation (G-01-1, T-01-51, T-01-53): acquisition order, cleanup
/// after a refusal, release on dispose and the final-path forms. <see cref="LinkScratch"/> is used
/// only as a disposable temp root; no test here creates a link.
/// </summary>
public class HeldDirectoryTests
{
    const string Refused = "refused for the test";

    static string? RefuseName(string path, string name) =>
        string.Equals(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), name, StringComparison.OrdinalIgnoreCase)
            ? Refused
            : null;

    [Fact]
    public void Create_makes_the_missing_directories_and_returns_a_path_without_the_extended_prefix()
    {
        using var scratch = new LinkScratch();
        var directory = Path.Combine(scratch.Root, "a", "b", "c");

        using var held = HeldDirectory.Create(directory, _ => null);

        Assert.True(Directory.Exists(directory));
        Assert.True(Directory.Exists(held.Path));
        Assert.DoesNotContain(@"\\?\", held.Path);
        Assert.EndsWith(Path.Combine("a", "b", "c"), held.Path);
    }

    [Fact]
    public void A_protected_existing_ancestor_is_refused_before_any_directory_is_created()
    {
        using var scratch = new LinkScratch();
        var anchor = Path.Combine(scratch.Root, "anchor");
        Directory.CreateDirectory(anchor);
        var directory = Path.Combine(anchor, "x", "y");

        var e = Assert.Throws<VdiffException>(() => HeldDirectory.Create(directory, p => RefuseName(p, "anchor")));

        Assert.Equal(Refused, e.Message);
        Assert.False(Directory.Exists(Path.Combine(anchor, "x")));
    }

    [Fact]
    public void A_refusal_after_the_directories_exist_removes_the_ones_it_created()
    {
        using var scratch = new LinkScratch();
        var anchor = Path.Combine(scratch.Root, "anchor");
        Directory.CreateDirectory(anchor);
        var directory = Path.Combine(anchor, "x", "y");

        var e = Assert.Throws<VdiffException>(() => HeldDirectory.Create(directory, p => RefuseName(p, "y")));

        Assert.Equal(Refused, e.Message);
        Assert.False(Directory.Exists(Path.Combine(anchor, "x")));
        Assert.True(Directory.Exists(anchor));
    }

    [Fact]
    public void Dispose_releases_the_directory_so_it_can_be_renamed_again()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scratch = new LinkScratch();
        var directory = Path.Combine(scratch.Root, "held");
        var moved = Path.Combine(scratch.Root, "moved");
        var held = HeldDirectory.Create(directory, _ => null);

        var whileHeld = Record.Exception(() => Directory.Move(directory, moved));
        held.Dispose();

        Assert.True(whileHeld is IOException or UnauthorizedAccessException, "the hold did not refuse a rename");
        Directory.Move(directory, moved);
        Assert.True(Directory.Exists(moved));
    }

    [Theory]
    [InlineData(@"\\?\C:\a\b", @"C:\a\b", true)]
    [InlineData(@"\\?\c:\a", @"c:\a", true)]
    [InlineData(@"\\?\C:\", @"C:\", true)]
    [InlineData(@"\\?\UNC\server\share\x", @"\\server\share\x", false)]
    [InlineData(@"\\?\Volume{00000000-0000-0000-0000-000000000000}\x", null, false)]
    [InlineData(@"\\.\N:\x", null, false)]
    [InlineData(@"a\b", null, false)]
    [InlineData("", null, false)]
    public void A_final_path_is_accepted_only_as_a_drive_letter_path_or_a_unc_path(
        string finalPath, string? expectedPath, bool judged)
    {
        if (expectedPath is null)
        {
            var e = Assert.Throws<VdiffException>(() => HeldDirectory.Normalize(finalPath));
            Assert.Equal(FinalPath.UnsupportedRootMessage, e.Message);
            return;
        }

        var result = HeldDirectory.Normalize(finalPath);

        Assert.Equal(expectedPath, result.Path);
        Assert.Equal(judged, result.Judged);
    }

    // ---- A drive root is held as the root (CR-01, T-01-60) ----
    // The root is taken from the process's own current directory, never a literal drive letter: the
    // drive-relative form "C:" names the current directory on that drive, so only this choice makes
    // the rows discriminate. No test here changes the current directory or writes into the root; the
    // existing root is opened read-only and nothing is created (the remainder is empty).

    static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Removes trailing directory and alternate separators but never shortens the text below the
    /// root of the path, so "/", "///" and "C:\" become the bare root and ".../a/b/" becomes ".../a/b".
    /// Test code only: on Linux and macOS the held path is the typed spelling.
    /// </summary>
    static string TrimTrailingSeparatorsKeepingRoot(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length < root.Length ? root : trimmed;
    }

    static bool SamePath(string left, string right) =>
        string.Equals(TrimTrailingSeparatorsKeepingRoot(left), TrimTrailingSeparatorsKeepingRoot(right), PathComparison);

    static string CurrentDriveRoot(out string currentDirectory)
    {
        currentDirectory = Directory.GetCurrentDirectory();
        var root = Path.GetPathRoot(currentDirectory)!;
        Assert.False(SamePath(currentDirectory, root), "the test host runs from the drive root, so the rows cannot discriminate");
        return root;
    }

    [Theory]
    [InlineData("root")]
    [InlineData("root-with-alternate-separator")]
    [InlineData("root-with-two-trailing-separators")]
    public void A_drive_root_is_held_as_the_root_and_never_as_the_current_directory(string spelling)
    {
        var root = CurrentDriveRoot(out var currentDirectory);
        var typed = spelling switch
        {
            "root" => root,
            "root-with-alternate-separator" => root.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            "root-with-two-trailing-separators" => root + Path.DirectorySeparatorChar + Path.AltDirectorySeparatorChar,
            _ => throw new ArgumentOutOfRangeException(nameof(spelling)),
        };

        using var held = HeldDirectory.Create(typed, _ => null);

        Assert.False(
            SamePath(held.Path, currentDirectory),
            "the held directory is the process's current directory, not the typed root");
        Assert.True(SamePath(held.Path, root), "the held directory is not the typed root");
    }

    [Theory]
    [InlineData("none")]
    [InlineData("separator")]
    [InlineData("alternate-separator")]
    public void A_directory_below_a_drive_root_is_held_without_its_trailing_separator_and_not_widened_to_the_root(string suffix)
    {
        var root = CurrentDriveRoot(out _);
        using var scratch = new LinkScratch();
        var below = Path.Combine(scratch.Root, "a", "b");
        var typed = suffix switch
        {
            "none" => below,
            "separator" => below + Path.DirectorySeparatorChar,
            "alternate-separator" => below + Path.AltDirectorySeparatorChar,
            _ => throw new ArgumentOutOfRangeException(nameof(suffix)),
        };

        using var held = HeldDirectory.Create(typed, _ => null);

        var path = TrimTrailingSeparatorsKeepingRoot(held.Path);
        Assert.EndsWith(Path.Combine("a", "b"), path, PathComparison);
        Assert.False(SamePath(path, root), "the directory was widened to the root");
        Assert.False(SamePath(path, scratch.Root), "the directory was narrowed to its parent");
        Assert.True(Directory.Exists(below));
    }

    [Fact]
    public void A_drive_root_is_judged_as_the_root_by_the_final_path_function_and_a_refusal_creates_nothing()
    {
        var root = CurrentDriveRoot(out var currentDirectory);
        var seen = new List<string>();

        var e = Assert.Throws<VdiffException>(() =>
        {
            using var held = HeldDirectory.Create(root, p =>
            {
                seen.Add(p);
                return SamePath(p, root) ? Refused : null;
            });
        });

        Assert.Equal(Refused, e.Message);
        Assert.Contains(seen, p => SamePath(p, root));
        Assert.DoesNotContain(seen, p => SamePath(p, currentDirectory));
        Assert.True(Directory.Exists(root));
    }
}
