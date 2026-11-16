using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using SysProcess = System.Diagnostics.Process;

namespace Vdiff.Core.Process;

/// <summary>Limits applied to one child process run.</summary>
public sealed record ProcessLimits(TimeSpan Timeout, long MaxStdoutBytes, long MaxStderrBytes);

/// <summary>Outcome of a child process that finished inside its limits.</summary>
/// <param name="StderrTail">The last bytes of stderr up to the stderr cap, decoded as UTF-8.</param>
public sealed record ProcessResult(int ExitCode, byte[] Stdout, string StderrTail)
{
    public string StdoutText => Encoding.UTF8.GetString(Stdout);
}

/// <summary>
/// Runs a child process with an argument array (never a shell), a timeout, a stdout cap and
/// a stderr cap. Both output streams are drained concurrently as raw bytes. A breach of the
/// timeout or of the stdout cap kills the whole process tree and fails with a
/// <see cref="VdiffException"/> (T-01-05).
/// </summary>
public static class BoundedProcess
{
    const int StderrInMessage = 1000;

    public static ProcessResult Run(
        string file,
        IReadOnlyList<string> args,
        ProcessLimits limits,
        byte[]? stdin = null,
        IReadOnlyDictionary<string, string?>? env = null,
        string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        if (workingDirectory is not null)
        {
            psi.WorkingDirectory = workingDirectory;
        }

        if (env is not null)
        {
            psi.Environment.Clear();
            foreach (var (key, value) in env)
            {
                if (value is not null)
                {
                    psi.Environment[key] = value;
                }
            }
        }

        SysProcess process;
        try
        {
            process = SysProcess.Start(psi) ?? throw new VdiffException($"could not start '{file}'");
        }
        catch (Win32Exception e)
        {
            throw new VdiffException($"could not start '{file}': {e.Message}", e);
        }

        using (process)
        {
            var stdoutBuffer = new MemoryStream();
            var overflow = 0;
            var stderrTail = new TailBuffer(limits.MaxStderrBytes);

            var stdinTask = Task.Run(() => WriteStdin(process, stdin));
            var stdoutTask = Task.Run(() => DrainStdout(process, stdoutBuffer, limits.MaxStdoutBytes, () => Interlocked.Exchange(ref overflow, 1)));
            var stderrTask = Task.Run(() => DrainStderr(process, stderrTail));

            var timedOut = false;
            if (!process.WaitForExit(limits.Timeout))
            {
                timedOut = true;
                KillTree(process);
                process.WaitForExit();
            }

            // After a normal exit the pipes close with the child. A grandchild that inherited
            // them could keep a drain open, so the wait is bounded and ends in a tree kill.
            if (!Task.WaitAll([stdoutTask, stderrTask, stdinTask], TimeSpan.FromSeconds(10)))
            {
                KillTree(process);
                Task.WaitAll([stdoutTask, stderrTask, stdinTask], TimeSpan.FromSeconds(10));
            }

            var tail = stderrTail.ToString();
            if (timedOut)
            {
                throw new VdiffException(
                    $"'{file}' exceeded the time limit of {limits.Timeout.TotalSeconds:0.##} s and was stopped" + Suffix(tail));
            }

            if (Volatile.Read(ref overflow) != 0)
            {
                throw new VdiffException(
                    $"'{file}' wrote more than {limits.MaxStdoutBytes} bytes to stdout and was stopped" + Suffix(tail));
            }

            return new ProcessResult(process.ExitCode, stdoutBuffer.ToArray(), tail);
        }
    }

    static string Suffix(string stderrTail)
    {
        if (stderrTail.Length == 0)
        {
            return ".";
        }

        var shown = stderrTail.Length > StderrInMessage ? stderrTail[^StderrInMessage..] : stderrTail;
        return "; stderr: " + shown.Trim();
    }

    static void WriteStdin(SysProcess process, byte[]? stdin)
    {
        // A child that exits before reading all of stdin closes the pipe: the IOException is
        // expected (C18) and the exit code and stderr decide the result.
        try
        {
            var stream = process.StandardInput.BaseStream;
            if (stdin is { Length: > 0 })
            {
                stream.Write(stdin, 0, stdin.Length);
            }

            stream.Flush();
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }
    }

