// ADR-001 trial (A): in-process .NET binding (TreeSitter.DotNet 1.3.0).
// usage: s2a <dir> [--blank NAME]... [--threads N] [--lang NAME] > out.json ; timings on stderr
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TreeSitter;

var root = args[0];
var blank = new List<string>(); int threads = Environment.ProcessorCount; string lang = "Cpp"; bool latin1 = false;
for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "--blank") blank.Add(args[++i]);
    else if (args[i] == "--threads") threads = int.Parse(args[++i]);
    else if (args[i] == "--lang") lang = args[++i];
    else if (args[i] == "--latin1") latin1 = true;
}
var sw = Stopwatch.StartNew();
var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
    .Where(f => f.EndsWith(".h") || f.EndsWith(".cpp") || f.EndsWith(".hpp") || f.EndsWith(".cc"))
    .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
    .OrderBy(f => f, StringComparer.Ordinal).ToArray();
var results = new List<Ty>[files.Length];
var tParse = new long[1];
var tl = new ThreadLocal<(Language, Parser)>(() => { var l = new Language(lang); return (l, new Parser(l)); }, true);
var swParse = Stopwatch.StartNew();
Parallel.For(0, files.Length, new ParallelOptions { MaxDegreeOfParallelism = threads }, idx =>
{
    var (_, parser) = tl.Value;
    // Read as UTF-8, blank macros with an equal-length run of spaces (length-preserving in UTF-16 code units).
    var text = latin1 ? Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(root, files[idx]))) : File.ReadAllText(Path.Combine(root, files[idx]), new UTF8Encoding(false));
    text = Blanker.Blank(text, blank);
    using var tree = parser.Parse(text)!;
    var list = new List<Ty>();
    Walk(tree.RootNode, files[idx], text, list, latin1);
    results[idx] = list;
});
var parseMs = swParse.Elapsed.TotalMilliseconds;
var all = results.SelectMany(r => r).ToList();
var json = JsonSerializer.Serialize(new { schema = "s2-trial", files = files.Length, types = all });
Console.Out.Write(json);
Console.Error.WriteLine($"A threads={threads} files={files.Length} types={all.Count} members={all.Sum(t => t.members)} parse_extract_ms={parseMs:F1} total_ms={sw.Elapsed.TotalMilliseconds:F1} abi={tl.Values.First().Item1.AbiVersion}");

static void Walk(Node n, string file, string text, List<Ty> outList, bool latin1)
{
    if ((n.Type == "class_specifier" || n.Type == "struct_specifier"))
    {
        var body = n.GetChildForField("body");
        var name = n.GetChildForField("name");
        if (body is not null && name is not null)
        {
            var bases = new List<string>();
            foreach (var c in n.Children)
                if (c.Type == "base_class_clause")
                    foreach (var b in c.NamedChildren)
                        if (b.Type != "access_specifier" && b.Type != "virtual" && b.Type != "comment") bases.Add(b.Text);
            int members = 0, nested = 0;
            foreach (var c in body.NamedChildren)
            {
                switch (c.Type)
                {
                    case "field_declaration": case "function_definition": case "declaration":
                    case "template_declaration": case "alias_declaration": members++; break;
                }
                if (c.Type == "field_declaration" && c.Children.Any(x => x.Type is "class_specifier" or "struct_specifier" or "enum_specifier")) nested++;
            }
            // Spans: UTF-8 byte offsets, converted from UTF-16 indexes (see ADR).
            int sb = latin1 ? n.StartIndex : Encoding.UTF8.GetByteCount(text.AsSpan(0, n.StartIndex));
            int eb = latin1 ? n.EndIndex : sb + Encoding.UTF8.GetByteCount(text.AsSpan(n.StartIndex, n.EndIndex - n.StartIndex));
            outList.Add(new Ty(file, n.Type == "class_specifier" ? "class" : "struct", name.Text, bases, members, nested, sb, eb, n.StartPosition.Row + 1));
        }
    }
    foreach (var c in n.Children) Walk(c, file, text, outList, latin1);
}

record Ty(string file, string kind, string name, List<string> bases, int members, int nested, int start_byte, int end_byte, int start_line);

static class Blanker
{
    public static string Blank(string s, List<string> macros)
    {
        if (macros.Count == 0) return s;
        var sb = new StringBuilder(s);
        int lineStart = 0;
        while (lineStart < s.Length)
        {
            int lineEnd = s.IndexOf('\n', lineStart); if (lineEnd < 0) lineEnd = s.Length;
            int k = lineStart; while (k < lineEnd && (s[k] == ' ' || s[k] == '\t')) k++;
            if (!(k < lineEnd && s[k] == '#'))
                foreach (var m in macros)
                {
                    int p = lineStart;
                    while ((p = s.IndexOf(m, p, lineEnd - p, StringComparison.Ordinal)) >= 0)
                    {
                        bool lb = p == 0 || !IsId(s[p - 1]); int e = p + m.Length; bool rb = e >= s.Length || !IsId(s[e]);
                        if (lb && rb) for (int q = p; q < e; q++) sb[q] = ' ';
                        p = e; if (p >= lineEnd) break;
                    }
                }
            lineStart = lineEnd + 1;
        }
        return sb.ToString();
    }
    static bool IsId(char c) => char.IsLetterOrDigit(c) || c == '_';
}
