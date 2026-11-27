using Vdiff.Core.Model;

namespace Vdiff.Tests.Contract;

/// <summary>
/// The model rules the draft-07 keyword subset cannot express: the type rules of D-19 and the edge rule
/// that via is present exactly on association edges. Each problem is fixed text that starts with a rule
/// tag and a colon and names the language and id of the type, or the kind and from of the edge, never a path.
/// </summary>
public static class ModelPresence
{
    public static IReadOnlyList<string> Problems(ModelFile model)
    {
        var problems = new List<string>();
        foreach (var type in model.Types)
        {
            CheckType(type, problems);
        }

        foreach (var edge in model.Edges)
        {
            CheckEdge(edge, problems);
        }

        return problems;
    }

    static void CheckType(TypeModel type, List<string> problems)
    {
        var who = $"type {type.Language} {type.Id}";
        if (!type.Partial && type.Files is not null)
        {
            problems.Add($"type-files-on-non-partial: {who} is not partial but has files");
        }

        var files = type.Files;
        if (type.Partial && (files is null || files.Count == 0))
        {
            problems.Add($"type-partial-without-files: {who} is partial but has no files");
        }

        // The three rules on the list itself apply only to a list that has entries.
        if (files is { Count: > 0 })
        {
            for (var i = 1; i < files.Count; i++)
            {
                var order = string.CompareOrdinal(files[i - 1], files[i]);
                if (order > 0)
                {
                    problems.Add($"type-files-order: {who} lists files out of ordinal order");
                }
                else if (order == 0)
                {
                    problems.Add($"type-files-repeat: {who} lists a file twice");
                }
            }

            if (!string.Equals(type.File, files[0], StringComparison.Ordinal))
            {
                problems.Add($"type-file-not-first: {who} has a file that is not the first entry of files");
            }
        }

        if (type.Access == "none")
        {
            problems.Add($"type-access-none: {who} has access none; absence is the only encoding of no type-level access");
        }
    }

    static void CheckEdge(EdgeModel edge, List<string> problems)
    {
        var who = $"edge {edge.Kind} from {edge.From.Language} {edge.From.Id}";
        if (edge.Kind != EdgeKinds.Association && edge.Via is not null)
        {
            problems.Add($"edge-via-on-non-association: {who} carries a via");
        }

        if (edge.Kind == EdgeKinds.Association && edge.Via is null)
        {
            problems.Add($"edge-association-without-via: {who} has no via");
        }
    }
}
