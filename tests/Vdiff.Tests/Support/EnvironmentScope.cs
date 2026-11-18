namespace Vdiff.Tests.Support;

/// <summary>
/// Sets process environment variables for the lifetime of the scope (a null value removes one)
/// and restores the previous values, removed or not, in <see cref="Dispose"/>. The environment is
/// process-wide, so a class that uses this carries <c>[Collection("sidecar")]</c>: the collection
/// runs one test at a time, and <c>SidecarFixture</c> leaves <c>VDIFF_CPP_TS</c> set for the
/// others, which this scope therefore restores rather than clears.
/// </summary>
public sealed class EnvironmentScope : IDisposable
{
    readonly Dictionary<string, string?> _saved = [];

    public EnvironmentScope(IReadOnlyDictionary<string, string?> variables)
    {
        foreach (var (key, value) in variables)
        {
            _saved[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public void Dispose()
    {
        foreach (var (key, value) in _saved)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
