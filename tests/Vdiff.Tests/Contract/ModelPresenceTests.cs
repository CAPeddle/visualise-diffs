using Vdiff.Core.Model;

namespace Vdiff.Tests.Contract;

/// <summary>
/// ModelPresence enforces the D-19 type rules and the edge via rule. Every row starts from the canonical
/// full model and breaks one rule, so the only reported problem must be that rule's. A row is never passed
/// back through OutputWriter.Serialize: the serialiser canonicalises and would sort the files again.
/// </summary>
[Trait("Category", "Contract")]
public class ModelPresenceTests
{
    static ModelFile CanonicalFull() => Canonical.Canonicalise(ContractSamples.FullModel());

    static ModelFile MapPartialType(Func<TypeModel, TypeModel> change)
    {
        var model = CanonicalFull();
        Assert.Single(model.Types, t => t.Partial);
        return model with { Types = model.Types.Select(t => t.Partial ? change(t) : t).ToList() };
    }

    static ModelFile MapEdge(string kind, Func<EdgeModel, EdgeModel> change)
    {
        var model = CanonicalFull();
        Assert.Single(model.Edges, e => e.Kind == kind);
        return model with { Edges = model.Edges.Select(e => e.Kind == kind ? change(e) : e).ToList() };
    }

    // Row name -> (a model that breaks one rule, the tag that rule's problem starts with).
    static readonly Dictionary<string, (Func<ModelFile> Break, string Rule)> Rows = new()
    {
        ["files_on_a_type_that_is_not_partial"] =
            (() => MapPartialType(t => t with { Partial = false }), "type-files-on-non-partial"),
        ["partial_type_without_files"] =
            (() => MapPartialType(t => t with { Files = null }), "type-partial-without-files"),
        ["file_is_not_the_first_entry_of_files"] =
            (() => MapPartialType(t => t with { File = t.Files![1] }), "type-file-not-first"),
        ["files_out_of_ordinal_order"] =
            (() => MapPartialType(t =>
            {
                var reversed = t.Files!.Reverse().ToList();
                return t with { Files = reversed, File = reversed[0] };
            }), "type-files-order"),
        ["files_repeat_an_entry"] =
            (() => MapPartialType(t => t with { Files = [t.Files![0], t.Files[0], t.Files[1]] }), "type-files-repeat"),
        ["type_level_access_of_none"] =
            (() => MapPartialType(t => t with { Access = "none" }), "type-access-none"),
        ["base_edge_with_a_via"] =
            (() => MapEdge(EdgeKinds.Base, e => e with { Via = "member_" }), "edge-via-on-non-association"),
        ["association_edge_without_a_via"] =
            (() => MapEdge(EdgeKinds.Association, e => e with { Via = null }), "edge-association-without-via"),
    };

    public static TheoryData<string> RowNames() => [.. Rows.Keys];

    [Fact]
    public void The_canonical_full_model_reports_no_problems()
    {
        var model = CanonicalFull();
        var beta = Assert.Single(model.Types, t => t.Partial);
        Assert.Equal(["src/Beta.a.cs", "src/Beta.b.cs"], beta.Files);

        Assert.Empty(ModelPresence.Problems(model));
    }

    [Fact]
    public void The_empty_model_reports_no_problems()
    {
        Assert.Empty(ModelPresence.Problems(ContractSamples.EmptyModel()));
    }

    [Theory]
    [MemberData(nameof(RowNames))]
    public void Breaking_one_rule_reports_that_rule_and_nothing_else(string row)
    {
        var (breakRule, rule) = Rows[row];

        var problems = ModelPresence.Problems(breakRule());

        var only = Assert.Single(problems);
        Assert.StartsWith(rule + ":", only, StringComparison.Ordinal);
    }
}
