using Vdiff.Core.Diff;
using Vdiff.Core.Model;
using Vdiff.Core.Output;

namespace Vdiff.Tests.Diff;

/// <summary>The differ on hand-built models: no repository, no sidecar, no process (D-07, TRC-02).</summary>
public class ModelDifferTests
{
    static MemberModel M(string key, string access = "public", string? signature = null, int start = 0)
    {
        var name = key.Contains('(') ? key[..key.IndexOf('(', StringComparison.Ordinal)] : key;
        return new MemberModel(key, name, key.Contains('(') ? "method" : "field", access, [], signature ?? "int " + key,
            new SourceSpan(start, start + 5, 1, 1));
    }

    static TypeModel T(string fqn, string kind = "class", string file = "a.h", int start = 0, string[]? bases = null, IReadOnlyList<MemberModel>? members = null)
    {
        var cut = fqn.LastIndexOf("::", StringComparison.Ordinal);
        return new TypeModel(
            Languages.Cpp, kind + " " + fqn, fqn, kind, cut < 0 ? fqn : fqn[(cut + 2)..], cut < 0 ? "" : fqn[..cut],
            null, false, file, null, [], bases ?? [], new SourceSpan(start, start + 100, 1, 9), members ?? []);
    }

    static ModelFileEntry Entry(string path, int errorNodes) =>
        new(path, Languages.Cpp, Modes.SyntaxOnly, null, errorNodes);

    static ModelFile Model(IEnumerable<TypeModel> types, IReadOnlyList<UnmodelledEntry>? unmodelled = null,
        IReadOnlyList<ModelFileEntry>? files = null, IReadOnlyList<string>? duplicates = null) =>
        new(
            ModelFile.SchemaName,
            new ExtractionInfo([new LanguageExtraction(Languages.Cpp, "cpp-ts", Modes.SyntaxOnly, null, null, [])]),
            files ?? [], unmodelled ?? [], [.. types], [], duplicates ?? []);

    static DiffFile Diff(IEnumerable<TypeModel> before, IEnumerable<TypeModel> after) =>
        ModelDiffer.Diff(Model(before), Model(after));

