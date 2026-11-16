namespace Vdiff.Core.Extraction;

/// <summary>How to start the sidecar: an executable and any leading arguments (for example a runtime and a dll).</summary>
public sealed record SidecarCommand(string File, IReadOnlyList<string> LeadingArgs);

/// <summary>Limits for one sidecar run (T-01-05, T-01-32).</summary>
public sealed record SidecarLimits(TimeSpan Timeout, long MaxStdoutBytes, long MaxStderrBytes, TimeSpan ParseBudget)
{
    public static SidecarLimits Default { get; } =
        new(TimeSpan.FromSeconds(120), 64L * 1024 * 1024, 1024 * 1024, TimeSpan.FromSeconds(10));
}

/// <summary>One file handed to the sidecar: a label path and the raw bytes.</summary>
public sealed record SidecarInput(string Path, byte[] Bytes);

public sealed record SidecarMember
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Access { get; init; } = "";
    public string Key { get; init; } = "";
    public string Signature { get; init; } = "";
    public int StartByte { get; init; }
    public int EndByte { get; init; }
    public int StartLine { get; init; }
    public int EndLine { get; init; }
}

public sealed record SidecarType
{
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
    public string Scope { get; init; } = "";
    public IReadOnlyList<string> Bases { get; init; } = [];
    public int StartByte { get; init; }
    public int EndByte { get; init; }
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public IReadOnlyList<SidecarMember> Members { get; init; } = [];
}

public sealed record SidecarFile
{
    public string Path { get; init; } = "";
    public string Status { get; init; } = "";
    public int ErrorNodes { get; init; }
    public IReadOnlyList<SidecarType> Types { get; init; } = [];
}

public sealed record SidecarResponse
{
    public string Schema { get; init; } = "";
    public string TreeSitter { get; init; } = "";
    public string TreeSitterCpp { get; init; } = "";
    public IReadOnlyList<string> MacrosBlanked { get; init; } = [];
    public IReadOnlyList<SidecarFile>? Files { get; init; }
}

public static class CppSidecar
{
    public const string RequestSchema = "vdiff-cpp-ts-request/v0";
    public const string ResponseSchema = "vdiff-cpp-ts/v0";

    public static SidecarCommand Locate() => throw new NotImplementedException();

    public static SidecarCommand Locate(string startDirectory, Func<string, string?> getEnvironmentVariable) =>
        throw new NotImplementedException();

    public static byte[] BuildRequest(IReadOnlyList<SidecarInput> files, IReadOnlyList<string> blank, TimeSpan parseBudget) =>
        throw new NotImplementedException();

    public static SidecarResponse ParseResponse(byte[] stdout, long maxBytes) => throw new NotImplementedException();

    public static SidecarResponse Extract(
        IReadOnlyList<SidecarInput> files, IReadOnlyList<string> blank, SidecarCommand command, SidecarLimits limits) =>
        throw new NotImplementedException();
}
