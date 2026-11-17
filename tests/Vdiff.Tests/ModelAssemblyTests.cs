using Vdiff.Core.Diff;
using Vdiff.Core.Extraction;
using Vdiff.Core.Git;
using Vdiff.Core.Model;

namespace Vdiff.Tests;

/// <summary>The model builder and differ on hand-made sidecar answers (no process, no repository).</summary>
public class ModelAssemblyTests
{
    static SidecarResponse Response(params SidecarFile[] files) =>
        new() { Schema = "vdiff-cpp-ts/v0", TreeSitter = "0.27.0", TreeSitterCpp = "0.23.4", Files = files };

    static SidecarType Type(string name, int start, params SidecarMember[] members) =>
        new() { Kind = "class", Name = name, Scope = "ns", StartByte = start, EndByte = start + 10, Members = members };

    static SidecarMember Member(string key, int start) =>
        new() { Name = key, Kind = "field", Access = "public", Key = key, Signature = "int " + key, StartByte = start };

    [Fact]
    public void A_timed_out_or_failed_file_is_unmodelled_never_modelled()
    {
        var response = Response(
            new SidecarFile { Path = "a.h", Status = "ok", Types = [Type("A", 0)] },
            new SidecarFile { Path = "b.h", Status = "timeout" },
            new SidecarFile { Path = "c.h", Status = "error", ErrorNodes = 4 });

        var model = ModelBuilder.Build("1111111", response, [new UnmodelledFile("d.h", "too-large")], []);

        Assert.Equal(["a.h"], model.Files.Select(f => f.Path));
        Assert.Equal(
            [("b.h", "parse-timeout"), ("c.h", "parse-error"), ("d.h", "too-large")],
            model.Unmodelled.Select(u => (u.Path, u.Reason)));
        Assert.Equal(["class ns::A"], model.Types.Select(t => t.Id));
    }

    [Fact]
    public void Unmodelled_entries_are_counted_in_the_diff_summary_and_the_warnings()
    {
        var baseModel = ModelBuilder.Build("1111111", Response(new SidecarFile { Path = "b.h", Status = "timeout" }), [], []);
        var headModel = ModelBuilder.Build(
            "2222222",
            Response(
                new SidecarFile { Path = "b.h", Status = "error" },
                new SidecarFile { Path = "c.h", Status = "timeout" }),
            [],
            []);

        var diff = ModelDiffer.Diff(baseModel, headModel);

        Assert.Equal(new UnmodelledCounts(Base: 1, Head: 2), diff.Summary.Unmodelled);
        Assert.Equal(
            ["base: 0 file(s) with parse errors, 1 file(s) not modelled", "head: 0 file(s) with parse errors, 2 file(s) not modelled"],
            diff.Warnings);
    }

    [Fact]
    public void Repeated_definitions_get_unique_ids_in_source_order()
    {
        var response = Response(new SidecarFile
        {
            Path = "a.h",
            Status = "ok",
            Types = [Type("A", 100, Member("x", 120), Member("x", 110)), Type("A", 0)],
        });

        var model = ModelBuilder.Build("1111111", response, [], []);

        Assert.Equal(["class ns::A", "class ns::A#2"], model.Types.Select(t => t.Id));
        // The definition earlier in the file keeps the plain id.
        Assert.Equal(0, model.Types[0].Span.StartByte);
        Assert.Equal(["x", "x#2"], model.Types[1].Members.Select(m => m.Key));
        Assert.Equal(110, model.Types[1].Members[0].Span.StartByte);
    }

    [Fact]
    public void Ordering_is_ordinal_not_culture_aware()
    {
        var response = Response(new SidecarFile
        {
            Path = "a.h",
            Status = "ok",
            Types = [Type("b", 0), Type("B", 20), Type("a", 40), Type("_c", 60)],
        });

        var model = ModelBuilder.Build("1111111", response, [], []);

        Assert.Equal(["class ns::B", "class ns::_c", "class ns::a", "class ns::b"], model.Types.Select(t => t.Id));
    }
}
