using System.Text;
using Vdiff.Core;
using Vdiff.Core.Cli;
using Vdiff.Core.Process;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Output;

/// <summary>
/// Runs the local mermaid-cli check (tools/mermaid-check/check.mjs) on the Mermaid text vdiff
/// writes and on deliberately broken text (TRC-02, D-09: synthetic content only, nothing leaves
/// the machine). A missing prerequisite (node, the installed packages, a browser) fails the test
/// with an install hint; it never skips. Select with <c>--filter "Category=MermaidCli"</c>.
/// </summary>
[Collection("sidecar")]
[Trait("Category", "MermaidCli")]
public class MermaidCliSyntaxTests
{
    const int PrerequisiteMissing = 3;
    const string InstallHint = "npm --prefix tools/mermaid-check ci --ignore-scripts";

    const string ClassDefs =
        "  classDef added fill:#d4f8d4,stroke:#2e7d32,color:#111\n" +
        "  classDef removed fill:#fde0e0,stroke:#c62828,color:#111,stroke-dasharray:4 3\n" +
        "  classDef modified fill:#fff4cc,stroke:#b58900,color:#111\n";

    const string HardHeaderPath = "include/shop/hard.h";

    static readonly string HardHeader = """
        #pragma once
        #include <map>
        #include <vector>
        namespace shop {
        template <class T, int N = 4>
        class Cell {
        public:
            enum class Mode : unsigned char { Fast, Slow };
            Cell();
            ~Cell();
            Cell& operator=(const Cell& other);
            bool operator==(const Cell& other) const;
            bool operator<(const Cell& other) const;
            T& operator[](int index);
            operator bool() const;
            std::map<int, std::vector<char*>> table;
            std::vector<int> seeds{1, 2, 3};
            int (*callback)(int, char*);
            static const int Limit = N * 2;
            template <class U> U convert(U value) const;
        private:
            int slots_[N];
        };
        }
        """.ReplaceLineEndings("\n") + "\n";

    const string KeywordHeaderPath = "include/keywords.h";

    // Global scope, no namespace: the class id is derived from the bare name, so each of these is a
    // Mermaid class-diagram keyword (review finding CR-01). `linked` derives from `link` so a keyword
    // is also exercised on an inheritance-edge line.
    static readonly string KeywordHeader = """
        #pragma once
        struct link { int a; };
        struct note { int a; };
        struct click { int a; };
        struct callback { int a; };
        struct href { int a; };
        struct cssClass { int a; };
        struct linked : public link { int b; };
        """.ReplaceLineEndings("\n") + "\n";

