using Vdiff.Core.Model;

namespace Vdiff.Tests.Contract;

/// <summary>
/// DiffPresence enforces what the draft-07 subset cannot express. Every row starts from the clean
/// ContractSamples.FullDiff and breaks exactly one rule, so the only reported problem must be that rule's.
/// A row never flips a state or appends an edge, which would also put a summary count off by one.
/// </summary>
[Trait("Category", "Contract")]
public class DiffPresenceTests
{
    static DiffFile MapType(DiffFile diff, string id, Func<TypeDiff, TypeDiff> change)
    {
        Assert.Single(diff.Types, t => t.Id == id);
        return diff with { Types = diff.Types.Select(t => t.Id == id ? change(t) : t).ToList() };
    }

    static DiffFile MapMember(DiffFile diff, string typeId, string key, Func<MemberDiff, MemberDiff> change) =>
        MapType(diff, typeId, t =>
        {
            Assert.Single(t.Members, m => m.Key == key);
            return t with { Members = t.Members.Select(m => m.Key == key ? change(m) : m).ToList() };
        });

    static DiffFile MapPackage(DiffFile diff, string state, Func<PackageDiff, PackageDiff> change)
    {
        Assert.Single(diff.Packages, p => p.State == state);
        return diff with { Packages = diff.Packages.Select(p => p.State == state ? change(p) : p).ToList() };
    }

    static DiffFile MapEdge(DiffFile diff, string state, Func<EdgeDiff, EdgeDiff> change)
    {
        Assert.Single(diff.Edges, e => e.State == state);
        return diff with { Edges = diff.Edges.Select(e => e.State == state ? change(e) : e).ToList() };
    }

    const string ModA = "class synthetic::ModA";
    const string ModB = "class Synthetic.ModB";
    const string Disc = "class synthetic::Disc";

    static readonly TypeRef AnyType = new(Languages.Cpp, "class synthetic::Other");

