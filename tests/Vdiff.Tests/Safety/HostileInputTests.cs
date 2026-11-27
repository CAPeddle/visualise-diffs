using System.Text.Json;
using Vdiff.Core.Cli;
using Vdiff.Core.Model;
using Vdiff.Core.Output;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Safety;

/// <summary>
/// The tool attacked in-process (T-01-01, T-01-13, T-01-28): hostile argument values must be
/// refused with exit 1 and a <c>vdiff:</c> message before any harmful effect, and hostile
/// repository content must never crash a run or write outside <c>--out</c>. Every repository is
/// synthetic (D-09). Sidecar misbehaviour is plan 01-05's and is not tested here.
/// </summary>
[Collection("sidecar")]
public class HostileInputTests
{
    static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    readonly SidecarFixture _sidecar;

    public HostileInputTests(SidecarFixture sidecar) => _sidecar = sidecar;

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

    static List<string> Args(SyntheticRepo repo, string baseId, string headId, params string[] extra)
    {
        var args = new List<string> { "--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", repo.OutDir };
        args.AddRange(extra);
        return args;
    }

    /// <summary>Exit 1, a <c>vdiff:</c> message, nothing written to --out and the sandbox unchanged.</summary>
    static string AssertRefused(SyntheticRepo repo, IReadOnlyList<string> args)
    {
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(args, out _, out var stderr);

        Assert.Equal(1, exit);
        Assert.StartsWith("vdiff: ", stderr);
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
        return stderr;
    }

    // ---- --base and --head ----

    public static TheoryData<string> HostileCommitValues() => new()
    {
        "--output=SENTINEL",
        "HEAD",
        "main",
        "HEAD~1",
        "abc",
        "zzzzzzzz",
        new string('a', 65),
        "abcdef1 2",
        "abcdef1\n",
        "abcdef1\r\n",
        "",
    };

    [Theory]
    [MemberData(nameof(HostileCommitValues))]
    public void A_hostile_base_value_is_refused_and_never_reaches_git(string value)
    {
        using var repo = new SyntheticRepo();
        var (_, headId) = BuildSmallHistory(repo);
        var sentinel = Path.Combine(repo.Root, "sentinel.txt");
        value = value.Replace("SENTINEL", sentinel);

        AssertRefused(repo, Args(repo, value, headId));

        Assert.False(File.Exists(sentinel));
    }

    [Theory]
    [MemberData(nameof(HostileCommitValues))]
    public void A_hostile_head_value_is_refused_and_never_reaches_git(string value)
    {
        using var repo = new SyntheticRepo();
        var (baseId, _) = BuildSmallHistory(repo);
        var sentinel = Path.Combine(repo.Root, "sentinel.txt");
        value = value.Replace("SENTINEL", sentinel);

        AssertRefused(repo, Args(repo, baseId, value));

        Assert.False(File.Exists(sentinel));
    }