    static string RepositoryRoot()
    {
        for (var dir = Path.GetFullPath(AppContext.BaseDirectory); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "vdiff.sln")))
            {
                return dir;
            }
        }

        throw new InvalidOperationException("vdiff.sln was not found above the test assembly");
    }

    /// <summary>Runs check.mjs on the text; a missing prerequisite fails the test, never skips.</summary>
    static (int Exit, string Stdout, string Stderr) Check(string mermaidText)
    {
        var root = RepositoryRoot();
        var dir = Path.Combine(Path.GetTempPath(), "vdiff-mmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "diagram.mmd");
            File.WriteAllBytes(file, new UTF8Encoding(false).GetBytes(mermaidText));

            ProcessResult result;
            try
            {
                result = BoundedProcess.Run(
                    "node",
                    [Path.Combine(root, "tools", "mermaid-check", "check.mjs"), file],
                    new ProcessLimits(TimeSpan.FromSeconds(150), 1024 * 1024, 64 * 1024));
            }
            catch (VdiffException e) when (e.Message.Contains("could not start", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"node could not be started ({e.Message}). Install node 22.13 or newer, then run `{InstallHint}`.", e);
            }

            if (result.ExitCode == PrerequisiteMissing)
            {
                Assert.Fail($"A prerequisite of the Mermaid check is missing: {result.StderrTail.Trim()} (fix: `{InstallHint}`, and install Chrome, Chromium or Edge).");
            }

            return (result.ExitCode, result.StdoutText, result.StderrTail);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    static string Summary(SyntheticRepo repo, string baseId, string headId)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = CliApp.Run(
            ["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", repo.OutDir], stdout, stderr);
        Assert.Equal("", stderr.ToString());
        Assert.Equal(0, exit);
        return File.ReadAllText(Path.Combine(repo.OutDir, "summary.mmd"));
    }

    /// <summary>A valid diagram of many generated classes, each with members.</summary>
    static string ManyClasses(int count, bool breakNearTheEnd)
    {
        var text = new StringBuilder("classDiagram\n");
        for (var i = 0; i < count; i++)
        {
            text.Append("  class Gen").Append(i).Append("[\"shop::Gen").Append(i).Append("\"]:::added {\n");
            text.Append("    +void member_number_one(int first, int second) [added]\n");
            text.Append("    +void member_number_two() [added]\n");
            if (breakNearTheEnd && i == count - 10)
            {
                // The block is never closed: one unbalanced brace.
                continue;
            }

            text.Append("  }\n");
        }

        text.Append(ClassDefs);
        return text.ToString();
    }

    /// <summary>The shape directories under tests/fixtures that hold a golden summary.</summary>
    public static TheoryData<string> GoldenSummaries()
    {
        var data = new TheoryData<string>();
        var fixtures = Path.Combine(RepositoryRoot(), "tests", "fixtures");
        if (Directory.Exists(fixtures))
        {
            foreach (var directory in Directory.EnumerateDirectories(fixtures)
                .OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal))
            {
                if (File.Exists(Path.Combine(directory, "expected", "summary.mmd")))
                {
                    data.Add(Path.GetFileName(directory));
                }
            }
        }

        return data;
    }

    // The proof that the provisional summary lines (renamed-from stereotype, labelled association, context
    // class) are real Mermaid: the pinned parser reads each golden. Local runs only in Phase 2; the pipeline
    // step that runs this category is advisory (RESEARCH Pitfall 12).
    [Theory]
    [MemberData(nameof(GoldenSummaries))]
    public void Golden_summary_passes_the_real_syntax_check(string shape)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "fixtures", shape, "expected", "summary.mmd"));

        Assert.Empty(MermaidLint.Check(text));
        var (exit, _, stderr) = Check(text);
        Assert.True(exit == 0, $"mermaid-check exited {exit} for the {shape} golden: {stderr}\n--- diagram ---\n{text}");
    }

    [Fact]
    public void Writer_output_for_a_header_of_hard_cases_passes_the_real_syntax_check()
    {
        using var repo = new SyntheticRepo();
        repo.Write("README.txt", "synthetic base\n");
        var baseId = repo.Commit("base");
        repo.Write(HardHeaderPath, HardHeader);
        var headId = repo.Commit("head");

        var text = Summary(repo, baseId, headId);

        Assert.Contains("shop::Cell", text);
        Assert.Empty(MermaidLint.Check(text));
        var (exit, _, stderr) = Check(text);
        Assert.True(exit == 0, $"mermaid-check exited {exit}: {stderr}\n--- diagram ---\n{text}");
    }

    [Fact]
    public void Writer_output_for_keyword_named_global_types_passes_the_real_syntax_check()
    {
        using var repo = new SyntheticRepo();
        repo.Write("README.txt", "synthetic base\n");
        var baseId = repo.Commit("base");
        repo.Write(KeywordHeaderPath, KeywordHeader);
        var headId = repo.Commit("head");

        var text = Summary(repo, baseId, headId);

        foreach (var name in new[] { "link", "note", "click", "callback", "href", "cssClass" })
        {
            Assert.Contains($"class C_{name}[\"{name}\"]:::added", text);
        }

        Assert.Contains("C_link <|-- C_linked", text);
        Assert.Empty(MermaidLint.Check(text));
        var (exit, _, stderr) = Check(text);
        Assert.True(exit == 0, $"mermaid-check exited {exit}: {stderr}\n--- diagram ---\n{text}");
    }

    [Fact]
    public void A_broken_diagram_with_an_unbalanced_brace_fails_the_check_and_is_not_a_missing_prerequisite()
    {
        var broken = "classDiagram\n  class A[\"shop::A\"]:::added {\n    +void f() [added]\n" + ClassDefs;

        var (exit, _, stderr) = Check(broken);

        Assert.NotEqual(0, exit);
        Assert.NotEqual(PrerequisiteMissing, exit);
        Assert.Contains("mermaid-check: FAILED", stderr);
    }

    [Fact]
    public void A_valid_diagram_over_fifty_thousand_characters_is_really_parsed_and_passes()
    {
        var text = ManyClasses(600, breakNearTheEnd: false);
        Assert.True(text.Length > 50_000, $"the generated diagram is only {text.Length} characters");

        var (exit, _, stderr) = Check(text);

        Assert.True(exit == 0, $"mermaid-check exited {exit}: {stderr}");
    }

    [Fact]
    public void A_broken_diagram_over_fifty_thousand_characters_fails_so_the_text_size_limit_was_raised()
    {
        var text = ManyClasses(600, breakNearTheEnd: true);
        Assert.True(text.Length > 50_000, $"the generated diagram is only {text.Length} characters");

        var (exit, _, stderr) = Check(text);

        Assert.NotEqual(0, exit);
        Assert.NotEqual(PrerequisiteMissing, exit);
        Assert.Contains("mermaid-check: FAILED", stderr);
    }

    [Fact]
    public void A_zero_change_diagram_of_classDiagram_and_three_classDef_lines_passes()
    {
        using var repo = new SyntheticRepo();
        repo.Write(HardHeaderPath, HardHeader);
        var headId = repo.Commit("only");

        var text = Summary(repo, headId, headId);

        Assert.Equal("classDiagram\n" + ClassDefs, text);
        var (exit, _, stderr) = Check(text);
        Assert.True(exit == 0, $"mermaid-check exited {exit}: {stderr}");
    }
}
