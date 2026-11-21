using System.Reflection;
using System.Text.Json;
using Vdiff.Core.Model;

namespace Vdiff.Tests.Contract;

/// <summary>
/// The records and the schema files are one contract (D-01, T-02-14). Parity runs in both directions:
/// every record reachable from a file record has a definition and every definition has a record, property
/// names and their order are equal, and each required list is the record's non-nullable properties.
/// </summary>
[Trait("Category", "Contract")]
public class SchemaParityTests
{
    static readonly Dictionary<string, (string SchemaFile, Type Root)> Roots = new()
    {
        ["ModelFile"] = (SchemaSupport.ModelSchemaFile, typeof(ModelFile)),
        ["DiffFile"] = (SchemaSupport.DiffSchemaFile, typeof(DiffFile)),
        ["RunMeta"] = (SchemaSupport.RunMetaSchemaFile, typeof(RunMeta)),
    };

    static PropertyInfo[] Properties(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    static bool IsModelRecord(Type type) =>
        type.Namespace == "Vdiff.Core.Model" && type.GetMethod("<Clone>$") is not null;

    static Type Unwrap(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            type = type.GetGenericArguments()[0];
        }

        return Nullable.GetUnderlyingType(type) ?? type;
    }

    static List<Type> Reachable(Type root)
    {
        var seen = new List<Type>();
        void Visit(Type type)
        {
            if (seen.Contains(type))
            {
                return;
            }

            seen.Add(type);
            foreach (var property in Properties(type))
            {
                var inner = Unwrap(property.PropertyType);
                if (IsModelRecord(inner))
                {
                    Visit(inner);
                }
            }
        }

        Visit(root);
        return seen;
    }

    static bool IsOptional(PropertyInfo property) =>
        Nullable.GetUnderlyingType(property.PropertyType) is not null
        || new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;

    static string Snake(PropertyInfo property) => JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);

    [Theory]
    [InlineData("ModelFile")]
    [InlineData("DiffFile")]
    [InlineData("RunMeta")]
    public void Schema_and_records_agree_in_both_directions(string root)
    {
        var (schemaFile, rootType) = Roots[root];
        using var doc = JsonDocument.Parse(SchemaSupport.ReadText(schemaFile));
        var schema = doc.RootElement;
        var definitions = schema.GetProperty("definitions").EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);

        var records = Reachable(rootType);
        var expectedDefinitions = records.Where(t => t != rootType).Select(t => t.Name).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(expectedDefinitions, definitions.Keys.Order(StringComparer.Ordinal).ToList());

        // Both directions of the pairing are covered: a record with no definition makes the lists differ in
        // the first Assert, a definition with no record in the same one. Now compare each pair.
        foreach (var record in records)
        {
            var node = record == rootType ? schema : definitions[record.Name];
            Assert.Equal("object", node.GetProperty("type").GetString());
            Assert.False(node.GetProperty("additionalProperties").GetBoolean(), record.Name + " is not closed");

            var properties = Properties(record);
            var schemaNames = node.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();
            Assert.Equal(properties.Select(Snake).ToList(), schemaNames);

            var required = node.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).Order(StringComparer.Ordinal).ToList();
            var nonNullable = properties.Where(p => !IsOptional(p)).Select(Snake).Order(StringComparer.Ordinal).ToList();
            Assert.Equal(nonNullable, required);
        }
    }

    static IEnumerable<string> Constants(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!);

    static List<string> EnumOf(JsonElement schema, string definition, string property) =>
        schema.GetProperty("definitions").GetProperty(definition).GetProperty("properties").GetProperty(property)
            .GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToList();

    static void SameSet(IEnumerable<string> expected, List<string> actual, string what) =>
        Assert.True(
            expected.Order(StringComparer.Ordinal).SequenceEqual(actual.Order(StringComparer.Ordinal)),
            $"{what}: expected [{string.Join(", ", expected.Order(StringComparer.Ordinal))}], found [{string.Join(", ", actual.Order(StringComparer.Ordinal))}]");

    [Fact]
    public void Enum_values_match_the_constants_and_the_three_access_enums_are_identical()
    {
        using var model = JsonDocument.Parse(SchemaSupport.ReadText(SchemaSupport.ModelSchemaFile));
        using var diff = JsonDocument.Parse(SchemaSupport.ReadText(SchemaSupport.DiffSchemaFile));
        var m = model.RootElement;
        var d = diff.RootElement;

        foreach (var (definition, property) in new[] { ("TypeModel", "language"), ("LanguageExtraction", "language"), ("ModelFileEntry", "language"), ("TypeRef", "language") })
        {
            SameSet(Constants(typeof(Languages)), EnumOf(m, definition, property), $"model {definition}.{property}");
        }

        foreach (var (definition, property) in new[] { ("TypeDiff", "language"), ("TypeRef", "language") })
        {
            SameSet(Constants(typeof(Languages)), EnumOf(d, definition, property), $"diff {definition}.{property}");
        }

        foreach (var (definition, property) in new[] { ("LanguageExtraction", "mode"), ("ModelFileEntry", "mode") })
        {
            SameSet(Constants(typeof(Modes)), EnumOf(m, definition, property), $"model {definition}.{property}");
        }

        SameSet(Constants(typeof(EdgeKinds)), EnumOf(m, "EdgeModel", "kind"), "model EdgeModel.kind");
        SameSet(Constants(typeof(EdgeKinds)), EnumOf(d, "EdgeDiff", "kind"), "diff EdgeDiff.kind");

        var states = Constants(typeof(States)).ToList();
        var typeStates = EnumOf(d, "TypeDiff", "state");
        var memberStates = EnumOf(d, "MemberDiff", "state");
        Assert.All(states.Where(s => s != States.BodyChanged), s => Assert.Contains(s, typeStates));
        Assert.DoesNotContain(States.BodyChanged, typeStates);
        Assert.All(states, s => Assert.Contains(s, memberStates));

        // D-19, D-20: the type-level access enum and the member diff's access_before and access_after are
        // each identical, as ordered sequences, to the member access enum. Name and required parity does
        // not look at enum contents.
        var memberAccess = EnumOf(m, "MemberModel", "access");
        Assert.Equal(7, memberAccess.Count);
        Assert.Equal(memberAccess, EnumOf(m, "TypeModel", "access"));
        Assert.Equal(memberAccess, EnumOf(d, "MemberDiff", "access_before"));
        Assert.Equal(memberAccess, EnumOf(d, "MemberDiff", "access_after"));
    }
}
