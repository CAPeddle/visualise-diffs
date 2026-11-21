using System.Text.Json;
using Vdiff.Core.Cli;
using Vdiff.Core.Extraction;
using Vdiff.Core.Git;
using Vdiff.Core.Model;
using Vdiff.Core.Output;
using Vdiff.Core.Pipeline;
using Vdiff.Core.Scope;
using Vdiff.Tests.Support;

namespace Vdiff.Tests;

/// <summary>
/// The tracer proof (D-09, synthetic repositories only): Fixture A is built by script, then
/// the pipeline is driven through the real sidecar.
/// </summary>
[Collection("sidecar")]
public class TracerEndToEndTests
{
    readonly SidecarFixture _sidecar;

    public TracerEndToEndTests(SidecarFixture sidecar) => _sidecar = sidecar;

    /// <summary>
    /// Fixture A. Base: class Gadget (public size() const and reset(), private count_), struct
    /// Legacy and a README. Head: size() returns long, reset() is gone, grow(int) is added,
    /// count_ is protected, Legacy is deleted, class Widget deriving from Gadget is added
    /// (constructor and destructor), the README is edited.
    /// </summary>
    static (string BaseId, string HeadId) BuildFixtureA(SyntheticRepo repo)
    {
        repo.Write("include/shop/gadget.h", """
            #pragma once
            namespace shop {
            class Gadget {
            public:
                int size() const;
                void reset();
            private:
                int count_;
            };
            }
            """.ReplaceLineEndings("\n") + "\n");
        repo.Write("include/shop/legacy.h", """
            #pragma once
            namespace shop {
            struct Legacy {
                int id;
            };
            }
            """.ReplaceLineEndings("\n") + "\n");
        repo.Write("README.md", "# Shop\n");
        var baseId = repo.Commit("base");

        repo.Write("include/shop/gadget.h", """
            #pragma once
            namespace shop {
            class Gadget {
            public:
                long size() const;
                void grow(int by);
            protected:
                int count_;
            };
            }
            """.ReplaceLineEndings("\n") + "\n");
        repo.Delete("include/shop/legacy.h");
        repo.Write("include/shop/widget.h", """
            #pragma once
            #include "gadget.h"
            namespace shop {
            class Widget : public Gadget {
            public:
                Widget();
                ~Widget();
            };
            }
            """.ReplaceLineEndings("\n") + "\n");
        repo.Write("README.md", "# Shop\n\nEdited.\n");
        var headId = repo.Commit("head");
        return (baseId, headId);
    }

    static void AssertFixtureADiff(DiffFile diff)
    {
        Assert.Equal(
            ["class shop::Gadget", "class shop::Widget", "struct shop::Legacy"],
            diff.Types.Select(t => t.Id));

        var gadget = diff.Types[0];
        Assert.Equal("modified", gadget.State);
        Assert.Equal("include/shop/gadget.h", gadget.BaseFile);
        Assert.Equal("include/shop/gadget.h", gadget.HeadFile);
        Assert.Equal(["count_", "grow(int by)", "reset()", "size() const"], gadget.Members.Select(m => m.Key));
        Assert.Equal(["modified", "added", "removed", "modified"], gadget.Members.Select(m => m.State));
        Assert.Equal("private", gadget.Members[0].AccessBefore);
        Assert.Equal("protected", gadget.Members[0].AccessAfter);
        Assert.Equal("int size() const", gadget.Members[3].SignatureBefore);
        Assert.Equal("long size() const", gadget.Members[3].SignatureAfter);

        var widget = diff.Types[1];
        Assert.Equal("added", widget.State);
        Assert.Null(widget.BaseFile);
        Assert.Equal("include/shop/widget.h", widget.HeadFile);
        Assert.Equal(["Gadget"], widget.Bases.Added);
        Assert.Equal(["Widget()", "~Widget()"], widget.Members.Select(m => m.Key));
        Assert.All(widget.Members, m => Assert.Equal("added", m.State));

        var legacy = diff.Types[2];
        Assert.Equal("removed", legacy.State);
        Assert.Equal("include/shop/legacy.h", legacy.BaseFile);
        Assert.Null(legacy.HeadFile);
        Assert.Equal(["id"], legacy.Members.Select(m => m.Key));
        Assert.Equal("removed", legacy.Members[0].State);

        Assert.Equal(new TypeCounts(Added: 1, Removed: 1, Modified: 1, Unchanged: 0), diff.Summary.Types);
        Assert.Equal(new MemberCounts(Added: 3, Removed: 2, Modified: 2), diff.Summary.Members);
        Assert.Equal(new UnmodelledCounts(Base: 0, Head: 0), diff.Summary.Unmodelled);
        Assert.Empty(diff.Warnings);
    }

    static void AssertExtractionMetadata(ModelFile model, params string[] files)
    {
        Assert.Equal("vdiff-model/v0-tracer", model.Schema);
        Assert.Equal("cpp-ts", model.Extraction.Extractor);
        Assert.Equal("syntax-only", model.Extraction.Mode);
        Assert.Empty(model.Extraction.MacrosBlanked);
        Assert.Equal(files, model.Files.Select(f => f.Path));
        Assert.All(model.Files, f => Assert.Equal(0, f.ErrorNodes));
        Assert.Empty(model.Unmodelled);
    }

