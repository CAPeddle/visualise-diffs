using Vdiff.Core;
using Vdiff.Core.Cli;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Safety;

/// <summary>
/// The contract of <see cref="FinalPath.Resolve"/> (T-01-36, T-01-38). Every expected value is
/// derived from <c>FinalPath.Resolve</c> of a real directory and never from the raw temp path,
/// because a temp area can itself sit behind a link. All links come from <see cref="LinkScratch"/>.
/// </summary>
public class FinalPathTests
{
    static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    static void AssertSamePath(string expected, string actual) => Assert.True(
        string.Equals(expected, actual, Comparison), $"expected '{expected}' but got '{actual}'");

    static string MakeReal(LinkScratch links, string name)
    {
        var path = Path.Combine(links.Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void A_path_below_a_real_directory_keeps_the_missing_remainder_unchanged()
    {
        using var links = new LinkScratch();
        var real = MakeReal(links, "real");
        var resolvedReal = FinalPath.Resolve(real);

        var actual = FinalPath.Resolve(Path.Combine(real, "missing", "deeper", "file.txt"));

        AssertSamePath(Path.Combine(resolvedReal, "missing", "deeper", "file.txt"), actual);
    }

    [Fact]
    public void A_link_resolves_to_its_target_and_the_missing_remainder_is_appended()
    {
        using var links = new LinkScratch();
        var real = MakeReal(links, "real");
        var resolvedReal = FinalPath.Resolve(real);
        var link = links.Link("alias", real);

        AssertSamePath(resolvedReal, FinalPath.Resolve(link));
        AssertSamePath(Path.Combine(resolvedReal, "not-there", "x"), FinalPath.Resolve(Path.Combine(link, "not-there", "x")));
    }

    [Fact]
    public void A_link_in_the_middle_of_a_path_is_resolved_and_the_rest_follows_it()
    {
        using var links = new LinkScratch();
        var real = MakeReal(links, "real");
        Directory.CreateDirectory(Path.Combine(real, "sub"));
        var resolvedReal = FinalPath.Resolve(real);
        var link = links.Link("alias", real);

        var actual = FinalPath.Resolve(Path.Combine(link, "sub", "file.txt"));

        AssertSamePath(Path.Combine(resolvedReal, "sub", "file.txt"), actual);
    }

    [Fact]
    public void A_chain_of_links_resolves_to_the_final_target()
    {
        using var links = new LinkScratch();
        var real = MakeReal(links, "real");
        var resolvedReal = FinalPath.Resolve(real);
        var first = links.Link("first", real);
        var second = links.Link("second", first);

        AssertSamePath(Path.Combine(resolvedReal, "x"), FinalPath.Resolve(Path.Combine(second, "x")));
    }

    [Fact]
    public void A_relative_link_target_is_resolved_from_the_links_own_directory()
    {
        // On Windows the junction holds an absolute target, so the relative branch runs only on
        // non-Windows hosts.
        using var links = new LinkScratch();
        var real = MakeReal(links, "real");
        var resolvedReal = FinalPath.Resolve(real);
        var link = links.Link("rel", real, relativeTarget: true);

        AssertSamePath(Path.Combine(resolvedReal, "x"), FinalPath.Resolve(Path.Combine(link, "x")));
    }

    [Fact]
    public void A_link_to_a_missing_target_resolves_to_that_missing_path()
    {
        using var links = new LinkScratch();
        var resolvedRoot = FinalPath.Resolve(links.Root);
        var link = links.Link("dangling", Path.Combine(links.Root, "no-such-dir"));

        var actual = FinalPath.Resolve(Path.Combine(link, "x"));

        AssertSamePath(Path.Combine(resolvedRoot, "no-such-dir", "x"), actual);
    }

    [Fact]
    public void A_link_loop_fails_closed_with_the_fixed_message()
    {
        using var links = new LinkScratch();
        var a = links.Link("a", Path.Combine(links.Root, "b"));
        links.Link("b", a);

        var e = Assert.Throws<VdiffException>(() => FinalPath.Resolve(Path.Combine(a, "x")));

        Assert.Equal(FinalPath.UnresolvableMessage, e.Message);
        Assert.DoesNotContain(links.Root, e.Message, Comparison);
    }

    [Fact]
    public void A_cycle_through_ancestor_links_fails_closed_with_the_fixed_message()
    {
        using var links = new LinkScratch();
        var first = links.Link("first", Path.Combine(links.Root, "second", "sub"));
        links.Link("second", Path.Combine(links.Root, "first", "sub"));

        var e = Assert.Throws<VdiffException>(() => FinalPath.Resolve(Path.Combine(first, "x")));

        Assert.Equal(FinalPath.UnresolvableMessage, e.Message);
        Assert.DoesNotContain(links.Root, e.Message, Comparison);
    }

    [Theory]
    [InlineData("extended")]
    [InlineData("device")]
    [InlineData("loopback-unc")]
    [InlineData("extended-unc")]
    [InlineData("volume-guid-path")]
    public void A_path_outside_the_drive_letter_namespace_is_refused_on_windows(string kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var links = new LinkScratch();
        var spelled = PathSpellings.NonDriveNamespace(kind, Path.Combine(MakeReal(links, "real"), "x"));

        var e = Assert.Throws<VdiffException>(() => FinalPath.Resolve(spelled));

        Assert.Equal(FinalPath.UnsupportedRootMessage, e.Message);
        Assert.DoesNotContain(links.Root, e.Message, Comparison);
    }

    [Fact]
    public void A_junction_whose_target_is_a_volume_guid_path_is_refused_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var links = new LinkScratch();
        var real = MakeReal(links, "real");
        var alias = links.Link("alias", PathSpellings.VolumeGuidPath(real));

        var e = Assert.Throws<VdiffException>(() => FinalPath.Resolve(Path.Combine(alias, "x")));

        Assert.Equal(FinalPath.UnsupportedRootMessage, e.Message);
    }

    [Theory]
    [InlineData("globalroot")]
    [InlineData("device-no-prefix")]
    [InlineData("unc")]
    public void A_junction_with_an_nt_namespace_target_is_refused_on_windows(string kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var links = new LinkScratch();
        var real = MakeReal(links, "real");
        File.WriteAllText(Path.Combine(real, "marker.txt"), "marker");
        // The print name is a drive-letter decoy: the reported target must still be the substitute name.
        var alias = links.RawJunction("alias", LinkScratch.NtSubstituteName(kind, real), links.Root);

        var reported = new DirectoryInfo(alias).LinkTarget;
        Assert.NotNull(reported);
        Assert.False(FinalPath.StartsWithDriveLetter(reported), "the reported target is a drive-letter path, so the premise failed");
        if (kind != "unc")
        {
            Assert.True(File.Exists(Path.Combine(alias, "marker.txt")), "the junction is not live");
        }

        var e = Assert.Throws<VdiffException>(() => FinalPath.Resolve(Path.Combine(alias, "x")));

        Assert.Equal(FinalPath.UnsupportedRootMessage, e.Message);
        Assert.DoesNotContain(links.Root, e.Message, Comparison);
    }

    [Fact]
    public void A_raw_junction_with_a_drive_letter_target_is_followed_like_a_plain_junction()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var links = new LinkScratch();
        var real = MakeReal(links, "real");
        File.WriteAllText(Path.Combine(real, "marker.txt"), "marker");
        var alias = links.RawJunction("alias", LinkScratch.NtSubstituteName("drive", real));

        var reported = new DirectoryInfo(alias).LinkTarget;
        Assert.NotNull(reported);
        Assert.True(FinalPath.StartsWithDriveLetter(reported), "the reported target is not a drive-letter path");
        Assert.True(File.Exists(Path.Combine(alias, "marker.txt")), "the junction is not live");
        AssertSamePath(Path.Combine(FinalPath.Resolve(real), "x"), FinalPath.Resolve(Path.Combine(alias, "x")));
    }

