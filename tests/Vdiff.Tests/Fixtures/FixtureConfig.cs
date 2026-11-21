namespace Vdiff.Tests.Fixtures;

// The fixture.yml document (D-05, D-13, D-18). These are plain mutable classes because YamlDotNet fills
// public setters; the keys are the snake_case spellings of the property names (shape, description, config,
// expected, repo, base, head; include, blank; diff, summary; state, due). Every key may be absent here so that
// a missing one reaches FixtureLoader as an empty string or null and gets a fixed message, rather than a
// YamlDotNet one. After Load succeeds, Expected, Diff and Summary are never null.

/// <summary>The whole fixture.yml.</summary>
public sealed class FixtureFile
{
    public string Shape { get; set; } = "";
    public string Description { get; set; } = "";
    public FixtureSettings? Config { get; set; }
    public ExpectedOutputs Expected { get; set; } = null!;
    public string? Repo { get; set; }
    public string? Base { get; set; }
    public string? Head { get; set; }
}

/// <summary>The tool configuration a fixture runs with: only the keys the CLI has today.</summary>
public sealed class FixtureSettings
{
    public List<string>? Include { get; set; }
    public List<string>? Blank { get; set; }
}

/// <summary>The two outputs a fixture has goldens for.</summary>
public sealed class ExpectedOutputs
{
    public OutputExpectation Diff { get; set; } = null!;
    public OutputExpectation Summary { get; set; } = null!;
}

/// <summary>One output's ratchet state: <c>pending</c> or <c>green</c>, and the phase it is due (display only).</summary>
public sealed class OutputExpectation
{
    public string State { get; set; } = "";
    public int? Due { get; set; }
}

/// <summary>Whether a fixture carries its source trees (synthetic) or names a local repository (private).</summary>
public enum FixtureKind
{
    Synthetic,
    Private,
}

/// <summary>One file of a fixture tree: its '/'-separated path under base/ or head/ and its exact bytes.</summary>
public sealed record TreeFile(string RelativePath, byte[] Bytes);

/// <summary>One output a fixture has a golden for: the short name, the file name, its state and its due phase.</summary>
public sealed record FixtureOutput(string Name, string FileName, string State, int? Due);

/// <summary>A fixture that passed every check of <see cref="FixtureLoader.Load"/>.</summary>
public sealed record LoadedFixture(
    string Shape,
    string Directory,
    FixtureKind Kind,
    FixtureFile File,
    IReadOnlyList<FixtureOutput> Outputs,
    IReadOnlyList<TreeFile> BaseTree,
    IReadOnlyList<TreeFile> HeadTree);

/// <summary>
/// A fixture could not be loaded. The message is always fixed text; it becomes a row of the published
/// fixture report. <see cref="Detail"/> is for the log and is null for a private fixture, so nothing from a
/// private fixture is ever echoed (D-13).
/// </summary>
public sealed class FixtureException(string message, string? detail) : Exception(message)
{
    public string? Detail { get; } = detail;
}
