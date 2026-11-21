using System.Text;
using Vdiff.Core.Output;
using Vdiff.Tests.Contract;
using Xunit.Abstractions;

namespace Vdiff.Tests.Fixtures;

/// <summary>
/// Self-tests of the golden-fixture harness on temporary fixtures (D-05, D-08, D-10, D-18). The committed
/// fixtures arrive in later plans; here every branch of the harness is driven by a real CLI run on a
/// repository built from a temporary fixture. The class runs the real CLI, so it joins the sidecar
/// collection.
/// </summary>
[Collection("sidecar")]
[Trait("Category", "Fixtures")]
public class FixtureHarnessTests(ITestOutputHelper output)
{
    // Written as concatenations, never as a multi-line literal: a literal takes the line endings of the
    // source file, and these bytes are compared with a golden byte for byte.
    const string GreenYaml =
        "shape: small-cpp\n" +
        "description: identical trees with no C++ source\n" +
        "expected:\n" +
        "  diff:\n" +
        "    state: green\n" +
        "  summary:\n" +
        "    state: green\n";

    const string EmptySummary =
        "classDiagram\n" +
        "  classDef added fill:#d4f8d4,stroke:#2e7d32,color:#111\n" +
        "  classDef removed fill:#fde0e0,stroke:#c62828,color:#111,stroke-dasharray:4 3\n" +
        "  classDef modified fill:#fff4cc,stroke:#b58900,color:#111\n";

    static readonly string EmptyDiffText = OutputWriter.Serialize(ContractSamples.EmptyDiff());

    static Dictionary<string, string> Notes() => new() { ["notes.txt"] = "synthetic notes\n" };

    static Dictionary<string, string> EmptyGoldens() => new()
    {
        [OutputWriter.DiffJsonFile] = EmptyDiffText,
        [OutputWriter.MermaidFile] = EmptySummary,
    };

