using Vdiff.Core.Diff;
using Vdiff.Core.Extraction;
using Vdiff.Core.Git;
using Vdiff.Core.Model;
using Vdiff.Core.Scope;

namespace Vdiff.Core.Pipeline;

public sealed record PipelineResult(ModelFile Base, ModelFile Head, DiffFile Diff);

/// <summary>The in-memory tracer pipeline (stub until the GREEN commit).</summary>
public static class TracerPipeline
{
    public static PipelineResult Run(
        GitObjectReader reader, string baseId, string headId, ScopeFilter scope,
        IReadOnlyList<string> blank, SidecarCommand command, SidecarLimits limits)
    {
        var baseModel = ModelBuilder.Build(baseId, new SidecarResponse(), [], blank);
        var headModel = ModelBuilder.Build(headId, new SidecarResponse(), [], blank);
        return new PipelineResult(baseModel, headModel, ModelDiffer.Diff(baseModel, headModel));
    }
}
