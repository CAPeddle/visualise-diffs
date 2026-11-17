using System.Globalization;
using System.Text;
using Vdiff.Core.Model;

namespace Vdiff.Core.Output;

/// <summary>
/// Renders the changed types as a Mermaid <c>classDiagram</c> (D-08): a class per changed type
/// carrying its state as <c>:::state</c>, one line per changed member with a trailing
/// <c>[state]</c> tag, inheritance edges between displayed types, and a <c>classDef</c> per state
/// in the colours of the S1 sample. Only changed types appear. The trailing-tag format is the
/// plan's provisional one until spike S1 is run.
/// </summary>
public static class MermaidWriter
{
    // Look-alike characters stand in for the ones Mermaid reads as syntax (T-01-20):
    // U+2774 and U+2775 (medium ornamental braces), U+223C (tilde operator), U+2033 (double prime).
    const char LeftBrace = (char)0x2774;
    const char RightBrace = (char)0x2775;
    const char Tilde = (char)0x223C;
    const char DoubleQuote = (char)0x2033;

    public static string Render(DiffFile diff)
    {
        // Canonical order is the type id, ordinal; class ids are made unique in that order.
        var shown = diff.Types
            .Where(t => t.State != States.Unchanged)
            .OrderBy(t => t.Id, StringComparer.Ordinal)
            .ToList();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var ids = shown.Select(t => ClassId(t.Fqn, used)).ToList();

        var text = new StringBuilder("classDiagram\n");
        for (var i = 0; i < shown.Count; i++)
        {
            AppendClass(text, shown[i], ids[i]);
        }

        foreach (var edge in Edges(shown, ids))
        {
            text.Append(edge).Append('\n');
        }

        text.Append("  classDef added fill:#d4f8d4,stroke:#2e7d32,color:#111\n");
        text.Append("  classDef removed fill:#fde0e0,stroke:#c62828,color:#111,stroke-dasharray:4 3\n");
        text.Append("  classDef modified fill:#fff4cc,stroke:#b58900,color:#111\n");
        return text.ToString();
    }

    static void AppendClass(StringBuilder text, TypeDiff type, string classId)
    {
        text.Append("  class ").Append(classId).Append("[\"").Append(Escape(type.Fqn)).Append("\"]:::").Append(type.State);
        var members = type.Members
            .Where(m => m.State != States.Unchanged)
            .OrderBy(m => m.Key, StringComparer.Ordinal)
            .ToList();
        if (members.Count == 0)
        {
            text.Append('\n');
            return;
        }

        text.Append(" {\n");
        foreach (var member in members)
        {
            var access = member.AccessAfter ?? member.AccessBefore;
            var signature = member.State == States.Removed
                ? member.SignatureBefore
                : member.SignatureAfter ?? member.SignatureBefore;
            if (string.IsNullOrEmpty(signature))
            {
                signature = member.Name;
            }

            // The state tag is last, so a signature ending in * or $ is never read as a classifier.
            text.Append("    ").Append(Visibility(access)).Append(Escape(signature))
                .Append(" [").Append(member.State).Append("]\n");
        }

        text.Append("  }\n");
    }

    static char Visibility(string? access) => access switch
    {
        "protected" => '#',
        "private" => '-',
        _ => '+',
    };

    /// <summary>The fqn with every character outside letters, digits and underscore replaced by an underscore, made unique.</summary>
    static string ClassId(string fqn, HashSet<string> used)
    {
        var builder = new StringBuilder(fqn.Length + 1);
        foreach (var c in fqn)
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');
        }

        if (builder.Length == 0)
        {
            builder.Append('_');
        }
        else if (char.IsAsciiDigit(builder[0]))
        {
            builder.Insert(0, 'T');
        }

        var baseId = builder.ToString();
        var id = baseId;
        for (var n = 2; !used.Add(id); n++)
        {
            id = baseId + "_" + n.ToString(CultureInfo.InvariantCulture);
        }

        return id;
    }

    /// <summary>
    /// One <c>base &lt;|-- derived</c> line per pair of displayed types, sorted. A base resolves by
    /// exact fqn, else by the one displayed type whose fqn ends in <c>::base</c>; anything else
    /// draws nothing, so no node is invented for a type that is not shown.
    /// </summary>
    static SortedSet<string> Edges(List<TypeDiff> shown, List<string> ids)
    {
        var byFqn = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < shown.Count; i++)
        {
            byFqn.TryAdd(shown[i].Fqn, i);
        }

        var edges = new SortedSet<string>(StringComparer.Ordinal);
        for (var derived = 0; derived < shown.Count; derived++)
        {
            foreach (var written in shown[derived].Bases.Added.Concat(shown[derived].Bases.Removed))
            {
                var name = BaseName(written);
                if (name.Length == 0)
                {
                    continue;
                }

                if (!byFqn.TryGetValue(name, out var target))
                {
                    var suffix = "::" + name;
                    var matches = Enumerable.Range(0, shown.Count)
                        .Where(i => shown[i].Fqn.EndsWith(suffix, StringComparison.Ordinal))
                        .Take(2)
                        .ToList();
                    if (matches.Count != 1)
                    {
                        continue;
                    }

                    target = matches[0];
                }

                if (target != derived)
                {
                    edges.Add("  " + ids[target] + " <|-- " + ids[derived]);
                }
            }
        }

        return edges;
    }

    /// <summary>A written base without template arguments (balanced angle brackets), a leading <c>::</c> and surrounding space.</summary>
    static string BaseName(string written)
    {
        var builder = new StringBuilder(written.Length);
        var depth = 0;
        foreach (var c in written)
        {
            if (c == '<')
            {
                depth++;
            }
            else if (c == '>' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0)
            {
                builder.Append(c);
            }
        }

        var name = builder.ToString().Trim();
        return name.StartsWith("::", StringComparison.Ordinal) ? name[2..] : name;
    }

    /// <summary>
    /// The single escape for every repository-derived string (T-01-20): braces, tilde and the double
    /// quote become look-alikes; control characters and line separators become one space.
    /// </summary>
    static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                '{' => LeftBrace,
                '}' => RightBrace,
                '~' => Tilde,
                '"' => DoubleQuote,
                _ when char.IsControl(c) || IsLineSeparator(c) => ' ',
                _ => c,
            });
        }

        return builder.ToString();
    }

    static bool IsLineSeparator(char c) =>
        char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
