using System.Globalization;
using Vdiff.Core.Model;

namespace Vdiff.Core.Diff;

/// <summary>
/// Diffs two models (D-07): types are matched by id (kind plus fully qualified name), members by
/// key (name plus parameter text). A type or member only on the head side is added, only on the
/// base side is removed, and in both a changed access or signature (or a changed set of bases) is
/// modified. A changed parameter list is a different key, so it reads as one member removed and
/// one added; so does an overload or a cv-qualifier change. Unchanged types are counted, not listed.
/// <para>
/// The result never depends on the order of the input: every list is ordered with a total ordinal
/// order (C39), and when two entries share an id or a key the first in that order is the one
/// compared. The model builder already makes ids unique with #n suffixes, so that case only
/// arises for hand-built models.
/// </para>
/// </summary>
public static class ModelDiffer
{
    public static DiffFile Diff(ModelFile baseModel, ModelFile headModel)
    {
        var baseTypes = Index(baseModel.Types, t => t.Id, CompareTypes);
        var headTypes = Index(headModel.Types, t => t.Id, CompareTypes);
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
        AddWarnings(warnings, "base", baseModel);
        AddWarnings(warnings, "head", headModel);

        return new DiffFile(DiffFile.SchemaName, summary, warnings, changed);
    }

    static TypeDiff DiffType(TypeModel? before, TypeModel? after)
    {
        if (before is null && after is not null)
        {
            return new TypeDiff(
                after.Id, after.Fqn, after.Kind, States.Added, null, after.File,
                new BaseChanges(Sorted(after.Bases), []),
                after.Members.OrderBy(m => m, MemberOrder).Select(Added).ToList());
        }

        if (before is not null && after is null)
        {
            return new TypeDiff(
                before.Id, before.Fqn, before.Kind, States.Removed, before.File, null,
                new BaseChanges([], Sorted(before.Bases)),
                before.Members.OrderBy(m => m, MemberOrder).Select(Removed).ToList());
        }

        var b = before!;
        var a = after!;
        var basesAdded = Sorted(a.Bases.Except(b.Bases, StringComparer.Ordinal));
        var basesRemoved = Sorted(b.Bases.Except(a.Bases, StringComparer.Ordinal));

        var beforeMembers = Index(b.Members, m => m.Key, CompareMembers);
        var afterMembers = Index(a.Members, m => m.Key, CompareMembers);
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
                // Both pairs are always filled, so a reader of one modified member never has to look
                // at a second place for the unchanged half (the report and the diagram both read the
                // *_after values of any member that still exists).
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

    /// <summary>
    /// Per side, in a fixed order: files with parse errors, files not modelled, then each repeated
    /// definition. Nothing is written for a zero count.
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
    static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key, Comparison<T> order)
    {
        var index = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items.OrderBy(i => i, Comparer<T>.Create(order)))
        {
            index.TryAdd(key(item), item);
        }

        return index;
    }

    static readonly Comparer<MemberModel> MemberOrder = Comparer<MemberModel>.Create(CompareMembers);

    /// <summary>
    /// Total order on members: key, then start byte, end byte and the remaining fields, all ordinal.
    /// Two members that compare equal here are identical in everything the diff reads.
    /// </summary>
    static int CompareMembers(MemberModel x, MemberModel y)
    {
        var c = string.CompareOrdinal(x.Key, y.Key);
        c = c != 0 ? c : x.Span.StartByte.CompareTo(y.Span.StartByte);
        c = c != 0 ? c : x.Span.EndByte.CompareTo(y.Span.EndByte);
        c = c != 0 ? c : string.CompareOrdinal(x.Signature, y.Signature);
        c = c != 0 ? c : string.CompareOrdinal(x.Access, y.Access);
        return c != 0 ? c : string.CompareOrdinal(x.Kind, y.Kind);
    }

    /// <summary>Total order on types: id, then file, start byte, end byte, bases and member keys, all ordinal.</summary>
    static int CompareTypes(TypeModel x, TypeModel y)
    {
        var c = string.CompareOrdinal(x.Id, y.Id);
        c = c != 0 ? c : string.CompareOrdinal(x.File, y.File);
        c = c != 0 ? c : x.Span.StartByte.CompareTo(y.Span.StartByte);
        c = c != 0 ? c : x.Span.EndByte.CompareTo(y.Span.EndByte);
        c = c != 0 ? c : string.CompareOrdinal(Joined(x.Bases.OrderBy(v => v, StringComparer.Ordinal)), Joined(y.Bases.OrderBy(v => v, StringComparer.Ordinal)));
        return c != 0 ? c : string.CompareOrdinal(Joined(x.Members.OrderBy(m => m, MemberOrder).Select(Fingerprint)), Joined(y.Members.OrderBy(m => m, MemberOrder).Select(Fingerprint)));
    }

    static string Joined(IEnumerable<string> values) => string.Join('\u0001', values);

    static string Fingerprint(MemberModel m) => m.Key + '\u0002' + m.Signature + '\u0002' + m.Access;
}
