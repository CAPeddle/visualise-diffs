using Vdiff.Core;
using Vdiff.Core.Cli;
using Vdiff.Core.Git;
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

    [Theory]
    [InlineData("globalroot")]
    [InlineData("device-no-prefix")]
    public void An_out_through_a_junction_with_an_nt_namespace_target_is_refused_with_the_fixed_message(string kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        // The print name is a drive-letter decoy: a reader that reported it instead of the
        // substitute name would let the junction through, so this pins the premise too.
        var alias = links.RawJunction("alias", LinkScratch.NtSubstituteName(kind, repo.RepoDir), links.Root);
        Assert.True(File.Exists(Path.Combine(alias, "include", "a.h")), "the junction is not live");
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

    static readonly string[] OutputNames =
        ["base.model.json", "head.model.json", "diff.json", "summary.mmd", "report.html"];

    [Fact]
    public void A_hard_link_planted_at_every_output_name_is_replaced_and_the_repository_files_are_unchanged()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var victims = new string[OutputNames.Length];
        for (var i = 0; i < OutputNames.Length; i++)
        {
            victims[i] = Path.Combine(repo.RepoDir, $"victim-{i + 1}.txt");
            File.WriteAllText(victims[i], $"original {i + 1}");
            links.HardLink(Path.Combine(repo.OutDir, OutputNames[i]), victims[i]);
        }

        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, repo.OutDir), out _, out var stderr);

        Assert.Equal(0, exit);
        Assert.Empty(stderr);
        for (var i = 0; i < OutputNames.Length; i++)
        {
            Assert.Equal($"original {i + 1}", File.ReadAllText(victims[i]));
            Assert.NotEqual($"original {i + 1}", File.ReadAllText(Path.Combine(repo.OutDir, OutputNames[i])));
        }

        Assert.StartsWith("classDiagram", File.ReadAllText(Path.Combine(repo.OutDir, "summary.mmd")));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Fact]
    public void A_file_symbolic_link_planted_at_an_output_name_is_replaced_and_its_target_is_unchanged()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var victim = Path.Combine(repo.RepoDir, "victim-1.txt");
        File.WriteAllText(victim, "original 1");
        var linkPath = Path.Combine(repo.OutDir, "diff.json");
        if (!links.TryFileLink(linkPath, victim))
        {
            return; // no privilege to create a file symbolic link on this host
        }

        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, repo.OutDir), out _, out var stderr);

        Assert.Equal(0, exit);
        Assert.Empty(stderr);
        Assert.Equal("original 1", File.ReadAllText(victim));
        Assert.Null(new FileInfo(linkPath).LinkTarget);
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Fact]
    public void A_failed_write_leaves_no_temporary_file_behind()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        Directory.CreateDirectory(Path.Combine(repo.OutDir, "diff.json"));

        var exit = RunCli(Args(repo.RepoDir, baseId, headId, repo.OutDir), out _, out var stderr);

        Assert.Equal(1, exit);
        Assert.StartsWith("vdiff: ", stderr);
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir, "*.tmp-*"));
    }

    [Fact]
    public void An_out_swapped_for_a_link_into_the_repository_after_the_first_check_is_refused_before_the_write()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        Directory.CreateDirectory(links.Root);
        var outPath = Path.Combine(links.Root, "swap", "aliased-out");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        var hookRan = false;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        var exit = CliApp.Run(
            Args(repo.RepoDir, baseId, headId, outPath),
            outWriter,
            errWriter,
            () =>
            {
                hookRan = true;
                links.Link("swap", repo.RepoDir);
            });

        Assert.True(hookRan);
        Assert.Equal(1, exit);
        Assert.Contains(Refusal, errWriter.ToString());
        Assert.Empty(Directory.EnumerateDirectories(repo.RepoDir, "aliased-out", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    /// <summary>
    /// True when the operating system refused the attempt with a sharing violation or access denied.
    /// A missing directory is not a refusal: it would mean the attempt never touched the target.
    /// </summary>
    static bool OsRefused(Action attempt)
    {
        try
        {
            attempt();
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e is not (DirectoryNotFoundException or FileNotFoundException);
        }
    }

    static void RenameAndBack(string directory)
    {
        var moved = directory + "-moved";
        Directory.Move(directory, moved);
        Directory.Move(moved, directory); // the attempt succeeded: undo it so the run can continue
    }

    static void DeleteAndRecreate(string directory)
    {
        Directory.Delete(directory);
        Directory.CreateDirectory(directory); // the attempt succeeded: undo it so the run can continue
    }

    [Fact]
    public void A_held_directory_cannot_be_renamed_or_deleted_by_another_process_during_the_write()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var inner = Path.Combine(repo.OutDir, "held", "inner");
        Directory.CreateDirectory(inner);
        var outPath = Path.Combine(inner, "run");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        var outcomes = new List<(string Attempt, bool Refused)>();
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // The hook records and never asserts: Run turns every exception into exit 1 (H-5).
        var hooks = new CliHooks(AfterHeld: path =>
        {
            var parent = Path.GetDirectoryName(path)!;
            var grandparent = Path.GetDirectoryName(parent)!;
            outcomes.Add(("rename the output directory", OsRefused(() => RenameAndBack(path))));
            outcomes.Add(("rename its parent", OsRefused(() => RenameAndBack(parent))));
            outcomes.Add(("rename its grandparent", OsRefused(() => RenameAndBack(grandparent))));
            outcomes.Add(("delete the output directory", OsRefused(() => DeleteAndRecreate(path))));
        });

        var exit = CliApp.Run(Args(repo.RepoDir, baseId, headId, outPath), outWriter, errWriter, hooks);

        Assert.True(exit == 0, errWriter.ToString());
        Assert.Equal(4, outcomes.Count);
        var allowed = outcomes.Where(o => !o.Refused).Select(o => o.Attempt).ToList();
        Assert.True(allowed.Count == 0, "the operating system did not refuse to: " + string.Join("; ", allowed));

        foreach (var name in FiveFiles)
        {
            Assert.True(File.Exists(Path.Combine(outPath, name)), name);
        }

        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir, "*-moved", SearchOption.AllDirectories));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Fact]
    public void A_link_entry_retargeted_while_the_held_directory_is_open_does_not_redirect_the_write()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var real = Path.Combine(links.Root, "real");
        Directory.CreateDirectory(Path.Combine(real, "include"));
        var link = links.Link("jn", real);
        var outPath = Path.Combine(link, "include");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // Deleting and re-creating the link entry is allowed while the target is held (measured at
        // planning time), so only a write by the held directory's own path stays out of the repository.
        var hooks = new CliHooks(AfterHeld: _ =>
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.Delete(link); // not recursive: removes only the junction
            }
            else
            {
                File.Delete(link);
            }

            links.Link("jn", repo.RepoDir);
        });

        var exit = CliApp.Run(Args(repo.RepoDir, baseId, headId, outPath), outWriter, errWriter, hooks);

        Assert.True(exit == 0, errWriter.ToString());
        foreach (var name in FiveFiles)
        {
            Assert.True(File.Exists(Path.Combine(real, "include", name)), name);
        }

        Assert.Equal(["a.h"], Directory.GetFileSystemEntries(Path.Combine(repo.RepoDir, "include")).Select(Path.GetFileName));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
        Assert.Empty(Directory.GetFileSystemEntries(real, "*.tmp-*", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFileSystemEntries(repo.RepoDir, "*.tmp-*", SearchOption.AllDirectories));
    }

    [Fact]
    public void A_swap_before_the_held_directory_is_opened_is_refused_by_its_final_path()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var outPath = Path.Combine(links.Root, "swap", "aliased-out");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        var hookRan = false;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // The swap lands after the second containment check and before the directory is held.
        var hooks = new CliHooks(AfterSecondCheck: () =>
        {
            hookRan = true;
            links.Link("swap", repo.RepoDir);
        });

        var exit = CliApp.Run(Args(repo.RepoDir, baseId, headId, outPath), outWriter, errWriter, hooks);

        Assert.True(hookRan);
        Assert.Equal("vdiff: " + Refusal + "\n", errWriter.ToString());
        Assert.Empty(outWriter.ToString());
        AssertRefusedAndUntouched(repo, before, exit, errWriter.ToString(), "aliased-out");
        Assert.Empty(Directory.GetFileSystemEntries(repo.RepoDir, "*.tmp-*", SearchOption.AllDirectories));
    }

    [Fact]
    public void A_protected_root_in_a_non_drive_namespace_is_refused_even_when_out_is_a_plain_path()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        var extendedRoot = PathSpellings.NonDriveNamespace("extended", repo.RepoDir);
        var gitDir = Path.Combine(repo.RepoDir, ".git");
        var location = new RepoLocation(extendedRoot, gitDir, gitDir, false);

        var e = Assert.Throws<VdiffException>(
            () => CliApp.RefuseUnsafeOut(Path.Combine(repo.RepoDir, "results"), location));

        Assert.Equal(FinalPath.UnsupportedRootMessage, e.Message);
    }

    // ---- VDIFF_PROTECTED_DIRS: the directories the CLI cannot know from its arguments (G-01-1, T-01-56, T-01-58) ----

    static string ProtectedRefusalLine => "vdiff: " + CliApp.ProtectedOutRefusal + "\n";

    /// <summary>Hooks that supply the variable and nothing else, so no process-wide environment changes.</summary>
    static CliHooks WithProtected(string? value, CliHooks? more = null) =>
        (more ?? new CliHooks()) with { GetEnvironmentVariable = name => name == CliApp.ProtectedDirectoriesVariable ? value : null };

    [Theory]
    [InlineData("below")]
    [InlineData("itself")]
    [InlineData("through-link")]
    public void An_out_inside_a_directory_named_by_the_protected_directories_variable_is_refused(string kind)
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var vroot = Path.Combine(repo.Root, "vroot");
        Directory.CreateDirectory(vroot);
        var outPath = kind switch
        {
            "below" => Path.Combine(vroot, "inner", "run"),
            "itself" => vroot,
            "through-link" => Path.Combine(links.Link("jv", vroot), "run"),
            _ => throw new InvalidOperationException(kind),
        };
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        var exit = CliApp.Run(Args(repo.RepoDir, baseId, headId, outPath), outWriter, errWriter, WithProtected(vroot));

        Assert.Equal(1, exit);
        Assert.Equal(ProtectedRefusalLine, errWriter.ToString());
        Assert.Empty(outWriter.ToString());
        Assert.Empty(Directory.GetFileSystemEntries(vroot));
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("empty-entry")]
    [InlineData("nine-entries")]
    public void A_malformed_protected_directories_variable_is_refused_with_a_fixed_message_naming_no_value(string kind)
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var separator = Path.PathSeparator.ToString();
        var absolute = Enumerable.Range(0, 9).Select(i => Path.Combine(repo.Root, "protected-" + i)).ToArray();
        var value = kind switch
        {
            "relative" => "relative-protected-directory",
            "empty-entry" => absolute[0] + separator + separator + absolute[1],
            "nine-entries" => string.Join(separator, absolute),
            _ => throw new InvalidOperationException(kind),
        };
        var outPath = Path.Combine(repo.Root, "never-out");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        var exit = CliApp.Run(Args(repo.RepoDir, baseId, headId, outPath), outWriter, errWriter, WithProtected(value));

        Assert.Equal(1, exit);
        Assert.Equal("vdiff: " + CliApp.ProtectedDirectoriesMalformed + "\n", errWriter.ToString());
        Assert.Contains(CliApp.ProtectedDirectoriesVariable, errWriter.ToString());
        Assert.DoesNotContain(value, errWriter.ToString());
        Assert.Empty(outWriter.ToString());
        Assert.False(Directory.Exists(outPath));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Fact]
    public void A_swap_into_a_protected_directories_entry_after_the_second_check_is_refused_by_the_held_directory()
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (baseId, headId) = BuildSmallHistory(repo);
        var vroot = Path.Combine(repo.Root, "vroot");
        Directory.CreateDirectory(vroot);
        var outPath = Path.Combine(links.Root, "swap", "aliased-out");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        var hookRan = false;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // The swap lands after the second containment check and before the directory is held.
        var hooks = WithProtected(vroot, new CliHooks(AfterSecondCheck: () =>
        {
            hookRan = true;
            links.Link("swap", vroot);
        }));

        var exit = CliApp.Run(Args(repo.RepoDir, baseId, headId, outPath), outWriter, errWriter, hooks);

        Assert.True(hookRan);
        Assert.Equal(1, exit);
        Assert.Equal(ProtectedRefusalLine, errWriter.ToString());
        Assert.Empty(outWriter.ToString());
        Assert.Empty(Directory.GetFileSystemEntries(vroot));
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Fact]
    public void An_out_outside_every_protected_directories_entry_is_still_written()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var vroot = Path.Combine(repo.Root, "vroot");
        Directory.CreateDirectory(vroot);
        var outPath = Path.Combine(repo.OutDir, "run");
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        var exit = CliApp.Run(Args(repo.RepoDir, baseId, headId, outPath), outWriter, errWriter, WithProtected(vroot));

        Assert.True(exit == 0, errWriter.ToString());
        Assert.Empty(errWriter.ToString());
        foreach (var name in FiveFiles)
        {
            Assert.True(File.Exists(Path.Combine(outPath, name)), name);
        }

        Assert.Empty(Directory.GetFileSystemEntries(vroot));
    }

    // ---- --out given as a drive root (CR-01, T-01-60, T-01-62) ----

    /// <summary>Thrown from the AfterHeld seam so the run stops before the first byte is written.</summary>
    sealed class StopBeforeFirstWrite : Exception;

    /// <summary>Trailing separators removed, never below the root of the path (test-local; the product trim is under test).</summary>
    static string TrimKeepingRoot(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length < root.Length ? root : trimmed;
    }

    [Fact]
    public void An_out_given_as_a_drive_root_reaches_the_held_directory_as_that_root_and_not_the_current_directory()
    {
        // Safe on a development machine by construction: the hook records the directory every file would
        // go to and throws before OutputWriter.WriteAll, so nothing is written into a real drive root, and
        // the process's current directory (process-wide state shared with parallel tests) is not changed.
        var currentDirectory = Directory.GetCurrentDirectory();
        var root = Path.GetPathRoot(currentDirectory)!;
        Assert.NotEqual(
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparer.OrdinalIgnoreCase);

        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        string? recorded = null;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        var hooks = WithProtected(null, new CliHooks(AfterHeld: path =>
        {
            recorded = path;
            throw new StopBeforeFirstWrite();
        }));

        var exit = CliApp.Run(Args(repo.RepoDir, baseId, headId, root), outWriter, errWriter, hooks);

        Assert.True(recorded is not null, "the run did not reach the held directory: " + errWriter);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Assert.False(
            string.Equals(TrimKeepingRoot(recorded!), TrimKeepingRoot(currentDirectory), comparison),
            "the held directory is the process's current directory, not the typed root");
        Assert.True(
            string.Equals(TrimKeepingRoot(recorded!), TrimKeepingRoot(root), comparison),
            "the held directory is not the typed root");
        Assert.Equal(1, exit);
        Assert.Equal("vdiff: unexpected failure (StopBeforeFirstWrite)\n", errWriter.ToString());
        Assert.Empty(outWriter.ToString());
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
        foreach (var name in FiveFiles)
        {
            Assert.False(File.Exists(Path.Combine(currentDirectory, name)), name);
        }
    }
}
