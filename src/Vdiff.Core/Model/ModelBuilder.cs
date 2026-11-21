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

    /// <param name="response">The sidecar's checked answer for that commit's in-scope files.</param>
    /// <param name="unmodelled">Files the read side could not model (too large, missing object and so on).</param>
    /// <param name="macros">The macro names requested for blanking; recorded together with what the sidecar reports.</param>
    public static ModelFile Build(
        SidecarResponse response, IReadOnlyList<UnmodelledFile> unmodelled, IReadOnlyList<string> macros)
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

        // Repeated ids (D-21, T-01-17): a repeated definition never crashes a run. An id that occurs once
        // stays plain. Once it occurs more than once, every definition of it carries "#<path>", and the
        // second and later definitions inside one file add "@2", "@3" in source order. The suffix names a
        // file, never an ordinal counted across files, so adding an unrelated file renumbers nothing.
        var typeIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in parsed)
        {
            foreach (var type in file.Types)
            {
                var plain = type.Kind + " " + Fqn(type);
                typeIdCounts[plain] = typeIdCounts.GetValueOrDefault(plain) + 1;
            }
        }

        var memberDuplicates = new List<string>();
        var types = new List<TypeModel>();
        foreach (var file in parsed)
        {
            var seenInFile = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var type in file.Types
                         .OrderBy(t => t.StartByte)
                         .ThenBy(t => t.EndByte)
                         .ThenBy(t => t.Kind, StringComparer.Ordinal)
                         .ThenBy(t => t.Scope, StringComparer.Ordinal)
                         .ThenBy(t => t.Name, StringComparer.Ordinal))
            {
                var fqn = Fqn(type);
                var id = Suffixed(type.Kind + " " + fqn, typeIdCounts, seenInFile, file.Path);
                types.Add(new TypeModel(
                    Languages.Cpp, id, fqn, type.Kind, type.Name, type.Scope, null, false, file.Path, null, [],
                    [.. type.Bases],
                    new SourceSpan(type.StartByte, type.EndByte, type.StartLine, type.EndLine),
                    BuildMembers(type, id, file.Path, memberDuplicates)));
            }
        }

        var duplicates = typeIdCounts
            .Where(pair => pair.Value > 1)
            .Select(pair => Describe(pair.Key, pair.Value))
            .Concat(memberDuplicates)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        return new ModelFile(
            ModelFile.SchemaName,
            new ExtractionInfo(
            [
                new LanguageExtraction(
                    Languages.Cpp,
                    "cpp-ts",
                    Modes.SyntaxOnly,
                    null,
                    null,
                    response.MacrosBlanked.Concat(macros).Distinct(StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList()),
            ]),
            parsed.Select(f => new ModelFileEntry(f.Path, Languages.Cpp, Modes.SyntaxOnly, null, f.ErrorNodes)).ToList(),
            skipped.OrderBy(u => u.Path, StringComparer.Ordinal).ThenBy(u => u.Reason, StringComparer.Ordinal).ToList(),
            types
                .OrderBy(t => t.Id, StringComparer.Ordinal)
                .ThenBy(t => t.File, StringComparer.Ordinal)
                .ThenBy(t => t.Span.StartByte)
                .ToList(),
            [],
            duplicates);
    }

    static string Fqn(SidecarType type) => type.Scope.Length == 0 ? type.Name : type.Scope + "::" + type.Name;

    static List<MemberModel> BuildMembers(SidecarType type, string typeId, string file, List<string> duplicates)
    {
        // Member keys follow the same rule as type ids, with the path of the file that declares the member
        // (for the tracer, always the type's file).
        var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var member in type.Members)
        {
            keyCounts[member.Key] = keyCounts.GetValueOrDefault(member.Key) + 1;
        }

        var seenInFile = new Dictionary<string, int>(StringComparer.Ordinal);
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
                Suffixed(member.Key, keyCounts, seenInFile, file), member.Name, member.Kind, member.Access, [], member.Signature,
                new SourceSpan(member.StartByte, member.EndByte, member.StartLine, member.EndLine)));
        }

        foreach (var (key, count) in keyCounts.Where(pair => pair.Value > 1))
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

    /// <summary>
    /// D-21: <paramref name="name"/> itself when it occurs once; otherwise <c>name#path</c>, with <c>@2</c>,
    /// <c>@3</c> added from the second occurrence inside the same file.
    /// </summary>
    static string Suffixed(string name, Dictionary<string, int> counts, Dictionary<string, int> seenInFile, string file)
    {
        if (counts[name] == 1)
        {
            return name;
        }

        var n = seenInFile[name] = seenInFile.GetValueOrDefault(name) + 1;
        return name + "#" + file + (n == 1 ? "" : "@" + n.ToString(CultureInfo.InvariantCulture));
    }
}
