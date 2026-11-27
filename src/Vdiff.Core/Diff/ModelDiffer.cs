using System.Globalization;
using Vdiff.Core.Model;

namespace Vdiff.Core.Diff;

/// <summary>
/// Diffs two models into a <c>vdiff-diff/v1</c> diff (D-02, D-07). Types are matched by language and id
/// (D-17), so a <c>cpp</c> and a <c>csharp</c> type with the same id are two types that diff separately;
/// members are matched by key (name plus parameter text). A type or member only on the head side is
/// added, only on the base side is removed, and in both a changed access, signature or set of modifiers
/// (R-13) is a modified member. A changed parameter list is a different key, so it reads as one member
/// removed and one added; so does an overload or a cv-qualifier change. Unchanged types are counted,
/// not listed.
/// <para>
/// The v1 diff reports no type-level attribute change (D-20). A type matched by language and id is
/// modified only when its bases or its members differ; a change to its <c>access</c>, <c>modifiers</c>,
/// <c>partial</c> or <c>files</c> alone leaves it unchanged, counted and not listed, and
/// <see cref="TypeDiff"/> carries no before and after pair for them. Phase 5 may add type-level change
/// fields as optional properties inside v1.
/// </para>
/// <para>
/// Rename matching, body-changed members, edge diffing and package pins belong to Phase 5 (R-13 to
/// R-15). Until then <c>packages</c> and <c>edges</c> stay empty, the renamed and body_changed counts
/// and every edge count are zero, and <c>renamed_from</c> and <c>rename</c> are absent; the shape is
/// already v1 (D-02). Edge identity is (kind, from, to_name, via) (D-23); Phase 5 keys on it.
/// </para>
/// <para>
/// The result never depends on the order of the input: every list is ordered with a total ordinal
/// order (C39), and when two entries share a key the first in that order is the one compared. The
/// model builder already makes ids unique with a path suffix (<c>#path</c>, and <c>@n</c> for a repeat
/// inside one file, D-21), so that case only arises for hand-built models.
/// </para>
/// </summary>
public static class ModelDiffer
{
    public static DiffFile Diff(ModelFile baseModel, ModelFile headModel)
    {
        var baseTypes = Index(baseModel.Types, TypeKey, Canonical.Types);
        var headTypes = Index(headModel.Types, TypeKey, Canonical.Types);
        var keys = baseTypes.Keys.Concat(headTypes.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);

        var changed = new List<TypeDiff>();
        var unchanged = 0;
        foreach (var key in keys)
        {
            baseTypes.TryGetValue(key, out var before);
            headTypes.TryGetValue(key, out var after);
            var diff = DiffType(before, after);
            if (diff.State == States.Unchanged)
            {
                unchanged++;
            }
            else
            {
                changed.Add(diff);
            }
        }

        var members = changed.SelectMany(t => t.Members).ToList();
        var summary = new DiffSummary(
            new TypeCounts(
                changed.Count(t => t.State == States.Added),
                changed.Count(t => t.State == States.Removed),
                changed.Count(t => t.State == States.Modified),
                0,
                unchanged),
            new MemberCounts(
                members.Count(m => m.State == States.Added),
                members.Count(m => m.State == States.Removed),
                members.Count(m => m.State == States.Modified),
                0,
                0),
            new EdgeCounts(0, 0, 0, 0, 0),
            new UnmodelledCounts(baseModel.Unmodelled.Count, headModel.Unmodelled.Count));

        var warnings = new List<string>();
        AddWarnings(warnings, "base", baseModel);
        AddWarnings(warnings, "head", headModel);

        return Canonical.Canonicalise(new DiffFile(DiffFile.SchemaName, summary, warnings, [], changed, []));
    }

    static string TypeKey(TypeModel type) => type.Language + '\u0000' + type.Id;

