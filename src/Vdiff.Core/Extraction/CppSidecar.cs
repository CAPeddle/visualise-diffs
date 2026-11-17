using System.Text.Encodings.Web;
using System.Text.Json;
using Vdiff.Core.Process;

namespace Vdiff.Core.Extraction;

/// <summary>How to start the sidecar: an executable and any leading arguments (for example a runtime and a dll).</summary>
public sealed record SidecarCommand(string File, IReadOnlyList<string> LeadingArgs);

/// <summary>Limits for one sidecar run (T-01-05, T-01-32).</summary>
public sealed record SidecarLimits(TimeSpan Timeout, long MaxStdoutBytes, long MaxStderrBytes, TimeSpan ParseBudget)
{
    static readonly SidecarLimits BuiltIn =
        new(TimeSpan.FromSeconds(120), 64L * 1024 * 1024, 1024 * 1024, TimeSpan.FromSeconds(10));

    public static SidecarLimits Default => BuiltIn;

    public static SidecarLimits FromEnvironment(Func<string, string?> getEnvironmentVariable) => BuiltIn;
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

/// <summary>
/// Client of the Rust tree-sitter sidecar (ADR-001): builds the stdin manifest, runs the
/// sidecar as a bounded child process, and parses and checks its one JSON answer. Every
/// failure is a <see cref="VdiffException"/> that carries at most a stderr tail and never
/// file content, and the client fails closed: a bad exit, empty output, bad JSON, a wrong
/// schema or an unknown file status is an error, never a silently empty model.
/// </summary>
public static class CppSidecar
{
    public const string RequestSchema = "vdiff-cpp-ts-request/v0";
    public const string ResponseSchema = "vdiff-cpp-ts/v0";

    const string EnvironmentVariable = "VDIFF_CPP_TS";
    const string CargoCommand = "cargo build --release --locked --manifest-path extractors/cpp-ts/Cargo.toml";
    const int StderrInMessage = 1000;

    static readonly string BinaryName = "vdiff-cpp-ts" + (OperatingSystem.IsWindows() ? ".exe" : "");

    // Unknown JSON properties are ignored (the default), so the sidecar can add fields
    // without breaking this client.
    static readonly JsonSerializerOptions ResponseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    static readonly JsonSerializerOptions RequestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Locates the sidecar from the app directory and the process environment.</summary>
    public static SidecarCommand Locate() =>
        Locate(AppContext.BaseDirectory, Environment.GetEnvironmentVariable);

    /// <summary>
    /// In order: the file named by the VDIFF_CPP_TS variable (set by the invoking user on their
    /// own machine, the same trust as PATH, T-01-09); the binary beside the app; the binary in
    /// <c>extractors/cpp-ts</c> beside the app; the cargo release build under the first folder
    /// found by walking up that holds <c>extractors/cpp-ts/Cargo.toml</c>.
    /// </summary>
    public static SidecarCommand Locate(string startDirectory, Func<string, string?> getEnvironmentVariable)
    {
        var fromVariable = getEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrEmpty(fromVariable))
        {
            if (File.Exists(fromVariable))
            {
                return new SidecarCommand(fromVariable, []);
            }

            throw new VdiffException(
                $"the {EnvironmentVariable} variable names a file that does not exist; unset it or point it at the vdiff-cpp-ts binary");
        }

        var beside = Path.Combine(startDirectory, BinaryName);
        if (File.Exists(beside))
        {
            return new SidecarCommand(beside, []);
        }

        var nested = Path.Combine(startDirectory, "extractors", "cpp-ts", BinaryName);
        if (File.Exists(nested))
        {
            return new SidecarCommand(nested, []);
        }