    static void DrainStdout(SysProcess process, MemoryStream buffer, long cap, Action markOverflow)
    {
        var stream = process.StandardOutput.BaseStream;
        var chunk = new byte[81920];
        try
        {
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > cap)
                {
                    markOverflow();
                    KillTree(process);
                    return;
                }

                buffer.Write(chunk, 0, read);
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
        }
    }

    static void DrainStderr(SysProcess process, TailBuffer tail)
    {
        var stream = process.StandardError.BaseStream;
        var chunk = new byte[8192];
        try
        {
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                tail.Append(chunk, read);
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
        }
    }

    static void KillTree(SysProcess process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
            // already exited
        }
    }

    /// <summary>Keeps only the last <c>cap</c> bytes written; everything before is discarded.</summary>
    sealed class TailBuffer(long cap)
    {
        readonly MemoryStream _data = new();
        readonly object _lock = new();

        public void Append(byte[] chunk, int count)
        {
            lock (_lock)
            {
                _data.Write(chunk, 0, count);
                if (_data.Length > 2 * Math.Max(cap, 1))
                {
                    Trim();
                }
            }
        }

        void Trim()
        {
            var bytes = _data.ToArray();
            var keep = (int)Math.Min(cap, bytes.Length);
            _data.SetLength(0);
            _data.Write(bytes, bytes.Length - keep, keep);
        }

        public override string ToString()
        {
            lock (_lock)
            {
                Trim();
                return Encoding.UTF8.GetString(_data.ToArray());
            }
        }
    }
}

/// <summary>
/// The single definition of the read-only git argument prefix and child environment. Production
/// code and the test snapshot helper both use it, so no git call can run a repository's
/// configured fsmonitor (which can execute a command or start a daemon that writes into
/// .git, T-01-30) or refresh the index. Production code never sets GIT_CONFIG_NOSYSTEM or
/// GIT_CONFIG_GLOBAL: the machine's global configuration may carry safe.directory entries.
/// </summary>
public static class GitCommand
{
    static readonly string[] PrefixArguments = ["-c", "core.fsmonitor=false", "--no-optional-locks"];

    static readonly string[] RemovedVariables =
    [
        "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR", "GIT_NAMESPACE", "GIT_EXTERNAL_DIFF",
    ];

    /// <summary><c>-c core.fsmonitor=false --no-optional-locks</c>, placed before every git verb.</summary>
    public static IReadOnlyList<string> Prefix { get; } = Array.AsReadOnly(PrefixArguments);

    /// <summary>The full argument list for one git call against a repository.</summary>
    public static IReadOnlyList<string> Build(string repoPath, IEnumerable<string> verbArguments)
    {
        var args = new List<string>(PrefixArguments) { "-C", repoPath };
        args.AddRange(verbArguments);
        return args;
    }

    /// <summary>
    /// The environment for a git child: the inherited one, minus the variables that can
    /// redirect git to another repository, plus GIT_OPTIONAL_LOCKS=0, GIT_TERMINAL_PROMPT=0
    /// and GIT_NO_LAZY_FETCH=1 (a missing object in a partial clone then fails offline
    /// instead of being fetched into .git, T-01-08).
    /// </summary>
    public static Dictionary<string, string?> BuildEnvironment(IReadOnlyDictionary<string, string?>? inherited = null)
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (inherited is not null)
        {
            foreach (var (key, value) in inherited)
            {
                env[key] = value;
            }
        }
        else
        {
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                env[(string)entry.Key] = (string?)entry.Value;
            }
        }

        foreach (var name in RemovedVariables)
        {
            env.Remove(name);
        }

        env["GIT_OPTIONAL_LOCKS"] = "0";
        env["GIT_TERMINAL_PROMPT"] = "0";
        env["GIT_NO_LAZY_FETCH"] = "1";
        return env;
    }

    /// <summary>Runs git against a repository through <see cref="BoundedProcess"/>.</summary>
    public static ProcessResult Run(string repoPath, IEnumerable<string> verbArguments, ProcessLimits limits, byte[]? stdin = null) =>
        BoundedProcess.Run("git", Build(repoPath, verbArguments), limits, stdin, BuildEnvironment());
}
