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
}