    static IEnumerable<IReadOnlyList<T>> Permutations<T>(IReadOnlyList<T> items)
    {
        if (items.Count <= 1)
        {
            yield return items;
            yield break;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, index) => index != i).ToList();
            foreach (var tail in Permutations(rest))
            {
                yield return [items[i], .. tail];
            }
        }
    }

    [Fact]
    public void A_changed_signature_is_a_modified_member_with_both_signatures()
    {
        var diff = Diff(
            [T("ns::A", members: [M("size() const", signature: "int size() const")])],
            [T("ns::A", members: [M("size() const", signature: "long size() const")])]);

        var member = Assert.Single(Assert.Single(diff.Types).Members);
        Assert.Equal("modified", member.State);
        Assert.Equal("int size() const", member.SignatureBefore);
        Assert.Equal("long size() const", member.SignatureAfter);
        Assert.Equal(member.AccessBefore, member.AccessAfter);
    }

    [Fact]
    public void A_changed_access_alone_is_a_modified_member_with_both_accesses()
    {
        var diff = Diff(
            [T("ns::A", members: [M("count_", access: "private")])],
            [T("ns::A", members: [M("count_", access: "protected")])]);

        var member = Assert.Single(Assert.Single(diff.Types).Members);
        Assert.Equal("modified", member.State);
        Assert.Equal("private", member.AccessBefore);
        Assert.Equal("protected", member.AccessAfter);
        Assert.Equal(member.SignatureBefore, member.SignatureAfter);
    }

    [Fact]
    public void A_member_with_both_access_and_signature_changed_lists_both_pairs()
    {
        var diff = Diff(
            [T("ns::A", members: [M("v", access: "public", signature: "int v")])],
            [T("ns::A", members: [M("v", access: "private", signature: "long v")])]);

        var member = Assert.Single(Assert.Single(diff.Types).Members);
        Assert.Equal("modified", member.State);
        Assert.Equal(("public", "private"), (member.AccessBefore, member.AccessAfter));
        Assert.Equal(("int v", "long v"), (member.SignatureBefore, member.SignatureAfter));
    }

    [Fact]
    public void Overloads_are_different_members_so_dropping_one_removes_only_that_one()
    {
        var diff = Diff(
            [T("ns::A", members: [M("f(int)", signature: "void f(int)"), M("f(long)", signature: "void f(long)")])],
            [T("ns::A", members: [M("f(int)", signature: "void f(int)")])]);

        var member = Assert.Single(Assert.Single(diff.Types).Members);
        Assert.Equal(("f(long)", "removed"), (member.Key, member.State));
    }

    [Fact]
    public void A_const_qualifier_makes_a_different_member()
    {
        var diff = Diff(
            [T("ns::A", members: [M("f()", signature: "int f()")])],
            [T("ns::A", members: [M("f() const", signature: "int f() const")])]);

        var members = Assert.Single(diff.Types).Members;
        Assert.Equal([("f()", "removed"), ("f() const", "added")], members.Select(m => (m.Key, m.State)));
    }

    [Fact]
    public void A_changed_parameter_list_is_a_removed_member_plus_an_added_member()
    {
        // A parameter rename stays removed plus added until Phase 5 (D-07: key is name plus parameter text).
        var diff = Diff(
            [T("ns::A", members: [M("f(int a)", signature: "void f(int a)")])],
            [T("ns::A", members: [M("f(int b)", signature: "void f(int b)")])]);

        var members = Assert.Single(diff.Types).Members;
        Assert.Equal([("f(int a)", "removed"), ("f(int b)", "added")], members.Select(m => (m.Key, m.State)));
        Assert.Equal(new MemberCounts(Added: 1, Removed: 1, Modified: 0), diff.Summary.Members);
    }

    [Fact]
    public void A_changed_base_list_modifies_the_type_with_sorted_added_and_removed_bases_and_untouched_members()
    {
        var diff = Diff(
            [T("ns::A", bases: ["Zed", "Old", "Keep"], members: [M("x")])],
            [T("ns::A", bases: ["Keep", "New2", "New1"], members: [M("x")])]);

        var type = Assert.Single(diff.Types);
        Assert.Equal("modified", type.State);
        Assert.Equal(["New1", "New2"], type.Bases.Added);
        Assert.Equal(["Old", "Zed"], type.Bases.Removed);
        Assert.Empty(type.Members);
    }

    [Fact]
    public void An_enum_underlying_type_change_behaves_like_a_base_list_change()
    {
        var diff = Diff(
            [T("ns::Mode", kind: "enum", bases: ["int"])],
            [T("ns::Mode", kind: "enum", bases: ["unsigned char"])]);

        var type = Assert.Single(diff.Types);
        Assert.Equal("modified", type.State);
        Assert.Equal(["unsigned char"], type.Bases.Added);
        Assert.Equal(["int"], type.Bases.Removed);
    }

    [Fact]
    public void An_unchanged_type_is_counted_but_not_listed_and_summary_counts_match_the_listed_entries()
    {
        var same = T("ns::Same", members: [M("a"), M("b")]);
        var diff = Diff(
            [same, T("ns::Gone", members: [M("g")]), T("ns::Edit", members: [M("keep"), M("drop")])],
            [same, T("ns::New", members: [M("n1"), M("n2")]), T("ns::Edit", members: [M("keep"), M("add")])]);

        Assert.Equal(["class ns::Edit", "class ns::Gone", "class ns::New"], diff.Types.Select(t => t.Id));
        Assert.Equal(new TypeCounts(Added: 1, Removed: 1, Modified: 1, Unchanged: 1), diff.Summary.Types);
        var members = diff.Types.SelectMany(t => t.Members).ToList();
        Assert.Equal(
            new MemberCounts(
                members.Count(m => m.State == "added"), members.Count(m => m.State == "removed"), members.Count(m => m.State == "modified")),
            diff.Summary.Members);
        // The unchanged member `keep` is not listed.
        Assert.DoesNotContain(members, m => m.Key == "keep");
    }

    [Fact]
    public void The_same_name_with_a_different_kind_is_one_removed_and_one_added_type()
    {
        var diff = Diff([T("ns::A", kind: "class", members: [M("x")])], [T("ns::A", kind: "struct", members: [M("x")])]);

        Assert.Equal([("class ns::A", "removed"), ("struct ns::A", "added")], diff.Types.Select(t => (t.Id, t.State)));
        Assert.Equal(new TypeCounts(Added: 1, Removed: 1, Modified: 0, Unchanged: 0), diff.Summary.Types);
    }

    [Fact]
    public void A_type_moved_between_files_shows_both_files_when_modified()
    {
        var diff = Diff(
            [T("ns::A", file: "old/a.h", members: [M("x", signature: "int x")])],
            [T("ns::A", file: "new/a.h", members: [M("x", signature: "long x")])]);

        var type = Assert.Single(diff.Types);
        Assert.Equal("old/a.h", type.BaseFile);
        Assert.Equal("new/a.h", type.HeadFile);
    }

    [Fact]
    public void Identical_content_in_different_files_is_still_unchanged()
    {
        var diff = Diff(
            [T("ns::A", file: "old/a.h", start: 10, members: [M("x")])],
            [T("ns::A", file: "new/a.h", start: 90, members: [M("x")])]);

        Assert.Empty(diff.Types);
        Assert.Equal(new TypeCounts(0, 0, 0, Unchanged: 1), diff.Summary.Types);
    }

    [Fact]
    public void Two_empty_models_give_zero_counts_and_no_types()
    {
        var diff = ModelDiffer.Diff(Model([]), Model([]));

        Assert.Empty(diff.Types);
        Assert.Empty(diff.Warnings);
        Assert.Equal(new TypeCounts(0, 0, 0, 0), diff.Summary.Types);
        Assert.Equal(new MemberCounts(0, 0, 0), diff.Summary.Members);
        Assert.Equal(new UnmodelledCounts(0, 0), diff.Summary.Unmodelled);
    }

    [Fact]
    public void Shuffling_the_types_and_members_of_either_model_never_changes_the_diff()
    {
        var baseTypes = new List<TypeModel>
        {
            T("ns::A", members: [M("a"), M("b", signature: "int b"), M("c(int)", signature: "void c(int)"), M("d")]),
            T("ns::B", kind: "struct", members: [M("x"), M("y")]),
            T("ns::Gone", members: [M("g1"), M("g2"), M("g3")]),
            T("ns::Same", members: [M("s")]),
        };
        var headTypes = new List<TypeModel>
        {
            T("ns::A", members: [M("a", access: "private"), M("b", signature: "long b"), M("c(long)", signature: "void c(long)"), M("e")]),
            T("ns::B", kind: "struct", bases: ["Q", "P"], members: [M("x"), M("y")]),
            T("ns::New", members: [M("n3"), M("n1"), M("n2")]),
            T("ns::Same", members: [M("s")]),
        };
        var expected = OutputWriter.Serialize(Diff(baseTypes, headTypes));

        var random = new Random(20260601);
        for (var round = 0; round < 25; round++)
        {
            TypeModel Shuffled(TypeModel t) => t with { Members = [.. t.Members.OrderBy(_ => random.Next())] };
            var shuffledBase = baseTypes.Select(Shuffled).OrderBy(_ => random.Next()).ToList();
            var shuffledHead = headTypes.Select(Shuffled).OrderBy(_ => random.Next()).ToList();

            Assert.Equal(expected, OutputWriter.Serialize(Diff(shuffledBase, shuffledHead)));
        }
    }

    [Fact]
    public void Types_and_members_that_share_an_id_or_key_are_ordered_totally_so_every_input_order_gives_one_diff()
    {
        // Hand-built input that the builder would have suffixed: the differ must still be input-order independent (C39).
        var first = T("ns::A", file: "a.h", start: 10, members: [M("m", signature: "int m"), M("m", signature: "long m", start: 40)]);
        var second = T("ns::A", file: "b.h", start: 10, members: [M("only_b")]);
        var third = T("ns::A", file: "b.h", start: 200, members: [M("only_c")]);
        var head = T("ns::A", file: "a.h", start: 10, members: [M("m", signature: "int m")]);

        var results = new HashSet<string>(StringComparer.Ordinal);
        foreach (var order in Permutations<TypeModel>([first, second, third]))
        {
            results.Add(OutputWriter.Serialize(Diff(order, [head])));
        }

        Assert.Single(results);
    }

    [Fact]
    public void Members_of_added_and_removed_types_come_out_in_ordinal_key_order()
    {
        var diff = Diff(
            [T("ns::Gone", members: [M("z"), M("B"), M("a")])],
            [T("ns::New", members: [M("y"), M("_c"), M("b")])]);

        Assert.Equal(["B", "a", "z"], diff.Types.Single(t => t.State == "removed").Members.Select(m => m.Key));
        Assert.Equal(["_c", "b", "y"], diff.Types.Single(t => t.State == "added").Members.Select(m => m.Key));
    }

    [Fact]
    public void Ordering_is_ordinal_and_a_soft_hyphen_in_a_name_makes_a_distinct_name()
    {
        var plain = T("ns::Ab");
        var hyphenated = T("ns::A­b");
        var lower = T("ns::a");
        var diff = Diff([], [plain, hyphenated, lower]);

        Assert.Equal(["class ns::Ab", "class ns::A­b", "class ns::a"], diff.Types.Select(t => t.Id));
        Assert.Equal(3, diff.Summary.Types.Added);
    }

    [Fact]
    public void No_warning_is_written_when_every_count_is_zero()
    {
        var diff = ModelDiffer.Diff(
            Model([], files: [Entry("a.h", 0)]),
            Model([], files: [Entry("a.h", 0)]));

        Assert.Empty(diff.Warnings);
    }

    [Fact]
    public void Parse_errors_and_unmodelled_files_are_separate_warnings_prefixed_by_side()
    {
        var baseModel = Model([], files: [Entry("a.h", 2), Entry("b.h", 0), Entry("c.h", 1)]);
        var headModel = Model([], unmodelled: [new UnmodelledEntry("d.h", "too-large")]);

        var diff = ModelDiffer.Diff(baseModel, headModel);

        Assert.Equal(["base: 2 file(s) with parse errors", "head: 1 file(s) not modelled"], diff.Warnings);
    }

    [Fact]
    public void Each_duplicates_entry_becomes_a_warning_on_its_side()
    {
        var baseModel = Model([], duplicates: ["class shop::X (2 definitions)", "member f() of class shop::Y (3 definitions)"]);
        var headModel = Model([], duplicates: ["class shop::Z (2 definitions)"]);

        var diff = ModelDiffer.Diff(baseModel, headModel);

        Assert.Equal(
            [
                "base: duplicate class shop::X (2 definitions)",
                "base: duplicate member f() of class shop::Y (3 definitions)",
                "head: duplicate class shop::Z (2 definitions)",
            ],
            diff.Warnings);
    }

    [Theory]
    [InlineData("parse-timeout")]
    [InlineData("parse-error")]
    [InlineData("too-large")]
    [InlineData("object-missing")]
    [InlineData("total-too-large")]
    [InlineData("non-utf8-path")]
    public void Every_unmodelled_reason_is_counted_on_its_side(string reason)
    {
        var entries = new[] { new UnmodelledEntry("a.h", reason), new UnmodelledEntry("b.h", reason) };
        var diff = ModelDiffer.Diff(Model([], unmodelled: entries), Model([], unmodelled: [entries[0]]));

        Assert.Equal(new UnmodelledCounts(Base: 2, Head: 1), diff.Summary.Unmodelled);
        Assert.Contains("base: 2 file(s) not modelled", diff.Warnings);
        Assert.Contains("head: 1 file(s) not modelled", diff.Warnings);
    }
}
