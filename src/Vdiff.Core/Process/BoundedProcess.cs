using System.Text;

namespace Vdiff.Core.Process;

/// <summary>Limits applied to one child process run.</summary>
public sealed record ProcessLimits(TimeSpan Timeout, long MaxStdoutBytes, long MaxStderrBytes);

/// <summary>Outcome of a child process that finished inside its limits.</summary>
public sealed record ProcessResult(int ExitCode, byte[] Stdout, string StderrTail)
{
    public string StdoutText => Encoding.UTF8.GetString(Stdout);
}

public static class BoundedProcess
{
    public static ProcessResult Run(
        string file,
        IReadOnlyList<string> args,
        ProcessLimits limits,
        byte[]? stdin = null,
        IReadOnlyDictionary<string, string?>? env = null,
        string? workingDirectory = null)
        => throw new NotImplementedException();
}

public static class GitCommand
{
    public static IReadOnlyList<string> Prefix => throw new NotImplementedException();

    public static Dictionary<string, string?> BuildEnvironment(IReadOnlyDictionary<string, string?>? inherited = null)
        => throw new NotImplementedException();
}
