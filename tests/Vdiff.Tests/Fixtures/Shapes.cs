namespace Vdiff.Tests.Fixtures;

/// <summary>The five fixture shape names (D-18); fixtures live under tests/fixtures/&lt;shape&gt;.</summary>
public static class Shapes
{
    public static readonly IReadOnlyList<string> All = ["small-cpp", "csharp", "cross-stack", "extract-class", "rename"];

    public static bool IsShape(string name) => All.Contains(name, StringComparer.Ordinal);
}
