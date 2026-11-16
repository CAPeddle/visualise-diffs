using System.Diagnostics;
using Vdiff.Core;
using Vdiff.Core.Process;

namespace Vdiff.Tests.Support;

public class SupportTests
{
    static readonly ProcessLimits Generous = new(TimeSpan.FromSeconds(60), 8 * 1024 * 1024, 64 * 1024);

    // ---- BoundedProcess ----

    [Fact]
    public void Run_returns_exit_code_and_stdout_of_a_finished_child()
    {
        var result = BoundedProcess.Run("git", ["--version"], Generous);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("git version", result.StdoutText);
    }

    [Fact]
    public void Run_reports_a_missing_executable_as_VdiffException()
    {
        Assert.Throws<VdiffException>(() =>
            BoundedProcess.Run("vdiff-no-such-executable-4f9a", [], Generous));
    }

    [Fact]
    public void Run_survives_stdin_written_to_a_child_that_already_exited()
    {
        // git --version never reads stdin and exits at once; 20 MB cannot fit in the pipe,
        // so the writer task hits an IOException that must not escape (C18).
        var twentyMegabytes = new byte[20 * 1024 * 1024];

        var result = BoundedProcess.Run("git", ["--version"], Generous, twentyMegabytes);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("git version", result.StdoutText);
    }

