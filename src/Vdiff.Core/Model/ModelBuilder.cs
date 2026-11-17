using Vdiff.Core.Extraction;
using Vdiff.Core.Git;

namespace Vdiff.Core.Model;

/// <summary>Turns one sidecar answer into a model file (stub until the GREEN commit).</summary>
public static class ModelBuilder
{
    public static ModelFile Build(
        string commit, SidecarResponse response, IReadOnlyList<UnmodelledFile> unmodelled, IReadOnlyList<string> macros) =>
        new(ModelFile.SchemaName, commit, new ExtractionInfo("cpp-ts", "syntax-only", "", "", []), [], [], []);
}