    // Row name -> (a diff that breaks one rule, the tag that rule's problem starts with).
    static readonly Dictionary<string, (Func<DiffFile, DiffFile> Break, string Rule)> Rows = new()
    {
        ["added_member_carrying_access_before"] =
            (d => MapMember(d, ModA, "show()", m => m with { AccessBefore = "public" }), "member-presence"),
        ["removed_member_carrying_signature_after"] =
            (d => MapMember(d, ModA, "paint()", m => m with { SignatureAfter = "void paint()" }), "member-presence"),
        ["modified_type_without_base_file"] =
            (d => MapType(d, ModA, t => t with { BaseFile = null }), "type-files"),
        ["renamed_from_on_a_modified_type"] =
            (d => MapType(d, ModB, t => t with { RenamedFrom = new RenameSource("class Synthetic.Old", "Synthetic.Old") }), "type-rename-fields"),
        ["renamed_from_key_on_an_added_member"] =
            (d => MapMember(d, ModA, "show()", m => m with { RenamedFromKey = "old()" }), "member-renamed-from-key"),
        ["summary_types_added_off_by_one"] =
            (d => d with { Summary = d.Summary with { Types = d.Summary.Types with { Added = d.Summary.Types.Added + 1 } } }, "count-types-added"),
        ["summary_members_body_changed_off_by_one"] =
            (d => d with { Summary = d.Summary with { Members = d.Summary.Members with { BodyChanged = d.Summary.Members.BodyChanged + 1 } } }, "count-members-body-changed"),
        ["summary_edges_modified_off_by_one"] =
            (d => d with { Summary = d.Summary with { Edges = d.Summary.Edges with { Modified = d.Summary.Edges.Modified + 1 } } }, "count-edges-modified"),
        ["listed_type_in_state_unchanged"] =
            (d => d with
            {
                Types = [.. d.Types, new TypeDiff(Languages.Cpp, "class synthetic::Same", "synthetic::Same", "class", States.Unchanged, "include/synthetic/same.h", "include/synthetic/same.h", null, null, new BaseChanges([], []), [])],
            }, "type-unchanged-listed"),
        ["listed_member_in_state_unchanged"] =
            (d => MapType(d, ModA, t => t with
            {
                Members = [.. t.Members, new MemberDiff("extra()", "extra", States.Unchanged, "public", "public", "void extra()", "void extra()", [], [], null)],
            }), "member-unchanged-listed"),
        ["score_milli_not_the_formula"] =
            (d => MapType(d, Disc, t => t with { Rename = t.Rename! with { ScoreMilli = t.Rename.ScoreMilli + 1 } }), "score-milli"),
        ["added_package_with_an_old_version"] =
            (d => MapPackage(d, "added", p => p with { OldVersion = "0.9.0" }), "package-versions"),
        ["updated_package_without_an_old_version"] =
            (d => MapPackage(d, "updated", p => p with { OldVersion = null }), "package-versions"),
        ["updated_package_without_a_new_version"] =
            (d => MapPackage(d, "updated", p => p with { NewVersion = null }), "package-versions"),
        ["removed_package_with_a_new_version"] =
            (d => MapPackage(d, "updated", p => p with { State = "removed" }), "package-versions"),
        ["base_edge_with_a_via"] =
            (d => MapEdge(d, States.Added, e => e with { Via = "member_" }), "edge-via"),
        ["association_edge_without_a_via"] =
            (d => MapEdge(d, States.Removed, e => e with { Via = null }), "edge-via"),
        ["added_edge_with_a_to_before"] =
            (d => MapEdge(d, States.Added, e => e with { ToBefore = AnyType }), "edge-to-before-state"),
        ["removed_edge_with_a_to_after"] =
            (d => MapEdge(d, States.Removed, e => e with { ToAfter = AnyType }), "edge-to-after-state"),
        ["modified_edge_with_neither_to_before_nor_to_after"] =
            (d => MapEdge(d, States.Modified, e => e with { ToBefore = null, ToAfter = null }), "edge-modified-needs-a-side"),
        ["modified_edge_with_equal_to_before_and_to_after"] =
            (d => MapEdge(d, States.Modified, e => e with { ToAfter = e.ToBefore }), "edge-modified-sides-equal"),
        ["modified_edge_with_a_to"] =
            (d => MapEdge(d, States.Modified, e => e with { To = AnyType }), "edge-modified-has-to"),
        ["modified_edge_sharing_an_identity_with_the_added_edge"] =
            (d =>
            {
                var added = Assert.Single(d.Edges, e => e.State == States.Added);
                return MapEdge(d, States.Modified, e => e with { From = added.From, ToName = added.ToName });
            }, "edge-shared-identity"),
    };

    public static TheoryData<string> RowNames() => [.. Rows.Keys];

    [Fact]
    public void The_full_and_the_empty_diff_report_no_problems()
    {
        Assert.Empty(DiffPresence.Problems(ContractSamples.FullDiff()));
        Assert.Empty(DiffPresence.Problems(ContractSamples.EmptyDiff()));
    }

    [Fact]
    public void A_modified_edge_with_one_side_unresolved_is_clean()
    {
        var full = ContractSamples.FullDiff();
        var modified = Assert.Single(full.Edges, e => e.State == States.Modified);
        Assert.NotNull(modified.ToBefore);
        Assert.NotNull(modified.ToAfter);

        var onlyBefore = MapEdge(full, States.Modified, e => e with { ToAfter = null });
        var onlyAfter = MapEdge(full, States.Modified, e => e with { ToBefore = null });

        Assert.Empty(DiffPresence.Problems(onlyBefore));
        Assert.Empty(DiffPresence.Problems(onlyAfter));
    }

    [Theory]
    [MemberData(nameof(RowNames))]
    public void Breaking_one_rule_reports_that_rule_and_nothing_else(string row)
    {
        var (breakRule, rule) = Rows[row];

        var problems = DiffPresence.Problems(breakRule(ContractSamples.FullDiff()));

        var only = Assert.Single(problems);
        Assert.StartsWith(rule + ":", only, StringComparison.Ordinal);
    }
}
