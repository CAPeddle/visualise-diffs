using Vdiff.Core.Output;

namespace Vdiff.Tests.Fixtures;

/// <summary>
/// Accept mode (D-06): the one way a golden file changes. A maintainer sets <see cref="Variable"/> to exactly
/// one shape name and runs the fixtures locally; the harness copies that fixture's <c>diff.json</c> and
/// <c>summary.mmd</c> from the run into its <c>expected/</c> directory, then fails the run so the change is
/// reviewed with <c>git diff</c> before it is committed. It refuses to run in CI, for any value but one shape
/// name, through a link, or outside the fixture's <c>expected/</c> directory. It never changes a
/// <c>state</c> in fixture.yml, and never creates <c>expected/</c> (a fixture without one is refused rather
/// than bootstrapped).
/// </summary>
public static class AcceptMode
{
    public const string Variable = "VDIFF_FIXTURE_ACCEPT";

    const string CiRefusal = "accept mode never runs in CI (TF_BUILD or CI is set)";
    const string ShapeRefusal = "VDIFF_FIXTURE_ACCEPT must name exactly one of the five shapes";

    static readonly string[] AcceptedFiles = [OutputWriter.DiffJsonFile, OutputWriter.MermaidFile];

    /// <summary>The value of <see cref="Variable"/> when it is exactly one shape name (no trimming, ordinal), else null.</summary>
    public static string? RequestedShape(Func<string, string?> getEnvironmentVariable)
    {
        var value = getEnvironmentVariable(Variable);
        return value is not null && Shapes.IsShape(value) ? value : null;
    }

    /// <summary>Null when accept mode is off or allowed; otherwise the fixed reason it must not run.</summary>
    public static string? Refusal(Func<string, string?> getEnvironmentVariable)
    {
        if (string.IsNullOrEmpty(getEnvironmentVariable(Variable)))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(getEnvironmentVariable("TF_BUILD")) || !string.IsNullOrEmpty(getEnvironmentVariable("CI")))
        {
            return CiRefusal;
        }

        return RequestedShape(getEnvironmentVariable) is null ? ShapeRefusal : null;
    }

    /// <summary>
    /// Copies the run's diff.json and summary.mmd into <c>&lt;fixtureDir&gt;/expected</c>, through no link, and
    /// returns the two relative paths. Every check runs before the first copy, so a refusal writes nothing.
    /// </summary>
    public static IReadOnlyList<string> Apply(string fixtureDir, string outDir)
    {
        var fixtureFull = Path.GetFullPath(fixtureDir);
        if (IsLink(new DirectoryInfo(fixtureFull)))
        {
            throw new InvalidOperationException("accept mode refuses a fixture directory that is a link");
        }

        var expectedFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(fixtureFull, "expected")));
        var expected = new DirectoryInfo(expectedFull);
        if (!expected.Exists)
        {
            throw new InvalidOperationException("accept mode needs the fixture's expected directory to exist");
        }

        if (IsLink(expected))
        {
            throw new InvalidOperationException("accept mode refuses an expected directory that is a link");
        }

        var copies = new List<(string Source, string Target, string Relative)>();
        foreach (var name in AcceptedFiles)
        {
            var target = Path.GetFullPath(Path.Combine(expectedFull, name));
            if (!string.Equals(Path.GetDirectoryName(target), expectedFull, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("accept mode writes only inside the fixture's expected directory");
            }

            var targetInfo = new FileInfo(target);
            if (targetInfo.Exists && IsLink(targetInfo))
            {
                throw new InvalidOperationException("accept mode refuses a golden file that is a link");
            }

            var source = new FileInfo(Path.Combine(outDir, name));
            if (!source.Exists || IsLink(source))
            {
                throw new InvalidOperationException("accept mode needs the run to have written both files");
            }

            copies.Add((source.FullName, target, "expected/" + name));
        }

        foreach (var (source, target, _) in copies)
        {
            File.Copy(source, target, overwrite: true);
        }

        return [.. copies.Select(c => c.Relative)];
    }

    static bool IsLink(FileSystemInfo info) =>
        info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null;
}
