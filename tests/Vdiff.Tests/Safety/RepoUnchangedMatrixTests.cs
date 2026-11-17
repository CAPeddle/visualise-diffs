using Vdiff.Core.Cli;
using Vdiff.Core.Process;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Safety;

/// <summary>
/// ROADMAP criterion 2 (TRC-01): the analysed repository is identical before and after a run,
/// in every repository state the roadmap names. All repositories are synthetic (D-09).
/// Every scenario takes a <see cref="RepoSnapshot"/> immediately before and after
/// <see cref="CliApp.Run"/> and compares the fields one by one, so a failure shows which of
/// the tree hash, status, HEAD, branch, refs, worktrees or stash moved.
/// </summary>
[Collection("sidecar")]
public class RepoUnchangedMatrixTests
{
    static readonly string[] FiveFiles = ["base.model.json", "diff.json", "head.model.json", "report.html", "summary.mmd"];

    readonly SidecarFixture _sidecar;

    public RepoUnchangedMatrixTests(SidecarFixture sidecar) => _sidecar = sidecar;

    /// <summary>
    /// History: C1 (base) on main, C2 (head) on feature, C3 on main. Tracked in every commit:
    /// README.md, include/b.h and a .gitignore that ignores *.log, so the dirty state below can
    /// be made on any checkout.
    /// </summary>
    static (string C1, string C2, string C3) BuildHistory(SyntheticRepo repo)
    {
        repo.Write(".gitignore", "*.log\n");
        repo.Write("README.md", "# Sample\n");
        repo.Write("include/a.h", "class Alpha {\npublic:\n    int size() const;\n};\n");
        repo.Write("include/b.h", "struct Beta {\n    int id;\n};\n");
        var c1 = repo.Commit("base");

        repo.Git("checkout", "-q", "-b", "feature");
        repo.Write("include/a.h", "class Alpha {\npublic:\n    long size() const;\n    void grow(int by);\n};\n");
        var c2 = repo.Commit("head");

        repo.Git("checkout", "-q", "main");
        repo.Write("docs.txt", "notes\n");
        var c3 = repo.Commit("other");
        return (c1, c2, c3);
    }

    /// <summary>One stash entry, then a staged edit, an unstaged edit, an untracked file and an ignored file.</summary>
    static void MakeDirty(SyntheticRepo repo)
    {
        File.WriteAllText(Path.Combine(repo.RepoDir, "README.md"), "# Sample\n\nstashed edit\n");
        repo.Git("stash", "push", "-q", "-m", "synthetic stash");

        repo.Write("README.md", "# Sample\n\nstaged edit\n");
        File.WriteAllText(Path.Combine(repo.RepoDir, "include", "b.h"), "struct Beta {\n    int id;\n    int unstaged;\n};\n");
        File.WriteAllText(Path.Combine(repo.RepoDir, "notes.txt"), "untracked\n");
        File.WriteAllText(Path.Combine(repo.RepoDir, "build.log"), "ignored\n");
    }

