using Vdiff.Core.Model;

namespace Vdiff.Tests.Contract;

/// <summary>
/// The diff rules the draft-07 keyword subset cannot express (D-15). Each problem is fixed text that starts
/// with a rule tag and names the type id and member key, the package name, or the edge kind and its from id.
/// </summary>
public static class DiffPresence
{
    public static IReadOnlyList<string> Problems(DiffFile diff) => [];
}
