using System.Text.RegularExpressions;

namespace Vdiff.Tests.Output;

/// <summary>
/// A structural lint for the Mermaid text vdiff writes (test code, not shipped). It checks the
/// shape the writer promises, line by line: the header, class lines carrying a state, member
/// lines ending in a state tag, inheritance edges between declared classes, one classDef per used
/// state, balanced braces and LF line ends. It is not a Mermaid parser: it proves the structure
/// cannot be altered by repository text, not that the renderer draws every glyph well (plan
/// 01-09 runs the real parser). It also checks the class-id prefix: every class id and edge
/// endpoint must start with <c>C_</c>, because a bare fqn-derived id can equal a Mermaid keyword
/// (link, note, click ...) and make the whole diagram unparseable (review finding CR-01, plan 01-11).
/// Beyond the lines the writer emits today it accepts the extra lines of the provisional v1 summary format
/// from <c>tests/fixtures/README.md</c>: the <c>renamed</c> and <c>context</c> classDefs, the
/// <c>renamed</c> and <c>body-changed</c> member tags, the two stereotype lines and the association
/// edge lines. Every one is a proposal until Phase 7 and the pinned Mermaid parse confirm it.
/// </summary>
public static class MermaidLint
{
    const string Id = "C_[A-Za-z0-9_]*";

    // The label between the quotes holds any character but a double quote, so a generic type's angle
    // brackets (Foo<>) are accepted; the writer's escape keeps braces and quotes out of real labels.
    static readonly Regex ClassLine = new(
        "^  class (" + Id + ")\\[\"[^\"]*\"\\]:::(\\w+)( \\{)?$", RegexOptions.CultureInvariant);

    static readonly Regex MemberLine = new(
        "^    [+#\\-][^{}\"]* \\[(added|removed|modified|renamed|body-changed)\\]$", RegexOptions.CultureInvariant);

    static readonly Regex StereotypeLine = new(
        "^    <<(managed wrapper|renamed from [^{}\"<>]+)>>$", RegexOptions.CultureInvariant);

    static readonly Regex EdgeLine = new("^  (" + Id + ") <\\|-- (" + Id + ")$", RegexOptions.CultureInvariant);

    static readonly Regex AssociationLine = new(
        "^  (" + Id + ") (?:-->|\\.\\.>) (" + Id + ") : uses(?: \\[(?:added|removed)\\])?$", RegexOptions.CultureInvariant);

    static readonly Regex ClassDefLine = new(
        "^  classDef (added|removed|modified|renamed|context) fill:#[0-9a-f]{6},stroke:#[0-9a-f]{6},color:#[0-9a-f]{3,6}(,stroke-dasharray:\\d+ \\d+)?$",
        RegexOptions.CultureInvariant);

    /// <summary>Returns the problems found; an empty list means the structure is sound.</summary>
    public static IReadOnlyList<string> Check(string text)
    {
        var problems = new List<string>();
        if (text.Contains('\r'))
        {
            problems.Add("carriage return in the text");
        }

        if (!text.EndsWith('\n') || text.EndsWith("\n\n", StringComparison.Ordinal))
        {
            problems.Add("the text must end with exactly one line feed");
        }

        var lines = text.Split('\n');
        if (lines[^1].Length == 0)
        {
            lines = lines[..^1];
        }

        if (lines.Length == 0 || lines[0] != "classDiagram")
        {
            problems.Add("the first line must be classDiagram");
        }

        if (lines.Length == 1 && lines[0] == "classDiagram")
        {
            problems.Add("a bare classDiagram is a Mermaid parse error: it needs a classDef or a class");
        }

        var classIds = new HashSet<string>(StringComparer.Ordinal);
        var classStates = new List<(string Id, string State)>();
        var edges = new List<(int Line, string From, string To, string What)>();
        var defined = new HashSet<string>(StringComparer.Ordinal);
        var inBlock = false;
        var seenMember = false;

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var number = i + 1;
            if (inBlock)
            {
                if (line == "  }")
                {
                    inBlock = false;
                }
                else if (MemberLine.IsMatch(line))
                {
                    seenMember = true;
                }
                else if (StereotypeLine.IsMatch(line))
                {
                    if (seenMember)
                    {
                        problems.Add($"line {number}: a stereotype line must come before the first member line of its class");
                    }
                }
                else
                {
                    problems.Add($"line {number}: not a member line ending in a state tag, a stereotype line, or it holds a brace or a quote");
                }

                continue;
            }

            var classMatch = ClassLine.Match(line);
            if (classMatch.Success)
            {
                var id = classMatch.Groups[1].Value;
                if (!classIds.Add(id))
                {
                    problems.Add($"line {number}: class id {id} is declared twice");
                }

                classStates.Add((id, classMatch.Groups[2].Value));
                inBlock = classMatch.Groups[3].Success;
                seenMember = false;
                continue;
            }

            var edge = EdgeLine.Match(line);
            if (edge.Success)
            {
                edges.Add((number, edge.Groups[1].Value, edge.Groups[2].Value, "an inheritance"));
                continue;
            }

            var association = AssociationLine.Match(line);
            if (association.Success)
            {
                edges.Add((number, association.Groups[1].Value, association.Groups[2].Value, "an association"));
                continue;
            }

            var def = ClassDefLine.Match(line);
            if (def.Success)
            {
                if (!defined.Add(def.Groups[1].Value))
                {
                    problems.Add($"line {number}: classDef {def.Groups[1].Value} is declared twice");
                }

                continue;
            }

            problems.Add($"line {number}: not a recognised line");
        }

        if (inBlock)
        {
            problems.Add("a class block is never closed");
        }

        if (text.Count(c => c == '{') != text.Count(c => c == '}'))
        {
            problems.Add("unbalanced braces");
        }

        foreach (var (id, state) in classStates)
        {
            if (!defined.Contains(state))
            {
                problems.Add($"class {id} has state {state} with no classDef");
            }
        }

        foreach (var (line, fromId, toId, what) in edges)
        {
            if (!classIds.Contains(fromId) || !classIds.Contains(toId))
            {
                problems.Add($"line {line}: {what} edge names a class that is not declared");
            }
        }

        return problems;
    }
}
