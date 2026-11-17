using Vdiff.Core.Cli;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Safety;

/// <summary>
/// An <c>--out</c> that reaches the analysed repository through a symbolic link or a directory
/// junction must be refused exactly like a plain path inside it (WR-01, T-01-36, roadmap criterion
/// 2). Every repository is synthetic (D-09). Every link is made through <see cref="LinkScratch"/>,
/// outside the sandbox.
/// </summary>
[Collection("sidecar")]
public class AliasedOutTests
{
    const string Refusal = "--out must not lie inside the analysed repository";

    static readonly string[] FiveFiles =
        ["base.model.json", "head.model.json", "diff.json", "summary.mmd", "report.html"];

    readonly SidecarFixture _sidecar;

    public AliasedOutTests(SidecarFixture sidecar) => _sidecar = sidecar;

    static (string BaseId, string HeadId) BuildSmallHistory(SyntheticRepo repo)
    {
        repo.Write("include/a.h", "class Alpha {\npublic:\n    int size() const;\n};\n");
        var baseId = repo.Commit("base");
        repo.Write("include/a.h", "class Alpha {\npublic:\n    long size() const;\n    void grow(int by);\n};\n");
        var headId = repo.Commit("head");
        return (baseId, headId);
    }

    static int RunCli(IReadOnlyList<string> args, out string stdout, out string stderr)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var exit = CliApp.Run(args, outWriter, errWriter);
        stdout = outWriter.ToString();
        stderr = errWriter.ToString();
        return exit;
    }

    static List<string> Args(string repoPath, string baseId, string headId, string outPath) =>
        ["--repo", repoPath, "--base", baseId, "--head", headId, "--out", outPath];

    static void AssertRefusedAndUntouched(SyntheticRepo repo, RepoSnapshot before, int exit, string stderr, string created)
    {
        Assert.Equal(1, exit);
        Assert.StartsWith("vdiff: ", stderr);
        Assert.Contains(Refusal, stderr);
        Assert.Empty(Directory.EnumerateDirectories(repo.RepoDir, created, SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Theory]
    [InlineData("", "aliased-out")]
    [InlineData("", "include/aliased-out")]
    [InlineData("", "")]
    [InlineData("include", "aliased-out")]
    [InlineData(".git", "aliased-out")]
    public void An_out_reached_through_a_directory_link_into_the_repository_is_refused_and_nothing_is_created(
        string linkTargetBelowRepo, string suffix)
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var target = linkTargetBelowRepo.Length == 0 ? repo.RepoDir : Path.Combine(repo.RepoDir, linkTargetBelowRepo);
        var link = links.Link("alias", target);
        var outPath = suffix.Length == 0 ? link : Path.Combine(link, suffix.Replace('/', Path.DirectorySeparatorChar));
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, outPath), out _, out var stderr);

        AssertRefusedAndUntouched(repo, before, exit, stderr, "aliased-out");
    }

    [Fact]
    public void A_repo_reached_through_a_directory_link_still_refuses_an_out_inside_it()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var repoLink = links.Link("repo-alias", repo.RepoDir);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repoLink, baseId, headId, Path.Combine(repo.RepoDir, "results")), out _, out var stderr);

        AssertRefusedAndUntouched(repo, before, exit, stderr, "results");
    }

    [Fact]
    public void An_out_reached_through_a_chain_of_links_into_the_repository_is_refused()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var first = links.Link("first", repo.RepoDir);
        var second = links.Link("second", first);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, Path.Combine(second, "aliased-out")), out _, out var stderr);

        AssertRefusedAndUntouched(repo, before, exit, stderr, "aliased-out");
    }

    [Fact]
    public void An_out_through_a_link_loop_is_refused_with_a_fixed_message_that_names_no_path()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var first = links.Link("loop-a", Path.Combine(links.Root, "loop-b"));
        links.Link("loop-b", first);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, Path.Combine(first, "x")), out var stdout, out var stderr);

        Assert.Equal(1, exit);
        Assert.Equal("vdiff: " + FinalPath.UnresolvableMessage + "\n", stderr);
        Assert.Empty(stdout);
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Fact]
    public void An_out_reached_through_a_directory_link_to_a_directory_outside_the_repository_is_still_accepted()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var link = links.Link("outside", repo.OutDir);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, Path.Combine(link, "run")), out _, out var stderr);

        Assert.Equal(0, exit);
        Assert.Empty(stderr);
        foreach (var name in FiveFiles)
        {
            Assert.True(File.Exists(Path.Combine(repo.OutDir, "run", name)), name);
        }

        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    static void AssertUnsupportedRootAndUntouched(
        SyntheticRepo repo, RepoSnapshot before, int exit, string stdout, string stderr)
    {
        Assert.Equal(1, exit);
        Assert.Equal("vdiff: " + FinalPath.UnsupportedRootMessage + "\n", stderr);
        Assert.Empty(stdout);
        Assert.Empty(Directory.EnumerateDirectories(repo.RepoDir, "aliased-out", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Theory]
    [InlineData("extended")]
    [InlineData("device")]
    [InlineData("loopback-unc")]
    [InlineData("extended-unc")]
    public void An_out_in_a_non_drive_namespace_is_refused_with_the_fixed_message_and_nothing_is_created(string kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var outPath = PathSpellings.NonDriveNamespace(kind, Path.Combine(repo.RepoDir, "aliased-out"));
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, outPath), out var stdout, out var stderr);

        AssertUnsupportedRootAndUntouched(repo, before, exit, stdout, stderr);
    }

    [Fact]
    public void A_repo_spelled_in_the_extended_namespace_still_refuses_an_out_inside_it()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var extendedRepo = PathSpellings.NonDriveNamespace("extended", repo.RepoDir);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(extendedRepo, baseId, headId, Path.Combine(repo.RepoDir, "results")), out _, out var stderr);

        AssertRefusedAndUntouched(repo, before, exit, stderr, "results");
    }

    [Fact]
    public void An_out_through_a_junction_with_a_volume_guid_target_is_refused_with_the_fixed_message()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var alias = links.Link("alias", PathSpellings.VolumeGuidPath(repo.RepoDir));
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, Path.Combine(alias, "aliased-out")), out var stdout, out var stderr);

        AssertUnsupportedRootAndUntouched(repo, before, exit, stdout, stderr);
    }

    [Fact]
    public void A_sibling_directory_whose_name_extends_the_repository_name_is_still_accepted()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var sibling = Path.Combine(repo.Root, "repo-out");
        var before = RepoSnapshot.Take(repo.Root, sibling);

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, sibling), out _, out var stderr);

        Assert.Equal(0, exit);
        Assert.Empty(stderr);
        foreach (var name in FiveFiles)
        {
            Assert.True(File.Exists(Path.Combine(sibling, name)), name);
        }

        Assert.Equal(before, RepoSnapshot.Take(repo.Root, sibling));
    }
}
