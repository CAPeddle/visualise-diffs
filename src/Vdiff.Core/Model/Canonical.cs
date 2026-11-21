using System.Text.Json;
using Vdiff.Core.Output;

namespace Vdiff.Core.Model;

/// <summary>
/// The one module that orders every collection of the model, the diff and the run meta (R-23). The
/// builder, the pipeline and the serialiser all call it, so serialised bytes never depend on input
/// order. Ordering is a stable <c>OrderBy</c> with ordinal comparers, never <c>List.Sort</c>, and every
/// comparer is total: on a full tie it ends with an ordinal compare of the element serialised with the
/// shared options (nested collections are canonicalised first, so that tie-break is order-free too).
/// </summary>
public static class Canonical
{
    public static readonly Comparer<TypeModel> Types = Comparer<TypeModel>.Create(CompareTypes);
    public static readonly Comparer<MemberModel> Members = Comparer<MemberModel>.Create(CompareMembers);
    public static readonly Comparer<EdgeModel> Edges = Comparer<EdgeModel>.Create(CompareEdges);

    public static ModelFile Canonicalise(ModelFile model) => model with
    {
        Extraction = new ExtractionInfo(
            model.Extraction.Languages
                .Select(l => l with { MacrosBlanked = SortedValues(l.MacrosBlanked) })
                .OrderBy(l => l, Comparer<LanguageExtraction>.Create(CompareLanguages))
                .ToList()),
        Files = model.Files.OrderBy(f => f, Comparer<ModelFileEntry>.Create(CompareFiles)).ToList(),
        Unmodelled = model.Unmodelled.OrderBy(u => u, Comparer<UnmodelledEntry>.Create(CompareUnmodelled)).ToList(),
        Types = model.Types.Select(CanonicaliseType).OrderBy(t => t, Types).ToList(),
        Edges = model.Edges.OrderBy(e => e, Edges).ToList(),
        Duplicates = SortedValues(model.Duplicates),
    };

    /// <summary>
    /// Today's diff collections: types by id, members by key, <c>bases.added</c> and <c>bases.removed</c> by
    /// value. Warnings keep the order given. Plan 02-12 extends this to the v1 diff rows.
    /// </summary>
    public static DiffFile Canonicalise(DiffFile diff) => diff with
    {
        Types = diff.Types
            .Select(t => t with
            {
                Bases = new BaseChanges(SortedValues(t.Bases.Added), SortedValues(t.Bases.Removed)),
                Members = t.Members.OrderBy(m => m.Key, StringComparer.Ordinal).ToList(),
            })
            .OrderBy(t => t.Language, StringComparer.Ordinal)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList(),
    };

    public static RunMeta Canonicalise(RunMeta meta) => meta with
    {
        Extractors = meta.Extractors
            .Select(e => e with
            {
                Tools = e.Tools
                    .OrderBy(t => t.Tool, StringComparer.Ordinal)
                    .ThenBy(t => t.Version, StringComparer.Ordinal)
                    .ToList(),
            })
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ThenBy(e => JsonSerializer.Serialize(e, OutputWriter.JsonOptions), StringComparer.Ordinal)
            .ToList(),
    };

    static List<string> SortedValues(IEnumerable<string> values) =>
        values.OrderBy(v => v, StringComparer.Ordinal).ToList();

    static TypeModel CanonicaliseType(TypeModel type) => type with
    {
        Files = type.Files is null ? null : SortedValues(type.Files),
        Modifiers = SortedValues(type.Modifiers),
        Bases = SortedValues(type.Bases),
        Members = type.Members
            .Select(CanonicaliseMember)
            .OrderBy(m => m, Members)
            .ToList(),
    };

    static MemberModel CanonicaliseMember(MemberModel member) => member with { Modifiers = SortedValues(member.Modifiers) };

    static int CompareTypes(TypeModel x, TypeModel y)
    {
        var c = string.CompareOrdinal(x.Language, y.Language);
        c = c != 0 ? c : string.CompareOrdinal(x.Id, y.Id);
        c = c != 0 ? c : string.CompareOrdinal(x.File, y.File);
        c = c != 0 ? c : x.Span.StartByte.CompareTo(y.Span.StartByte);
        return c != 0 ? c : Tie(CanonicaliseType(x), CanonicaliseType(y));
    }

    static int CompareMembers(MemberModel x, MemberModel y)
    {
        var c = string.CompareOrdinal(x.Key, y.Key);
        c = c != 0 ? c : x.Span.StartByte.CompareTo(y.Span.StartByte);
        c = c != 0 ? c : x.Span.EndByte.CompareTo(y.Span.EndByte);
        return c != 0 ? c : Tie(CanonicaliseMember(x), CanonicaliseMember(y));
    }

    /// <summary>
    /// The edge identity first (D-23: kind, from, to_name, via with an absent via first), then the resolved
    /// target (absent first), which is not part of the identity.
    /// </summary>
    static int CompareEdges(EdgeModel x, EdgeModel y)
    {
        var c = string.CompareOrdinal(x.Kind, y.Kind);
        c = c != 0 ? c : string.CompareOrdinal(x.From.Language, y.From.Language);
        c = c != 0 ? c : string.CompareOrdinal(x.From.Id, y.From.Id);
        c = c != 0 ? c : string.CompareOrdinal(x.ToName, y.ToName);
        c = c != 0 ? c : CompareAbsentFirst(x.Via, y.Via);
        c = c != 0 ? c : CompareAbsentFirst(x.To?.Language, y.To?.Language);
        c = c != 0 ? c : CompareAbsentFirst(x.To?.Id, y.To?.Id);
        return c != 0 ? c : Tie(x, y);
    }

    static int CompareLanguages(LanguageExtraction x, LanguageExtraction y)
    {
        var c = string.CompareOrdinal(x.Language, y.Language);
        return c != 0 ? c : Tie(x, y);
    }

    static int CompareFiles(ModelFileEntry x, ModelFileEntry y)
    {
        var c = string.CompareOrdinal(x.Path, y.Path);
        c = c != 0 ? c : string.CompareOrdinal(x.Language, y.Language);
        return c != 0 ? c : Tie(x, y);
    }

    static int CompareUnmodelled(UnmodelledEntry x, UnmodelledEntry y)
    {
        var c = string.CompareOrdinal(x.Path, y.Path);
        c = c != 0 ? c : string.CompareOrdinal(x.Reason, y.Reason);
        return c != 0 ? c : Tie(x, y);
    }

    static int CompareAbsentFirst(string? x, string? y) =>
        x is null ? (y is null ? 0 : -1) : (y is null ? 1 : string.CompareOrdinal(x, y));

    /// <summary>The final tie-break: the two elements serialised with the shared options, compared ordinally.</summary>
    static int Tie<T>(T x, T y) =>
        string.CompareOrdinal(
            JsonSerializer.Serialize(x, OutputWriter.JsonOptions), JsonSerializer.Serialize(y, OutputWriter.JsonOptions));
}
