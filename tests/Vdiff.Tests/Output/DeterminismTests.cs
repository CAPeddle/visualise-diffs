using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vdiff.Core.Cli;
using Vdiff.Core.Model;
using Vdiff.Core.Output;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Output;

/// <summary>
/// D-08 outputs at CLI level on a synthetic repository (D-09): byte-identical across runs, free
/// of local paths and identities (T-01-18), LF only, UTF-8 without a byte-order mark.
/// </summary>
[Collection("sidecar")]
public class DeterminismTests
{
    static readonly string[] FileNames =
        ["base.model.json", "diff.json", "head.model.json", "report.html", "summary.mmd"];

    static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    // The base header holds a std::map field behind an export macro; the head adds an operator
    // and nested template arguments, so the model and diff carry angle brackets and ampersands.
    const string BaseHeader =
        "// café 中文\n" +
        "#pragma once\n" +
        "#define EXPORT_API\n" +
        "#define LIB_CALL\n" +
        "namespace shop {\n" +
        "class EXPORT_API Gadget {\n" +
        "public:\n" +
        "    int size() const;\n" +
        "    std::map<int, int> table_;\n" +
        "};\n" +
        "}\n";

    const string HeadHeader =
        "// café 中文\n" +
        "#pragma once\n" +
        "#define EXPORT_API\n" +
        "#define LIB_CALL\n" +
        "namespace shop {\n" +
        "class EXPORT_API Gadget {\n" +
        "public:\n" +
        "    long size() const;\n" +
        "    Gadget& operator<<(int value);\n" +
        "    std::map<int, int> table_;\n" +
        "    LIB_CALL std::map<int, std::vector<char*>> index_;\n" +
        "};\n" +
        "class EXPORT_API Widget : public Gadget {\n" +
        "public:\n" +
        "    ~Widget();\n" +
        "};\n" +
        "}\n";

    static (string BaseId, string HeadId) Build(SyntheticRepo repo)
    {
        repo.Write("include/shop/gadget.h", BaseHeader);
        repo.Write("include/shop/legacy.h", "#pragma once\nnamespace shop {\nstruct Legacy {\n    int id;\n};\n}\n");
        repo.Write("README.md", "# Shop\n");
        var baseId = repo.Commit("base");
        repo.Write("include/shop/gadget.h", HeadHeader);
        repo.Delete("include/shop/legacy.h");
        repo.Write("README.md", "# Shop\n\nEdited.\n");
        var headId = repo.Commit("head");
        return (baseId, headId);
    }

    static readonly string[] BlankFlags = ["--blank", "EXPORT_API", "--blank", "LIB_CALL"];

    static string Run(SyntheticRepo repo, string baseId, string headId, string outName, params string[] extra)
    {
        var outDir = Path.Combine(repo.Root, outName);
        var args = new List<string> { "--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", outDir };
        args.AddRange(extra);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = CliApp.Run(args, stdout, stderr);
        Assert.Equal("", stderr.ToString());
        Assert.Equal(0, exit);
        return outDir;
    }