    [Fact]
    public void Pipeline_on_fixture_A_returns_the_expected_diff_through_the_real_sidecar()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildFixtureA(repo);
        var reader = new GitObjectReader(repo.RepoDir);

        var result = TracerPipeline.Run(
            reader,
            reader.ResolveCommit(baseId),
            reader.ResolveCommit(headId),
            new ScopeFilter(),
            [],
            _sidecar.Command,
            SidecarLimits.Default);

        AssertFixtureADiff(result.Diff);
        AssertExtractionMetadata(result.Base, "include/shop/gadget.h", "include/shop/legacy.h");
        AssertExtractionMetadata(result.Head, "include/shop/gadget.h", "include/shop/widget.h");
        Assert.Equal(baseId, result.Meta.BaseCommit);
        Assert.Equal(headId, result.Meta.HeadCommit);
    }

    static void AssertRunMeta(RunMeta meta, string baseId, string headId)
    {
        Assert.Equal("vdiff-run-meta/v1", meta.Schema);
        Assert.Equal(baseId, meta.BaseCommit);
        Assert.Equal(headId, meta.HeadCommit);
        Assert.False(string.IsNullOrEmpty(meta.VdiffVersion));
        Assert.DoesNotContain('+', meta.VdiffVersion);
        var extractor = Assert.Single(meta.Extractors);
        Assert.Equal("cpp-ts", extractor.Name);
        Assert.Equal(
            [new ToolVersion("tree-sitter", "0.27.0"), new ToolVersion("tree-sitter-cpp", "0.23.4")],
            extractor.Tools);
    }

    static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    static T ReadJson<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReadOptions)!;

    static int RunCli(string[] args, out string stdout, out string stderr)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var exit = CliApp.Run(args, outWriter, errWriter);
        stdout = outWriter.ToString();
        stderr = errWriter.ToString();
        return exit;
    }

    [Fact]
    public void Cli_run_on_fixture_A_writes_every_output_file_and_leaves_the_repository_untouched()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildFixtureA(repo);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var exit = RunCli(
            ["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", repo.OutDir],
            out _, out var stderr);

        Assert.Equal("", stderr);
        Assert.Equal(0, exit);
        Assert.Equal(
            OutputWriter.FileNames.Order(StringComparer.Ordinal),
            Directory.GetFiles(repo.OutDir).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        // Written as UTF-8 without a byte-order mark and with LF line ends only.
        foreach (var file in Directory.GetFiles(repo.OutDir))
        {
            var bytes = File.ReadAllBytes(file);
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, file);
            Assert.Equal((byte)'\n', bytes[^1]);
        }

        var diff = ReadJson<DiffFile>(Path.Combine(repo.OutDir, "diff.json"));
        AssertFixtureADiff(diff);
        using (var diffJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.OutDir, "diff.json"))))
        {
            Assert.False(diffJson.RootElement.TryGetProperty("base", out _));
            Assert.False(diffJson.RootElement.TryGetProperty("head", out _));
        }

        var head = ReadJson<ModelFile>(Path.Combine(repo.OutDir, "head.model.json"));
        AssertExtractionMetadata(head, "include/shop/gadget.h", "include/shop/widget.h");
        var baseModel = ReadJson<ModelFile>(Path.Combine(repo.OutDir, "base.model.json"));
        AssertExtractionMetadata(baseModel, "include/shop/gadget.h", "include/shop/legacy.h");

        AssertRunMeta(ReadJson<RunMeta>(Path.Combine(repo.OutDir, OutputWriter.MetaFile)), baseId, headId);

        var mermaid = File.ReadAllText(Path.Combine(repo.OutDir, "summary.mmd"));
        Assert.StartsWith("classDiagram", mermaid);
        Assert.Contains(":::added", mermaid);
        Assert.Contains(":::removed", mermaid);
        Assert.Contains(":::modified", mermaid);
        Assert.Equal(3, mermaid.Split('\n').Count(l => l.TrimStart().StartsWith("classDef ", StringComparison.Ordinal)));
        Assert.Contains("[added]", mermaid);
        Assert.Contains("[removed]", mermaid);
        Assert.Contains("[modified]", mermaid);

        var html = File.ReadAllText(Path.Combine(repo.OutDir, "report.html"));
        Assert.Contains("shop::Widget", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", html, StringComparison.OrdinalIgnoreCase);

        // The whole sandbox (work tree and .git) and every git-visible state are as they were.
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Fact]
    public void Cli_refuses_an_out_directory_inside_the_repository()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildFixtureA(repo);
        var before = RepoSnapshot.Take(repo.Root);

        foreach (var inside in new[] { Path.Combine(repo.RepoDir, "results"), Path.Combine(repo.RepoDir, ".git", "vdiff"), repo.RepoDir })
        {
            var exit = RunCli(
                ["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", inside],
                out _, out var stderr);

            Assert.Equal(1, exit);
            Assert.StartsWith("vdiff: ", stderr);
            Assert.Contains("--out must not lie inside the analysed repository", stderr);
        }

        Assert.False(Directory.Exists(Path.Combine(repo.RepoDir, "results")));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root));
    }
}
