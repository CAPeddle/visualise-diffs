using Vdiff.Core.Model;

namespace Vdiff.Tests.Contract;

/// <summary>
/// The diff rules the draft-07 keyword subset cannot express (D-15). Each problem is fixed text that starts
/// with a rule tag and a colon, then names the type id and member key, the package name, or the edge kind
/// and its from id; it never holds a file path, so the text is the same on every machine. The rules are the
/// ones docs/schema/README.md and the schema descriptions state; the type-level change that the diff does not
/// report (D-20) is not checked, because TypeDiff has none.
/// </summary>
public static class DiffPresence
{
    public static IReadOnlyList<string> Problems(DiffFile diff)
    {
        var problems = new List<string>();
        CheckTypes(diff, problems);
        CheckPackages(diff, problems);
        CheckEdges(diff, problems);
        CheckCounts(diff, problems);
        return problems;
    }

    static void CheckTypes(DiffFile diff, List<string> problems)
    {
        foreach (var type in diff.Types)
        {
            var who = $"type {type.Language} {type.Id}";
            if (type.State == States.Unchanged)
            {
                problems.Add($"type-unchanged-listed: {who} is listed in state unchanged; unchanged types are counted, never listed");
            }

            // base_file when the base side exists, head_file when the head side exists.
            if (type.State is States.Removed or States.Modified or States.Renamed && type.BaseFile is null)
            {
                problems.Add($"type-files: {who} in state {type.State} has no base_file");
            }

            if (type.State is States.Added or States.Modified or States.Renamed && type.HeadFile is null)
            {
                problems.Add($"type-files: {who} in state {type.State} has no head_file");
            }

            var renamed = type.State == States.Renamed;
            if (renamed != (type.RenamedFrom is not null))
            {
                problems.Add($"type-rename-fields: {who} in state {type.State}: renamed_from must be {Needed(renamed)}");
            }

            if (renamed != (type.Rename is not null))
            {
                problems.Add($"type-rename-fields: {who} in state {type.State}: rename must be {Needed(renamed)}");
            }

            if (type.Rename is { } rename && rename.ScoreMilli != ExpectedScore(rename))
            {
                problems.Add($"score-milli: {who} has score_milli {rename.ScoreMilli}, the formula gives {ExpectedScore(rename)}");
            }

            foreach (var member in type.Members)
            {
                CheckMember($"{who}, member {member.Key}", member, problems);
            }
        }
    }

    static string Needed(bool needed) => needed ? "present" : "absent";

    // 500 * shared / total (integer division, 0 when total is 0), plus 200, 200 and 100 for the three booleans.
    static int ExpectedScore(RenameScore rename)
    {
        var jaccard = rename.MemberJaccard.Total == 0 ? 0 : 500 * rename.MemberJaccard.Shared / rename.MemberJaccard.Total;
        return jaccard
            + (rename.SameFileOrRenamePair ? 200 : 0)
            + (rename.SameNamespace ? 200 : 0)
            + (rename.SameBases ? 100 : 0);
    }

    static void CheckMember(string who, MemberDiff member, List<string> problems)
    {
        if (member.State == States.Unchanged)
        {
            problems.Add($"member-unchanged-listed: {who} is listed in state unchanged; unchanged members are never listed");
        }

        var baseSide = member.State != States.Added;
        var headSide = member.State != States.Removed;
        CheckSide(who, member.State, "before", baseSide, member.AccessBefore is not null, member.SignatureBefore is not null, member.ModifiersBefore is not null, problems);
        CheckSide(who, member.State, "after", headSide, member.AccessAfter is not null, member.SignatureAfter is not null, member.ModifiersAfter is not null, problems);

        var renamed = member.State == States.Renamed;
        if (renamed != (member.RenamedFromKey is not null))
        {
            problems.Add($"member-renamed-from-key: {who} in state {member.State}: renamed_from_key must be {Needed(renamed)}");
        }
    }

    static void CheckSide(string who, string state, string side, bool exists, bool access, bool signature, bool modifiers, List<string> problems)
    {
        foreach (var (field, present) in new[] { ("access", access), ("signature", signature), ("modifiers", modifiers) })
        {
            if (present != exists)
            {
                problems.Add($"member-presence: {who} in state {state}: {field}_{side} must be {Needed(exists)}");
            }
        }
    }