    [Fact]
    public void Run_kills_a_child_tree_that_outlives_the_timeout()
    {
        // The alias runs through sh, which starts sleep: two processes, and the whole tree must go.
        var limits = new ProcessLimits(TimeSpan.FromSeconds(2), 1024 * 1024, 64 * 1024);
        var watch = Stopwatch.StartNew();

        var e = Assert.Throws<VdiffException>(() =>
            BoundedProcess.Run("git", ["-c", "alias.slow=!sleep 60", "slow"], limits));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), "the run did not stop at the timeout");
        Assert.Contains("time limit", e.Message);
    }

    [Fact]
    public void Run_fails_when_stdout_passes_its_cap()
    {
        var limits = new ProcessLimits(TimeSpan.FromSeconds(60), 5, 64 * 1024);

        var e = Assert.Throws<VdiffException>(() => BoundedProcess.Run("git", ["--version"], limits));

        Assert.Contains("stdout", e.Message);
    }

    // ---- GitCommand ----

    [Fact]
    public void GitCommand_prefix_is_exactly_the_read_only_override()
    {
        Assert.Equal(["-c", "core.fsmonitor=false", "--no-optional-locks"], GitCommand.Prefix);
    }

    [Fact]
    public void GitCommand_environment_removes_redirecting_variables_and_sets_the_safe_ones()
    {
        var inherited = new Dictionary<string, string?>
        {
            ["GIT_DIR"] = "elsewhere",
            ["GIT_WORK_TREE"] = "elsewhere",
            ["GIT_INDEX_FILE"] = "elsewhere",
            ["GIT_COMMON_DIR"] = "elsewhere",
            ["GIT_NAMESPACE"] = "elsewhere",
            ["GIT_EXTERNAL_DIFF"] = "elsewhere",
            ["GIT_NO_LAZY_FETCH"] = "0",
            ["KEEP_ME"] = "kept",
        };

        var env = GitCommand.BuildEnvironment(inherited);

        foreach (var name in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR", "GIT_NAMESPACE", "GIT_EXTERNAL_DIFF" })
        {
            Assert.False(env.ContainsKey(name), name + " must be removed");
        }

        Assert.Equal("0", env["GIT_OPTIONAL_LOCKS"]);
        Assert.Equal("0", env["GIT_TERMINAL_PROMPT"]);
        Assert.Equal("1", env["GIT_NO_LAZY_FETCH"]);
        Assert.Equal("kept", env["KEEP_ME"]);
    }

    [Fact]
    public void RepoSnapshot_commands_all_start_with_the_shared_prefix()
    {
        var commands = RepoSnapshot.BuildCommands("some-dir");

        Assert.NotEmpty(commands);
        Assert.All(commands, c => Assert.Equal(GitCommand.Prefix, c.Take(GitCommand.Prefix.Count).ToArray()));
    }

    // ---- SyntheticRepo ----

    [Fact]
    public void Two_sandboxes_with_the_same_history_have_the_same_commit_ids()
    {
        static string Build(out string second)
        {
            using var repo = new SyntheticRepo();
            repo.Write("a.txt", "one\n");
            var first = repo.Commit("first");
            repo.Write("dir/b.txt", "two\n");
            second = repo.Commit("second");
            return first;
        }

        var firstA = Build(out var secondA);
        var firstB = Build(out var secondB);

        Assert.Equal(firstA, firstB);
        Assert.Equal(secondA, secondB);
        Assert.NotEqual(firstA, secondA);
        Assert.Equal(40, firstA.Length);
    }

    [Fact]
    public void Git_configuration_comes_only_from_the_sandbox()
    {
        using var repo = new SyntheticRepo();

        var origins = repo.Git("config", "--list", "--show-origin", "--includes")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.NotEmpty(origins);
        var sandbox = Path.GetFullPath(repo.Root).Replace('\\', '/');
        Assert.All(origins, line =>
        {
            // The repository's own config is shown relative; the command line has no file.
            // A system or global config would be an absolute path outside the sandbox.
            var origin = line.Split('\t')[0];
            if (origin is "command line:" or "file:.git/config")
            {
                return;
            }

            Assert.StartsWith(sandbox, origin.Replace("file:", "").Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void A_crlf_file_is_stored_byte_for_byte()
    {
        using var repo = new SyntheticRepo();
        repo.Write("crlf.txt", "a\r\nb\r\n");
        repo.Commit("crlf");

        Assert.Equal("6", repo.Git("cat-file", "-s", "HEAD:crlf.txt").Trim());
    }

    [Fact]
    public void Plumbing_builds_symlink_gitlink_and_over_long_path_entries()
    {
        using var repo = new SyntheticRepo();
        var blob = repo.WriteBlob("target\n");
        var longName = "include/" + new string('n', 200) + ".h";
        repo.Write("a.txt", "a\n");
        repo.AddCacheInfo("120000", blob, "link");
        repo.AddCacheInfo("160000", "0123456789abcdef0123456789abcdef01234567", "module");
        repo.AddCacheInfo("100644", blob, longName);
        repo.Commit("plumbing");

        var listing = repo.Git("ls-tree", "-r", "HEAD");

        Assert.Contains("120000 blob " + blob + "\tlink", listing);
        Assert.Contains("160000 commit 0123456789abcdef0123456789abcdef01234567\tmodule", listing);
        Assert.Contains("100644 blob " + blob + "\t" + longName, listing);
    }

    [Fact]
    public void Dispose_removes_the_sandbox_including_read_only_git_files()
    {
        string root;
        using (var repo = new SyntheticRepo())
        {
            repo.Write("a.txt", "a\n");
            repo.Commit("one");
            root = repo.Root;
            Assert.True(Directory.Exists(root));
        }

        Assert.False(Directory.Exists(root));
    }

    // ---- RepoSnapshot ----

    [Fact]
    public void Snapshot_of_an_untouched_sandbox_taken_twice_is_equal()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.txt", "a\n");
        repo.Commit("one");

        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        var after = RepoSnapshot.Take(repo.Root, repo.OutDir);

        Assert.Equal(before, after);
    }

    [Fact]
    public void Snapshot_ignores_the_excluded_output_directory()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.txt", "a\n");
        repo.Commit("one");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        File.WriteAllText(Path.Combine(repo.OutDir, "result.json"), "{}");

        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Theory]
    [InlineData("edit-tracked")]
    [InlineData("add-untracked")]
    [InlineData("stash")]
    [InlineData("move-head")]
    [InlineData("add-worktree")]
    public void Snapshot_detects_each_kind_of_change(string change)
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.txt", "a\n");
        repo.Commit("one");
        if (change == "stash")
        {
            File.WriteAllText(Path.Combine(repo.RepoDir, "a.txt"), "dirty\n");
        }

        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        switch (change)
        {
            case "edit-tracked":
                File.WriteAllText(Path.Combine(repo.RepoDir, "a.txt"), "edited\n");
                break;
            case "add-untracked":
                File.WriteAllText(Path.Combine(repo.RepoDir, "new.txt"), "new\n");
                break;
            case "stash":
                repo.Git("stash");
                break;
            case "move-head":
                repo.Commit("two");
                break;
            case "add-worktree":
                repo.Git("worktree", "add", "-q", Path.Combine(repo.Root, "linked"), "-b", "linked");
                break;
        }

        Assert.NotEqual(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Fact]
    public void Snapshot_of_a_detached_head_does_not_throw_and_records_the_failed_question()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.txt", "a\n");
        var id = repo.Commit("one");
        repo.Checkout(id, detach: true);

        var snapshot = RepoSnapshot.Take(repo.Root, repo.OutDir);

        Assert.Contains("symbolic-ref -q HEAD -> exit 1", snapshot.GitState);
    }

    [Fact]
    public void Snapshot_of_a_bare_repository_does_not_throw_and_records_the_failed_question()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.txt", "a\n");
        repo.Commit("one");
        var bare = Path.Combine(repo.Root, "bare.git");
        repo.Git("clone", "-q", "--bare", repo.RepoDir, bare);

        var snapshot = RepoSnapshot.Take(repo.Root, repo.OutDir, [bare]);

        Assert.Contains("== bare.git", snapshot.GitState);
        Assert.Contains("status --porcelain=v1 --untracked-files=all -> exit 128", snapshot.GitState);
    }
}
