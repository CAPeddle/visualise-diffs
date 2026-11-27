using System.Text;
using System.Text.RegularExpressions;
using Vdiff.Core.Output;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Vdiff.Tests.Fixtures;

/// <summary>
/// Loads one fixture directory (D-05, D-13, D-18). The YAML is read with YamlDotNet's default deserialiser,
/// which rejects a key the classes do not have, so a typo such as <c>blnak</c> fails the load instead of
/// running a fixture without its blanking. Every <see cref="FixtureException"/> message is a constant; a
/// synthetic failure may carry the offending relative path or the YAML error in
/// <see cref="FixtureException.Detail"/>, a private failure never does (D-13).
/// </summary>
public static partial class FixtureLoader
{
    public const int MaxFixtureFileBytes = 65536;
    public const int MaxDescriptionLength = 300;
    public const int MaxTreeFileBytes = 256 * 1024;
    public const int MaxTreeFiles = 200;

    const string FixtureFileName = "fixture.yml";

    static readonly string[] BuildOutputSegments = ["bin", "obj", "target", "out", "node_modules"];

    static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    [GeneratedRegex("^[0-9a-f]{40}([0-9a-f]{24})?$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitIdPattern();

    public static LoadedFixture Load(string fixtureDir, FixtureKind kind)
    {
        var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(fixtureDir)));
        string? Detail(string text) => kind == FixtureKind.Synthetic ? text : null;

        var yamlPath = Path.Combine(fixtureDir, FixtureFileName);
        var yamlInfo = new FileInfo(yamlPath);
        if (!yamlInfo.Exists || IsLink(yamlInfo))
        {
            throw new FixtureException("fixture.yml is missing", null);
        }

        if (yamlInfo.Length > MaxFixtureFileBytes)
        {
            throw new FixtureException("fixture.yml is larger than 64 KiB", null);
        }

        var bytes = File.ReadAllBytes(yamlPath);
        FixtureFile? file;
        try
        {
            file = Yaml.Deserialize<FixtureFile?>(new UTF8Encoding(false, true).GetString(bytes));
        }
        catch (Exception e) when (e is YamlException or DecoderFallbackException or InvalidOperationException)
        {
            throw new FixtureException("fixture.yml is not valid for the fixture format", Detail(e.Message));
        }

        if (file is null)
        {
            throw new FixtureException("fixture.yml is empty", null);
        }

        if (!string.Equals(file.Shape, directoryName, StringComparison.Ordinal) || !Shapes.IsShape(file.Shape))
        {
            throw new FixtureException("shape must equal the directory name and be one of the five shape names", Detail(directoryName));
        }

        if (string.IsNullOrEmpty(file.Description) || file.Description.Length > MaxDescriptionLength)
        {
            throw new FixtureException("description must be 1 to 300 characters", null);
        }

        if (file.Expected is null || file.Expected.Diff is null || file.Expected.Summary is null)
        {
            throw new FixtureException("expected must give diff and summary", null);
        }

        var outputs = new List<FixtureOutput>
        {
            CheckOutput(FixtureHarness.DiffOutput, OutputWriter.DiffJsonFile, file.Expected.Diff),
            CheckOutput(FixtureHarness.SummaryOutput, OutputWriter.MermaidFile, file.Expected.Summary),
        };

        return kind == FixtureKind.Synthetic
            ? LoadSynthetic(fixtureDir, file, outputs)
            : LoadPrivate(fixtureDir, file, outputs);
    }

    static FixtureOutput CheckOutput(string name, string fileName, OutputExpectation expectation)
    {
        if (!string.Equals(expectation.State, Ratchet.Pending, StringComparison.Ordinal)
            && !string.Equals(expectation.State, Ratchet.Green, StringComparison.Ordinal))
        {
            throw new FixtureException("state must be pending or green", null);
        }

        if (expectation.Due is { } due && (due < 2 || due > 9))
        {
            throw new FixtureException("due must be a phase number from 2 to 9", null);
        }

        return new FixtureOutput(name, fileName, expectation.State, expectation.Due);
    }

    static LoadedFixture LoadSynthetic(string fixtureDir, FixtureFile file, List<FixtureOutput> outputs)
    {
        if (file.Repo is not null || file.Base is not null || file.Head is not null)
        {
            throw new FixtureException("a synthetic fixture must not have repo, base or head keys", null);
        }

        var baseDir = Path.Combine(fixtureDir, "base");
        var headDir = Path.Combine(fixtureDir, "head");
        if (!Directory.Exists(baseDir) || !Directory.Exists(headDir))
        {
            throw new FixtureException("a synthetic fixture needs base and head directories", null);
        }

        var baseTree = ReadTree(baseDir, "base");
        var headTree = ReadTree(headDir, "head");
        return new LoadedFixture(file.Shape, fixtureDir, FixtureKind.Synthetic, file, outputs, baseTree, headTree);
    }

    static LoadedFixture LoadPrivate(string fixtureDir, FixtureFile file, List<FixtureOutput> outputs)
    {
        if (string.IsNullOrEmpty(file.Repo) || !Path.IsPathFullyQualified(file.Repo) || !Directory.Exists(file.Repo))
        {
            throw new FixtureException("a private fixture needs repo: an absolute path to an existing directory", null);
        }

        if (file.Base is null || file.Head is null
            || !CommitIdPattern().IsMatch(file.Base) || !CommitIdPattern().IsMatch(file.Head))
        {
            throw new FixtureException("a private fixture needs base and head as full commit ids", null);
        }

        if (Directory.Exists(Path.Combine(fixtureDir, "base")) || Directory.Exists(Path.Combine(fixtureDir, "head")))
        {
            throw new FixtureException("a private fixture carries no source trees", null);
        }

        return new LoadedFixture(file.Shape, fixtureDir, FixtureKind.Private, file, outputs, [], []);
    }

    static bool IsLink(FileSystemInfo info) =>
        info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null;

    /// <summary>
    /// Reads a tree by walking it directory by directory, so a link is seen and refused before anything
    /// is entered (T-02-20). The label ("base" or "head") only prefixes a failure's detail. Files come back sorted ordinally by their '/'-separated relative path, with
    /// the bytes exactly as stored.
    /// </summary>
    static List<TreeFile> ReadTree(string root, string label)
    {
        var files = new List<TreeFile>();
        Walk(new DirectoryInfo(root), "", label, files);
        files.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));
        return files;
    }

    static void Walk(DirectoryInfo directory, string prefix, string label, List<TreeFile> files)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            var relative = prefix.Length == 0 ? entry.Name : prefix + "/" + entry.Name;
            if (IsLink(entry))
            {
                throw new FixtureException("a fixture tree must not contain a link", label + "/" + relative);
            }

            if (BuildOutputSegments.Contains(entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                throw new FixtureException("a fixture tree must not contain a build output directory name", label + "/" + relative);
            }

            if (entry is DirectoryInfo child)
            {
                Walk(child, relative, label, files);
                continue;
            }

            var info = (FileInfo)entry;
            if (info.Length > MaxTreeFileBytes)
            {
                throw new FixtureException("a fixture tree file is larger than 256 KiB", label + "/" + relative);
            }

            if (files.Count >= MaxTreeFiles)
            {
                throw new FixtureException("a fixture tree has more than 200 files", label + "/" + relative);
            }

            files.Add(new TreeFile(relative, File.ReadAllBytes(info.FullName)));
        }
    }
}
