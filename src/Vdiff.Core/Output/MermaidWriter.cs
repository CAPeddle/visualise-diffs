using System.Text;
using Vdiff.Core.Model;

namespace Vdiff.Core.Output;

/// <summary>
/// Renders the changed types as a Mermaid <c>classDiagram</c> (D-08): a class per changed type
/// carrying its state as <c>:::state</c>, one line per changed member with a trailing
/// <c>[state]</c> tag, and a <c>classDef</c> per state in the colours of the S1 sample. Only
/// changed types appear.
/// </summary>
public static class MermaidWriter
{
    // Look-alike characters stand in for the ones Mermaid reads as syntax.
    const char LeftBrace = '❴';
    const char RightBrace = '❵';
    const char Tilde = '∼';
    const char DoubleQuote = '″';

    public static string Render(DiffFile diff)
    {
        var text = new StringBuilder("classDiagram\n");
        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in diff.Types.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            var classId = ClassId(type.Fqn, usedIds);
            text.Append("  class ").Append(classId).Append("[\"").Append(EscapeLabel(type.Fqn)).Append("\"]:::").Append(type.State);
            if (type.Members.Count == 0)
            {
                text.Append('\n');
                continue;
            }

            text.Append(" {\n");
            foreach (var member in type.Members)
            {
                var access = member.AccessAfter ?? member.AccessBefore;
                var signature = member.SignatureAfter ?? member.SignatureBefore ?? member.Name;
                text.Append("    ").Append(Visibility(access)).Append(EscapeMember(signature))
                    .Append(" [").Append(member.State).Append("]\n");
            }

            text.Append("  }\n");
        }

        text.Append("  classDef added fill:#d4f8d4,stroke:#2e7d32,color:#111\n");
        text.Append("  classDef removed fill:#fde0e0,stroke:#c62828,color:#111,stroke-dasharray:4 3\n");
        text.Append("  classDef modified fill:#fff4cc,stroke:#b58900,color:#111\n");
        return text.ToString();
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
        var builder = new StringBuilder(fqn.Length);
        foreach (var c in fqn)
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');
        }

        var baseId = builder.Length == 0 ? "_" : builder.ToString();
        var id = baseId;
        for (var n = 2; !used.Add(id); n++)
        {
            id = baseId + "_" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return id;
    }

    static string EscapeMember(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                '{' => LeftBrace,
                '}' => RightBrace,
                '~' => Tilde,
                < ' ' => ' ',
                _ => c,
            });
        }

        return builder.ToString();
    }

    static string EscapeLabel(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                '"' => DoubleQuote,
                < ' ' => ' ',
                _ => c,
            });
        }

        return builder.ToString();
    }
}
