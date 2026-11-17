using System.Globalization;
using Vdiff.Core.Model;

namespace Vdiff.Core.Diff;

/// <summary>
/// Diffs two models (D-07): types are matched by id, members by key. A type or member only on
/// the head side is added, only on the base side is removed, and in both a changed access or
/// signature (or a changed set of bases) is modified. Unchanged types are counted, not listed.
/// Everything is ordinal-sorted so the output is deterministic.
/// </summary>
public static class ModelDiffer
{
    public static DiffFile Diff(ModelFile baseModel, ModelFile headModel)
    {
        var baseTypes = Index(baseModel.Types, t => t.Id);
        var headTypes = Index(headModel.Types, t => t.Id);
        var ids = baseTypes.Keys.Concat(headTypes.Keys).Distinct(StringComparer.Ordinal).OrderBy(i => i, StringComparer.Ordinal);

        var changed = new List<TypeDiff>();
        var unchanged = 0;
        foreach (var id in ids)
        {
            baseTypes.TryGetValue(id, out var before);
            headTypes.TryGetValue(id, out var after);
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
                unchanged),
            new MemberCounts(
                members.Count(m => m.State == States.Added),
                members.Count(m => m.State == States.Removed),
                members.Count(m => m.State == States.Modified)),
            new UnmodelledCounts(baseModel.Unmodelled.Count, headModel.Unmodelled.Count));

        var warnings = new List<string>();
        AddWarning(warnings, "base", baseModel);
        AddWarning(warnings, "head", headModel);

        return new DiffFile(
            DiffFile.SchemaName,
            baseModel.Commit,
            headModel.Commit,
            summary,
            warnings.OrderBy(w => w, StringComparer.Ordinal).ToList(),
            changed);
    }

    static TypeDiff DiffType(TypeModel? before, TypeModel? after)
    {
        if (before is null && after is not null)
        {
            return new TypeDiff(
                after.Id, after.Fqn, after.Kind, States.Added, null, after.File,
                new BaseChanges(Sorted(after.Bases), []),
                after.Members.Select(Added).ToList());
        }

        if (before is not null && after is null)
        {
            return new TypeDiff(
                before.Id, before.Fqn, before.Kind, States.Removed, before.File, null,
                new BaseChanges([], Sorted(before.Bases)),
                before.Members.Select(Removed).ToList());
        }

        var b = before!;
        var a = after!;
        var basesAdded = Sorted(a.Bases.Except(b.Bases, StringComparer.Ordinal));
        var basesRemoved = Sorted(b.Bases.Except(a.Bases, StringComparer.Ordinal));

        var beforeMembers = Index(b.Members, m => m.Key);
        var afterMembers = Index(a.Members, m => m.Key);
        var keys = beforeMembers.Keys.Concat(afterMembers.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);
        var memberDiffs = new List<MemberDiff>();
        foreach (var key in keys)
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
                     || !string.Equals(mb.Signature, ma.Signature, StringComparison.Ordinal))
            {
                memberDiffs.Add(new MemberDiff(key, ma.Name, States.Modified, mb.Access, ma.Access, mb.Signature, ma.Signature));
            }
        }

        var state = basesAdded.Count > 0 || basesRemoved.Count > 0 || memberDiffs.Count > 0 ? States.Modified : States.Unchanged;
        return new TypeDiff(a.Id, a.Fqn, a.Kind, state, b.File, a.File, new BaseChanges(basesAdded, basesRemoved), memberDiffs);
    }

    static MemberDiff Added(MemberModel m) =>
        new(m.Key, m.Name, States.Added, null, m.Access, null, m.Signature);

    static MemberDiff Removed(MemberModel m) =>
        new(m.Key, m.Name, States.Removed, m.Access, null, m.Signature, null);

    static List<string> Sorted(IEnumerable<string> values) =>
        values.OrderBy(v => v, StringComparer.Ordinal).ToList();

    static void AddWarning(List<string> warnings, string side, ModelFile model)
    {
        var withErrors = model.Files.Count(f => f.ErrorNodes > 0);
        var notModelled = model.Unmodelled.Count;
        if (withErrors > 0 || notModelled > 0)
        {
            warnings.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{side}: {withErrors} file(s) with parse errors, {notModelled} file(s) not modelled"));
        }
    }

    /// <summary>Keyed lookup in which the first entry wins, so a repeated key can never throw.</summary>
    static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var index = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            index.TryAdd(key(item), item);
        }

        return index;
    }
}
