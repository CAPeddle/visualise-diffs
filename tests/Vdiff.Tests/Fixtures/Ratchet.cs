namespace Vdiff.Tests.Fixtures;

/// <summary>
/// What one run produced for one fixture output. A false <see cref="ToolSucceeded"/>,
/// <see cref="GoldenPresent"/>, <see cref="OutputPresent"/> or <see cref="SchemaValid"/> means the run
/// proved nothing about the content, whatever <see cref="Matched"/> says.
/// </summary>
public sealed record OutputCheck(bool ToolSucceeded, bool GoldenPresent, bool OutputPresent, bool SchemaValid, bool Matched);

/// <summary>The ratchet's verdict for one fixture output. Status is RED, GREEN or FAIL.</summary>
public sealed record RatchetOutcome(bool Passed, string Status, string Detail);

/// <summary>
/// The expected-failure ratchet (D-10, D-18). A fixture output is <c>pending</c> (red by design until the
/// work that makes it match lands) or <c>green</c> (must match its golden). The rule branches on the state
/// alone: <c>due</c> is shown in the detail but never compared with a phase number, so a pending output
/// stays pending until someone sets it to green. A crashed or broken run is FAIL for every state, so a
/// broken harness can never look like "red by design". Every detail is fixed text: no path, SHA, type name
/// or echoed value, so private mode can print outcomes safely.
/// </summary>
public static class Ratchet
{
    public const string Pending = "pending";
    public const string Green = "green";

    const string Red = "RED";
    const string GreenStatus = "GREEN";
    const string Fail = "FAIL";

    public static RatchetOutcome Evaluate(string state, int? due, OutputCheck check)
    {
        if (!check.ToolSucceeded)
        {
            return Failed("the tool run failed");
        }

        if (!check.GoldenPresent)
        {
            return Failed("the expected file is missing");
        }

        if (!check.OutputPresent)
        {
            return Failed("the tool wrote no such output");
        }

        if (!check.SchemaValid)
        {
            return Failed("the output does not validate against its schema");
        }

        return state switch
        {
            Pending when !check.Matched =>
                new RatchetOutcome(true, Red, due is { } phase ? $"pending, due phase {phase}" : "pending"),
            Pending => Failed("unexpectedly matches the golden: set state: green in fixture.yml"),
            Green when check.Matched => new RatchetOutcome(true, GreenStatus, "matches"),
            Green => Failed("does not match the golden"),
            _ => Failed("unknown state in fixture.yml"),
        };
    }

    static RatchetOutcome Failed(string detail) => new(false, Fail, detail);
}
