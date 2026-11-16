using Microsoft.Extensions.FileSystemGlobbing;

namespace Vdiff.Core.Scope;

/// <summary>
/// Decides which changed paths are C++ files in scope (D-10): a C++ header or source
/// extension (case-insensitive), and, when include globs are given, a match against at
/// least one of them (case-sensitive).
/// </summary>
public sealed class ScopeFilter
{
    static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".h", ".hpp", ".hh", ".hxx", ".cpp", ".cc", ".cxx",
    };

    // Null when there are no globs: a Matcher with no include pattern matches nothing, so the
    // "no --include" case must never consult it.
    readonly Matcher? _matcher;

    public ScopeFilter(IEnumerable<string>? includeGlobs = null)
    {
        var globs = includeGlobs?.ToList();
        if (globs is { Count: > 0 })
        {
            // The default Matcher is case-insensitive; globs here are case-sensitive (C37).
            _matcher = new Matcher(StringComparison.Ordinal);
            foreach (var glob in globs)
            {
                _matcher.AddInclude(glob);
            }
        }
    }

    public bool IsInScope(string path)
    {
        if (string.IsNullOrEmpty(path) || !Extensions.Contains(Path.GetExtension(path)))
        {
            return false;
        }

        return _matcher is null || _matcher.Match(path).HasMatches;
    }
}
