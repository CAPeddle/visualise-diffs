using Microsoft.Extensions.FileSystemGlobbing;

namespace Vdiff.Core.Scope;

/// <summary>
/// Decides which changed paths are C++ files in scope (D-10): a C++ header or source
/// extension (ordinal, case-insensitive), and, when include globs are given, a match against at
/// least one of them (ordinal, case-sensitive, <c>/</c> separators, the way git reports paths).
/// The caller (the command line) has already bounded the number and length of the globs.
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

    /// <exception cref="VdiffException">A glob is empty (or only <c>./</c>).</exception>
    public ScopeFilter(IEnumerable<string>? includeGlobs = null)
    {
        var globs = includeGlobs?.ToList();
        if (globs is not { Count: > 0 })
        {
            return;
        }

        // One matcher per filter. The default Matcher is case-insensitive; globs here are
        // case-sensitive, so the comparison is stated explicitly (C37).
        _matcher = new Matcher(StringComparison.Ordinal);
        foreach (var glob in globs)
        {
            _matcher.AddInclude(Normalise(glob));
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

    /// <summary>Drops any leading <c>./</c>, so <c>./src/**</c> means <c>src/**</c>; rejects what is then empty.</summary>
    static string Normalise(string glob)
    {
        var pattern = glob;
        while (pattern.StartsWith("./", StringComparison.Ordinal))
        {
            pattern = pattern[2..];
        }

        if (pattern.Length == 0 || pattern.All(c => c == '/'))
        {
            throw new VdiffException("an --include glob must not be empty");
        }

        return pattern;
    }
}