    static TypeDiff DiffType(TypeModel? before, TypeModel? after)
    {
        if (before is null && after is not null)
        {
            return new TypeDiff(
                after.Language, after.Id, after.Fqn, after.Kind, States.Added, null, after.File, null, null,
                new BaseChanges(Sorted(after.Bases), []),
                after.Members.OrderBy(m => m, Canonical.Members).Select(Added).ToList());
        }

        if (before is not null && after is null)
        {
            return new TypeDiff(
                before.Language, before.Id, before.Fqn, before.Kind, States.Removed, before.File, null, null, null,
                new BaseChanges([], Sorted(before.Bases)),
                before.Members.OrderBy(m => m, Canonical.Members).Select(Removed).ToList());
        }

        var b = before!;
        var a = after!;
        var basesAdded = Sorted(a.Bases.Except(b.Bases, StringComparer.Ordinal));
        var basesRemoved = Sorted(b.Bases.Except(a.Bases, StringComparer.Ordinal));

        var beforeMembers = Index(b.Members, m => m.Key, Canonical.Members);
        var afterMembers = Index(a.Members, m => m.Key, Canonical.Members);
        var memberKeys = beforeMembers.Keys.Concat(afterMembers.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);
        var memberDiffs = new List<MemberDiff>();
        foreach (var key in memberKeys)
        {
            beforeMembers.TryGetValue(key, out var mb);
            afterMembers.TryGetValue(key, out var ma);
            if (mb is null)
            {
                memberDiffs.Add(Added(ma!));
            }
            else if (ma is null)
            {
                memberDiffs.Add(Removed(mb));
            }
            else if (!string.Equals(mb.Access, ma.Access, StringComparison.Ordinal)
                     || !string.Equals(mb.Signature, ma.Signature, StringComparison.Ordinal)
                     || !Sorted(mb.Modifiers).SequenceEqual(Sorted(ma.Modifiers), StringComparer.Ordinal))
            {
                // Every before and after pair is filled for a modified member, so a reader of one
                // never has to look at a second place for the unchanged half.
                memberDiffs.Add(new MemberDiff(
                    key, ma.Name, States.Modified, mb.Access, ma.Access, mb.Signature, ma.Signature,
                    Sorted(mb.Modifiers), Sorted(ma.Modifiers), null));
            }
        }

        // D-20: a type is modified by its bases or members only; its access, modifiers, partial and files are not compared.
        var state = basesAdded.Count > 0 || basesRemoved.Count > 0 || memberDiffs.Count > 0 ? States.Modified : States.Unchanged;
        return new TypeDiff(
            a.Language, a.Id, a.Fqn, a.Kind, state, b.File, a.File, null, null,
            new BaseChanges(basesAdded, basesRemoved), memberDiffs);
    }

    static MemberDiff Added(MemberModel m) =>
        new(m.Key, m.Name, States.Added, null, m.Access, null, m.Signature, null, Sorted(m.Modifiers), null);

    static MemberDiff Removed(MemberModel m) =>
        new(m.Key, m.Name, States.Removed, m.Access, null, m.Signature, null, Sorted(m.Modifiers), null, null);

    static List<string> Sorted(IEnumerable<string> values) =>
        values.OrderBy(v => v, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Per side, in a fixed order: files with parse errors, files not modelled, then each repeated
    /// definition. Nothing is written for a zero count. <see cref="Canonical"/> sorts the whole list ordinally.
    /// </summary>
    static void AddWarnings(List<string> warnings, string side, ModelFile model)
    {
        var withErrors = model.Files.Count(f => f.ErrorNodes > 0);
        if (withErrors > 0)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"{side}: {withErrors} file(s) with parse errors"));
        }

        if (model.Unmodelled.Count > 0)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"{side}: {model.Unmodelled.Count} file(s) not modelled"));
        }

        foreach (var duplicate in model.Duplicates.OrderBy(d => d, StringComparer.Ordinal))
        {
            warnings.Add($"{side}: duplicate {duplicate}");
        }
    }

    /// <summary>
    /// Keyed lookup in canonical order in which the first entry wins, so a repeated key can neither
    /// throw nor depend on the order of the input.
    /// </summary>
    static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key, IComparer<T> order)
    {
        var index = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items.OrderBy(i => i, order))
        {
            index.TryAdd(key(item), item);
        }

        return index;
    }
}
