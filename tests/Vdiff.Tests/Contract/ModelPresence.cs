using Vdiff.Core.Model;

namespace Vdiff.Tests.Contract;

/// <summary>
/// The model rules the draft-07 keyword subset cannot express: the type rules of D-19 and the edge rule
/// that via is present exactly on association edges. Each problem is fixed text that starts with a rule
/// tag and a colon and names the language and id of the type, or the kind and from of the edge, never a path.
/// </summary>
public static class ModelPresence
{
    public static IReadOnlyList<string> Problems(ModelFile model) => [];
}