    static string[] Hashes(string outDir)
    {
        Assert.Equal(FileNames, Directory.GetFiles(outDir).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        return FileNames.Select(n => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(outDir, n))))).ToArray();
    }

    [Fact]
    public void Two_runs_on_the_same_input_give_five_byte_identical_files()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        var first = Run(repo, baseId, headId, "out-a", BlankFlags);
        var second = Run(repo, baseId, headId, "out-b", BlankFlags);

        Assert.Equal(Hashes(first), Hashes(second));
        Assert.Contains("shop::Widget", File.ReadAllText(Path.Combine(first, "summary.mmd")));
    }

    [Fact]
    public void Reversing_the_order_of_repeated_blank_flags_gives_identical_files()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        var forward = Run(repo, baseId, headId, "out-a", "--blank", "EXPORT_API", "--blank", "LIB_CALL");
        var reversed = Run(repo, baseId, headId, "out-b", "--blank", "LIB_CALL", "--blank", "EXPORT_API");

        Assert.Equal(Hashes(forward), Hashes(reversed));
    }

    [Fact]
    public void A_run_with_base_equal_to_head_exits_zero_with_zero_counts_and_is_itself_identical_across_runs()
    {
        using var repo = new SyntheticRepo();
        var (_, headId) = Build(repo);

        var first = Run(repo, headId, headId, "out-a", BlankFlags);
        var second = Run(repo, headId, headId, "out-b", BlankFlags);

        var diff = JsonSerializer.Deserialize<DiffFile>(File.ReadAllText(Path.Combine(first, "diff.json")), ReadOptions)!;
        Assert.Empty(diff.Types);
        Assert.Empty(diff.Warnings);
        Assert.Equal(new TypeCounts(0, 0, 0, 0), diff.Summary.Types);
        Assert.Equal(new MemberCounts(0, 0, 0), diff.Summary.Members);
        Assert.Equal(new UnmodelledCounts(0, 0), diff.Summary.Unmodelled);
        Assert.Equal(headId, diff.Base);
        Assert.Equal(headId, diff.Head);

        var mermaid = File.ReadAllText(Path.Combine(first, "summary.mmd"));
        Assert.Empty(MermaidLint.Check(mermaid));
        Assert.StartsWith("classDiagram\n  classDef added ", mermaid);
        Assert.DoesNotContain("  class ", mermaid);
        Assert.Equal(Hashes(first), Hashes(second));
    }

    [Fact]
    public void Model_and_diff_json_show_angle_brackets_and_ampersands_literally()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        var outDir = Run(repo, baseId, headId, "out-a", BlankFlags);

        // table_ is unchanged, so only the head model carries std::map<int, int>; the diff carries
        // the added operator and the added nested-template field.
        var expected = new Dictionary<string, string[]>
        {
            ["diff.json"] = ["operator<<", "Gadget&", "std::map<int, std::vector<char*>>"],
            ["head.model.json"] = ["operator<<", "Gadget&", "std::map<int, int>", "std::map<int, std::vector<char*>>"],
        };
        foreach (var (name, fragments) in expected)
        {
            var json = File.ReadAllText(Path.Combine(outDir, name));
            foreach (var fragment in fragments)
            {
                Assert.Contains(fragment, json);
            }

            Assert.DoesNotMatch("(?i)\\\\u00(3c|3e|26|27)", json);
        }
    }

    [Fact]
    public void A_quote_or_a_backslash_in_a_name_is_still_escaped_in_the_json()
    {
        var member = new MemberDiff("k", "n", "added", null, "public", null, "char c = '\"'; // a\\b <x>");
        var type = new TypeDiff("class a\"b\\c", "a\"b\\c", "class", "added", null, "f.h", new BaseChanges([], []), [member]);
        var diff = new DiffFile(
            DiffFile.SchemaName,
            "b",
            "h",
            new DiffSummary(new TypeCounts(1, 0, 0, 0), new MemberCounts(1, 0, 0), new UnmodelledCounts(0, 0)),
            [],
            [type]);

        var json = OutputWriter.Serialize(diff);

        Assert.Contains("\"class a\\\"b\\\\c\"", json);
        Assert.Contains("a\\\\b <x>", json);
        Assert.DoesNotContain('\r', json);
        Assert.Equal(diff.Types[0].Id, JsonSerializer.Deserialize<DiffFile>(json, ReadOptions)!.Types[0].Id);
    }

    [Fact]
    public void No_output_file_contains_the_sandbox_the_temp_directory_the_user_name_or_the_machine_name()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        var outDir = Run(repo, baseId, headId, "out-a", BlankFlags);

        // A commit id is hexadecimal and could contain a short name by chance; take the ids out first.
        var identities = new List<string>
        {
            repo.Root,
            repo.Root.Replace('\\', '/'),
            repo.Root.Replace("\\", "\\\\", StringComparison.Ordinal),
            Path.GetFileName(repo.Root),
            Path.GetTempPath().TrimEnd('\\', '/'),
            Path.GetTempPath().TrimEnd('\\', '/').Replace('\\', '/'),
            Path.GetTempPath().TrimEnd('\\', '/').Replace("\\", "\\\\", StringComparison.Ordinal),
        };
        foreach (var name in new[] { Environment.UserName, Environment.MachineName })
        {
            // Names under three characters would match ordinary words.
            if (name.Length >= 3)
            {
                identities.Add(name);
            }
        }

        foreach (var file in FileNames)
        {
            var text = Regex.Replace(File.ReadAllText(Path.Combine(outDir, file)), "[0-9a-f]{40}", "<commit>");
            foreach (var identity in identities)
            {
                Assert.False(
                    text.Contains(identity, StringComparison.OrdinalIgnoreCase),
                    $"{file} contains a local identity: {identity}");
            }
        }
    }

    [Fact]
    public void Every_output_file_is_utf8_without_a_byte_order_mark_with_line_feeds_only_and_ends_with_a_line_feed()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        var outDir = Run(repo, baseId, headId, "out-a", BlankFlags);

        var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
        foreach (var file in FileNames)
        {
            var bytes = File.ReadAllBytes(Path.Combine(outDir, file));
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, file + " starts with a byte-order mark");
            Assert.Equal((byte)'\n', bytes[^1]);
            Assert.DoesNotContain('﻿', strict.GetString(bytes));
        }
    }

    [Fact]
    public void The_mermaid_and_html_outputs_of_a_real_run_pass_the_structural_checks()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = Build(repo);

        var outDir = Run(repo, baseId, headId, "out-a", BlankFlags);

        var mermaid = File.ReadAllText(Path.Combine(outDir, "summary.mmd"));
        Assert.Empty(MermaidLint.Check(mermaid));
        Assert.Contains("C_shop__Gadget <|-- C_shop__Widget", mermaid);
        var html = File.ReadAllText(Path.Combine(outDir, "report.html"));
        Assert.DoesNotMatch("(?i)<script|https?:", html);
    }
}
