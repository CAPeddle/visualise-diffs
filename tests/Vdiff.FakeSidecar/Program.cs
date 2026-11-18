using System.Diagnostics;
using System.Text;

// A stand-in for the real vdiff-cpp-ts sidecar that misbehaves on purpose (T-01-05, T-01-29).
// The mode comes from the FAKE_SIDECAR_MODE environment variable, so it can also be selected
// through VDIFF_CPP_TS, which can only name an executable. The process id is written to the
// file named by FAKE_SIDECAR_PID_FILE (when set) so a test can confirm the child was killed.
// No mode reads stdin: a child that ignores its input is part of what is being proven.
// Mode ok-empty-after-delay sleeps for FAKE_SIDECAR_DELAY_MS before it replies like ok-empty,
// so a test can act while a real vdiff run is still busy (plan 01-17).

var mode = Environment.GetEnvironmentVariable("FAKE_SIDECAR_MODE") ?? "";
var pidFile = Environment.GetEnvironmentVariable("FAKE_SIDECAR_PID_FILE");
if (!string.IsNullOrEmpty(pidFile))
{
    File.WriteAllText(pidFile, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

const string Prefix = "{\"schema\":\"vdiff-cpp-ts/v0\",\"tree_sitter\":\"fake\",\"tree_sitter_cpp\":\"fake\",\"macros_blanked\":[],\"files\":";

static void Reply(string filesJson)
{
    using var stdout = Console.OpenStandardOutput();
    stdout.Write(Encoding.UTF8.GetBytes(Prefix + filesJson + "}\n"));
    stdout.Flush();
}

static void FloodStream(Stream stream, long bytes)
{
    var chunk = new byte[64 * 1024];
    Array.Fill(chunk, (byte)'x');
    for (long written = 0; bytes < 0 || written < bytes; written += chunk.Length)
    {
        stream.Write(chunk, 0, chunk.Length);
    }

    stream.Flush();
}

switch (mode)
{
    case "hang":
        Thread.Sleep(Timeout.Infinite);
        return 0;

    case "flood-stdout":
        using (var stdout = Console.OpenStandardOutput())
        {
            try
            {
                FloodStream(stdout, -1);
            }
            catch (IOException)
            {
                // the parent stopped reading and killed us
            }
        }

        return 0;

    case "flood-stderr":
        using (var stderr = Console.OpenStandardError())
        {
            FloodStream(stderr, 8L * 1024 * 1024);
            stderr.Write(Encoding.UTF8.GetBytes("\nfake-sidecar-stderr-end-marker\n"));
            stderr.Flush();
        }

        Reply("[]");
        return 0;

    case "exit3":
        Console.Error.WriteLine("fake sidecar failure: boom");
        return 3;

    case "garbage":
        Console.Out.Write("this is not json at all");
        Console.Out.Flush();
        return 0;

    case "truncated":
        Console.Out.Write(Prefix.Substring(0, Prefix.Length / 2));
        Console.Out.Flush();
        return 0;

    case "wrong-schema":
        Console.Out.Write("{\"schema\":\"vdiff-cpp-ts/v99\",\"files\":[]}\n");
        Console.Out.Flush();
        return 0;

    case "ok-empty":
        Reply("[]");
        return 0;

    case "ok-empty-after-delay":
        // Keeps a real vdiff run busy for FAKE_SIDECAR_DELAY_MS (5000 when unset or not a
        // non-negative integer), then succeeds with an empty file list, as ok-empty does.
        var delayText = Environment.GetEnvironmentVariable("FAKE_SIDECAR_DELAY_MS");
        var delayMs = int.TryParse(delayText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsedDelay)
            ? parsedDelay
            : 5000;
        Thread.Sleep(delayMs);
        Reply("[]");
        return 0;

    case "ok-timeout-file":
        Reply("[{\"path\":\"big.h\",\"status\":\"timeout\",\"error_nodes\":0,\"types\":[]}]");
        return 0;

    case "ok-error-file":
        Reply("[{\"path\":\"bad.h\",\"status\":\"error\",\"error_nodes\":3,\"types\":[]}]");
        return 0;

    case "unknown-status":
        Reply("[{\"path\":\"odd.h\",\"status\":\"partial\",\"error_nodes\":0,\"types\":[]}]");
        return 0;

    default:
        Console.Error.WriteLine("fake sidecar: unknown FAKE_SIDECAR_MODE");
        return 64;
}
