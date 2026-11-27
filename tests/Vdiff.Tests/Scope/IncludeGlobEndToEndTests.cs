using System.Text.Json;
using Vdiff.Core.Cli;
using Vdiff.Core.Model;
using Vdiff.Core.Output;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Scope;

/// <summary>
/// D-10 through the real command line and sidecar, on a synthetic repository (D-09): only changed
/// C++ files are extracted, <c>--include</c> globs narrow the set, and the order of the flags never
/// changes the output.
/// </summary>
[Collection("sidecar")]
public class IncludeGlobEndToEndTests
{
    static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    static readonly string[] OutputFiles = [.. OutputWriter.FileNames.Order(StringComparer.Ordinal)];

    static string Header(string cls, params string[] fields) =>
        $"#pragma once\nclass {cls} {{\npublic:\n" + string.Concat(fields.Select(f => $"    int {f};\n")) + "};\n";

    /// <summary>
    /// Base: include/a/alpha.h (Alpha), include/a/gone.h (Gone), include/b/beta.h (Beta), include/c/gamma.h
    /// (Gamma, never touched) and include/c/mode.h (Mode, only its executable bit changes), plus a README.
    /// Head: Alpha and Beta gain a field, gone.h is deleted, include/a/fresh.h (Fresh) is added.
    /// </summary>
    static (string BaseId, string HeadId) Build(SyntheticRepo repo)
    {
        repo.Write("include/a/alpha.h", Header("Alpha", "a1"));
        repo.Write("include/a/gone.h", Header("Gone", "g1"));
        repo.Write("include/b/beta.h", Header("Beta", "b1"));
        repo.Write("include/c/gamma.h", Header("Gamma", "c1"));
        repo.Write("include/c/mode.h", Header("Mode", "m1"));
        repo.Write("README.md", "# Demo\n");
        var baseId = repo.Commit("base");

        repo.Write("include/a/alpha.h", Header("Alpha", "a1", "a2"));
        repo.Delete("include/a/gone.h");
        repo.Write("include/a/fresh.h", Header("Fresh", "f1"));
        repo.Write("include/b/beta.h", Header("Beta", "b1", "b2"));
        repo.Git("update-index", "--chmod=+x", "include/c/mode.h");
        repo.Write("README.md", "# Demo\n\nEdited.\n");
        var headId = repo.Commit("head");
        return (baseId, headId);
    }

    static int Run(SyntheticRepo repo, string baseId, string headId, string outDir, params string[] extra)
    {
        string[] args = ["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", outDir, .. extra];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = CliApp.Run(args, stdout, stderr);
        Assert.Equal("", stderr.ToString());
        return exit;
    }

