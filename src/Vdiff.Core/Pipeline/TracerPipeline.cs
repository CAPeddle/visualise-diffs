using Vdiff.Core.Diff;
using Vdiff.Core.Extraction;
using Vdiff.Core.Git;
using Vdiff.Core.Model;
using Vdiff.Core.Scope;

namespace Vdiff.Core.Pipeline;

public sealed record PipelineResult(ModelFile Base, ModelFile Head, DiffFile Diff, RunMeta Meta);

/// <summary>
/// The in-memory tracer pipeline (D-10): changed files, the blobs of each side, one sidecar
/// call per commit, a model per side and the diff. It reaches the repository only through
/// <see cref="GitObjectReader"/>, resolves no ids and writes nothing, so a test can drive it
/// without the command line.
/// </summary>
public static class TracerPipeline
{
    public static PipelineResult Run(
        GitObjectReader reader,
        string baseId,
        string headId,
        ScopeFilter scope,
        IReadOnlyList<string> blank,
        SidecarCommand command,
        SidecarLimits limits)
    {
        var changes = reader.ListChanged(baseId, headId);

        // Only changed, in-scope C++ files are extracted, each side from its own blob (D-10).
        var inScope = changes.Files.Where(f => scope.IsInScope(f.Path)).ToList();
        // A path git could not give as UTF-8 cannot be told apart by side, so it is unmodelled on both.
        var pathLevel = changes.Unmodelled.Where(u => scope.IsInScope(u.Path)).ToList();

        var (baseModel, _) = BuildSide(
            reader, baseId, inScope.Where(f => f.BaseBlob is not null).Select(f => new BlobRequest(f.Path, f.BaseBlob!)),
            pathLevel, blank, command, limits);
        var (headModel, headResponse) = BuildSide(
            reader, headId, inScope.Where(f => f.HeadBlob is not null).Select(f => new BlobRequest(f.Path, f.HeadBlob!)),
            pathLevel, blank, command, limits);

        // Versions come from the sidecar response, not from the model, so the model can drop them
        // without this code changing.
        var meta = new RunMeta(
            RunMeta.SchemaName,
            RunMeta.CurrentVdiffVersion,
            baseId,
            headId,
            [
                new ExtractorRun("cpp-ts",
                [
                    new ToolVersion("tree-sitter", headResponse.TreeSitter),
                    new ToolVersion("tree-sitter-cpp", headResponse.TreeSitterCpp),
                ]),
            ]);

        return new PipelineResult(baseModel, headModel, ModelDiffer.Diff(baseModel, headModel), meta);
    }

    static (ModelFile Model, SidecarResponse Response) BuildSide(
        GitObjectReader reader,
        string commit,
        IEnumerable<BlobRequest> requests,
        IReadOnlyList<UnmodelledFile> pathLevel,
        IReadOnlyList<string> blank,
        SidecarCommand command,
        SidecarLimits limits)
    {
        // The per-blob and total budgets turn a large change into unmodelled files, not a failed run.
        var read = reader.ReadBlobs([.. requests]);

        // Called even with no files, so the version metadata is always present (ADR-001).
        var response = CppSidecar.Extract(
            read.Blobs.Select(b => new SidecarInput(b.Path, b.Bytes)).ToList(), blank, command, limits);

        return (ModelBuilder.Build(commit, response, [.. pathLevel, .. read.Unmodelled], blank), response);
    }
}