    /// <summary>Writes one fixture directory under <paramref name="root"/> and returns its path.</summary>
    static string WriteFixture(
        string root,
        string directoryName,
        string yaml,
        IReadOnlyDictionary<string, string>? baseFiles,
        IReadOnlyDictionary<string, string>? headFiles,
        IReadOnlyDictionary<string, string>? expectedFiles)
    {
        var dir = Path.Combine(root, directoryName);
        Directory.CreateDirectory(dir);
        WriteText(Path.Combine(dir, "fixture.yml"), yaml);
        foreach (var (sub, files) in new[] { ("base", baseFiles), ("head", headFiles), ("expected", expectedFiles) })
        {
            if (files is null)
            {
                continue;
            }

            Directory.CreateDirectory(Path.Combine(dir, sub));
            foreach (var (relative, text) in files)
            {
                var path = Path.Combine(dir, sub, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                WriteText(path, text);
            }
        }

        return dir;
    }

    static void WriteText(string path, string text) => File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));

    /// <summary>A fresh temporary fixtures root, removed on dispose. It never holds a directory link.</summary>
    sealed class TempFixtures : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "vdiff-fx-" + Guid.NewGuid().ToString("N"));

        public TempFixtures() => Directory.CreateDirectory(Root);

        public string Add(
            string directoryName,
            string yaml,
            IReadOnlyDictionary<string, string>? baseFiles,
            IReadOnlyDictionary<string, string>? headFiles,
            IReadOnlyDictionary<string, string>? expectedFiles) =>
            WriteFixture(Root, directoryName, yaml, baseFiles, headFiles, expectedFiles);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    [Fact]
    public void A_valid_fixture_loads_strictly_with_its_outputs_and_both_trees()
    {
        using var fixtures = new TempFixtures();
        var dir = fixtures.Add("small-cpp", GreenYaml, Notes(), Notes(), EmptyGoldens());

        var loaded = FixtureLoader.Load(dir, FixtureKind.Synthetic);

        Assert.Equal("small-cpp", loaded.Shape);
        Assert.Equal(FixtureKind.Synthetic, loaded.Kind);
        Assert.Equal(
            [("diff", "diff.json", "green", (int?)null), ("summary", "summary.mmd", "green", (int?)null)],
            loaded.Outputs.Select(o => (o.Name, o.FileName, o.State, o.Due)));
        Assert.Equal(["notes.txt"], loaded.BaseTree.Select(t => t.RelativePath));
        Assert.Equal(["notes.txt"], loaded.HeadTree.Select(t => t.RelativePath));
    }

    [Fact]
    public void A_green_fixture_with_identical_trees_passes_green_end_to_end()
    {
        using var fixtures = new TempFixtures();
        var dir = fixtures.Add("small-cpp", GreenYaml, Notes(), Notes(), EmptyGoldens());
        var loaded = FixtureLoader.Load(dir, FixtureKind.Synthetic);

        var result = FixtureHarness.RunSynthetic(loaded, accept: false);

        Assert.Equal(2, result.Outcomes.Count);
        foreach (var outcome in result.Outcomes)
        {
            output.WriteLine($"{outcome.Row.Output}: {outcome.Row.Status} ({outcome.Row.Detail})");
            Assert.True(outcome.Passed, $"{outcome.Row.Output}: {outcome.Row.Status} {outcome.Row.Detail}");
            Assert.Equal("GREEN", outcome.Row.Status);
            Assert.Null(outcome.MismatchDiff);
        }

        Assert.Empty(result.Accepted);
    }

    // ---- fixtures whose trees really differ ----

    static Dictionary<string, string> BaseHeader() => new()
    {
        ["include/a.h"] = "namespace synthetic { struct Alpha { int x; }; }\n",
    };

    static Dictionary<string, string> HeadHeader() => new()
    {
        ["include/a.h"] = "namespace synthetic { struct Alpha { int x; int y; }; }\n",
    };

    /// <summary>A fixture.yml with a baseline that loads, and one knob per mutation a test needs.</summary>
    static string Yaml(
        string shape = "small-cpp",
        string description = "one struct gains a field",
        string top = "",
        string config = "",
        string diffState = "pending",
        string summaryState = "pending",
        string due = "    due: 5\n") =>
        "shape: " + shape + "\n" +
        "description: " + description + "\n" +
        top +
        config +
        "expected:\n" +
        "  diff:\n" +
        "    state: " + diffState + "\n" +
        due +
        "  summary:\n" +
        "    state: " + summaryState + "\n" +
        due;

    IReadOnlyList<FixtureOutcome> Run(string yaml, Dictionary<string, string> baseFiles, Dictionary<string, string> headFiles, Dictionary<string, string> goldens)
    {
        using var fixtures = new TempFixtures();
        var dir = fixtures.Add("small-cpp", yaml, baseFiles, headFiles, goldens);
        var outcomes = FixtureHarness.RunSynthetic(FixtureLoader.Load(dir, FixtureKind.Synthetic), accept: false).Outcomes;
        foreach (var outcome in outcomes)
        {
            output.WriteLine($"{outcome.Row.Output}: {outcome.Row.Status} ({outcome.Row.Detail})");
        }

        return outcomes;
    }

    [Fact]
    public void Pending_outputs_that_differ_from_their_goldens_are_RED_and_carry_a_unified_diff()
    {
        var outcomes = Run(Yaml(), BaseHeader(), HeadHeader(), EmptyGoldens());

        Assert.Equal(2, outcomes.Count);
        foreach (var outcome in outcomes)
        {
            Assert.True(outcome.Passed);
            Assert.Equal("RED", outcome.Row.Status);
            Assert.Equal("pending, due phase 5", outcome.Row.Detail);
            Assert.Contains("@@", outcome.MismatchDiff);
        }
    }

    [Fact]
    public void The_same_fixture_marked_green_is_FAIL_with_a_unified_diff()
    {
        var outcomes = Run(Yaml(diffState: "green", summaryState: "green"), BaseHeader(), HeadHeader(), EmptyGoldens());

        Assert.Equal(2, outcomes.Count);
        foreach (var outcome in outcomes)
        {
            Assert.False(outcome.Passed);
            Assert.Equal("FAIL", outcome.Row.Status);
            Assert.Contains("@@", outcome.MismatchDiff);
        }
    }

    [Fact]
    public void A_green_fixture_marked_pending_is_FAIL_because_it_unexpectedly_matches()
    {
        var outcomes = Run(Yaml(diffState: "pending", summaryState: "pending"), Notes(), Notes(), EmptyGoldens());

        Assert.Equal(2, outcomes.Count);
        foreach (var outcome in outcomes)
        {
            Assert.False(outcome.Passed);
            Assert.Equal("FAIL", outcome.Row.Status);
            Assert.StartsWith("unexpectedly matches", outcome.Row.Detail);
            Assert.Null(outcome.MismatchDiff);
        }
    }

    [Fact]
    public void A_failed_tool_run_is_FAIL_and_never_RED()
    {
        var config = "config:\n  blank:\n    - not-an-identifier\n";
        var outcomes = Run(Yaml(config: config), BaseHeader(), HeadHeader(), EmptyGoldens());

        Assert.Equal(2, outcomes.Count);
        foreach (var outcome in outcomes)
        {
            Assert.False(outcome.Passed);
            Assert.Equal("FAIL", outcome.Row.Status);
            Assert.Equal("the tool run failed", outcome.Row.Detail);
            Assert.Null(outcome.MismatchDiff);
        }
    }

    [Fact]
    public void A_missing_golden_is_FAIL_while_the_other_output_is_still_judged()
    {
        var goldens = new Dictionary<string, string> { [OutputWriter.DiffJsonFile] = EmptyDiffText };

        var outcomes = Run(Yaml(), BaseHeader(), HeadHeader(), goldens);

        Assert.Equal("RED", outcomes[0].Row.Status);
        Assert.True(outcomes[0].Passed);
        Assert.Equal("FAIL", outcomes[1].Row.Status);
        Assert.False(outcomes[1].Passed);
        Assert.Equal("the expected file is missing", outcomes[1].Row.Detail);
        Assert.Null(outcomes[1].MismatchDiff);
    }

    [Fact]
    public void A_missing_output_is_FAIL_in_every_state()
    {
        var run = new FixtureRun(0, "", "", Path.Combine(Path.GetTempPath(), "vdiff-fx-absent-" + Guid.NewGuid().ToString("N")));
        foreach (var state in new[] { "pending", "green" })
        {
            using var fixtures = new TempFixtures();
            var dir = fixtures.Add("small-cpp", Yaml(diffState: state, summaryState: state), Notes(), Notes(), EmptyGoldens());

            var outcomes = FixtureHarness.Evaluate(FixtureLoader.Load(dir, FixtureKind.Synthetic), run);

            Assert.All(outcomes, o =>
            {
                Assert.False(o.Passed);
                Assert.Equal("FAIL", o.Row.Status);
                Assert.Equal("the tool wrote no such output", o.Row.Detail);
            });
        }
    }

    [Fact]
    public void Any_text_on_stderr_is_FAIL_even_with_exit_code_zero()
    {
        using var fixtures = new TempFixtures();
        var dir = fixtures.Add("small-cpp", Yaml(), Notes(), Notes(), EmptyGoldens());
        var outDir = Path.Combine(fixtures.Root, "written");
        Directory.CreateDirectory(outDir);
        WriteText(Path.Combine(outDir, OutputWriter.DiffJsonFile), EmptyDiffText);
        WriteText(Path.Combine(outDir, OutputWriter.MermaidFile), EmptySummary);

        var outcomes = FixtureHarness.Evaluate(FixtureLoader.Load(dir, FixtureKind.Synthetic), new FixtureRun(0, "", "a warning\n", outDir));

        Assert.All(outcomes, o =>
        {
            Assert.False(o.Passed);
            Assert.Equal("the tool run failed", o.Row.Detail);
        });
    }

    // ---- the schema gate ----

    static void WriteSampleOutputs(string outDir, string? runMetaOverride = null)
    {
        Directory.CreateDirectory(outDir);
        WriteText(Path.Combine(outDir, OutputWriter.BaseModelFile), OutputWriter.Serialize(ContractSamples.FullModel()));
        WriteText(Path.Combine(outDir, OutputWriter.HeadModelFile), OutputWriter.Serialize(ContractSamples.FullModel()));
        WriteText(Path.Combine(outDir, OutputWriter.DiffJsonFile), OutputWriter.Serialize(ContractSamples.FullDiff()));
        WriteText(Path.Combine(outDir, OutputWriter.MetaFile), runMetaOverride ?? OutputWriter.Serialize(ContractSamples.FullMeta()));
    }

    [Fact]
    public void The_schema_gate_accepts_the_sample_outputs()
    {
        using var fixtures = new TempFixtures();
        var outDir = Path.Combine(fixtures.Root, "outputs");

        WriteSampleOutputs(outDir);

        Assert.True(FixtureHarness.SchemaGate(outDir));
    }

    [Fact]
    public void The_schema_gate_rejects_a_run_meta_with_another_schema_name()
    {
        using var fixtures = new TempFixtures();
        var outDir = Path.Combine(fixtures.Root, "outputs");
        var meta = OutputWriter.Serialize(ContractSamples.FullMeta());
        Assert.Contains("vdiff-run-meta/v1", meta);

        WriteSampleOutputs(outDir, meta.Replace("vdiff-run-meta/v1", "vdiff-run-meta/v2", StringComparison.Ordinal));

        Assert.False(FixtureHarness.SchemaGate(outDir));
    }

    [Fact]
    public void The_schema_gate_rejects_a_directory_with_a_missing_output()
    {
        using var fixtures = new TempFixtures();
        var outDir = Path.Combine(fixtures.Root, "outputs");
        WriteSampleOutputs(outDir);
        File.Delete(Path.Combine(outDir, OutputWriter.HeadModelFile));

        Assert.False(FixtureHarness.SchemaGate(outDir));
    }

    // ---- UnifiedDiff ----

    static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

    static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void A_unified_diff_of_equal_files_is_empty()
    {
        Assert.Equal("", FixtureHarness.UnifiedDiff(Bytes("a\n"), Bytes("a\n"), "diff.json"));
    }

    [Fact]
    public void A_unified_diff_shows_removed_and_added_lines_and_no_absolute_path()
    {
        var diff = FixtureHarness.UnifiedDiff(Bytes("a\n"), Bytes("b\n"), "diff.json");

        var lines = Lines(diff);
        Assert.Contains("-a", lines);
        Assert.Contains("+b", lines);
        Assert.Contains(lines, l => l.StartsWith("@@", StringComparison.Ordinal));
        Assert.DoesNotContain(Path.GetTempPath().TrimEnd('\\', '/'), diff, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_long_unified_diff_is_cut_to_200_lines_and_a_final_count()
    {
        var expected = string.Concat(Enumerable.Range(0, 300).Select(i => "a" + i + "\n"));
        var actual = string.Concat(Enumerable.Range(0, 300).Select(i => "b" + i + "\n"));

        var diff = FixtureHarness.UnifiedDiff(Bytes(expected), Bytes(actual), "summary.mmd");

        var lines = diff.TrimEnd('\n').Split('\n');
        Assert.Equal(201, lines.Length);
        Assert.Matches(@"^\.\.\. \([1-9][0-9]* more lines\)$", lines[^1]);
    }

    // ---- loader negatives: one clean baseline, one mutation per row ----

    const string NotValid = "fixture.yml is not valid for the fixture format";
    const string ShapeMessage = "shape must equal the directory name and be one of the five shape names";

    public static TheoryData<string, string, string> LoaderNegatives => new()
    {
        { Yaml(diffState: "green", summaryState: "green", top: "bogus: 1\n"), "small-cpp", NotValid },
        { Yaml(diffState: "green", summaryState: "green", config: "config:\n  blnak:\n    - EXPORT_API\n"), "small-cpp", NotValid },
        { Yaml(shape: "rename", diffState: "green", summaryState: "green"), "small-cpp", ShapeMessage },
        { Yaml(shape: "not-a-shape", diffState: "green", summaryState: "green"), "not-a-shape", ShapeMessage },
        { Yaml(diffState: "due", summaryState: "green"), "small-cpp", "state must be pending or green" },
        { Yaml(diffState: "Pending", summaryState: "green"), "small-cpp", "state must be pending or green" },
        { Yaml(diffState: "green", summaryState: "green", top: "repo: /tmp/x\n"), "small-cpp", "a synthetic fixture must not have repo, base or head keys" },
        { Yaml(diffState: "green", summaryState: "green", top: "base: 0000000000000000000000000000000000000000\n"), "small-cpp", "a synthetic fixture must not have repo, base or head keys" },
        { Yaml(description: "''", diffState: "green", summaryState: "green"), "small-cpp", "description must be 1 to 300 characters" },
        { Yaml(description: new string('d', 301), diffState: "green", summaryState: "green"), "small-cpp", "description must be 1 to 300 characters" },
        { Yaml(diffState: "pending", summaryState: "pending", due: "    due: 1\n"), "small-cpp", "due must be a phase number from 2 to 9" },
        { Yaml(diffState: "pending", summaryState: "pending", due: "    due: 10\n"), "small-cpp", "due must be a phase number from 2 to 9" },
        { "shape: small-cpp\ndescription: no expected\n", "small-cpp", "expected must give diff and summary" },
        { "shape: small-cpp\ndescription: no summary\nexpected:\n  diff:\n    state: green\n", "small-cpp", "expected must give diff and summary" },
        { "", "small-cpp", "fixture.yml is empty" },
    };

    [Theory]
    [MemberData(nameof(LoaderNegatives))]
    public void A_synthetic_fixture_with_one_defect_is_refused_with_its_own_message(string yaml, string directoryName, string expectedMessage)
    {
        using var fixtures = new TempFixtures();
        var dir = fixtures.Add(directoryName, yaml, Notes(), Notes(), EmptyGoldens());

        var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Synthetic));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void A_missing_fixture_file_is_refused()
    {
        using var fixtures = new TempFixtures();
        var dir = fixtures.Add("small-cpp", GreenYaml, Notes(), Notes(), EmptyGoldens());
        File.Delete(Path.Combine(dir, "fixture.yml"));

        var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Synthetic));

        Assert.Equal("fixture.yml is missing", exception.Message);
    }

    [Fact]
    public void A_synthetic_fixture_without_a_head_directory_is_refused()
    {
        using var fixtures = new TempFixtures();
        var dir = fixtures.Add("small-cpp", GreenYaml, Notes(), null, EmptyGoldens());

        var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Synthetic));

        Assert.Equal("a synthetic fixture needs base and head directories", exception.Message);
    }

    [Fact]
    public void A_fixture_file_of_65537_bytes_is_refused_and_one_of_65536_is_not()
    {
        var prefix = GreenYaml + "# ";
        foreach (var (size, refused) in new[] { (65537, true), (65536, false) })
        {
            using var fixtures = new TempFixtures();
            var yaml = prefix + new string('x', size - prefix.Length - 1) + "\n";
            Assert.Equal(size, Bytes(yaml).Length);
            var dir = fixtures.Add("small-cpp", yaml, Notes(), Notes(), EmptyGoldens());

            if (refused)
            {
                var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Synthetic));
                Assert.Equal("fixture.yml is larger than 64 KiB", exception.Message);
            }
            else
            {
                Assert.Equal("small-cpp", FixtureLoader.Load(dir, FixtureKind.Synthetic).Shape);
            }
        }
    }

    [Theory]
    [InlineData("head", "obj/x.h")]
    [InlineData("base", "bin/x.h")]
    [InlineData("head", "src/target/x.h")]
    [InlineData("base", "out/x.h")]
    [InlineData("head", "node_modules/x.h")]
    public void A_tree_with_a_build_output_directory_name_is_refused(string side, string relative)
    {
        using var fixtures = new TempFixtures();
        var baseFiles = Notes();
        var headFiles = Notes();
        (side == "head" ? headFiles : baseFiles)[relative] = "struct X {};\n";
        var dir = fixtures.Add("small-cpp", GreenYaml, baseFiles, headFiles, EmptyGoldens());

        var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Synthetic));

        Assert.Equal("a fixture tree must not contain a build output directory name", exception.Message);
        Assert.StartsWith(side + "/", exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tree_with_more_than_200_files_or_a_file_over_256_KiB_is_refused()
    {
        using (var fixtures = new TempFixtures())
        {
            var many = Enumerable.Range(0, 201).ToDictionary(i => "f" + i + ".txt", _ => "x\n");
            var dir = fixtures.Add("small-cpp", GreenYaml, many, Notes(), EmptyGoldens());

            var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Synthetic));

            Assert.Equal("a fixture tree has more than 200 files", exception.Message);
        }

        using (var fixtures = new TempFixtures())
        {
            var large = new Dictionary<string, string> { ["big.txt"] = new string('x', 256 * 1024 + 1) };
            var dir = fixtures.Add("small-cpp", GreenYaml, large, Notes(), EmptyGoldens());

            var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Synthetic));

            Assert.Equal("a fixture tree file is larger than 256 KiB", exception.Message);
        }
    }

    [Fact]
    public void A_directory_link_inside_a_tree_is_refused_and_not_followed()
    {
        using var scratch = new Support.LinkScratch();
        var elsewhere = Path.Combine(scratch.Root, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        WriteText(Path.Combine(elsewhere, "secret.txt"), "not part of the fixture\n");
        var dir = WriteFixture(scratch.Root, "small-cpp", GreenYaml, Notes(), Notes(), EmptyGoldens());
        scratch.Link(Path.Combine("small-cpp", "base", "linked"), elsewhere);

        var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Synthetic));

        Assert.Equal("a fixture tree must not contain a link", exception.Message);
        Assert.Equal("base/linked", exception.Detail);
        Assert.True(File.Exists(Path.Combine(elsewhere, "secret.txt")));
    }

    // ---- private fixtures: fixed messages, never a detail (D-13) ----

    static string PrivateYaml(string repo, string baseId, string headId, string top = "") =>
        "shape: small-cpp\n" +
        "description: a private run\n" +
        "repo: '" + repo + "'\n" +
        "base: " + baseId + "\n" +
        "head: " + headId + "\n" +
        top +
        "expected:\n" +
        "  diff:\n" +
        "    state: pending\n" +
        "  summary:\n" +
        "    state: pending\n";

    const string FortyHex = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void A_private_fixture_loads_with_empty_trees()
    {
        using var fixtures = new TempFixtures();
        var repoDir = Path.Combine(fixtures.Root, "clone");
        Directory.CreateDirectory(repoDir);
        var dir = fixtures.Add("small-cpp", PrivateYaml(repoDir, FortyHex, FortyHex + "0123456789abcdef01234567"), null, null, null);

        var loaded = FixtureLoader.Load(dir, FixtureKind.Private);

        Assert.Equal(FixtureKind.Private, loaded.Kind);
        Assert.Empty(loaded.BaseTree);
        Assert.Empty(loaded.HeadTree);
        Assert.Equal(2, loaded.Outputs.Count);
    }

    [Fact]
    public void A_private_fixture_with_a_base_directory_is_refused_with_no_detail()
    {
        using var fixtures = new TempFixtures();
        var dir = fixtures.Add("small-cpp", PrivateYaml(fixtures.Root, FortyHex, FortyHex), Notes(), null, null);

        var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Private));

        Assert.Equal("a private fixture carries no source trees", exception.Message);
        Assert.Null(exception.Detail);
    }

    [Fact]
    public void A_private_fixture_with_a_short_commit_id_is_refused_with_no_detail()
    {
        using var fixtures = new TempFixtures();
        var dir = fixtures.Add("small-cpp", PrivateYaml(fixtures.Root, "0123456789ab", FortyHex), null, null, null);

        var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Private));

        Assert.Equal("a private fixture needs base and head as full commit ids", exception.Message);
        Assert.Null(exception.Detail);
    }

    [Fact]
    public void A_private_fixture_with_a_relative_or_missing_repo_is_refused_with_no_detail()
    {
        foreach (var repo in new[] { "clone", Path.Combine(Path.GetTempPath(), "vdiff-fx-absent-" + Guid.NewGuid().ToString("N")) })
        {
            using var fixtures = new TempFixtures();
            var dir = fixtures.Add("small-cpp", PrivateYaml(repo, FortyHex, FortyHex), null, null, null);

            var exception = Assert.Throws<FixtureException>(() => FixtureLoader.Load(dir, FixtureKind.Private));

            Assert.Equal("a private fixture needs repo: an absolute path to an existing directory", exception.Message);
            Assert.Null(exception.Detail);
        }
    }

    [Fact]
    public void A_private_fixture_with_an_unknown_key_is_refused_with_no_detail_but_a_synthetic_one_has_it()
    {
        using var fixtures = new TempFixtures();
        var privateDir = fixtures.Add("small-cpp", PrivateYaml(fixtures.Root, FortyHex, FortyHex, top: "bogus: 1\n"), null, null, null);
        var syntheticDir = fixtures.Add("rename", Yaml(shape: "rename", diffState: "green", summaryState: "green", top: "bogus: 1\n"), Notes(), Notes(), EmptyGoldens());

        var privateException = Assert.Throws<FixtureException>(() => FixtureLoader.Load(privateDir, FixtureKind.Private));
        var syntheticException = Assert.Throws<FixtureException>(() => FixtureLoader.Load(syntheticDir, FixtureKind.Synthetic));

        Assert.Equal(NotValid, privateException.Message);
        Assert.Null(privateException.Detail);
        Assert.Contains("bogus", syntheticException.Detail);
    }

    // ---- RunAll and the report ----

    static string TempReportPath() =>
        Path.Combine(Path.GetTempPath(), "vdiff-fxr-" + Guid.NewGuid().ToString("N"), "fixture-report.md");

    [Fact]
    public void RunAll_judges_every_fixture_logs_each_mismatch_and_writes_the_report()
    {
        using var fixtures = new TempFixtures();
        fixtures.Add("small-cpp", GreenYaml, Notes(), Notes(), EmptyGoldens());
        fixtures.Add("rename", Yaml(shape: "rename"), BaseHeader(), HeadHeader(), EmptyGoldens());
        var reportPath = TempReportPath();
        var log = new List<string>();
        try
        {
            var outcomes = FixtureHarness.RunAll(
                fixtures.Root,
                log.Add,
                name => name == FixtureReport.Variable ? reportPath : null);

            Assert.Equal(4, outcomes.Count);
            Assert.All(outcomes, o => Assert.True(o.Passed, $"{o.Row.Fixture}/{o.Row.Output}: {o.Row.Status} {o.Row.Detail}"));
            Assert.Equal(
                ["rename:diff:RED", "rename:summary:RED", "small-cpp:diff:GREEN", "small-cpp:summary:GREEN"],
                outcomes.Select(o => $"{o.Row.Fixture}:{o.Row.Output}:{o.Row.Status}").Order(StringComparer.Ordinal));

            var report = File.ReadAllText(reportPath);
            Assert.Equal(6, report.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.DoesNotContain('\r', report);

            var text = string.Join('\n', log);
            Assert.Contains("--- rename/diff (RED) ---", text);
            Assert.Contains("--- rename/summary (RED) ---", text);
            Assert.DoesNotContain("--- small-cpp/", text);
            Assert.Equal(2, log.Count(l => l.StartsWith("--- ", StringComparison.Ordinal)));
            Assert.Contains("@@", text);
        }
        finally
        {
            DeleteDirectory(Path.GetDirectoryName(reportPath)!);
        }
    }

    [Fact]
    public void RunAll_writes_no_report_when_the_variable_is_not_set()
    {
        using var fixtures = new TempFixtures();
        fixtures.Add("small-cpp", GreenYaml, Notes(), Notes(), EmptyGoldens());
        var reportPath = TempReportPath();

        var outcomes = FixtureHarness.RunAll(fixtures.Root, _ => { }, _ => null);

        Assert.Equal(2, outcomes.Count);
        Assert.False(File.Exists(reportPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(reportPath)));
    }

    [Fact]
    public void RunAll_reports_a_directory_that_is_not_a_shape_as_one_FAIL_row()
    {
        using var fixtures = new TempFixtures();
        Directory.CreateDirectory(Path.Combine(fixtures.Root, "stray"));

        var outcomes = FixtureHarness.RunAll(fixtures.Root, _ => { }, _ => null);

        var only = Assert.Single(outcomes);
        Assert.False(only.Passed);
        Assert.Equal("FAIL", only.Row.Status);
        Assert.Equal("-", only.Row.Output);
        Assert.Equal("not one of the five shape names", only.Row.Detail);
    }

    [Fact]
    public void RunAll_reports_a_fixture_that_cannot_be_loaded_as_a_FAIL_row_per_output()
    {
        using var fixtures = new TempFixtures();
        fixtures.Add("small-cpp", Yaml(diffState: "green", summaryState: "green", top: "bogus: 1\n"), Notes(), Notes(), EmptyGoldens());
        var log = new List<string>();

        var outcomes = FixtureHarness.RunAll(fixtures.Root, log.Add, _ => null);

        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, o =>
        {
            Assert.False(o.Passed);
            Assert.Equal("FAIL", o.Row.Status);
            Assert.Equal(NotValid, o.Row.Detail);
        });
        Assert.Contains(log, l => l.Contains("bogus", StringComparison.Ordinal));
    }

    [Fact]
    public void RunAll_logs_the_exit_code_and_stderr_of_a_failed_run()
    {
        using var fixtures = new TempFixtures();
        fixtures.Add("small-cpp", Yaml(config: "config:\n  blank:\n    - not-an-identifier\n"), BaseHeader(), HeadHeader(), EmptyGoldens());
        var log = new List<string>();

        var outcomes = FixtureHarness.RunAll(fixtures.Root, log.Add, _ => null);

        Assert.All(outcomes, o => Assert.Equal("FAIL", o.Row.Status));
        Assert.Contains(log, l => l.Contains("exit code 1", StringComparison.Ordinal));
        Assert.Contains(log, l => l.Contains("--blank", StringComparison.Ordinal));
    }

    [Fact]
    public void RunAll_fails_when_the_fixtures_root_does_not_exist_and_never_skips()
    {
        var absent = Path.Combine(Path.GetTempPath(), "vdiff-fx-absent-" + Guid.NewGuid().ToString("N"));

        var exception = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FixtureHarness.RunAll(absent, _ => { }, _ => null));

        Assert.Contains("fixtures root not found", exception.Message);
    }

    static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