    static T Read<T>(string dir, string file) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(dir, file)), ReadOptions)!;

    static string[] Files(string dir, string model) =>
        [.. Read<ModelFile>(dir, model).Files.Select(f => f.Path)];

    static string[] Include(string glob) => ["--include", glob];

    [Fact]
    public void Without_include_only_the_changed_files_of_every_directory_are_extracted()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        Assert.Equal(0, Run(repo, baseId, headId, repo.OutDir));

        // gamma.h is unchanged, mode.h changed only its executable bit, the README is not C++.
        Assert.Equal(["include/a/alpha.h", "include/a/gone.h", "include/b/beta.h"], Files(repo.OutDir, "base.model.json"));
        Assert.Equal(["include/a/alpha.h", "include/a/fresh.h", "include/b/beta.h"], Files(repo.OutDir, "head.model.json"));
        var diff = Read<DiffFile>(repo.OutDir, "diff.json");
        Assert.Equal(["class Alpha", "class Beta", "class Fresh", "class Gone"], diff.Types.Select(t => t.Id));
        Assert.Equal(["modified", "modified", "added", "removed"], diff.Types.Select(t => t.State));
    }

    [Fact]
    public void A_deleted_file_is_in_the_base_model_only_and_an_added_file_in_the_head_model_only()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        Assert.Equal(0, Run(repo, baseId, headId, repo.OutDir));

        Assert.Contains("include/a/gone.h", Files(repo.OutDir, "base.model.json"));
        Assert.DoesNotContain("include/a/gone.h", Files(repo.OutDir, "head.model.json"));
        Assert.Contains("include/a/fresh.h", Files(repo.OutDir, "head.model.json"));
        Assert.DoesNotContain("include/a/fresh.h", Files(repo.OutDir, "base.model.json"));
    }

    [Fact]
    public void An_unchanged_file_and_a_mode_only_change_appear_in_neither_model()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        Assert.Equal(0, Run(repo, baseId, headId, repo.OutDir));

        foreach (var model in new[] { "base.model.json", "head.model.json" })
        {
            Assert.DoesNotContain("include/c/gamma.h", Files(repo.OutDir, model));
            Assert.DoesNotContain("include/c/mode.h", Files(repo.OutDir, model));
        }

        Assert.DoesNotContain(Read<DiffFile>(repo.OutDir, "diff.json").Types, t => t.Id is "class Gamma" or "class Mode");
    }

    [Fact]
    public void One_include_glob_keeps_only_that_directory()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        Assert.Equal(0, Run(repo, baseId, headId, repo.OutDir, Include("include/a/**")));

        Assert.Equal(["include/a/alpha.h", "include/a/gone.h"], Files(repo.OutDir, "base.model.json"));
        Assert.Equal(["include/a/alpha.h", "include/a/fresh.h"], Files(repo.OutDir, "head.model.json"));
        Assert.Equal(
            ["class Alpha", "class Fresh", "class Gone"],
            Read<DiffFile>(repo.OutDir, "diff.json").Types.Select(t => t.Id));
    }

    [Fact]
    public void A_leading_dot_slash_gives_the_same_output_as_without_it()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);
        var plain = Path.Combine(repo.Root, "out-plain");
        var dotted = Path.Combine(repo.Root, "out-dotted");

        Assert.Equal(0, Run(repo, baseId, headId, plain, Include("include/a/**")));
        Assert.Equal(0, Run(repo, baseId, headId, dotted, Include("./include/a/**")));

        AssertIdentical(plain, dotted);
    }

    [Fact]
    public void Include_matching_is_case_sensitive_end_to_end()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        Assert.Equal(0, Run(repo, baseId, headId, repo.OutDir, Include("Include/A/**")));

        Assert.Empty(Read<DiffFile>(repo.OutDir, "diff.json").Types);
    }

    [Fact]
    public void A_glob_that_matches_no_changed_file_is_a_successful_empty_run()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        Assert.Equal(0, Run(repo, baseId, headId, repo.OutDir, Include("nowhere/**")));

        Assert.Equal(OutputFiles, Directory.GetFiles(repo.OutDir).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Empty(Files(repo.OutDir, "base.model.json"));
        Assert.Empty(Files(repo.OutDir, "head.model.json"));
        var diff = Read<DiffFile>(repo.OutDir, "diff.json");
        Assert.Empty(diff.Types);
        Assert.Equal(new TypeCounts(0, 0, 0, 0, 0), diff.Summary.Types);
        Assert.Empty(diff.Warnings);
    }

    [Fact]
    public void Two_include_flags_in_either_order_give_byte_identical_output()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);
        var forward = Path.Combine(repo.Root, "out-forward");
        var backward = Path.Combine(repo.Root, "out-backward");

        Assert.Equal(0, Run(repo, baseId, headId, forward, [.. Include("include/a/**"), .. Include("include/b/**")]));
        Assert.Equal(0, Run(repo, baseId, headId, backward, [.. Include("include/b/**"), .. Include("include/a/**")]));

        AssertIdentical(forward, backward);
        Assert.Equal(
            ["class Alpha", "class Beta", "class Fresh", "class Gone"],
            Read<DiffFile>(forward, "diff.json").Types.Select(t => t.Id));
    }

    static void AssertIdentical(string left, string right)
    {
        Assert.Equal(OutputFiles, Directory.GetFiles(left).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var file in OutputFiles)
        {
            Assert.True(
                File.ReadAllBytes(Path.Combine(left, file)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(right, file))),
                file + " differs");
        }
    }

    [Fact]
    public void More_than_the_allowed_number_of_globs_is_refused_before_anything_runs()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);
        var args = new List<string> { "--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", repo.OutDir };
        for (var i = 0; i < CliOptions.MaxIncludeGlobs + 1; i++)
        {
            args.AddRange(Include("include/a/**"));
        }

        using var stderr = new StringWriter();
        var exit = CliApp.Run(args, new StringWriter(), stderr);

        Assert.Equal(1, exit);
        Assert.StartsWith("vdiff: ", stderr.ToString());
        Assert.Contains("--include", stderr.ToString());
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
    }

    [Fact]
    public void An_over_long_glob_is_refused_before_anything_runs()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        using var stderr = new StringWriter();
        var exit = CliApp.Run(
            ["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", repo.OutDir, "--include", new string('a', CliOptions.MaxGlobLength + 1)],
            new StringWriter(), stderr);

        Assert.Equal(1, exit);
        Assert.StartsWith("vdiff: ", stderr.ToString());
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
    }

    [Fact]
    public void The_limits_themselves_are_accepted_and_one_past_them_is_not()
    {
        string[] fixedArgs = ["--repo", "r", "--base", "abcdef1", "--head", "abcdef2", "--out", "o"];

        var atCount = CliOptions.Parse([.. fixedArgs, .. Enumerable.Range(0, CliOptions.MaxIncludeGlobs).SelectMany(i => Include("d" + i + "/**"))]);
        Assert.Equal(CliOptions.MaxIncludeGlobs, atCount.Include.Count);
        Assert.Throws<Vdiff.Core.VdiffException>(() =>
            CliOptions.Parse([.. fixedArgs, .. Enumerable.Range(0, CliOptions.MaxIncludeGlobs + 1).SelectMany(i => Include("d" + i + "/**"))]));

        var atLength = CliOptions.Parse([.. fixedArgs, .. Include(new string('a', CliOptions.MaxGlobLength))]);
        Assert.Equal(CliOptions.MaxGlobLength, atLength.Include[0].Length);
        Assert.Throws<Vdiff.Core.VdiffException>(() =>
            CliOptions.Parse([.. fixedArgs, .. Include(new string('a', CliOptions.MaxGlobLength + 1))]));
        Assert.Throws<Vdiff.Core.VdiffException>(() => CliOptions.Parse([.. fixedArgs, .. Include("a\0b")]));
    }
}
