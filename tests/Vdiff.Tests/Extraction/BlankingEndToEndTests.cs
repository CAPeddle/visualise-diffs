using System.Text;
using System.Text.Json;
using Vdiff.Core.Cli;
using Vdiff.Core.Model;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Extraction;

/// <summary>
/// Macro blanking (D-05) proven through the real CLI and the real sidecar on a synthetic
/// repository: a type behind an export macro is missing without <c>--blank</c> and found with it,
/// and every span stays a true UTF-8 byte offset in the committed file because blanking keeps the
/// length.
/// </summary>
[Collection("sidecar")]
public class BlankingEndToEndTests
{
    const string HeaderPath = "include/shop/export.h";

    static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    static T ReadJson<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReadOptions)!;

    // The leading comment holds multi-byte characters, so a character offset and a byte offset differ.
    const string BaseHeader =
        "// café 中文\n" +
        "#pragma once\n" +
        "#define EXPORT_API\n" +
        "#define LIB_CALL\n" +
        "namespace shop {\n" +
        "class EXPORT_API Foo : public Base {\n" +
        "public:\n" +
        "    int n_;\n" +
        "};\n" +
        "}\n";

    const string HeadHeader =
        "// café 中文\n" +
        "#pragma once\n" +
        "#define EXPORT_API\n" +
        "#define LIB_CALL\n" +
        "namespace shop {\n" +
        "class EXPORT_API Foo : public Base {\n" +
        "public:\n" +
        "    int n_;\n" +
        "    EXPORT_API int size() const; LIB_CALL int after_macro;\n" +
        "};\n" +
        "}\n";

    static (SyntheticRepo Repo, string BaseId, string HeadId) Build(byte[]? bom = null)
    {
        var repo = new SyntheticRepo();
        repo.Write(HeaderPath, With(bom, BaseHeader));
        var baseId = repo.Commit("base");
        repo.Write(HeaderPath, With(bom, HeadHeader));
        var headId = repo.Commit("head");
        return (repo, baseId, headId);
    }

    static byte[] With(byte[]? prefix, string text) =>
        [.. prefix ?? [], .. new UTF8Encoding(false).GetBytes(text)];

    static int RunCli(SyntheticRepo repo, string baseId, string headId, string[] extra, out string stderr)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        string[] args = ["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", repo.OutDir, .. extra];
        var exit = CliApp.Run(args, outWriter, errWriter);
        stderr = errWriter.ToString();
        return exit;
    }

    static int IndexOf(byte[] haystack, string needle)
    {
        var bytes = Encoding.UTF8.GetBytes(needle);
        return haystack.AsSpan().IndexOf(bytes);
    }

    [Fact]
    public void Without_blank_the_type_behind_an_export_macro_is_missing_and_the_file_is_labelled()
    {
        var (repo, baseId, headId) = Build();
        using var sandbox = repo;

        Assert.Equal(0, RunCli(repo, baseId, headId, [], out var stderr));
        Assert.Equal("", stderr);

        var head = ReadJson<ModelFile>(Path.Combine(repo.OutDir, "head.model.json"));
        Assert.DoesNotContain(head.Types, t => t.Id == "class shop::Foo");
        Assert.Empty(head.Extraction.MacrosBlanked);
        // Never hidden: the unparsed region shows as error nodes on the file and as a diff warning.
        Assert.True(head.Files.Single().ErrorNodes >= 1);
        var diff = ReadJson<DiffFile>(Path.Combine(repo.OutDir, "diff.json"));
        Assert.Contains(diff.Warnings, w => w.StartsWith("head: 1 file(s) with parse errors", StringComparison.Ordinal));
    }

    [Fact]
    public void With_blank_the_type_is_found_with_its_base_and_members_and_the_macros_are_recorded()
    {
        var (repo, baseId, headId) = Build();
        using var sandbox = repo;

        Assert.Equal(0, RunCli(repo, baseId, headId, ["--blank", "EXPORT_API", "--blank", "LIB_CALL"], out var stderr));
        Assert.Equal("", stderr);

        var head = ReadJson<ModelFile>(Path.Combine(repo.OutDir, "head.model.json"));
        var foo = Assert.Single(head.Types);
        Assert.Equal("class shop::Foo", foo.Id);
        Assert.Equal(["Base"], foo.Bases);
        Assert.Equal(["after_macro", "n_", "size() const"], foo.Members.Select(m => m.Key));
        Assert.All(foo.Members, m => Assert.Equal("public", m.Access));
        Assert.Equal(0, head.Files.Single().ErrorNodes);

        var baseModel = ReadJson<ModelFile>(Path.Combine(repo.OutDir, "base.model.json"));
        Assert.Equal(["EXPORT_API", "LIB_CALL"], head.Extraction.MacrosBlanked);
        Assert.Equal(["EXPORT_API", "LIB_CALL"], baseModel.Extraction.MacrosBlanked);
        Assert.Equal(["class shop::Foo"], baseModel.Types.Select(t => t.Id));

        var diff = ReadJson<DiffFile>(Path.Combine(repo.OutDir, "diff.json"));
        Assert.Equal(["class shop::Foo"], diff.Types.Select(t => t.Id));
        Assert.Equal(["after_macro", "size() const"], diff.Types[0].Members.Select(m => m.Key));
        Assert.All(diff.Types[0].Members, m => Assert.Equal("added", m.State));
        Assert.Empty(diff.Warnings);
    }

    [Fact]
    public void Repeated_blank_flags_are_recorded_sorted_and_distinct()
    {
        var (repo, baseId, headId) = Build();
        using var sandbox = repo;

        var exit = RunCli(
            repo, baseId, headId,
            ["--blank", "LIB_CALL", "--blank", "EXPORT_API", "--blank", "LIB_CALL", "--blank", "EXPORT_API"],
            out _);

        Assert.Equal(0, exit);
        foreach (var name in new[] { "head.model.json", "base.model.json" })
        {
            var model = ReadJson<ModelFile>(Path.Combine(repo.OutDir, name));
            Assert.Equal(["EXPORT_API", "LIB_CALL"], model.Extraction.MacrosBlanked);
        }
    }

    [Fact]
    public void Spans_are_true_utf8_byte_offsets_in_the_committed_file()
    {
        var (repo, baseId, headId) = Build();
        using var sandbox = repo;
        Assert.Equal(0, RunCli(repo, baseId, headId, ["--blank", "EXPORT_API", "--blank", "LIB_CALL"], out _));

        var committed = With(null, HeadHeader);
        Assert.NotEqual(HeadHeader.Length, committed.Length); // the comment really holds multi-byte text
        var head = ReadJson<ModelFile>(Path.Combine(repo.OutDir, "head.model.json"));
        var foo = Assert.Single(head.Types);

        // The type starts where `class` starts in the original bytes, macro and all.
        Assert.Equal(IndexOf(committed, "class EXPORT_API Foo"), foo.Span.StartByte);
        Assert.Equal(IndexOf(committed, "};\n}\n") + 1, foo.Span.EndByte); // stops after the closing brace
        Assert.Equal(6, foo.Span.StartLine);
        Assert.Equal(10, foo.Span.EndLine);

        // A member declared after a macro on the same line starts at its own first byte, and the
        // second declaration on that line (after LIB_CALL) starts exactly where its text starts.
        var size = foo.Members.Single(m => m.Key == "size() const");
        Assert.Equal(IndexOf(committed, "int size() const;"), size.Span.StartByte);
        Assert.Equal(size.Span.StartByte + "int size() const;".Length, size.Span.EndByte);
        var after = foo.Members.Single(m => m.Key == "after_macro");
        Assert.Equal(IndexOf(committed, "int after_macro;"), after.Span.StartByte);
        Assert.Equal(after.Span.StartByte + "int after_macro;".Length, after.Span.EndByte);
        Assert.Equal(9, after.Span.StartLine);

        // The span cuts the original bytes at the declaration, not at an offset shifted by blanking.
        var sliced = Encoding.UTF8.GetString(committed, size.Span.StartByte, size.Span.EndByte - size.Span.StartByte);
        Assert.Equal("int size() const;", sliced);
    }

    [Fact]
    public void A_byte_order_mark_stays_and_every_span_after_it_is_offset_by_exactly_three_bytes()
    {
        var (plainRepo, plainBase, plainHead) = Build();
        using var plainSandbox = plainRepo;
        Assert.Equal(0, RunCli(plainRepo, plainBase, plainHead, ["--blank", "EXPORT_API", "--blank", "LIB_CALL"], out _));
        var plain = Assert.Single(ReadJson<ModelFile>(Path.Combine(plainRepo.OutDir, "head.model.json")).Types);

        var (bomRepo, bomBase, bomHead) = Build([0xEF, 0xBB, 0xBF]);
        using var bomSandbox = bomRepo;
        Assert.Equal(0, RunCli(bomRepo, bomBase, bomHead, ["--blank", "EXPORT_API", "--blank", "LIB_CALL"], out _));
        var withBom = Assert.Single(ReadJson<ModelFile>(Path.Combine(bomRepo.OutDir, "head.model.json")).Types);

        Assert.Equal("class shop::Foo", withBom.Id);
        Assert.Equal(plain.Span.StartByte + 3, withBom.Span.StartByte);
        Assert.Equal(plain.Span.EndByte + 3, withBom.Span.EndByte);
        Assert.Equal(plain.Span.StartLine, withBom.Span.StartLine);
        Assert.Equal(
            plain.Members.Select(m => (m.Key, m.Span.StartByte + 3, m.Span.EndByte + 3)),
            withBom.Members.Select(m => (m.Key, m.Span.StartByte, m.Span.EndByte)));
    }
}