    [Fact]
    public void A_valid_looking_but_unknown_commit_id_is_refused()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);

        AssertRefused(repo, Args(repo, baseId, "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef"));
        AssertRefused(repo, Args(repo, "deadbeef", headId));
    }

    [Fact]
    public void A_branch_named_like_the_head_abbreviation_never_changes_which_commit_is_analysed()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var abbreviation = headId[..7];
        // A branch with the abbreviation's name, pointing at the base commit instead of the head.
        repo.Git("branch", abbreviation, baseId);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(Args(repo, baseId, abbreviation), out _, out var stderr);

        if (exit == 0)
        {
            // Analysed the intended commit: the head of the diff is the commit, not the branch tip.
            var meta = JsonSerializer.Deserialize<RunMeta>(File.ReadAllText(Path.Combine(repo.OutDir, OutputWriter.MetaFile)), ReadOptions)!;
            Assert.Equal(headId, meta.HeadCommit);
            Assert.NotEqual(baseId, meta.HeadCommit);
        }
        else
        {
            Assert.Equal(1, exit);
            Assert.StartsWith("vdiff: ", stderr);
            Assert.Contains("shadowed", stderr);
            Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        }

        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    // ---- --repo ----

    [Fact]
    public void A_missing_repo_a_file_and_a_directory_that_is_not_a_repository_are_refused()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var plainDir = Path.Combine(repo.Root, "plain");
        Directory.CreateDirectory(plainDir);
        var aFile = Path.Combine(repo.Root, "afile.txt");
        File.WriteAllText(aFile, "x\n");

        foreach (var bad in new[] { Path.Combine(repo.Root, "missing"), aFile, plainDir, "-c" })
        {
            var args = new List<string> { "--repo", bad, "--base", baseId, "--head", headId, "--out", repo.OutDir };
            AssertRefused(repo, args);
        }
    }

    // ---- --out ----

    [Fact]
    public void An_out_directory_inside_the_work_tree_the_root_or_git_is_refused_and_nothing_is_created()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var subdir = Path.Combine(repo.RepoDir, "include");

        foreach (var (repoArg, outArg) in new[]
        {
            (repo.RepoDir, Path.Combine(repo.RepoDir, "results")),
            (repo.RepoDir, repo.RepoDir),
            (repo.RepoDir, Path.Combine(repo.RepoDir, ".git")),
            (repo.RepoDir, Path.Combine(repo.RepoDir, ".git", "vdiff-out")),
            (subdir, Path.Combine(repo.RepoDir, "results")),
            (subdir, subdir),
            (subdir, repo.RepoDir),
        })
        {
            var args = new List<string> { "--repo", repoArg, "--base", baseId, "--head", headId, "--out", outArg };
            var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

            var exit = RunCli(args, out _, out var stderr);

            Assert.Equal(1, exit);
            Assert.StartsWith("vdiff: ", stderr);
            Assert.Contains("--out must not lie inside the analysed repository", stderr);
            Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
        }

        Assert.False(Directory.Exists(Path.Combine(repo.RepoDir, "results")));
    }

    [Fact]
    public void A_relative_out_whose_dotdot_segments_resolve_inside_the_repository_is_refused()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var subdir = Path.Combine(repo.RepoDir, "include");
        var cwd = Environment.CurrentDirectory;

        // Relative to the current directory, climbing out and back into the repository.
        var viaDotDot = Path.GetRelativePath(cwd, Path.Combine(repo.RepoDir, "include", "..", "results"));
        var viaSubdir = Path.GetRelativePath(cwd, Path.Combine(subdir, "..", "include", "results"));
        Assert.Contains("..", viaDotDot);

        foreach (var (repoArg, outArg) in new[] { (repo.RepoDir, viaDotDot), (subdir, viaDotDot), (subdir, viaSubdir) })
        {
            var args = new List<string> { "--repo", repoArg, "--base", baseId, "--head", headId, "--out", outArg };
            var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

            var exit = RunCli(args, out _, out var stderr);

            Assert.Equal(1, exit);
            Assert.StartsWith("vdiff: ", stderr);
            Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
        }

        Assert.False(Directory.Exists(Path.Combine(repo.RepoDir, "results")));
    }

    [Fact]
    public void An_out_path_that_is_an_existing_file_is_refused()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        var aFile = Path.Combine(repo.Root, "out-is-a-file");
        File.WriteAllText(aFile, "keep\n");
        var args = new List<string> { "--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", aFile };
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(args, out _, out var stderr);

        Assert.Equal(1, exit);
        Assert.StartsWith("vdiff: ", stderr);
        Assert.Equal("keep\n", File.ReadAllText(aFile));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    // ---- --blank and --include ----

    [Theory]
    [InlineData("A B")]
    [InlineData("-X")]
    [InlineData("A;B")]
    [InlineData("")]
    [InlineData("1ABC")]
    [InlineData("A$(id)")]
    public void A_malformed_blank_name_is_refused(string name)
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);

        AssertRefused(repo, Args(repo, baseId, headId, "--blank", name));
    }

    [Fact]
    public void A_blank_name_of_129_characters_is_refused_and_128_is_accepted()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);

        AssertRefused(repo, Args(repo, baseId, headId, "--blank", new string('A', 129)));

        var exit = RunCli(Args(repo, baseId, headId, "--blank", new string('A', 128)), out _, out var stderr);
        Assert.Equal("", stderr);
        Assert.Equal(0, exit);
    }

    [Fact]
    public void More_than_256_blank_names_are_refused_and_256_are_accepted()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        string[] Many(int count) => Enumerable.Range(0, count).SelectMany(i => new[] { "--blank", "MACRO_" + i }).ToArray();

        AssertRefused(repo, Args(repo, baseId, headId, Many(257)));

        var exit = RunCli(Args(repo, baseId, headId, Many(256)), out _, out var stderr);
        Assert.Equal("", stderr);
        Assert.Equal(0, exit);
    }

    [Fact]
    public void A_malformed_include_glob_is_refused()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);

        foreach (var glob in new[] { "", "a\0b", new string('g', 513) })
        {
            AssertRefused(repo, Args(repo, baseId, headId, "--include", glob));
        }
    }

    [Fact]
    public void More_than_64_include_globs_are_refused_and_64_are_accepted()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);
        string[] Many(int count) => Enumerable.Range(0, count).SelectMany(i => new[] { "--include", "include/g" + i + "/**" }).ToArray();

        AssertRefused(repo, Args(repo, baseId, headId, Many(65)));

        var exit = RunCli(Args(repo, baseId, headId, Many(64)), out _, out var stderr);
        Assert.Equal("", stderr);
        Assert.Equal(0, exit);
    }

    // ---- flags ----

    [Fact]
    public void Unknown_flags_repeated_single_value_flags_and_missing_values_are_refused()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallHistory(repo);

        AssertRefused(repo, Args(repo, baseId, headId, "--output", "x"));
        AssertRefused(repo, Args(repo, baseId, headId, "-v"));
        AssertRefused(repo, Args(repo, baseId, headId, "stray"));
        AssertRefused(repo, Args(repo, baseId, headId, "--repo", repo.RepoDir));
        AssertRefused(repo, Args(repo, baseId, headId, "--base", baseId));
        AssertRefused(repo, Args(repo, baseId, headId, "--out", repo.OutDir));
        AssertRefused(repo, Args(repo, baseId, headId, "--blank"));
        AssertRefused(repo, ["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out"]);
        AssertRefused(repo, ["--repo", repo.RepoDir, "--base", baseId, "--head", headId]);
        AssertRefused(repo, []);
    }

    [Fact]
    public void Help_prints_the_usage_and_exits_0()
    {
        var exit = RunCli(["--help"], out var stdout, out var stderr);

        Assert.Equal(0, exit);
        Assert.Equal("", stderr);
        Assert.StartsWith("usage: vdiff", stdout);
    }

    // ---- repository content ----

    static ModelFile ReadModel(SyntheticRepo repo, string file) =>
        JsonSerializer.Deserialize<ModelFile>(File.ReadAllText(Path.Combine(repo.OutDir, file)), ReadOptions)!;

    static void AssertRunSucceedsAndStaysInsideOut(SyntheticRepo repo, string baseId, string headId, RepoSnapshot before)
    {
        var exit = RunCli(Args(repo, baseId, headId), out _, out var stderr);

        Assert.Equal("", stderr);
        Assert.Equal(0, exit);
        Assert.Equal(
            OutputWriter.FileNames.Order(StringComparer.Ordinal),
            Directory.GetFiles(repo.OutDir).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.GetDirectories(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Fact]
    public void Paths_with_spaces_non_ascii_characters_a_leading_dash_and_200_characters_run_and_write_only_under_out()
    {
        using var repo = new SyntheticRepo();
        repo.Write("include/a.h", "struct Seed {\n    int id;\n};\n");
        var baseId = repo.Commit("base");

        repo.Write("include/with space.h", "struct Spaced {\n    int id;\n};\n");
        repo.Write("include/café テスト.h", "struct Unicode {\n    int id;\n};\n");
        repo.Write("include/-leading-dash.h", "struct Dashed {\n    int id;\n};\n");
        // 200 characters: made by plumbing, with no file in the Windows work tree (C27).
        var fileName = new string('n', 198) + ".h";
        Assert.Equal(200, fileName.Length);
        var longName = "include/" + fileName;
        repo.AddCacheInfo("100644", repo.WriteBlob("struct Longest {\n    int id;\n};\n"), longName);
        var headId = repo.Commit("head");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        AssertRunSucceedsAndStaysInsideOut(repo, baseId, headId, before);

        var diffText = File.ReadAllText(Path.Combine(repo.OutDir, "diff.json"));
        foreach (var type in new[] { "struct Spaced", "struct Unicode", "struct Dashed", "struct Longest" })
        {
            Assert.Contains(type, diffText);
        }

        Assert.Empty(ReadModel(repo, "head.model.json").Unmodelled);
    }

    [Fact]
    public void A_header_of_random_binary_bytes_runs_to_exit_0()
    {
        using var repo = new SyntheticRepo();
        var (baseId, _) = BuildSmallHistory(repo);
        var junk = new byte[8192];
        new Random(20201).NextBytes(junk);
        repo.Write("include/noise.h", junk);
        var headId = repo.Commit("noise");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        AssertRunSucceedsAndStaysInsideOut(repo, baseId, headId, before);
    }

    [Fact]
    public void A_header_over_4_MiB_is_listed_as_unmodelled_too_large()
    {
        using var repo = new SyntheticRepo();
        var (baseId, _) = BuildSmallHistory(repo);
        var line = System.Text.Encoding.ASCII.GetBytes("// filler line\n");
        var big = new byte[(4 * 1024 * 1024 / line.Length + 2) * line.Length];
        for (var offset = 0; offset < big.Length; offset += line.Length)
        {
            line.CopyTo(big, offset);
        }

        Assert.True(big.Length > 4 * 1024 * 1024);
        repo.Write("include/big.h", big);
        var headId = repo.Commit("big");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        AssertRunSucceedsAndStaysInsideOut(repo, baseId, headId, before);

        Assert.Contains(new UnmodelledEntry("include/big.h", "too-large"), ReadModel(repo, "head.model.json").Unmodelled);
    }

    [Fact]
    public void A_blob_whose_loose_object_was_deleted_is_listed_as_unmodelled_object_missing()
    {
        using var repo = new SyntheticRepo();
        var (baseId, _) = BuildSmallHistory(repo);
        repo.Write("include/gone.h", "struct Gone {\n    int unique_marker_4711;\n};\n");
        var headId = repo.Commit("gone");
        var blob = repo.Git("rev-parse", "HEAD:include/gone.h").Trim();
        var loose = Path.Combine(repo.RepoDir, ".git", "objects", blob[..2], blob[2..]);
        Assert.True(File.Exists(loose), "the blob should be a loose object in a fresh synthetic repository");
        File.SetAttributes(loose, FileAttributes.Normal);
        File.Delete(loose);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        AssertRunSucceedsAndStaysInsideOut(repo, baseId, headId, before);

        Assert.Contains(new UnmodelledEntry("include/gone.h", "object-missing"), ReadModel(repo, "head.model.json").Unmodelled);
        // Nothing was fetched back into the repository: the object is still absent.
        Assert.False(File.Exists(loose));
    }
}