    static int RunCli(string[] args, out string stdout, out string stderr)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var exit = CliApp.Run(args, outWriter, errWriter);
        stdout = outWriter.ToString();
        stderr = errWriter.ToString();
        return exit;
    }

    static string[] RunArgs(string repoDir, string baseId, string headId, string outDir) =>
        ["--repo", repoDir, "--base", baseId, "--head", headId, "--out", outDir];

    /// <summary>Field-by-field comparison, so a failure names what moved.</summary>
    static void AssertSameSnapshot(RepoSnapshot before, RepoSnapshot after)
    {
        Assert.Equal(before.TreeHash, after.TreeHash);
        var beforeLines = before.GitState.Split('\n');
        var afterLines = after.GitState.Split('\n');
        Assert.Equal(beforeLines, afterLines);
        Assert.Equal(before, after);
    }

    static void RunAndAssertUnchanged(
        SyntheticRepo repo, string repoArg, string baseId, string headId, IEnumerable<string>? gitDirs = null)
    {
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir, gitDirs);

        var exit = RunCli(RunArgs(repoArg, baseId, headId, repo.OutDir), out _, out var stderr);

        Assert.Equal("", stderr);
        Assert.Equal(0, exit);
        AssertSameSnapshot(before, RepoSnapshot.Take(repo.Root, repo.OutDir, gitDirs));
        Assert.Equal(FiveFiles, Directory.GetFiles(repo.OutDir).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Neither_commit_checked_out_with_a_dirty_tree_a_stash_and_a_linked_worktree_leaves_everything_unchanged()
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        var worktree = Path.Combine(repo.Root, "wt");
        repo.Git("worktree", "add", "-q", worktree, "feature");
        MakeDirty(repo);

        // Sanity: the scenario really is the one the roadmap names.
        Assert.Equal("main", repo.Git("rev-parse", "--abbrev-ref", "HEAD").Trim());
        Assert.Contains("M  README.md", repo.Git("status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Contains(" M include/b.h", repo.Git("status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Contains("?? notes.txt", repo.Git("status", "--porcelain=v1", "--untracked-files=all").Replace("\r", ""));
        Assert.DoesNotContain("build.log", repo.Git("status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Contains("synthetic stash", repo.Git("stash", "list"));
        Assert.Contains("[feature]", repo.Git("worktree", "list"));

        RunAndAssertUnchanged(repo, repo.RepoDir, c1, c2, [repo.RepoDir, worktree]);
    }

    [Fact]
    public void Detached_head_at_the_head_commit_with_a_dirty_tree_leaves_everything_unchanged()
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        repo.Checkout(c2, detach: true);
        MakeDirty(repo);

        Assert.Equal("HEAD", repo.Git("rev-parse", "--abbrev-ref", "HEAD").Trim());
        Assert.Equal(c2, repo.Git("rev-parse", "HEAD").Trim());

        RunAndAssertUnchanged(repo, repo.RepoDir, c1, c2);
    }

    [Fact]
    public void Detached_head_at_an_unrelated_commit_with_repo_given_as_a_subdirectory_leaves_everything_unchanged()
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, c3) = BuildHistory(repo);
        repo.Checkout(c3, detach: true);
        MakeDirty(repo);

        RunAndAssertUnchanged(repo, Path.Combine(repo.RepoDir, "include"), c1, c2);
    }

    [Fact]
    public void A_bare_repository_is_analysed_and_left_unchanged()
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        var bare = Path.Combine(repo.Root, "bare.git");
        repo.Git("clone", "--bare", "--quiet", repo.RepoDir, bare);
        Assert.Equal("true", repo.Git("-C", bare, "rev-parse", "--is-bare-repository").Trim());

        RunAndAssertUnchanged(repo, bare, c1, c2, [bare]);
    }

    [Fact]
    public void Two_runs_in_a_row_into_the_same_out_directory_leave_everything_unchanged_and_five_files()
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        MakeDirty(repo);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        for (var run = 0; run < 2; run++)
        {
            var exit = RunCli(RunArgs(repo.RepoDir, c1, c2, repo.OutDir), out _, out var stderr);
            Assert.Equal("", stderr);
            Assert.Equal(0, exit);
        }

        AssertSameSnapshot(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
        Assert.Equal(FiveFiles, Directory.GetFiles(repo.OutDir).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.GetDirectories(repo.OutDir));
    }

    [Fact]
    public void Everything_a_run_writes_lands_under_out_and_a_sibling_canary_is_untouched()
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        var canaryDir = Path.Combine(repo.Root, "sibling");
        Directory.CreateDirectory(canaryDir);
        var canary = Path.Combine(canaryDir, "canary.txt");
        File.WriteAllText(canary, "do not touch\n");
        var canaryTime = File.GetLastWriteTimeUtc(canary);
        var rootEntriesBefore = Directory.GetFileSystemEntries(repo.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(RunArgs(repo.RepoDir, c1, c2, repo.OutDir), out _, out var stderr);

        Assert.Equal("", stderr);
        Assert.Equal(0, exit);
        Assert.Equal("do not touch\n", File.ReadAllText(canary));
        Assert.Equal(canaryTime, File.GetLastWriteTimeUtc(canary));
        Assert.Equal(
            rootEntriesBefore,
            Directory.GetFileSystemEntries(repo.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        AssertSameSnapshot(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    // ---- fsmonitor (C25, T-01-30) ----

    static ProcessResult UnprotectedGit(string repoDir, params string[] args)
    {
        // Deliberately without GitCommand.Prefix: this is how an unguarded tool would call git.
        var full = new List<string> { "-C", repoDir };
        full.AddRange(args);
        var limits = new ProcessLimits(TimeSpan.FromSeconds(120), 64L * 1024 * 1024, 1024 * 1024);
        return BoundedProcess.Run("git", full, limits, null, GitCommand.BuildEnvironment());
    }

    static string ForwardSlashes(string path) => path.Replace('\\', '/');

    [Fact]
    public void A_core_fsmonitor_hook_is_never_run_by_a_vdiff_run_or_by_the_snapshot()
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        var markerDir = Path.Combine(Path.GetTempPath(), "vdiff-marker-" + Guid.NewGuid().ToString("N"));
        try
        {
            var hookDir = Path.Combine(repo.Root, "hooks");
            Directory.CreateDirectory(hookDir);
            var hook = Path.Combine(hookDir, "fsmonitor.sh");
            // LF line ends: Git's shell cannot run a CRLF script.
            File.WriteAllBytes(
                hook,
                new System.Text.UTF8Encoding(false).GetBytes(
                    "#!/bin/sh\n" +
                    $"mkdir -p \"{ForwardSlashes(markerDir)}\"\n" +
                    $"echo ran >> \"{ForwardSlashes(markerDir)}/ran.txt\"\n" +
                    "exit 0\n"));
            repo.Git("config", "core.fsmonitor", ForwardSlashes(hook));
            MakeDirty(repo);

            // Positive control: an unguarded git run does start the hook, so the assertions
            // below prove something.
            UnprotectedGit(repo.RepoDir, "status", "--porcelain=v1");
            Assert.True(
                Directory.Exists(markerDir),
                "positive control failed: the fsmonitor hook did not run for an unguarded git status, so this scenario would prove nothing");
            Directory.Delete(markerDir, recursive: true);

            var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
            Assert.False(Directory.Exists(markerDir), "the snapshot's own git calls ran the fsmonitor hook");

            var exit = RunCli(RunArgs(repo.RepoDir, c1, c2, repo.OutDir), out _, out var stderr);

            Assert.Equal("", stderr);
            Assert.Equal(0, exit);
            Assert.False(Directory.Exists(markerDir), "the run executed the repository's fsmonitor hook");
            var after = RepoSnapshot.Take(repo.Root, repo.OutDir);
            Assert.False(Directory.Exists(markerDir), "the snapshot's own git calls ran the fsmonitor hook");
            AssertSameSnapshot(before, after);
            Assert.False(Directory.Exists(Path.Combine(repo.RepoDir, ".git", "fsmonitor--daemon")));
        }
        finally
        {
            if (Directory.Exists(markerDir))
            {
                Directory.Delete(markerDir, recursive: true);
            }
        }
    }

    [Fact]
    public void A_core_fsmonitor_true_setting_never_starts_a_daemon_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // the built-in daemon scenario is the Windows one (C25)
        }

        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        try
        {
            repo.Git("config", "core.fsmonitor", "true");
            MakeDirty(repo);

            // Positive control, in a second repository because a daemon leaves its directory
            // behind: an unguarded git status starts the daemon.
            using (var control = new SyntheticRepo())
            {
                control.Write("a.txt", "a\n");
                control.Commit("control");
                control.Git("config", "core.fsmonitor", "true");
                try
                {
                    UnprotectedGit(control.RepoDir, "status", "--porcelain=v1");
                    Assert.True(
                        DaemonIsRunning(control.RepoDir),
                        "positive control failed: an unguarded git status did not start the fsmonitor daemon, so this scenario would prove nothing");
                }
                finally
                {
                    StopDaemon(control.RepoDir);
                }
            }

            var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
            Assert.False(DaemonIsRunning(repo.RepoDir), "the snapshot started an fsmonitor daemon");

            var exit = RunCli(RunArgs(repo.RepoDir, c1, c2, repo.OutDir), out _, out var stderr);

            Assert.Equal("", stderr);
            Assert.Equal(0, exit);
            Assert.False(DaemonIsRunning(repo.RepoDir), "the run started an fsmonitor daemon");
            var after = RepoSnapshot.Take(repo.Root, repo.OutDir);
            Assert.False(DaemonIsRunning(repo.RepoDir), "the snapshot started an fsmonitor daemon");
            AssertSameSnapshot(before, after);
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(repo.RepoDir, ".git"), "fsmonitor--daemon*"));
        }
        finally
        {
            // A regression must not leave a daemon running after the test.
            StopDaemon(repo.RepoDir);
        }
    }

    // `fsmonitor--daemon status` and `stop` only talk to a running daemon; neither starts one.
    static bool DaemonIsRunning(string repoDir) =>
        UnprotectedGit(repoDir, "fsmonitor--daemon", "status").ExitCode == 0;

    static void StopDaemon(string repoDir) =>
        UnprotectedGit(repoDir, "fsmonitor--daemon", "stop");
}
