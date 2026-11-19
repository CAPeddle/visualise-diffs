using System.Globalization;
using Vdiff.Core.Extraction;
using Vdiff.Core.Git;

namespace Vdiff.Core.Model;

/// <summary>
/// Turns one sidecar answer into a model file. Everything is ordered with stable
/// <c>OrderBy</c> and total ordinal comparers, never <c>List.Sort</c> (C39, C22), so the same
/// input always gives the same bytes.
/// </summary>
public static class ModelBuilder
{
    public const string ReasonParseTimeout = "parse-timeout";
    public const string ReasonParseError = "parse-error";

    /// <param name="commit">The full commit id this model describes.</param>
    /// <param name="response">The sidecar's checked answer for that commit's in-scope files.</param>
    /// <param name="unmodelled">Files the read side could not model (too large, missing object and so on).</param>
    /// <param name="macros">The macro names requested for blanking; recorded together with what the sidecar reports.</param>
    public static ModelFile Build(
        string commit, SidecarResponse response, IReadOnlyList<UnmodelledFile> unmodelled, IReadOnlyList<string> macros)
    {
        var skipped = unmodelled.Select(u => new UnmodelledEntry(u.Path, u.Reason)).ToList();
        var parsed = new List<SidecarFile>();
        foreach (var file in response.Files ?? [])
        {
            // A file the sidecar gave up on is never presented as modelled (C1c, D-11).
            switch (file.Status)
            {
                case "ok":
                    parsed.Add(file);
                    break;
                case "timeout":
                    skipped.Add(new UnmodelledEntry(file.Path, ReasonParseTimeout));
                    break;
                default:
                    skipped.Add(new UnmodelledEntry(file.Path, ReasonParseError));
                    break;
            }
        }

        parsed = parsed.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();

        // Unique ids in source order (file ordinal, then start byte), so repeated definitions never
        // crash a run: the first keeps the plain id, later ones get #2, #3 (T-01-17).
        var seenTypeIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var memberDuplicates = new List<string>();
        var types = new List<TypeModel>();
        foreach (var file in parsed)
        {
            foreach (var type in file.Types
                         .OrderBy(t => t.StartByte)
                         .ThenBy(t => t.EndByte)
                         .ThenBy(t => t.Kind, StringComparer.Ordinal)
                         .ThenBy(t => t.Scope, StringComparer.Ordinal)
                         .ThenBy(t => t.Name, StringComparer.Ordinal))
            {
                var fqn = type.Scope.Length == 0 ? type.Name : type.Scope + "::" + type.Name;
                var id = Unique(seenTypeIds, type.Kind + " " + fqn);
                types.Add(new TypeModel(
                    id, fqn, type.Kind, type.Name, type.Scope, file.Path, [.. type.Bases],
                    new SourceSpan(type.StartByte, type.EndByte, type.StartLine, type.EndLine),
                    BuildMembers(type, id, memberDuplicates)));
            }
        }

        var duplicates = seenTypeIds
            .Where(pair => pair.Value > 1)
            .Select(pair => Describe(pair.Key, pair.Value))
            .Concat(memberDuplicates)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        return new ModelFile(
            ModelFile.SchemaName,
            commit,
            new ExtractionInfo(
                "cpp-ts",
                "syntax-only",
                response.TreeSitter,
                response.TreeSitterCpp,
                response.MacrosBlanked.Concat(macros).Distinct(StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList()),
            parsed.Select(f => new ModelFileEntry(f.Path, f.ErrorNodes)).ToList(),
            skipped.OrderBy(u => u.Path, StringComparer.Ordinal).ThenBy(u => u.Reason, StringComparer.Ordinal).ToList(),
            types
                .OrderBy(t => t.Id, StringComparer.Ordinal)
                .ThenBy(t => t.File, StringComparer.Ordinal)
                .ThenBy(t => t.Span.StartByte)
                .ToList())
        {
            Duplicates = duplicates,
        };
    }

    static List<MemberModel> BuildMembers(SidecarType type, string typeId, List<string> duplicates)
    {
        var seenKeys = new Dictionary<string, int>(StringComparer.Ordinal);
        var members = new List<MemberModel>();
        // Total order, so two members that start at the same byte are never left to input order (C39).
        foreach (var member in type.Members
                     .OrderBy(m => m.StartByte)
                     .ThenBy(m => m.EndByte)
                     .ThenBy(m => m.Key, StringComparer.Ordinal)
                     .ThenBy(m => m.Signature, StringComparer.Ordinal)
                     .ThenBy(m => m.Kind, StringComparer.Ordinal)
                     .ThenBy(m => m.Access, StringComparer.Ordinal))
        {
            members.Add(new MemberModel(
                Unique(seenKeys, member.Key), member.Name, member.Kind, member.Access, member.Signature,
                new SourceSpan(member.StartByte, member.EndByte, member.StartLine, member.EndLine)));
        }

        foreach (var (key, count) in seenKeys.Where(pair => pair.Value > 1))
        {
            duplicates.Add("member " + Describe(key, count, typeId));
        }

        return members
            .OrderBy(m => m.Key, StringComparer.Ordinal)
            .ThenBy(m => m.Span.StartByte)
            .ToList();
    }

    /// <summary>One <c>duplicates</c> entry: <c>class shop::X (2 definitions)</c>, or a member of a named type.</summary>
    static string Describe(string name, int count, string? ownerId = null) =>
        (ownerId is null ? name : name + " of " + ownerId)
        + " (" + count.ToString(CultureInfo.InvariantCulture) + " definitions)";

    /// <summary>The first use of a name is the name itself; later uses get <c>#2</c>, <c>#3</c>.</summary>
    static string Unique(Dictionary<string, int> seen, string name)
    {
        seen[name] = seen.GetValueOrDefault(name) + 1;
        var count = seen[name];
        return count == 1 ? name : name + "#" + count.ToString(CultureInfo.InvariantCulture);
    }
}
