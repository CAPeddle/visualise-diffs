using Vdiff.Core.Diff;
using Vdiff.Core.Extraction;
using Vdiff.Core.Git;
using Vdiff.Core.Model;
using Vdiff.Core.Output;

namespace Vdiff.Tests.Diff;

/// <summary>
/// The model builder's duplicate-id policy and canonical order, on hand-made sidecar answers
/// (no process, no repository). Complements <c>ModelAssemblyTests</c> from plan 01-03.
/// </summary>
public class ModelBuilderTests
{
    static SidecarResponse Response(params SidecarFile[] files) =>
        new() { Schema = "vdiff-cpp-ts/v0", TreeSitter = "0.27.0", TreeSitterCpp = "0.23.4", Files = files };

    static SidecarMember Member(string key, int start, string signature = "int m") =>
        new() { Name = key, Kind = "method", Access = "public", Key = key, Signature = signature, StartByte = start, EndByte = start + 4 };

    static SidecarType Type(string name, int start, params SidecarMember[] members) =>
        new() { Kind = "class", Name = name, Scope = "shop", StartByte = start, EndByte = start + 50, Members = members };

    static SidecarFile File(string path, params SidecarType[] types) =>
        new() { Path = path, Status = "ok", Types = types };

    static ModelFile Build(params SidecarFile[] files) => ModelBuilder.Build(Response(files), [], []);

    [Fact]
    public void Repeated_types_get_path_suffixed_ids_and_at_n_only_for_a_repeat_in_one_file()
    {
        // D-21: once an id repeats, every definition carries its file path, the first one included.
        var model = Build(
            File("src/b.h", Type("X", 5)),
            File("src/a.h", Type("X", 300), Type("X", 20)));

        Assert.Equal(
            ["class shop::X#src/a.h", "class shop::X#src/a.h@2", "class shop::X#src/b.h"],
            model.Types.Select(t => t.Id));
        Assert.Equal(
            [("src/a.h", 20), ("src/a.h", 300), ("src/b.h", 5)],
            model.Types.Select(t => (t.File, t.Span.StartByte)));
        Assert.Equal(["class shop::X (3 definitions)"], model.Duplicates);
    }

    [Fact]
    public void Repeated_member_keys_in_one_type_get_hash_n_keys_and_are_recorded_with_their_type()
    {
        var model = Build(File("a.h", Type("Y", 0, Member("f()", 30), Member("f()", 10), Member("g()", 20))));

        var members = Assert.Single(model.Types).Members;
        Assert.Equal(["f()#a.h", "f()#a.h@2", "g()"], members.Select(m => m.Key));
        Assert.Equal([10, 30, 20], members.Select(m => m.Span.StartByte));
        Assert.Equal(["member f() of class shop::Y (2 definitions)"], model.Duplicates);
    }

    [Fact]
    public void The_same_member_key_in_two_different_types_is_not_a_duplicate()
    {
        var model = Build(File("a.h", Type("A", 0, Member("f()", 5)), Type("B", 100, Member("f()", 105))));

        Assert.Empty(model.Duplicates);
        Assert.All(model.Types, t => Assert.Equal("f()", Assert.Single(t.Members).Key));
    }

    [Fact]
    public void Duplicates_are_ordinal_sorted_and_always_written_even_when_empty()
    {
        var clean = Build(File("a.h", Type("A", 0)));
        Assert.Empty(clean.Duplicates);
        Assert.Contains("\"duplicates\": []", OutputWriter.Serialize(clean), StringComparison.Ordinal);

        var model = Build(File("a.h", Type("b", 0), Type("b", 100), Type("B", 200), Type("B", 300), Type("B", 400)));
        Assert.Equal(["class shop::B (3 definitions)", "class shop::b (2 definitions)"], model.Duplicates);
    }

    [Fact]
    public void Shuffling_the_answer_never_changes_the_serialised_model()
    {
        SidecarFile[] files =
        [
            File("src/b.h", Type("X", 5, Member("m()", 8), Member("m()", 9, "long m"))),
            File("src/a.h", Type("X", 300, Member("z", 310)), Type("Q", 20, Member("n", 25), Member("n", 26)), Type("X", 150)),
            File("src/c.h", Type("W", 0)),
        ];
        var expected = OutputWriter.Serialize(Build(files));

        var random = new Random(7);
        for (var round = 0; round < 25; round++)
        {
            var shuffled = files
                .Select(f => new SidecarFile
                {
                    Path = f.Path,
                    Status = f.Status,
                    Types = [.. f.Types.Select(t => new SidecarType
                    {
                        Kind = t.Kind, Name = t.Name, Scope = t.Scope, StartByte = t.StartByte, EndByte = t.EndByte,
                        Members = [.. t.Members.OrderBy(_ => random.Next())],
                    }).OrderBy(_ => random.Next())],
                })
                .OrderBy(_ => random.Next())
                .ToArray();

            Assert.Equal(expected, OutputWriter.Serialize(Build(shuffled)));
        }
    }

