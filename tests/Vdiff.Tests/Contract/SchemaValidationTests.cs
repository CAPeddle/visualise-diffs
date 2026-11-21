using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>The only schema keywords the three files may use (D-15). NJsonSchema ignores the fixed-value and conditional keywords.</summary>
    public static readonly IReadOnlySet<string> AllowedKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "$schema", "title", "description", "type", "properties", "required", "additionalProperties",
        "items", "enum", "pattern", "minimum", "maximum", "minLength", "uniqueItems", "definitions", "$ref",
    };

    /// <summary>
    /// Every keyword a schema text uses. In a schema object every key is a keyword; the values of
    /// <c>properties</c> and <c>definitions</c> are name-to-schema maps whose names are not keywords;
    /// <c>items</c> is a schema; <c>enum</c>, <c>required</c> and scalars are leaves.
    /// </summary>
    public static IReadOnlySet<string> KeywordsUsed(string schemaText)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(schemaText);
        Walk(doc.RootElement, used);
        return used;
    }

    static void Walk(JsonElement schema, HashSet<string> used)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var keyword in schema.EnumerateObject())
        {
            used.Add(keyword.Name);
            switch (keyword.Name)
            {
                case "properties" or "definitions":
                    foreach (var named in keyword.Value.EnumerateObject())
                    {
                        Walk(named.Value, used);
                    }

                    break;
                case "items":
                    Walk(keyword.Value, used);
                    break;
            }
        }
    }

    static readonly HashSet<string> NotValidating = new(StringComparer.Ordinal) { "$schema", "title", "description", "definitions" };

    static JsonNode ModelJson() => JsonNode.Parse(OutputWriter.Serialize(ContractSamples.FullModel()))!;

    static JsonNode DiffJson() => JsonNode.Parse(OutputWriter.Serialize(ContractSamples.FullDiff()))!;

    static JsonNode MetaJson() => JsonNode.Parse(OutputWriter.Serialize(ContractSamples.FullMeta()))!;

    static JsonObject FindObject(JsonNode array, Func<JsonObject, bool> where) =>
        array.AsArray().Select(n => n!.AsObject()).First(where);

    // Row name -> (keyword exercised, schema file, the document to break, the break, the error kind NJsonSchema must report).
    static readonly Dictionary<string, (string Keyword, string SchemaFile, Func<JsonNode> Sample, Action<JsonNode> Break, string ErrorKind)> Negatives = new()
    {
        ["type"] = ("type", SchemaSupport.ModelSchemaFile, ModelJson, m => m["files"] = "not a list", "ArrayExpected"),
        ["required"] = ("required", SchemaSupport.ModelSchemaFile, ModelJson, m => m.AsObject().Remove("schema"), "PropertyRequired"),
        ["additionalProperties"] = ("additionalProperties", SchemaSupport.ModelSchemaFile, ModelJson,
            m => FindObject(m["types"]!, _ => true)["extra"] = 1, "NoAdditionalPropertiesAllowed"),
        ["properties"] = ("properties", SchemaSupport.ModelSchemaFile, ModelJson,
            m => FindObject(m["types"]!, _ => true)["span"]!["end_line"] = "ten", "IntegerExpected"),
        ["items"] = ("items", SchemaSupport.ModelSchemaFile, ModelJson, m => m["duplicates"] = new JsonArray(1), "StringExpected"),
        ["enum"] = ("enum", SchemaSupport.ModelSchemaFile, ModelJson, m => FindObject(m["types"]!, _ => true)["kind"] = "widget", "NotInEnumeration"),
        ["pattern"] = ("pattern", SchemaSupport.ModelSchemaFile, ModelJson, m => FindObject(m["unmodelled"]!, _ => true)["reason"] = "Not A Code", "PatternMismatch"),
        ["minimum"] = ("minimum", SchemaSupport.ModelSchemaFile, ModelJson, m => FindObject(m["types"]!, _ => true)["span"]!["start_byte"] = -1, "NumberTooSmall"),
        ["maximum"] = ("maximum", SchemaSupport.DiffSchemaFile, DiffJson,
            d => FindObject(d["types"]!, t => t.ContainsKey("rename"))["rename"]!["score_milli"] = 1001, "NumberTooBig"),
        ["minLength"] = ("minLength", SchemaSupport.ModelSchemaFile, ModelJson, m => FindObject(m["types"]!, _ => true)["id"] = "", "StringTooShort"),
        ["uniqueItems"] = ("uniqueItems", SchemaSupport.ModelSchemaFile, ModelJson, m =>
        {
            var type = FindObject(m["types"]!, t => t["modifiers"]!.AsArray().Count > 0);
            var first = type["modifiers"]![0]!.GetValue<string>();
            type["modifiers"] = new JsonArray(first, first);
        }, "ItemsNotUnique"),
        ["$ref"] = ("$ref", SchemaSupport.ModelSchemaFile, ModelJson, m => FindObject(m["edges"]!, e => e.ContainsKey("from"))["from"] = new JsonObject(), "PropertyRequired"),
        ["run_meta_schema_name"] = ("enum", SchemaSupport.RunMetaSchemaFile, MetaJson, r => r["schema"] = "vdiff-run-meta/v2", "NotInEnumeration"),
    };

    public static TheoryData<string> NegativeRowNames() => [.. Negatives.Keys];

    [Fact]
    public void Every_schema_file_uses_only_allowed_keywords()
    {
        foreach (var file in SchemaSupport.AllSchemaFiles)
        {
            var extra = KeywordsUsed(SchemaSupport.ReadText(file)).Except(AllowedKeywords).Order(StringComparer.Ordinal).ToList();
            Assert.True(extra.Count == 0, $"{file} uses keywords outside the allow-list: {string.Join(", ", extra)}");
        }
    }

    [Fact]
    public void The_keyword_walker_reports_a_disallowed_keyword_and_ignores_property_names()
    {
        const string text =
            "{\"type\": \"object\", \"const\": 1, \"if\": {}, \"properties\": {\"title\": {\"type\": \"string\", \"minLength\": 1}, \"enum\": {\"items\": {\"pattern\": \"x\"}}}}";

        var used = KeywordsUsed(text);

        Assert.Contains("const", used);
        Assert.Contains("if", used);
        Assert.Equal(new[] { "const", "if", "items", "minLength", "pattern", "properties", "type" }, used.Order(StringComparer.Ordinal));
        Assert.NotEmpty(used.Except(AllowedKeywords));
    }

    [Fact]
    public void Every_validating_keyword_a_schema_uses_has_a_negative_row()
    {
        var used = SchemaSupport.AllSchemaFiles
            .SelectMany(f => KeywordsUsed(SchemaSupport.ReadText(f)))
            .Except(NotValidating)
            .ToHashSet(StringComparer.Ordinal);
        var covered = Negatives.Values.Select(r => r.Keyword).ToHashSet(StringComparer.Ordinal);

        var missing = used.Except(covered).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, "keywords with no negative row: " + string.Join(", ", missing));
        Assert.NotEmpty(used);
    }

    [Theory]
    [MemberData(nameof(NegativeRowNames))]
    public void A_document_that_breaks_the_keyword_is_rejected(string row)
    {
        var (_, schemaFile, sample, breakIt, errorKind) = Negatives[row];
        var document = sample();
        Assert.Empty(SchemaSupport.Errors(schemaFile, document.ToJsonString()));

        breakIt(document);
        var errors = SchemaSupport.Errors(schemaFile, document.ToJsonString());

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.StartsWith(errorKind + " at ", StringComparison.Ordinal));
    }

    [Fact]
    public void The_full_samples_with_every_optional_property_set_validate()
    {
        Assert.Empty(SchemaSupport.Errors(SchemaSupport.ModelSchemaFile, OutputWriter.Serialize(ContractSamples.FullModel())));
        Assert.Empty(SchemaSupport.Errors(SchemaSupport.DiffSchemaFile, OutputWriter.Serialize(ContractSamples.FullDiff())));
        Assert.Empty(SchemaSupport.Errors(SchemaSupport.RunMetaSchemaFile, OutputWriter.Serialize(ContractSamples.FullMeta())));
    }

    [Fact]
    public void An_empty_model_and_an_empty_diff_validate()
    {
        Assert.Empty(SchemaSupport.Errors(SchemaSupport.ModelSchemaFile, OutputWriter.Serialize(ContractSamples.EmptyModel())));
        Assert.Empty(SchemaSupport.Errors(SchemaSupport.DiffSchemaFile, OutputWriter.Serialize(ContractSamples.EmptyDiff())));
    }

    [Fact]
    public void A_model_or_a_diff_with_the_types_list_omitted_is_rejected()
    {
        var model = JsonNode.Parse(OutputWriter.Serialize(ContractSamples.EmptyModel()))!;
        var diff = JsonNode.Parse(OutputWriter.Serialize(ContractSamples.EmptyDiff()))!;
        Assert.True(model.AsObject().Remove("types"));
        Assert.True(diff.AsObject().Remove("types"));

        Assert.Contains(SchemaSupport.Errors(SchemaSupport.ModelSchemaFile, model.ToJsonString()), e => e.StartsWith("PropertyRequired at ", StringComparison.Ordinal));
        Assert.Contains(SchemaSupport.Errors(SchemaSupport.DiffSchemaFile, diff.ToJsonString()), e => e.StartsWith("PropertyRequired at ", StringComparison.Ordinal));
    }
}
