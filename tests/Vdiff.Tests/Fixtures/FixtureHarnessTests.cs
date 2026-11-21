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
}
