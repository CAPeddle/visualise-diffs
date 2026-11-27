using System.Globalization;
using System.Net;
using System.Text;
using Vdiff.Core.Model;

namespace Vdiff.Core.Output;

/// <summary>
/// Renders one self-contained report page (D-08): inline CSS, a summary line, the warnings, a
/// table of the changed types and members and the Mermaid text in an escaped block. There is no
/// script element, no external URL and no remote font or image (T-01-06). Every value that
/// comes from the analysed repository passes through <see cref="WebUtility.HtmlEncode(string)"/>.
/// </summary>
public static class HtmlWriter
{
    /// <summary>
    /// The commit ids on the page come from <paramref name="meta"/>, so the page is not a canonical
    /// output: it differs between two runs over identical trees under different commits.
    /// </summary>
    public static string Render(DiffFile diff, string mermaid, RunMeta meta)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n");
        html.Append("<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n");
        html.Append("<title>vdiff report</title>\n");
        html.Append("<style>\n");
        html.Append("body { font-family: sans-serif; margin: 2em; color: #111; }\n");
        html.Append("table { border-collapse: collapse; margin: 1em 0; }\n");
        html.Append("th, td { border: 1px solid #999; padding: 0.25em 0.6em; text-align: left; vertical-align: top; }\n");
        html.Append("tr.type td { background: #eee; font-weight: bold; }\n");
        html.Append(".added { background: #d4f8d4; }\n");
        html.Append(".removed { background: #fde0e0; }\n");
        html.Append(".modified { background: #fff4cc; }\n");
        html.Append("pre { background: #f6f6f6; padding: 1em; overflow: auto; }\n");
        html.Append("</style>\n</head>\n<body>\n");
        html.Append("<h1>Class diagram changes</h1>\n");

        var types = diff.Summary.Types;
        var members = diff.Summary.Members;
        html.Append("<p>Base <code>").Append(Enc(meta.BaseCommit)).Append("</code>, head <code>").Append(Enc(meta.HeadCommit)).Append("</code></p>\n");
        html.Append("<p>")
            .Append(string.Create(
                CultureInfo.InvariantCulture,
                $"Types: {types.Added} added, {types.Removed} removed, {types.Modified} modified, {types.Unchanged} unchanged. " +
                $"Members: {members.Added} added, {members.Removed} removed, {members.Modified} modified. " +
                $"Not modelled: {diff.Summary.Unmodelled.Base} on the base side, {diff.Summary.Unmodelled.Head} on the head side."))
            .Append("</p>\n");

        if (diff.Warnings.Count > 0)
        {
            html.Append("<h2>Warnings</h2>\n<ul>\n");
            foreach (var warning in diff.Warnings)
            {
                html.Append("<li>").Append(Enc(warning)).Append("</li>\n");
            }

            html.Append("</ul>\n");
        }

        html.Append("<h2>Changed types</h2>\n");
        html.Append("<table>\n<tr><th>Type or member</th><th>State</th><th>Detail</th></tr>\n");
        foreach (var type in diff.Types)
        {
            html.Append("<tr class=\"type\"><td>").Append(Enc(type.Id)).Append("</td><td class=\"").Append(Enc(type.State)).Append("\">")
                .Append(Enc(type.State)).Append("</td><td>").Append(Enc(TypeDetail(type))).Append("</td></tr>\n");
            foreach (var member in type.Members)
            {
                html.Append("<tr><td>").Append(Enc(member.Key)).Append("</td><td class=\"").Append(Enc(member.State)).Append("\">")
                    .Append(Enc(member.State)).Append("</td><td>").Append(Enc(MemberDetail(member))).Append("</td></tr>\n");
            }
        }

        html.Append("</table>\n");
        html.Append("<h2>Mermaid</h2>\n<pre>").Append(Enc(mermaid)).Append("</pre>\n");
        html.Append("</body>\n</html>\n");
        return html.ToString();
    }

    static string TypeDetail(TypeDiff type)
    {
        var parts = new List<string>();
        var file = type.HeadFile ?? type.BaseFile;
        if (file is not null)
        {
            parts.Add(file);
        }

        if (type.Bases.Added.Count > 0)
        {
            parts.Add("bases added: " + string.Join(", ", type.Bases.Added));
        }

        if (type.Bases.Removed.Count > 0)
        {
            parts.Add("bases removed: " + string.Join(", ", type.Bases.Removed));
        }

        return string.Join("; ", parts);
    }

    static string MemberDetail(MemberDiff member)
    {
        var parts = new List<string>();
        if (member.SignatureBefore is not null && member.SignatureAfter is not null)
        {
            // An access-only change keeps both signatures; show the text once, not as an arrow from itself.
            parts.Add(
                string.Equals(member.SignatureBefore, member.SignatureAfter, StringComparison.Ordinal)
                    ? member.SignatureAfter
                    : member.SignatureBefore + " -> " + member.SignatureAfter);
        }
        else
        {
            parts.Add(member.SignatureAfter ?? member.SignatureBefore ?? member.Name);
        }

        if (member.AccessBefore is not null && member.AccessAfter is not null)
        {
            if (!string.Equals(member.AccessBefore, member.AccessAfter, StringComparison.Ordinal))
            {
                parts.Add(member.AccessBefore + " -> " + member.AccessAfter);
            }
        }
        else
        {
            parts.Add(member.AccessAfter ?? member.AccessBefore ?? "");
        }

        return string.Join("; ", parts.Where(p => p.Length > 0));
    }

    static string Enc(string text) => WebUtility.HtmlEncode(text);
}
