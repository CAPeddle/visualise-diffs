namespace Vdiff.Core.Scope;

/// <summary>Decides which changed paths are C++ files in scope (D-10).</summary>
public sealed class ScopeFilter
{
    public ScopeFilter(IEnumerable<string>? includeGlobs = null) => throw new NotImplementedException();

    public bool IsInScope(string path) => throw new NotImplementedException();
}