    static void CheckPackages(DiffFile diff, List<string> problems)
    {
        foreach (var package in diff.Packages)
        {
            var who = $"package {package.Name}";
            var oldExpected = package.State is "updated" or "removed";
            var newExpected = package.State is "updated" or "added";
            if (oldExpected != (package.OldVersion is not null))
            {
                problems.Add($"package-versions: {who} in state {package.State}: old_version must be {Needed(oldExpected)}");
            }

            if (newExpected != (package.NewVersion is not null))
            {
                problems.Add($"package-versions: {who} in state {package.State}: new_version must be {Needed(newExpected)}");
            }
        }
    }

    static void CheckEdges(DiffFile diff, List<string> problems)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in diff.Edges)
        {
            var who = $"edge {edge.Kind} from {edge.From.Language} {edge.From.Id}";
            if (edge.State == "unchanged")
            {
                problems.Add($"edge-unchanged-listed: {who} is listed in state unchanged; unchanged edges are counted, never listed");
            }

            var association = edge.Kind == EdgeKinds.Association;
            if (association != (edge.Via is not null))
            {
                problems.Add($"edge-via: {who}: via must be {Needed(association)}");
            }

            var modified = edge.State == States.Modified;
            if (!modified && edge.ToBefore is not null)
            {
                problems.Add($"edge-to-before-state: {who} in state {edge.State} carries to_before");
            }

            if (!modified && edge.ToAfter is not null)
            {
                problems.Add($"edge-to-after-state: {who} in state {edge.State} carries to_after");
            }

            if (modified)
            {
                if (edge.ToBefore is null && edge.ToAfter is null)
                {
                    problems.Add($"edge-modified-needs-a-side: {who} is modified and carries neither to_before nor to_after");
                }

                if (edge.ToBefore is not null && edge.ToAfter is not null && edge.ToBefore == edge.ToAfter)
                {
                    problems.Add($"edge-modified-sides-equal: {who} is modified and to_before equals to_after");
                }

                if (edge.To is not null)
                {
                    problems.Add($"edge-modified-has-to: {who} is modified and carries a to");
                }
            }

            // Identity is kind, from, to_name and via; the resolved to is not part of it (D-23).
            var identity = string.Join('\u001f', edge.Kind, edge.From.Language, edge.From.Id, edge.ToName, edge.Via ?? "\u0000");
            if (!identities.Add(identity))
            {
                problems.Add($"edge-shared-identity: {who} shares its identity (kind, from, to_name, via) with an earlier listed edge");
            }
        }
    }

    static void CheckCounts(DiffFile diff, List<string> problems)
    {
        int Types(string state) => diff.Types.Count(t => t.State == state);
        int Members(string state) => diff.Types.Sum(t => t.Members.Count(m => m.State == state));
        int Edges(string state) => diff.Edges.Count(e => e.State == state);

        var types = diff.Summary.Types;
        Count("types-added", types.Added, Types(States.Added), problems);
        Count("types-removed", types.Removed, Types(States.Removed), problems);
        Count("types-modified", types.Modified, Types(States.Modified), problems);
        Count("types-renamed", types.Renamed, Types(States.Renamed), problems);

        var members = diff.Summary.Members;
        Count("members-added", members.Added, Members(States.Added), problems);
        Count("members-removed", members.Removed, Members(States.Removed), problems);
        Count("members-modified", members.Modified, Members(States.Modified), problems);
        Count("members-renamed", members.Renamed, Members(States.Renamed), problems);
        Count("members-body-changed", members.BodyChanged, Members(States.BodyChanged), problems);

        var edges = diff.Summary.Edges;
        Count("edges-added", edges.Added, Edges(States.Added), problems);
        Count("edges-removed", edges.Removed, Edges(States.Removed), problems);
        Count("edges-modified", edges.Modified, Edges(States.Modified), problems);
        Count("edges-renamed", edges.Renamed, Edges(States.Renamed), problems);
    }

    static void Count(string name, int summary, int listed, List<string> problems)
    {
        if (summary != listed)
        {
            problems.Add($"count-{name}: summary says {summary}, {listed} listed");
        }
    }
}
