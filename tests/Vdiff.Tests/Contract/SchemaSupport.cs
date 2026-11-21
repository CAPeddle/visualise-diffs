using System.Collections.Concurrent;
using NJsonSchema;
using NJsonSchema.Validation;
using Vdiff.Core.Output;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Contract;

/// <summary>
/// Loads the three committed v1 schema files (D-01, D-15) and validates documents against them. A document
/// is validated by NJsonSchema offline: the files hold no remote reference.
/// </summary>
public static class SchemaSupport
{
    public const string ModelSchemaFile = "vdiff-model-v1.schema.json";
    public const string DiffSchemaFile = "vdiff-diff-v1.schema.json";
    public const string RunMetaSchemaFile = "vdiff-run-meta-v1.schema.json";

    public static readonly IReadOnlyList<string> AllSchemaFiles = [ModelSchemaFile, DiffSchemaFile, RunMetaSchemaFile];

    static readonly ConcurrentDictionary<string, JsonSchema> Cache = new(StringComparer.Ordinal);

    public static string SchemaDirectory() => Path.Combine(RepoRoot.Find(), "docs", "schema");

    /// <summary>The text of a schema file, for tests that walk it as JSON.</summary>
    public static string ReadText(string schemaFile) => File.ReadAllText(Path.Combine(SchemaDirectory(), schemaFile));

    /// <summary>Parses a schema file once and caches it for the process.</summary>
    public static JsonSchema Load(string schemaFile) =>
        Cache.GetOrAdd(schemaFile, f => JsonSchema.FromJsonAsync(ReadText(f)).GetAwaiter().GetResult());

    /// <summary>
    /// Every validation error as "&lt;Kind&gt; at &lt;Path&gt;". The offending value is never included, so a
    /// caller that handles target content (the private harness, plan 02-09) can log the result (T-02-15).
    /// </summary>
    public static IReadOnlyList<string> Errors(string schemaFile, string json)
    {
        var lines = new List<string>();
        foreach (var error in Load(schemaFile).Validate(json))
        {
            Flatten(error, lines);
        }

        return lines;
    }

    static void Flatten(ValidationError error, List<string> lines)
    {
        lines.Add($"{error.Kind} at {error.Path}");
        if (error is ChildSchemaValidationError child)
        {
            foreach (var nested in child.Errors.Values.SelectMany(e => e))
            {
                Flatten(nested, lines);
            }
        }
    }

    /// <summary>The schema that describes an output file, or null for a file with no schema.</summary>
    public static string? SchemaFileFor(string outputFileName) => outputFileName switch
    {
        OutputWriter.BaseModelFile or OutputWriter.HeadModelFile => ModelSchemaFile,
        OutputWriter.DiffJsonFile => DiffSchemaFile,
        OutputWriter.MetaFile => RunMetaSchemaFile,
        _ => null,
    };

    /// <summary>
    /// Validates every output file of a run that has a schema. Each problem is one line,
    /// "&lt;file&gt;: missing" or "&lt;file&gt;: &lt;Kind&gt; at &lt;Path&gt;"; an empty list means all validate.
    /// </summary>
    public static IReadOnlyList<string> ValidateOutputs(string outDir)
    {
        var problems = new List<string>();
        foreach (var name in OutputWriter.FileNames)
        {
            var schemaFile = SchemaFileFor(name);
            if (schemaFile is null)
            {
                continue;
            }

            var path = Path.Combine(outDir, name);
            if (!File.Exists(path))
            {
                problems.Add($"{name}: missing");
                continue;
            }

            problems.AddRange(Errors(schemaFile, File.ReadAllText(path)).Select(e => $"{name}: {e}"));
        }

        return problems;
    }
}
