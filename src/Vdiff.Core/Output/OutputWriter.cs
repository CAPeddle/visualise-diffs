using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vdiff.Core.Model;

namespace Vdiff.Core.Output;

/// <summary>
/// Canonical JSON serialisation and the fixed output files (D-08). File names are
/// constants: a path from the analysed repository is never used as a file name (T-01-28).
/// </summary>
public static class OutputWriter
{
    public const string BaseModelFile = "base.model.json";
    public const string HeadModelFile = "head.model.json";
    public const string DiffJsonFile = "diff.json";
    public const string MermaidFile = "summary.mmd";
    public const string HtmlFile = "report.html";
    public const string MetaFile = "run.meta.json";

    /// <summary>Every file a run writes, in write order; tests read this list instead of naming files.</summary>
    public static readonly IReadOnlyList<string> FileNames =
        [BaseModelFile, HeadModelFile, DiffJsonFile, MermaidFile, HtmlFile, MetaFile];

    /// <summary>
    /// The canonical outputs: files whose bytes are meant to depend only on the trees and the config.
    /// Commit ids and tool versions belong in run.meta.json.
    /// </summary>
    public static readonly IReadOnlyList<string> CanonicalFileNames =
        [BaseModelFile, HeadModelFile, DiffJsonFile, MermaidFile];

    static readonly UTF8Encoding Utf8NoBom = new(false);

    // The default NewLine on Windows is CRLF. UnsafeRelaxedJsonEscaping keeps operator<< readable;
    // that is safe because this JSON is never embedded in HTML (the page embeds Mermaid text only,
    // through HtmlEncode, C21).
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Indented snake_case JSON with LF line ends, nulls omitted and a trailing LF. A model, a diff or a run
    /// meta is put in canonical order first (R-23), so this is the one entry point and no caller can skip it.
    /// </summary>
    public static string Serialize<T>(T value) => value switch
    {
        ModelFile model => Json(Canonical.Canonicalise(model)),
        DiffFile diff => Json(Canonical.Canonicalise(diff)),
        RunMeta meta => Json(Canonical.Canonicalise(meta)),
        _ => Json(value),
    };

    static string Json<TValue>(TValue value) => JsonSerializer.Serialize(value, JsonOptions) + "\n";

    /// <summary>
    /// Writes every file in <see cref="FileNames"/> into <paramref name="held"/> as UTF-8 without a byte-order mark
    /// and with LF line ends, every one by <see cref="HeldDirectory.Path"/> and never by the path
    /// the operator typed. The directory is created and held by the caller, so this method creates
    /// no directory. Callers build every model and the diff first, so a failure before the
    /// directory is acquired leaves it absent or untouched.
    /// Check-to-write interval (T-01-43, T-01-46). On Windows the output directory is opened without delete
    /// sharing before the first file is created and held until the last file is in place, so the operating
    /// system refuses to rename or delete it or any of its ancestors; its final path is read from the open
    /// handle and judged against the protected roots, and every file is created and moved by that final path,
    /// never by the path the operator typed, because a link entry in that path can still be retargeted while
    /// the directory is held. On Linux and macOS no handle is held: the resolved path is judged before and
    /// after the directories are created and the files are written by the resolved path, so the interval
    /// between that judgement and each write stays open for a process that is able to rename a directory.
    /// </summary>
    internal static void WriteAll(
        HeldDirectory held,
        ModelFile baseModel,
        ModelFile headModel,
        DiffFile diff,
        string mermaid,
        string html,
        RunMeta meta)
    {
        WriteText(held.Path, BaseModelFile, Serialize(baseModel));
        WriteText(held.Path, HeadModelFile, Serialize(headModel));
        WriteText(held.Path, DiffJsonFile, Serialize(diff));
        WriteText(held.Path, MermaidFile, mermaid);
        WriteText(held.Path, HtmlFile, html);
        WriteText(held.Path, MetaFile, Serialize(meta));
    }

    /// <summary>
    /// Writes one file to a random temporary name created with <see cref="FileMode.CreateNew"/>
    /// in <paramref name="outDir"/>, then moves it onto the final name with overwrite. The move
    /// replaces the directory entry instead of writing through it, so a hard link or file
    /// symbolic link planted at a final name is replaced and the file it pointed to is never
    /// touched (T-01-42, review finding WR-01). A process killed between the create and the move
    /// can leave one <c>*.tmp-*</c> file in <paramref name="outDir"/>, never in the analysed
    /// repository; that is accepted.
    /// </summary>
    static void WriteText(string outDir, string fileName, string text)
    {
        var bytes = Utf8NoBom.GetBytes(text.ReplaceLineEndings("\n"));
        var finalPath = Path.Combine(outDir, fileName);
        var tempPath = finalPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // best effort: the original failure is the one to report
            }

            throw;
        }
    }
}
