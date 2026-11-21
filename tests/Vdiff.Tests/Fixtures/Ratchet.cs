namespace Vdiff.Tests.Fixtures;

/// <summary>What one run produced for one fixture output (RED stub: behaviour comes in the GREEN commit).</summary>
public sealed record OutputCheck(bool ToolSucceeded, bool GoldenPresent, bool OutputPresent, bool SchemaValid, bool Matched);

/// <summary>The ratchet's verdict for one fixture output.</summary>
public sealed record RatchetOutcome(bool Passed, string Status, string Detail);

public static class Ratchet
{
    public const string Pending = "pending";
    public const string Green = "green";

    public static RatchetOutcome Evaluate(string state, int? due, OutputCheck check) =>
        throw new NotImplementedException();
}
