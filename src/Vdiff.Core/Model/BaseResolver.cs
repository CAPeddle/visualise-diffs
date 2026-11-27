using System.Text;

namespace Vdiff.Core.Model;

/// <summary>
/// Resolves a written base name to a modelled type for the syntax-only C++ extractors (D-16): the
/// tracer and the tree-sitter sidecar. This is their resolution rule, not a frozen or language-universal
/// algorithm. The frozen schema fixes only the edge shape (<c>from</c>, optional <c>to</c>,
/// <c>to_name</c>); a semantic extractor (the Roslyn C# extractor, Phase 4) sets <c>to</c> to the
/// resolved symbol's full id instead.
/// </summary>
public static class BaseResolver
{
    /// <summary>A written base without template arguments (balanced angle brackets), a leading <c>::</c> and surrounding space.</summary>
    public static string Strip(string written)
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
    /// The type <paramref name="written"/> names, among the candidates of <paramref name="from"/>'s language:
    /// the one type whose fqn equals the stripped name, or, when none does, the one type whose fqn ends in
    /// <c>::</c> plus the stripped name. Anything else (no match, several matches, or the type itself) is null.
    /// </summary>
    public static TypeRef? Resolve(string written, TypeModel from, IReadOnlyList<TypeModel> candidates)
    {
        var name = Strip(written);
        if (name.Length == 0)
        {
            return null;
        }

        var sameLanguage = candidates.Where(c => string.Equals(c.Language, from.Language, StringComparison.Ordinal));
        var exact = sameLanguage.Where(c => string.Equals(c.Fqn, name, StringComparison.Ordinal)).Take(2).ToList();
        TypeModel? match;
        if (exact.Count > 0)
        {
            match = exact.Count == 1 ? exact[0] : null;
        }
        else
        {
            var suffix = "::" + name;
            var bySuffix = sameLanguage.Where(c => c.Fqn.EndsWith(suffix, StringComparison.Ordinal)).Take(2).ToList();
            match = bySuffix.Count == 1 ? bySuffix[0] : null;
        }

        if (match is null || string.Equals(match.Id, from.Id, StringComparison.Ordinal))
        {
            return null;
        }

        return new TypeRef(match.Language, match.Id);
    }
}
