using System.Text.Json;
using Vdiff.Core.Cli;
using Vdiff.Core.Model;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Extraction;

/// <summary>
/// Fixture B (D-09, synthetic and generic): one header with a namespace, a class template, an
/// enum class with an underlying type, a nested struct, operators and aliases. The head adds an
/// enumerator, drops the constness of a conversion operator and adds an alias. Driven through
/// the real CLI and the real sidecar.
/// </summary>
[Collection("sidecar")]
public class RichFixtureEndToEndTests
{
    const string HeaderPath = "include/shop/box.h";

    static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    static T ReadJson<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReadOptions)!;

    static string BaseHeader() => """
        #pragma once
        namespace shop {
        template <class T>
        class Box {
        public:
            enum class Mode : unsigned char { Fast, Slow };
            struct Slot {
                int index;
                T value;
            };
            using Item = std::pair<int, int>;
            Box();
            ~Box();
            bool operator==(const Box& other) const;
            operator bool() const;
            int count_a, count_b;
        private:
            int (*callback_)(int);
        };
        }
        """.ReplaceLineEndings("\n") + "\n";

    static string HeadHeader() => """
        #pragma once
        namespace shop {
        template <class T>
        class Box {
        public:
            enum class Mode : unsigned char { Fast, Slow, Medium };
            struct Slot {
                int index;
                T value;
            };
            using Item = std::pair<int, int>;
            using Size = unsigned long;
            Box();
            ~Box();
            bool operator==(const Box& other) const;
            operator bool();
            int count_a, count_b;
        private:
            int (*callback_)(int);
        };
        }
        """.ReplaceLineEndings("\n") + "\n";

    static (SyntheticRepo Repo, string BaseId, string HeadId) Build()
    {
        var repo = new SyntheticRepo();
        repo.Write(HeaderPath, BaseHeader());
        var baseId = repo.Commit("base");
        repo.Write(HeaderPath, HeadHeader());
        var headId = repo.Commit("head");
        return (repo, baseId, headId);
    }

    static int RunCli(string[] args, out string stderr)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var exit = CliApp.Run(args, outWriter, errWriter);
        stderr = errWriter.ToString();
        return exit;
    }

    [Fact]
    public void Diff_on_fixture_B_reports_the_exact_member_states()
    {
        var (repo, baseId, headId) = Build();
        using var sandbox = repo;

        var exit = RunCli(["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", repo.OutDir], out var stderr);
        Assert.Equal("", stderr);
        Assert.Equal(0, exit);

        var diff = ReadJson<DiffFile>(Path.Combine(repo.OutDir, "diff.json"));
        Assert.Equal(["class shop::Box", "enum shop::Box::Mode"], diff.Types.Select(t => t.Id));
        Assert.All(diff.Types, t => Assert.Equal("modified", t.State));
        Assert.Equal(["class", "enum"], diff.Types.Select(t => t.Kind));

        var box = diff.Types[0];
        Assert.Equal(["Size", "operator bool()", "operator bool() const"], box.Members.Select(m => m.Key));
        Assert.Equal(["added", "added", "removed"], box.Members.Select(m => m.State));
        Assert.Equal("using Size = unsigned long", box.Members[0].SignatureAfter);
        Assert.Equal("public", box.Members[0].AccessAfter);
        Assert.Equal("operator bool", box.Members[1].Name);
        Assert.Equal("operator bool()", box.Members[1].SignatureAfter);
        Assert.Equal("operator bool() const", box.Members[2].SignatureBefore);
        Assert.Empty(box.Bases.Added);
        Assert.Empty(box.Bases.Removed);

        var mode = diff.Types[1];
        Assert.Equal(["Medium"], mode.Members.Select(m => m.Key));
        Assert.Equal(["added"], mode.Members.Select(m => m.State));
        Assert.Equal("none", mode.Members[0].AccessAfter);
        Assert.Empty(mode.Bases.Added);

        // Slot is unchanged, so it is counted and not listed.
        Assert.Equal(new TypeCounts(Added: 0, Removed: 0, Modified: 2, Unchanged: 1), diff.Summary.Types);
        Assert.Equal(new MemberCounts(Added: 3, Removed: 1, Modified: 0), diff.Summary.Members);
        Assert.Equal(new UnmodelledCounts(Base: 0, Head: 0), diff.Summary.Unmodelled);
        Assert.Empty(diff.Warnings);
    }

    [Fact]
    public void Head_model_carries_the_richer_kinds_scopes_and_access_sections()
    {
        var (repo, baseId, headId) = Build();
        using var sandbox = repo;

        Assert.Equal(0, RunCli(["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", repo.OutDir], out _));
        var head = ReadJson<ModelFile>(Path.Combine(repo.OutDir, "head.model.json"));

        Assert.Equal(
            ["class shop::Box", "enum shop::Box::Mode", "struct shop::Box::Slot"],
            head.Types.Select(t => t.Id));
        Assert.All(head.Types, t => Assert.Equal(HeaderPath, t.File));
        Assert.Equal([new ModelFileEntry(HeaderPath, Languages.Cpp, Modes.SyntaxOnly, null, 0)], head.Files);

        var box = head.Types[0];
        Assert.Equal("shop", box.Scope);
        Assert.Equal("Box", box.Name);
        string[] expected =
        [
            "public alias Item",
            "public alias Size",
            "public field count_a",
            "public field count_b",
            "public method Box()",
            "public method operator bool()",
            "public method operator==(const Box& other) const",
            "public method ~Box()",
            "private field callback_",
        ];
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            box.Members.Select(m => $"{m.Access} {m.Kind} {m.Key}").Order(StringComparer.Ordinal));
        Assert.Equal("int (*callback_)(int)", box.Members.Single(m => m.Key == "callback_").Signature);
        Assert.Equal("int count_a", box.Members.Single(m => m.Key == "count_a").Signature);
        Assert.Equal("int count_b", box.Members.Single(m => m.Key == "count_b").Signature);

        var mode = head.Types[1];
        Assert.Equal("shop::Box", mode.Scope);
        Assert.Equal(["unsigned char"], mode.Bases);
        Assert.Equal(["Fast", "Medium", "Slow"], mode.Members.Select(m => m.Key));
        Assert.All(mode.Members, m => Assert.Equal(("enumerator", "none"), (m.Kind, m.Access)));

        var slot = head.Types[2];
        Assert.Equal("shop::Box", slot.Scope);
        Assert.Equal("struct", slot.Kind);
        Assert.Equal(["index", "value"], slot.Members.Select(m => m.Key));
        Assert.All(slot.Members, m => Assert.Equal("public", m.Access));
    }
}
