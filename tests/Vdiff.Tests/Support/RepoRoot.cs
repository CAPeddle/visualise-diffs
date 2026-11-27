namespace Vdiff.Tests.Support;

/// <summary>
/// Locates the checkout root (the directory holding <c>vdiff.sln</c>) above the test binaries.
/// A missing root fails the calling test; nothing here ever skips.
/// </summary>
public static class RepoRoot
{
    public static string Find()
    {
        for (var dir = Path.GetFullPath(AppContext.BaseDirectory); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "vdiff.sln")))
            {
                return dir;
            }
        }

        throw new Xunit.Sdk.XunitException(
            "could not find vdiff.sln above the test binaries; this test never skips");
    }
}
