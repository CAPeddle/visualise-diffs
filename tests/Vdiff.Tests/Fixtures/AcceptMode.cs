namespace Vdiff.Tests.Fixtures;

/// <summary>RED-phase stub (plan 02-07 task 3); replaced in the GREEN commit.</summary>
public static class AcceptMode
{
    public const string Variable = "VDIFF_FIXTURE_ACCEPT";

    public static string? RequestedShape(Func<string, string?> getEnvironmentVariable) => null;

    public static string? Refusal(Func<string, string?> getEnvironmentVariable) => null;

    public static IReadOnlyList<string> Apply(string fixtureDir, string outDir) => [];
}
