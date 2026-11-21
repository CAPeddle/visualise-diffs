using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Vdiff.Tests.Contract;

/// <summary>
/// Pins the three v1 schema files after the freeze review of plan 02-06 (D-01, D-02). The hash is taken over
/// the file text with CRLF turned into LF, so a CRLF checkout cannot break it (the .gitattributes rule already
/// keeps the files LF; the normalisation is defence in depth).
/// </summary>
[Trait("Category", "Contract")]
public class SchemaFreezeTests
{
    const string ModelSchemaSha256 = "C62EA54155F1155D8543626C5503F58D9272D59463E95C0985937F427EC66A72";
    const string DiffSchemaSha256 = "1BB22E1D6658067FA3F8952343BA6EB29E2084D6B8407CA31F3E1C65F30C479A";
    const string RunMetaSchemaSha256 = "47964D1D2C017128E930FBA5202CD06F176491DA38314A2B8CC07E6882EA2F2A";

    public static TheoryData<string, string> Pins => new()
    {
        { SchemaSupport.ModelSchemaFile, ModelSchemaSha256 },
        { SchemaSupport.DiffSchemaFile, DiffSchemaSha256 },
        { SchemaSupport.RunMetaSchemaFile, RunMetaSchemaSha256 },
    };

    [Theory]
    [MemberData(nameof(Pins))]
    public void Schema_file_matches_its_frozen_hash(string schemaFile, string expected)
    {
        var actual = LfHash(SchemaSupport.ReadText(schemaFile));

        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            $"docs/schema/{schemaFile} changed after the v1 freeze. An additive relaxation (a new enum value or a new optional property) is allowed inside v1: update the hash here in the same commit and give the reason in the commit message. A rename, removal or tightening needs vdiff-*/v2 and a migration (docs/schema/README.md).");
    }

    [Fact]
    public void Exactly_the_three_pinned_schema_files_exist()
    {
        var pinned = Pins.Select(row => (string)row[0]).Order(StringComparer.Ordinal).ToArray();
        var listed = SchemaSupport.AllSchemaFiles.Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(pinned, listed);

        var onDisk = Directory.GetFiles(SchemaSupport.SchemaDirectory(), "*.schema.json")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(pinned, onDisk);
    }

    [Fact]
    public void Readme_says_the_schemas_are_frozen()
    {
        var readme = File.ReadAllText(Path.Combine(SchemaSupport.SchemaDirectory(), "README.md"));
        Assert.Contains("Status: frozen on ", readme, StringComparison.Ordinal);
    }

    static string LfHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))));
}
