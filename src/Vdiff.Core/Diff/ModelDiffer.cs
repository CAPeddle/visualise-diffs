using Vdiff.Core.Model;

namespace Vdiff.Core.Diff;

/// <summary>Diffs two models by type id and member key (stub until the GREEN commit).</summary>
public static class ModelDiffer
{
    public static DiffFile Diff(ModelFile baseModel, ModelFile headModel) =>
        new(DiffFile.SchemaName, baseModel.Commit, headModel.Commit,
            new DiffSummary(new TypeCounts(0, 0, 0, 0), new MemberCounts(0, 0, 0), new UnmodelledCounts(0, 0)), [], []);
}