        for (var dir = Path.GetFullPath(startDirectory); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "extractors", "cpp-ts", "Cargo.toml")))
            {
                var built = Path.Combine(dir, "extractors", "cpp-ts", "target", "release", BinaryName);
                if (File.Exists(built))
                {
                    return new SidecarCommand(built, []);
                }

                break;
            }
        }

        throw new VdiffException(
            $"the C++ sidecar (vdiff-cpp-ts) was not found; build it with `{CargoCommand}` " +
            $"or set the {EnvironmentVariable} variable to the path of the binary");
    }

    /// <summary>
    /// The stdin manifest (protocol v0): one JSON header line ended by a line feed, then the
    /// raw bytes of each file back to back. Lengths are byte lengths of the content.
    /// </summary>
    public static byte[] BuildRequest(IReadOnlyList<SidecarInput> files, IReadOnlyList<string> blank, TimeSpan parseBudget)
    {
        // The sidecar accepts 1 to 600000 ms and refuses anything else with exit 2.
        var budgetMs = (long)parseBudget.TotalMilliseconds;
        if (budgetMs is < 1 or > 600_000)
        {
            throw new VdiffException("the per-file parse budget must be between 1 ms and 600000 ms");
        }

        var header = new
        {
            Schema = RequestSchema,
            Blank = blank,
            ParseBudgetMs = budgetMs,
            Files = files.Select(f => new { f.Path, Len = f.Bytes.Length }).ToList(),
        };

        using var stream = new MemoryStream();
        stream.Write(JsonSerializer.SerializeToUtf8Bytes(header, RequestOptions));
        stream.WriteByte((byte)'\n');
        foreach (var file in files)
        {
            stream.Write(file.Bytes);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Parses and checks the sidecar's answer. A file with status "timeout" or "error" is
    /// returned with that status, never dropped (D-11); anything else is rejected.
    /// </summary>
    public static SidecarResponse ParseResponse(byte[] stdout, long maxBytes)
    {
        if (stdout.Length == 0)
        {
            throw new VdiffException("the sidecar produced no output");
        }

        if (stdout.Length > maxBytes)
        {
            throw new VdiffException($"the sidecar response is larger than the limit of {maxBytes} bytes");
        }

        SidecarResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<SidecarResponse>(stdout, ResponseOptions);
        }
        catch (JsonException)
        {
            // The exception text can quote input, so it is not passed on.
            throw new VdiffException("the sidecar response is not valid JSON of the expected shape");
        }

        if (response is null)
        {
            throw new VdiffException("the sidecar response is not valid JSON of the expected shape");
        }

        if (!string.Equals(response.Schema, ResponseSchema, StringComparison.Ordinal))
        {
            throw new VdiffException($"the sidecar response has an unexpected schema (expected {ResponseSchema})");
        }

        if (response.Files is null)
        {
            throw new VdiffException("the sidecar response has no files list");
        }

        foreach (var file in response.Files)
        {
            if (file is null || file.Status is not ("ok" or "timeout" or "error"))
            {
                throw new VdiffException("the sidecar response has a file with a missing or unknown status (expected ok, timeout or error)");
            }
        }

        return response;
    }

    /// <summary>
    /// Runs the sidecar once, even with zero files so the version metadata is always present
    /// (ADR-001), through <see cref="BoundedProcess"/>.
    /// </summary>
    public static SidecarResponse Extract(
        IReadOnlyList<SidecarInput> files, IReadOnlyList<string> blank, SidecarCommand command, SidecarLimits limits)
    {
        var request = BuildRequest(files, blank, limits.ParseBudget);
        var processLimits = new ProcessLimits(limits.Timeout, limits.MaxStdoutBytes, limits.MaxStderrBytes);
        var result = BoundedProcess.Run(command.File, command.LeadingArgs, processLimits, request);

        if (result.ExitCode != 0)
        {
            throw new VdiffException($"the sidecar exited with code {result.ExitCode}" + StderrSuffix(result));
        }

        if (result.Stdout.Length == 0)
        {
            throw new VdiffException("the sidecar produced no output" + StderrSuffix(result));
        }

        return ParseResponse(result.Stdout, limits.MaxStdoutBytes);
    }

    static string StderrSuffix(ProcessResult result)
    {
        var tail = result.StderrTail.Trim();
        if (tail.Length > StderrInMessage)
        {
            tail = tail[^StderrInMessage..];
        }

        return tail.Length > 0 ? ": " + tail : ".";
    }
}

