using System.Text.Json;
using System.Text.RegularExpressions;
using Vdiff.Core.Cli;
using Vdiff.Core.Output;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Contract;

/// <summary>
/// The three v1 schema files (D-01, D-15): they load offline, a real tracer run validates against them
/// (D-03, R-10), and their identity documentation is present (D-21, D-22).
/// </summary>
[Collection("sidecar")]
[Trait("Category", "Contract")]
public class SchemaValidationTests
{
    // Base: a class with a written base that resolves inside the same file, a private member that becomes
    // public in head, and a header that head removes. Head adds a new type in a new header.
    const string BaseShapeHeader =
        "#pragma once\n" +
        "namespace synthetic {\n" +
        "class Shape {\n" +
        "public:\n" +
        "    int id() const;\n" +
        "};\n" +
        "class Circle : public Shape {\n" +
        "public:\n" +
        "    double radius() const;\n" +
        "private:\n" +
        "    int tag_;\n" +
        "};\n" +
        "}\n";

    const string HeadShapeHeader =
        "#pragma once\n" +
        "namespace synthetic {\n" +
        "class Shape {\n" +
        "public:\n" +
        "    int id() const;\n" +
        "};\n" +
        "class Circle : public Shape {\n" +
        "public:\n" +
        "    double radius() const;\n" +
        "    int tag_;\n" +
        "};\n" +
        "}\n";

    /// <summary>Commits the two-commit repository, runs the CLI on it and returns the output directory.</summary>
    internal static string RunTracer(SyntheticRepo repo)
    {
        repo.Write("include/synthetic/shape.h", BaseShapeHeader);
        repo.Write("include/synthetic/legacy.h", "#pragma once\nnamespace synthetic {\nstruct Legacy {\n    int v;\n};\n}\n");
        var baseId = repo.Commit("base");
        repo.Write("include/synthetic/shape.h", HeadShapeHeader);
        repo.Write("include/synthetic/extra.h", "#pragma once\nnamespace synthetic {\nclass Extra {\npublic:\n    void run();\n};\n}\n");
        repo.Delete("include/synthetic/legacy.h");
        var headId = repo.Commit("head");

        var outDir = Path.Combine(repo.Root, "out");
        var args = new List<string> { "--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", outDir };
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = CliApp.Run(args, stdout, stderr);
        Assert.Equal("", stderr.ToString());
        Assert.Equal(0, exit);
        return outDir;
    }

    [Fact]
    public void The_three_schema_files_load_offline()
    {
        Assert.Equal(3, SchemaSupport.AllSchemaFiles.Count);
        foreach (var file in SchemaSupport.AllSchemaFiles)
        {
            Assert.NotNull(SchemaSupport.Load(file));
            var text = SchemaSupport.ReadText(file);
            Assert.DoesNotContain("\"$id\"", text);
            var refs = Regex.Matches(text, "\"\\$ref\":\\s*\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.NotEmpty(refs);
            Assert.All(refs, r => Assert.StartsWith("#/definitions/", r, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Tracer_output_validates_against_the_v1_schemas()
    {
        using var repo = new SyntheticRepo();

        var outDir = RunTracer(repo);

        Assert.Empty(SchemaSupport.ValidateOutputs(outDir));
        using var head = JsonDocument.Parse(File.ReadAllText(Path.Combine(outDir, OutputWriter.HeadModelFile)));
        var edges = head.RootElement.GetProperty("edges").EnumerateArray().ToList();
        Assert.Contains(edges, e => e.GetProperty("kind").GetString() == "base" && e.TryGetProperty("to", out _));
    }

    [Fact]
    public void SchemaFileFor_maps_the_json_outputs_and_gives_null_for_the_two_without_a_schema()
    {
        Assert.Equal(SchemaSupport.ModelSchemaFile, SchemaSupport.SchemaFileFor("base.model.json"));
        Assert.Equal(SchemaSupport.ModelSchemaFile, SchemaSupport.SchemaFileFor("head.model.json"));
        Assert.Equal(SchemaSupport.DiffSchemaFile, SchemaSupport.SchemaFileFor("diff.json"));
        Assert.Equal(SchemaSupport.RunMetaSchemaFile, SchemaSupport.SchemaFileFor("run.meta.json"));
        Assert.Null(SchemaSupport.SchemaFileFor("summary.mmd"));
        Assert.Null(SchemaSupport.SchemaFileFor("report.html"));
    }

    [Fact]
    public void The_identity_grammar_is_documented_in_the_readme_and_the_id_and_key_descriptions()
    {
        var readme = File.ReadAllLines(Path.Combine(SchemaSupport.SchemaDirectory(), "README.md"));
        Assert.Contains(readme, line => line.StartsWith("## Identity", StringComparison.Ordinal));

        var perLanguage = new HashSet<string>(StringComparer.Ordinal) { "TypeModel.id", "MemberModel.key", "TypeDiff.id", "MemberDiff.key" };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in new[] { SchemaSupport.ModelSchemaFile, SchemaSupport.DiffSchemaFile })
        {
            using var doc = JsonDocument.Parse(SchemaSupport.ReadText(file));
            foreach (var definition in doc.RootElement.GetProperty("definitions").EnumerateObject())
            {
                foreach (var property in definition.Value.GetProperty("properties").EnumerateObject())
                {
                    if (property.Name is not ("id" or "key"))
                    {
                        continue;
                    }

                    var name = $"{definition.Name}.{property.Name}";
                    var description = property.Value.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    Assert.True(description.Contains("Identity", StringComparison.Ordinal), $"{file} {name}: description lacks 'Identity'");
                    Assert.True(description.Contains("#<path>", StringComparison.Ordinal), $"{file} {name}: description lacks '#<path>'");
                    if (perLanguage.Contains(name))
                    {
                        seen.Add(name);
                        foreach (var language in new[] { "cpp", "csharp", "cpp-cli" })
                        {
                            Assert.True(description.Contains(language, StringComparison.Ordinal), $"{file} {name}: description does not name {language}");
                        }
                    }
                }
            }
        }

        Assert.Equal(perLanguage.Order(StringComparer.Ordinal), seen.Order(StringComparer.Ordinal));
    }
}
