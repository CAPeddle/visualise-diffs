using System.Text;
using System.Text.Json;
using Vdiff.Core.Model;
using Vdiff.Core.Output;
using Vdiff.Tests.Contract;
using Vdiff.Tests.Output;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Fixtures;

/// <summary>
/// Self-checks of the committed goldens (D-06, D-07, D-08, R-23). A golden is written by hand from the
/// fixture's intent, so these tests are what keep it a valid, canonical document today: it must validate
/// against the frozen v1 schema, keep the presence rules, and equal what the writer would emit for the same
/// content, byte for byte. They never run the tool, so they hold while every output is still pending.
/// </summary>
[Trait("Category", "Fixtures")]
public class FixtureGoldenTests
{
    static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    static string FixturesRoot() => Path.Combine(RepoRoot.Find(), "tests", "fixtures");

    /// <summary>The name of every directory under tests/fixtures, in ordinal order.</summary>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        var root = Path.Combine(RepoRoot.Find(), "tests", "fixtures");
        if (Directory.Exists(root))
        {
            foreach (var name in Directory.EnumerateDirectories(root)
                .Select(d => Path.GetFileName(d))
                .OrderBy(n => n, StringComparer.Ordinal))
            {
                data.Add(name);
            }
        }

        return data;
    }

    static string Golden(string shape, string fileName) =>
        Path.Combine(FixturesRoot(), shape, "expected", fileName);

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_file_loads(string shape)
    {
        var loaded = FixtureLoader.Load(Path.Combine(FixturesRoot(), shape), FixtureKind.Synthetic);

        Assert.Equal(shape, loaded.Shape);
        Assert.Equal(2, loaded.Outputs.Count);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Golden_diff_validates_round_trips_and_keeps_the_presence_rules(string shape)
    {
        var path = Golden(shape, OutputWriter.DiffJsonFile);
        Assert.True(File.Exists(path), $"{shape}: expected/diff.json is missing");
        var bytes = File.ReadAllBytes(path);
        var text = new UTF8Encoding(false, true).GetString(bytes);

        var schemaErrors = SchemaSupport.Errors(SchemaSupport.DiffSchemaFile, text);
        Assert.True(schemaErrors.Count == 0, $"{shape}: the golden diff breaks the frozen schema:\n" + string.Join('\n', schemaErrors));

        var diff = JsonSerializer.Deserialize<DiffFile>(text, ReadOptions);
        Assert.NotNull(diff);

        var presence = DiffPresence.Problems(diff);
        Assert.True(presence.Count == 0, $"{shape}: the golden diff breaks a presence rule:\n" + string.Join('\n', presence));

        // The writer is the one place that decides key order, canonical order, indentation and the
        // trailing line feed; a golden that differs from its own re-serialisation can never be matched.
        var canonical = OutputWriter.Serialize(diff);
        Assert.True(
            string.Equals(canonical, text, StringComparison.Ordinal),
            $"{shape}: the golden diff is not what OutputWriter.Serialize writes for the same content; "
            + "check key order, canonical order of lists, indentation and the final line feed. "
            + "First difference at character " + FirstDifference(canonical, text));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Golden_summary_is_well_formed(string shape)
    {
        var path = Golden(shape, OutputWriter.MermaidFile);
        Assert.True(File.Exists(path), $"{shape}: expected/summary.mmd is missing");
        var bytes = File.ReadAllBytes(path);

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, $"{shape}: the golden summary starts with a byte-order mark");
        Assert.DoesNotContain((byte)'\r', bytes);

        var text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.True(text.EndsWith('\n') && !text.EndsWith("\n\n", StringComparison.Ordinal), $"{shape}: the golden summary must end with exactly one line feed");

        var problems = MermaidLint.Check(text);
        Assert.True(problems.Count == 0, $"{shape}: the golden summary fails the lint:\n" + string.Join('\n', problems));
    }

    [Fact]
    public void The_rename_fixture_moves_its_header_as_a_git_rename_pair()
    {
        var rename = Path.Combine(FixturesRoot(), "rename");
        Assert.True(Directory.Exists(rename), "tests/fixtures/rename is missing");
        var loaded = FixtureLoader.Load(rename, FixtureKind.Synthetic);

        var (repo, baseId, headId) = FixtureHarness.BuildSyntheticRepo(loaded);
        using (repo)
        {
            // same_file_or_rename_pair is true by construction only if git itself pairs the two files.
            var listing = repo.Git("diff", "--find-renames", "--name-status", baseId, headId);
            var lines = listing.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Contains(
                lines,
                l => l.StartsWith('R')
                    && l.Contains("include/synthetic/circle.h", StringComparison.Ordinal)
                    && l.Contains("include/synthetic/disc.h", StringComparison.Ordinal));
        }
    }

    static int FirstDifference(string a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return n;
    }
}
