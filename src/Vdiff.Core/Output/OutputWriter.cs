using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vdiff.Core.Model;

namespace Vdiff.Core.Output;

/// <summary>
/// Canonical JSON serialisation and the five fixed output files (D-08). File names are
/// constants: a path from the analysed repository is never used as a file name (T-01-28).
/// </summary>
public static class OutputWriter
{
    public const string BaseModelFile = "base.model.json";
    public const string HeadModelFile = "head.model.json";
    public const string DiffJsonFile = "diff.json";
    public const string MermaidFile = "summary.mmd";
    public const string HtmlFile = "report.html";

    static readonly UTF8Encoding Utf8NoBom = new(false);

    // The default NewLine on Windows is CRLF. UnsafeRelaxedJsonEscaping keeps operator<< readable;
    // that is safe because this JSON is never embedded in HTML (the page embeds Mermaid text only,
    // through HtmlEncode, C21).
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Indented snake_case JSON with LF line ends, nulls omitted and a trailing LF.</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions) + "\n";

    /// <summary>
    /// Writes the five files into <paramref name="outDir"/> as UTF-8 without a byte-order mark
    /// and with LF line ends. Callers build every model and the diff first, so a failure before
    /// this call leaves the directory absent or untouched.
    /// </summary>
    public static void WriteAll(
        string outDir, ModelFile baseModel, ModelFile headModel, DiffFile diff, string mermaid, string html)
    {
        Directory.CreateDirectory(outDir);
        WriteText(outDir, BaseModelFile, Serialize(baseModel));
        WriteText(outDir, HeadModelFile, Serialize(headModel));
        WriteText(outDir, DiffJsonFile, Serialize(diff));
        WriteText(outDir, MermaidFile, mermaid);
        WriteText(outDir, HtmlFile, html);
    }

    static void WriteText(string outDir, string fileName, string text)
    {
        File.WriteAllBytes(Path.Combine(outDir, fileName), Utf8NoBom.GetBytes(text.ReplaceLineEndings("\n")));
    }
}
