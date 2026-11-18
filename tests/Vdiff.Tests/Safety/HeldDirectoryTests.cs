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
}