    [Fact]
    public void Members_that_start_at_the_same_byte_are_still_ordered_the_same_whatever_the_input_order()
    {
        // Two declarators can begin at one byte; the tie must not fall back to input order (C39).
        var one = Member("p", 10, "int p");
        var two = Member("p", 10, "long p");

        var forward = Build(File("a.h", Type("A", 0, one, two)));
        var backward = Build(File("a.h", Type("A", 0, two, one)));

        Assert.Equal(OutputWriter.Serialize(forward), OutputWriter.Serialize(backward));
        Assert.Equal(["p#a.h", "p#a.h@2"], forward.Types[0].Members.Select(m => m.Key));
    }

    [Fact]
    public void Unmodelled_entries_of_every_reason_are_kept_sorted_and_counted_by_the_diff()
    {
        string[] reasons = ["too-large", "object-missing", "total-too-large", "non-utf8-path"];
        var read = reasons.Select((r, i) => new UnmodelledFile("r" + i + ".h", r)).ToList();
        var response = Response(
            new SidecarFile { Path = "t.h", Status = "timeout" },
            new SidecarFile { Path = "e.h", Status = "error" });

        var model = ModelBuilder.Build(response, read, []);

        Assert.Equal(6, model.Unmodelled.Count);
        Assert.Equal(
            ["e.h", "r0.h", "r1.h", "r2.h", "r3.h", "t.h"],
            model.Unmodelled.Select(u => u.Path));
        var diff = ModelDiffer.Diff(model, Build());
        Assert.Equal(6, diff.Summary.Unmodelled.Base);
        Assert.Contains("base: 6 file(s) not modelled", diff.Warnings);
    }

    [Fact]
    public void Duplicates_on_either_side_surface_as_diff_warnings_and_do_not_stop_the_diff()
    {
        var baseModel = Build(File("a.h", Type("X", 0)));
        var headModel = Build(File("a.h", Type("X", 0), Type("X", 100, Member("f()", 110), Member("f()", 120))));

        var diff = ModelDiffer.Diff(baseModel, headModel);

        // D-21: once the id repeats, both head definitions carry a path suffix, so the base's plain
        // id is removed and both suffixed ids are added.
        Assert.Equal(
            [("class shop::X", "removed"), ("class shop::X#a.h", "added"), ("class shop::X#a.h@2", "added")],
            diff.Types.Select(t => (t.Id, t.State)));
        Assert.Equal(new TypeCounts(Added: 2, Removed: 1, Modified: 0, Unchanged: 0), diff.Summary.Types);
        Assert.Equal(
            ["head: duplicate class shop::X (2 definitions)", "head: duplicate member f() of class shop::X#a.h@2 (2 definitions)"],
            diff.Warnings);
    }

    [Fact]
    public void Adding_an_earlier_sorting_file_with_a_repeated_type_leaves_the_other_ids_unchanged()
    {
        var one = Build(File("src/b.h", Type("X", 5)), File("src/c.h", Type("X", 10), Type("X", 60)));
        var two = Build(
            File("src/a.h", Type("X", 1)), File("src/b.h", Type("X", 5)), File("src/c.h", Type("X", 10), Type("X", 60)));

        string[] before = ["class shop::X#src/b.h", "class shop::X#src/c.h", "class shop::X#src/c.h@2"];
        Assert.Equal(before, one.Types.Select(t => t.Id));
        Assert.Equal(["class shop::X#src/a.h", .. before], two.Types.Select(t => t.Id));
        Assert.Equal(["class shop::X (3 definitions)"], one.Duplicates);
        Assert.Equal(["class shop::X (4 definitions)"], two.Duplicates);
    }

    static SidecarType Derived(string scope, string name, int start, params string[] bases) =>
        new() { Kind = "class", Name = name, Scope = scope, StartByte = start, EndByte = start + 50, Bases = bases };

    static EdgeModel BaseEdge(string fromId, string? toId, string written) =>
        new(EdgeKinds.Base, new TypeRef(Languages.Cpp, fromId), toId is null ? null : new TypeRef(Languages.Cpp, toId), written, null);

    [Fact]
    public void A_base_naming_one_type_by_its_stripped_name_gets_a_resolved_edge()
    {
        var model = Build(File("a.h", Derived("ns", "Base", 0), Derived("ns", "Derived", 100, "Base<int>")));

        Assert.Equal(
            [BaseEdge("class ns::Derived", "class ns::Base", "Base<int>")],
            model.Edges);
    }

    [Fact]
    public void A_base_matching_two_types_by_suffix_gets_an_edge_without_a_target()
    {
        var model = Build(File("a.h", Derived("one", "Base", 0), Derived("two", "Base", 60), Derived("ns", "Derived", 120, "Base")));

        Assert.Equal([BaseEdge("class ns::Derived", null, "Base")], model.Edges);
    }

