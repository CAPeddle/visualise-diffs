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

    static ModelFile Build(params SidecarFile[] files) => ModelBuilder.Build("1111111", Response(files), [], []);

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

        var model = ModelBuilder.Build("1111111", response, read, []);

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
}
