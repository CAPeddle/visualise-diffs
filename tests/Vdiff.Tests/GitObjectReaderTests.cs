using System.Text;
using Vdiff.Core;
using Vdiff.Core.Cli;
using Vdiff.Core.Git;
using Vdiff.Core.Process;
using Vdiff.Core.Scope;
using Vdiff.Tests.Support;

namespace Vdiff.Tests;

public class GitObjectReaderTests
{
    static readonly string Zero40 = new('0', 40);

    static byte[] Raw(params object[] parts)
    {
        var bytes = new List<byte>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case string s:
                    bytes.AddRange(Encoding.ASCII.GetBytes(s));
                    break;
                case byte[] b:
                    bytes.AddRange(b);
                    break;
                case byte single:
                    bytes.Add(single);
                    break;
            }
        }

        return bytes.ToArray();
    }

    static string BlobId(SyntheticRepo repo, string path) => repo.Git("rev-parse", "HEAD:" + path).Trim();

    static string SamePath(string path) => FinalPath.Resolve(path).TrimEnd('\\', '/');

    // ---- ResolveCommit ----

    [Fact]
    public void ResolveCommit_accepts_seven_to_sixty_four_hex_characters_in_either_case()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.h", "x\n");
        var id = repo.Commit("one");
        var reader = new GitObjectReader(repo.RepoDir);

        Assert.Equal(id, reader.ResolveCommit(id));
        Assert.Equal(id, reader.ResolveCommit(id[..7]));
        Assert.Equal(id, reader.ResolveCommit(id[..12].ToUpperInvariant()));
    }

    [Theory]
    [InlineData("HEAD")]
    [InlineData("main")]
    [InlineData("--output=x")]
    [InlineData("abcdef")]
    [InlineData(" abcdef1")]
    [InlineData("abcdef1 ")]
    [InlineData("abcdef1\n")]
    [InlineData("abcdefg")]
    [InlineData("")]
    public void ResolveCommit_rejects_anything_but_hex_before_any_process_starts(string input)
    {
        Assert.Throws<VdiffException>(() => GitObjectReader.ValidateCommitInput(input));
    }

    [Fact]
    public void ResolveCommit_rejects_sixty_five_characters_and_accepts_sixty_four()
    {
        GitObjectReader.ValidateCommitInput(new string('a', 64));
        Assert.Throws<VdiffException>(() => GitObjectReader.ValidateCommitInput(new string('a', 65)));
    }

    [Fact]
    public void ResolveCommit_rejects_a_valid_looking_unknown_id_after_rev_parse()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.h", "x\n");
        repo.Commit("one");
        var reader = new GitObjectReader(repo.RepoDir);

        Assert.Throws<VdiffException>(() => reader.ResolveCommit("deadbeefdeadbeef"));
    }

    [Fact]
    public void ResolveCommit_refuses_an_abbreviation_shadowed_by_a_branch_name()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.h", "x\n");
        var first = repo.Commit("one");
        repo.Write("b.h", "y\n");
        var second = repo.Commit("two");
        // A branch named like the 7-character abbreviation of the first commit, pointing elsewhere.
        repo.Git("branch", first[..7], second);
        var reader = new GitObjectReader(repo.RepoDir);

        var e = Assert.Throws<VdiffException>(() => reader.ResolveCommit(first[..7]));

        Assert.Contains("shadow", e.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first, reader.ResolveCommit(first));
    }

    // ---- git version gate ----

    [Fact]
    public void GitVersion_parses_a_windows_version_line()
    {
        var version = GitVersion.Parse("git version 2.55.0.windows.3");

        Assert.Equal(new GitVersion(2, 55, 0), version);
    }

    [Fact]
    public void GitVersion_minimum_is_2_45_and_older_git_is_refused_with_the_reason()
    {
        var e = Assert.Throws<VdiffException>(() => GitVersion.EnsureMinimum(new GitVersion(2, 44, 9)));

        Assert.Contains("2.45", e.Message);
        Assert.Contains("lazy", e.Message, StringComparison.OrdinalIgnoreCase);
        GitVersion.EnsureMinimum(new GitVersion(2, 45, 0));
        GitVersion.EnsureMinimum(new GitVersion(3, 0, 0));
    }

    [Fact]
    public void GitVersion_rejects_text_it_cannot_parse()
    {
        Assert.Throws<VdiffException>(() => GitVersion.Parse("not git"));
    }

    // ---- argument builders ----

    public static TheoryData<string> BuilderNames() => new() { "version", "resolve", "diff-tree", "batch-check", "batch" };

    [Theory]
    [MemberData(nameof(BuilderNames))]
    public void Every_argument_list_starts_with_the_shared_read_only_prefix(string builder)
    {
        var id = new string('a', 40);
        IReadOnlyList<string> args = builder switch
        {
            "version" => GitObjectReader.VersionArguments(),
            "resolve" => GitObjectReader.ResolveCommitArguments("repo", id),
            "diff-tree" => GitObjectReader.DiffTreeArguments("repo", id, id),
            "batch-check" => GitObjectReader.CatFileBatchCheckArguments("repo"),
            _ => GitObjectReader.CatFileBatchArguments("repo"),
        };

        Assert.Equal(GitCommand.Prefix, args.Take(GitCommand.Prefix.Count).ToArray());
    }

    [Fact]
    public void The_resolve_and_diff_arguments_are_hardened()
    {
        var id = new string('a', 40);

        var resolve = GitObjectReader.ResolveCommitArguments("repo", id);
        Assert.Contains("--end-of-options", resolve);
        Assert.Equal(id + "^{commit}", resolve[^1]);
        Assert.True(resolve.ToList().IndexOf("--end-of-options") < resolve.ToList().IndexOf(id + "^{commit}"));

        var diff = GitObjectReader.DiffTreeArguments("repo", id, id);
        Assert.Contains("diff-tree", diff);
        Assert.Contains("--raw", diff);
        Assert.Contains("-z", diff);
        Assert.Contains("--no-renames", diff);
        Assert.Contains("--no-abbrev", diff);
    }

    [Fact]
    public void The_diff_arguments_refuse_anything_but_a_full_hex_id()
    {
        Assert.Throws<VdiffException>(() => GitObjectReader.DiffTreeArguments("repo", "HEAD", new string('a', 40)));
        Assert.Throws<VdiffException>(() => GitObjectReader.DiffTreeArguments("repo", new string('a', 40), "--output=x"));
    }

    // ---- ListChanged / ParseDiffTree ----

    [Fact]
    public void ListChanged_returns_added_modified_and_deleted_files_sorted_by_ordinal_path()
    {
        using var repo = new SyntheticRepo();
        repo.Write("b.h", "b1\n");
        repo.Write("a.h", "a1\n");
        repo.Write("Z.h", "z1\n");
        repo.Write("gone.cpp", "g\n");
        repo.Write("same.h", "s\n");
        var baseId = repo.Commit("base");
        var oldB = BlobId(repo, "b.h");
        var oldGone = BlobId(repo, "gone.cpp");
        repo.Write("b.h", "b2\n");
        repo.Write("new.h", "n\n");
        repo.Delete("gone.cpp");
        var headId = repo.Commit("head");
        var reader = new GitObjectReader(repo.RepoDir);

        var changes = reader.ListChanged(baseId, headId);

        Assert.Equal(["b.h", "gone.cpp", "new.h"], changes.Files.Select(f => f.Path));
        var b = changes.Files[0];
        Assert.Equal(oldB, b.BaseBlob);
        Assert.Equal(BlobId(repo, "b.h"), b.HeadBlob);
        var gone = changes.Files[1];
        Assert.Equal(oldGone, gone.BaseBlob);
        Assert.Null(gone.HeadBlob);
        var added = changes.Files[2];
        Assert.Null(added.BaseBlob);
        Assert.Equal(BlobId(repo, "new.h"), added.HeadBlob);
        Assert.Empty(changes.Unmodelled);
    }

    [Fact]
    public void ListChanged_orders_upper_case_before_lower_case_by_ordinal_comparison()
    {
        using var repo = new SyntheticRepo();
        repo.Write("keep.h", "k\n");
        var baseId = repo.Commit("base");
        repo.Write("a.h", "a\n");
        repo.Write("B.h", "b\n");
        repo.Write("sub/c.h", "c\n");
        repo.Write("Z.h", "z\n");
        var headId = repo.Commit("head");
        var reader = new GitObjectReader(repo.RepoDir);

        var paths = reader.ListChanged(baseId, headId).Files.Select(f => f.Path).ToArray();

        Assert.Equal(["B.h", "Z.h", "a.h", "sub/c.h"], paths);
    }

    [Fact]
    public void ListChanged_judges_each_side_by_its_own_mode()
    {
        using var repo = new SyntheticRepo();
        repo.Write("f.h", "regular\n");
        var baseId = repo.Commit("base");
        var oldBlob = BlobId(repo, "f.h");
        var linkTarget = repo.WriteBlob("f.h.real");
        repo.AddCacheInfo("120000", linkTarget, "f.h"); // regular file turned into a symlink
        var headId = repo.Commit("head");
        var reader = new GitObjectReader(repo.RepoDir);

        var changes = reader.ListChanged(baseId, headId);

        var file = Assert.Single(changes.Files);
        Assert.Equal("f.h", file.Path);
        Assert.Equal(oldBlob, file.BaseBlob);
        Assert.Null(file.HeadBlob);
    }

    [Fact]
    public void ListChanged_drops_mode_only_changes_gitlinks_and_symlinks()
    {
        using var repo = new SyntheticRepo();
        repo.Write("run.h", "same\n");
        var baseId = repo.Commit("base");
        repo.Git("update-index", "--chmod=+x", "run.h"); // mode-only change
        var blob = repo.WriteBlob("target");
        repo.AddCacheInfo("120000", blob, "link.h");
        repo.AddCacheInfo("160000", "0123456789abcdef0123456789abcdef01234567", "module");
        var headId = repo.Commit("head");
        var reader = new GitObjectReader(repo.RepoDir);

        var changes = reader.ListChanged(baseId, headId);

        Assert.Empty(changes.Files);
        Assert.Empty(changes.Unmodelled);
    }

    [Fact]
    public void ListChanged_of_identical_commits_is_empty()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.h", "a\n");
        var id = repo.Commit("one");
        var reader = new GitObjectReader(repo.RepoDir);

        Assert.Empty(reader.ListChanged(id, id).Files);
    }

    [Fact]
    public void ParseDiffTree_reads_each_record_kind_and_never_keeps_a_zero_id()
    {
        var a = new string('a', 40);
        var b = new string('b', 40);
        var raw = Raw(
            $":000000 100644 {Zero40} {a} A\0new.h\0",
            $":100644 100755 {a} {b} M\0mod.h\0",
            $":100644 000000 {a} {Zero40} D\0del.h\0",
            $":100644 120000 {a} {b} T\0type.h\0");

        var changes = GitObjectReader.ParseDiffTree(raw);

        Assert.Equal(["del.h", "mod.h", "new.h", "type.h"], changes.Files.Select(f => f.Path));
        Assert.Equal((a, (string?)null), (changes.Files[0].BaseBlob!, changes.Files[0].HeadBlob));
        Assert.Equal((a, b), (changes.Files[1].BaseBlob!, changes.Files[1].HeadBlob!));
        Assert.Equal(((string?)null, a), (changes.Files[2].BaseBlob, changes.Files[2].HeadBlob));
        Assert.Equal((a, (string?)null), (changes.Files[3].BaseBlob!, changes.Files[3].HeadBlob));
        Assert.DoesNotContain(changes.Files, f => f.BaseBlob == Zero40 || f.HeadBlob == Zero40);
    }

    [Fact]
    public void ParseDiffTree_reports_a_path_that_is_not_valid_utf8_as_unmodelled()
    {
        var a = new string('a', 40);
        var b = new string('b', 40);
        var raw = Raw(
            $":100644 100644 {a} {b} M\0",
            new byte[] { (byte)'x', 0xFF, 0xFE, (byte)'.', (byte)'h' },
            (byte)0,
            $":100644 100644 {a} {b} M\0ok.h\0");

        var changes = GitObjectReader.ParseDiffTree(raw);

        Assert.Equal(["ok.h"], changes.Files.Select(f => f.Path));
        var skipped = Assert.Single(changes.Unmodelled);
        Assert.Equal("non-utf8-path", skipped.Reason);
    }

    [Fact]
    public void ParseDiffTree_rejects_a_truncated_record()
    {
        Assert.Throws<VdiffException>(() => GitObjectReader.ParseDiffTree(Raw($":100644 100644 {new string('a', 40)} ")));
    }

    // ---- ReadBlobs ----

    [Fact]
    public void ReadBlobs_returns_the_exact_stored_bytes_without_filters()
    {
        using var repo = new SyntheticRepo();
        var binary = new byte[] { 0, 1, 2, 255, 254, 10, 13, 10, 0 };
        repo.Write("crlf.h", "a\r\nb\r\n");
        repo.Write("bin.cpp", binary);
        repo.Write("empty.h", "");
        repo.Commit("one");
        var reader = new GitObjectReader(repo.RepoDir);

        var result = reader.ReadBlobs(
        [
            new BlobRequest("bin.cpp", BlobId(repo, "bin.cpp")),
            new BlobRequest("crlf.h", BlobId(repo, "crlf.h")),
            new BlobRequest("empty.h", BlobId(repo, "empty.h")),
        ]);

        Assert.Empty(result.Unmodelled);
        Assert.Equal(["bin.cpp", "crlf.h", "empty.h"], result.Blobs.Select(b => b.Path));
        Assert.Equal(binary, result.Blobs[0].Bytes);
        Assert.Equal(Encoding.ASCII.GetBytes("a\r\nb\r\n"), result.Blobs[1].Bytes);
        Assert.Empty(result.Blobs[2].Bytes);
    }

    [Fact]
    public void ReadBlobs_marks_a_blob_over_the_cap_as_too_large_without_reading_it()
    {
        using var repo = new SyntheticRepo();
        repo.Write("small.h", "tiny\n");
        repo.Write("big.h", new string('x', 100));
        repo.Commit("one");
        var reader = new GitObjectReader(repo.RepoDir);

        var result = reader.ReadBlobs(
            [new BlobRequest("big.h", BlobId(repo, "big.h")), new BlobRequest("small.h", BlobId(repo, "small.h"))],
            maxBlobBytes: 50);

        Assert.Equal(["small.h"], result.Blobs.Select(b => b.Path));
        var skipped = Assert.Single(result.Unmodelled);
        Assert.Equal(("big.h", "too-large"), (skipped.Path, skipped.Reason));
    }

    [Fact]
    public void ReadBlobs_marks_a_missing_object_as_object_missing()
    {
        using var repo = new SyntheticRepo();
        repo.Write("keep.h", "keep\n");
        repo.Write("lost.h", "lost\n");
        repo.Commit("one");
        var lost = BlobId(repo, "lost.h");
        var loose = Path.Combine(repo.RepoDir, ".git", "objects", lost[..2], lost[2..]);
        File.SetAttributes(loose, FileAttributes.Normal);
        File.Delete(loose);
        var reader = new GitObjectReader(repo.RepoDir);

        var result = reader.ReadBlobs(
            [new BlobRequest("keep.h", BlobId(repo, "keep.h")), new BlobRequest("lost.h", lost)]);

        Assert.Equal(["keep.h"], result.Blobs.Select(b => b.Path));
        var skipped = Assert.Single(result.Unmodelled);
        Assert.Equal(("lost.h", "object-missing"), (skipped.Path, skipped.Reason));
    }

    [Fact]
    public void ReadBlobs_stops_accepting_once_the_total_budget_would_be_passed()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.h", "aaaaaa");
        repo.Write("b.h", "bbbbbb");
        repo.Write("c.h", "cccccc");
        repo.Write("d.h", "d");
        repo.Commit("one");
        var reader = new GitObjectReader(repo.RepoDir);
        var entries = new[] { "a.h", "b.h", "c.h", "d.h" }.Select(p => new BlobRequest(p, BlobId(repo, p))).ToList();

        var result = reader.ReadBlobs(entries, maxBlobBytes: 100, maxTotalBytes: 13);

        Assert.Equal(["a.h", "b.h"], result.Blobs.Select(b => b.Path));
        // c.h does not fit, and d.h (1 byte, which would fit) is dropped too: every later entry in path order.
        Assert.Equal([("c.h", "total-too-large"), ("d.h", "total-too-large")], result.Unmodelled.Select(u => (u.Path, u.Reason)));
    }

    [Fact]
    public void ReadBlobs_refuses_an_id_that_is_not_hex_or_is_all_zero()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.h", "a\n");
        repo.Commit("one");
        var reader = new GitObjectReader(repo.RepoDir);

        Assert.Throws<VdiffException>(() => reader.ReadBlobs([new BlobRequest("a.h", "HEAD:a.h")]));
        Assert.Throws<VdiffException>(() => reader.ReadBlobs([new BlobRequest("a.h", Zero40)]));
    }

    [Fact]
    public void ReadBlobs_with_no_entries_returns_nothing()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.h", "a\n");
        repo.Commit("one");
        var reader = new GitObjectReader(repo.RepoDir);

        var result = reader.ReadBlobs([]);

        Assert.Empty(result.Blobs);
        Assert.Empty(result.Unmodelled);
    }

    // ---- ToplevelOrGitDir ----

    [Fact]
    public void ToplevelOrGitDir_is_the_absolute_work_tree_root_for_a_repository_and_a_subdirectory()
    {
        using var repo = new SyntheticRepo();
        repo.Write("sub/dir/a.h", "a\n");
        repo.Commit("one");

        var fromRoot = new GitObjectReader(repo.RepoDir).ToplevelOrGitDir();
        var fromSub = new GitObjectReader(Path.Combine(repo.RepoDir, "sub", "dir")).ToplevelOrGitDir();

        Assert.True(Path.IsPathRooted(fromRoot));
        Assert.Equal(SamePath(repo.RepoDir), SamePath(fromRoot), ignoreCase: OperatingSystem.IsWindows());
        Assert.Equal(fromRoot, fromSub);
    }

    [Fact]
    public void ToplevelOrGitDir_from_a_linked_worktree_is_that_worktree_and_the_common_dir_is_the_main_git_dir()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.h", "a\n");
        repo.Commit("one");
        var linked = Path.Combine(repo.Root, "linked");
        repo.Git("worktree", "add", "-q", linked, "-b", "linked");

        var location = new GitObjectReader(linked).Locate();

        Assert.Equal(SamePath(linked), SamePath(location.Root), ignoreCase: OperatingSystem.IsWindows());
        Assert.Equal(SamePath(Path.Combine(repo.RepoDir, ".git")), SamePath(location.CommonDir), ignoreCase: OperatingSystem.IsWindows());
        Assert.NotEqual(SamePath(location.GitDir), SamePath(location.CommonDir));
        Assert.False(location.IsBare);
    }

    [Fact]
    public void ToplevelOrGitDir_for_a_bare_repository_is_the_git_directory()
    {
        using var repo = new SyntheticRepo();
        repo.Write("a.h", "a\n");
        repo.Commit("one");
        var bare = Path.Combine(repo.Root, "bare.git");
        repo.Git("clone", "-q", "--bare", repo.RepoDir, bare);

        var reader = new GitObjectReader(bare);

        Assert.Equal(SamePath(bare), SamePath(reader.ToplevelOrGitDir()), ignoreCase: OperatingSystem.IsWindows());
        Assert.True(reader.Locate().IsBare);
    }

    // ---- ScopeFilter ----

    [Theory]
    [InlineData("a.h", true)]
    [InlineData("dir/a.hpp", true)]
    [InlineData("a.hh", true)]
    [InlineData("a.hxx", true)]
    [InlineData("a.cpp", true)]
    [InlineData("a.cc", true)]
    [InlineData("a.cxx", true)]
    [InlineData("DIR/A.HPP", true)]
    [InlineData("a.Cpp", true)]
    [InlineData("a.c", false)]
    [InlineData("a.cs", false)]
    [InlineData("a.txt", false)]
    [InlineData("Makefile", false)]
    [InlineData("dir.h/readme", false)]
    public void ScopeFilter_without_globs_takes_every_cpp_extension_case_insensitively(string path, bool expected)
    {
        Assert.Equal(expected, new ScopeFilter().IsInScope(path));
    }

    [Fact]
    public void ScopeFilter_with_globs_needs_a_match_and_matches_case_sensitively()
    {
        var filter = new ScopeFilter(["include/a/**", "src/*.cpp"]);

        Assert.True(filter.IsInScope("include/a/x.h"));
        Assert.True(filter.IsInScope("include/a/deep/er/x.hpp"));
        Assert.True(filter.IsInScope("src/main.cpp"));
        Assert.False(filter.IsInScope("Include/A/x.h"));
        Assert.False(filter.IsInScope("include/b/x.h"));
        Assert.False(filter.IsInScope("include/a/x.txt"), "extension is still required");
        Assert.False(filter.IsInScope("src/deep/main.cpp"));
    }

    [Fact]
    public void ScopeFilter_with_an_empty_glob_list_behaves_like_no_globs()
    {
        Assert.True(new ScopeFilter([]).IsInScope("anywhere/x.h"));
    }
}