    [Fact]
    public void A_base_naming_no_modelled_type_gets_an_edge_without_a_target()
    {
        var model = Build(File("a.h", Derived("ns", "Derived", 0, "std::exception", "Missing")));

        Assert.Equal(
            [BaseEdge("class ns::Derived", null, "Missing"), BaseEdge("class ns::Derived", null, "std::exception")],
            model.Edges);
    }

    [Fact]
    public void Two_exact_fqn_matches_from_a_duplicate_definition_give_an_edge_without_a_target()
    {
        var model = Build(
            File("a.h", Derived("ns", "Base", 0)),
            File("b.h", Derived("ns", "Base", 0)),
            File("c.h", Derived("app", "Derived", 0, "ns::Base")));

        Assert.Equal([BaseEdge("class app::Derived", null, "ns::Base")], model.Edges);
    }

    [Fact]
    public void A_base_that_resolves_to_the_type_itself_gets_an_edge_without_a_target()
    {
        var model = Build(File("a.h", Derived("ns", "Self", 0, "Self")));

        Assert.Equal([BaseEdge("class ns::Self", null, "Self")], model.Edges);
    }

    static IReadOnlyList<T> Order<T>(bool reversed, params T[] forward) => reversed ? [.. forward.Reverse()] : forward;

    static SourceSpan Span(int start) => new(start, start + 10, 1, 2);

    /// <summary>One model holding every kind of collection; <paramref name="reversed"/> lists each one in the opposite order.</summary>
    static ModelFile HandBuilt(bool reversed)
    {
        MemberModel Member(string key, params string[] modifiers) =>
            new(key, key, "method", "public", Order(reversed, modifiers), "int " + key, Span(5));

        TypeModel Type(string language, string id, string file, bool partial, string[]? files, params MemberModel[] members) =>
            new(language, id, id, "class", id, "", null, partial, file, files is null ? null : Order(reversed, files),
                Order(reversed, "virtual", "final"), Order(reversed, "Alpha", "Beta"), Span(0), Order(reversed, members));

        return new ModelFile(
            ModelFile.SchemaName,
            new ExtractionInfo(Order(reversed,
                new LanguageExtraction(Languages.Cpp, "cpp-ts", Modes.SyntaxOnly, null, null, Order(reversed, "A_MACRO", "B_MACRO")),
                new LanguageExtraction(Languages.CSharp, "roslyn", Modes.Semantic, null, 0, []))),
            Order(reversed,
                new ModelFileEntry("a.h", Languages.Cpp, Modes.SyntaxOnly, null, 0),
                new ModelFileEntry("b.h", Languages.Cpp, Modes.SyntaxOnly, null, 0),
                new ModelFileEntry("c.cs", Languages.CSharp, Modes.Semantic, null, 0)),
            Order(reversed, new UnmodelledEntry("x.h", "parse-error"), new UnmodelledEntry("y.h", "too-large")),
            Order(reversed,
                Type(Languages.Cpp, "class a::A", "a.h", false, null, Member("f()", "const", "virtual"), Member("g()")),
                Type(Languages.Cpp, "class a::B", "a.h", true, ["a.h", "b.h"]),
                Type(Languages.CSharp, "class N.C", "c.cs", false, null)),
            Order(reversed,
                new EdgeModel(EdgeKinds.Association, new TypeRef(Languages.Cpp, "class a::B"), null, "A", "f()"),
                new EdgeModel(EdgeKinds.Base, new TypeRef(Languages.Cpp, "class a::A"), null, "Alpha", null),
                new EdgeModel(EdgeKinds.Base, new TypeRef(Languages.Cpp, "class a::A"), new TypeRef(Languages.Cpp, "class a::C"), "Beta", null)),
            Order(reversed, "class a::A (2 definitions)", "class a::Z (2 definitions)"));
    }

    [Fact]
    public void Canonicalising_a_reverse_ordered_model_gives_the_bytes_of_the_forward_ordered_one()
    {
        var expected = OutputWriter.Serialize(Canonical.Canonicalise(HandBuilt(reversed: false)));

        Assert.Equal(expected, OutputWriter.Serialize(Canonical.Canonicalise(HandBuilt(reversed: true))));
    }

    [Fact]
    public void The_serialiser_is_the_single_entry_point_and_writes_canonical_order_by_itself()
    {
        Assert.Equal(
            OutputWriter.Serialize(HandBuilt(reversed: false)),
            OutputWriter.Serialize(HandBuilt(reversed: true)));
    }

    [Fact]
    public void Edges_sort_by_their_identity_before_the_resolved_target()
    {
        var model = Canonical.Canonicalise(HandBuilt(reversed: true));

        Assert.Equal(
            [EdgeKinds.Association, EdgeKinds.Base, EdgeKinds.Base],
            model.Edges.Select(e => e.Kind));
        Assert.Equal(["A", "Alpha", "Beta"], model.Edges.Select(e => e.ToName));
        Assert.Equal(["class a::B", "class a::A", "class a::A"], model.Edges.Select(e => e.From.Id));
        Assert.Equal(["a.h", "b.h"], model.Types[1].Files);
    }
}