    [Theory]
    [InlineData(@"C:\work\out", true)]
    [InlineData(@"z:\x", true)]
    [InlineData(@"C:\", true)]
    [InlineData(@"GLOBALROOT\Device\HarddiskVolumeN\work", false)]
    [InlineData(@"\Device\HarddiskVolumeN\work", false)]
    [InlineData(@"ice\HarddiskVolumeN\work", false)]
    [InlineData(@"UNC\localhost\C$\work", false)]
    [InlineData(@"\\localhost\C$\work", false)]
    [InlineData(@"\\?\C:\work", false)]
    [InlineData(@"\\.\C:\work", false)]
    [InlineData(@"Volume{00000000-0000-0000-0000-000000000000}\work", false)]
    [InlineData(@"..\work", false)]
    [InlineData("work", false)]
    [InlineData(@"\work", false)]
    [InlineData("C:work", false)]
    [InlineData("C:", false)]
    [InlineData("", false)]
    [InlineData("\u212A:\\work", false)]
    public void A_reported_link_target_is_accepted_only_when_it_is_a_drive_letter_path(string reported, bool expected)
    {
        Assert.Equal(expected, FinalPath.StartsWithDriveLetter(reported));
    }

    [Fact]
    public void A_drive_letter_path_with_a_lower_case_drive_and_forward_slashes_is_still_accepted()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var links = new LinkScratch();
        var real = MakeReal(links, "real");
        var resolvedReal = FinalPath.Resolve(real);
        var spelled = char.ToLowerInvariant(real[0]) + real[1..].Replace('\\', '/');

        AssertSamePath(resolvedReal, FinalPath.Resolve(spelled));
    }
}
