using System.Diagnostics;
using System.Text;
using Vdiff.Core;
using Vdiff.Core.Extraction;
using Vdiff.Core.Process;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Safety;

/// <summary>
/// A misbehaving sidecar cannot hang, flood or poison a run (T-01-05, T-01-29). The fake
/// sidecar (<c>tests/Vdiff.FakeSidecar</c>) stands in for a hostile or broken child; its mode
/// and pid file travel in process-wide environment variables, so the class runs in the
/// <c>sidecar</c> collection, one test at a time, and restores every variable it sets.
/// </summary>
[Collection("sidecar")]
public class SidecarBoundsTests
{
    const long OneMiB = 1024 * 1024;

    static readonly string FakeSidecarPath = Path.Combine(
        AppContext.BaseDirectory, "Vdiff.FakeSidecar" + (OperatingSystem.IsWindows() ? ".exe" : ""));

    static SidecarCommand FakeCommand => new(FakeSidecarPath, []);

    static SidecarLimits TinyLimits(TimeSpan? timeout = null) =>
        new(timeout ?? TimeSpan.FromSeconds(60), OneMiB, 64 * 1024, TimeSpan.FromSeconds(10));

    static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    static readonly SidecarInput SecretInput = new("src/secret.h", Utf8("class TOP_SECRET_FILE_CONTENT {};\n"));

    /// <summary>Runs <paramref name="action"/> with the given process environment, restoring it afterwards.</summary>
    static T WithEnvironment<T>(IReadOnlyDictionary<string, string?> variables, Func<T> action)
    {
        var saved = variables.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (key, value) in variables)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            return action();
        }
        finally
        {
            foreach (var (key, value) in saved)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    static Dictionary<string, string?> Mode(string mode, string? pidFile = null) => new()
    {
        ["FAKE_SIDECAR_MODE"] = mode,
        ["FAKE_SIDECAR_PID_FILE"] = pidFile,
    };

    static string NewPidFile() => Path.Combine(Path.GetTempPath(), "vdiff-fake-pid-" + Guid.NewGuid().ToString("N"));

    static SidecarResponse Extract(string mode, SidecarLimits limits, IReadOnlyList<SidecarInput>? files = null, string? pidFile = null) =>
        WithEnvironment(Mode(mode, pidFile), () => CppSidecar.Extract(files ?? [SecretInput], [], FakeCommand, limits));

    static VdiffException ExtractFails(string mode, SidecarLimits limits, IReadOnlyList<SidecarInput>? files = null, string? pidFile = null) =>
        Assert.Throws<VdiffException>(() => Extract(mode, limits, files, pidFile));

    /// <summary>The fake wrote its pid when it started; the process must be gone shortly after a breach.</summary>
    static void AssertChildIsGone(string pidFile)
    {
        Assert.True(File.Exists(pidFile), "the fake sidecar never wrote its pid file, so it never started");
        var pid = int.Parse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return; // no process with that id any more
            }

            Thread.Sleep(100);
        }

        Assert.Fail($"the fake sidecar (pid {pid}) is still running after the run failed");
    }

    [Fact]
    public void The_fake_sidecar_binary_is_in_the_test_output()
    {
        Assert.True(File.Exists(FakeSidecarPath), FakeSidecarPath);
    }

    [Fact]
    public void A_hanging_sidecar_fails_at_the_timeout_naming_it_and_the_child_is_killed()
    {
        var pidFile = NewPidFile();
        try
        {
            var watch = Stopwatch.StartNew();
            var e = ExtractFails("hang", TinyLimits(TimeSpan.FromSeconds(3)), pidFile: pidFile);
            watch.Stop();

            Assert.Contains("time limit of 3", e.Message);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
            AssertChildIsGone(pidFile);
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    [Fact]
    public void A_sidecar_flooding_stdout_fails_naming_the_output_cap_and_the_child_is_killed()
    {
        var pidFile = NewPidFile();
        try
        {
            var e = ExtractFails("flood-stdout", TinyLimits(), pidFile: pidFile);

            Assert.Contains($"more than {OneMiB} bytes to stdout", e.Message);
            AssertChildIsGone(pidFile);
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    [Fact]
    public void A_stderr_flood_is_drained_and_a_valid_response_after_it_is_accepted()
    {
        var response = Extract("flood-stderr", TinyLimits());

        Assert.Equal(CppSidecar.ResponseSchema, response.Schema);
        Assert.Empty(response.Files!);
    }

    [Fact]
    public void The_stderr_tail_of_a_flood_is_bounded_to_its_cap_and_keeps_the_end()
    {
        const int cap = 64 * 1024;
        var result = WithEnvironment(
            Mode("flood-stderr"),
            () => BoundedProcess.Run(FakeSidecarPath, [], new ProcessLimits(TimeSpan.FromSeconds(60), OneMiB, cap)));

        Assert.Equal(0, result.ExitCode);
        Assert.True(Encoding.UTF8.GetByteCount(result.StderrTail) <= cap, $"tail is {result.StderrTail.Length} characters");
        Assert.Contains("fake-sidecar-stderr-end-marker", result.StderrTail);
    }

    [Fact]
    public void A_non_zero_exit_fails_with_the_stderr_tail_and_never_with_file_content()
    {
        var e = ExtractFails("exit3", TinyLimits());

        Assert.Contains("exited with code 3", e.Message);
        Assert.Contains("fake sidecar failure: boom", e.Message);
        Assert.DoesNotContain("TOP_SECRET_FILE_CONTENT", e.Message);
    }

    [Theory]
    [InlineData("garbage", "not valid JSON")]
    [InlineData("truncated", "not valid JSON")]
    [InlineData("wrong-schema", "unexpected schema")]
    [InlineData("unknown-status", "unknown status")]
    public void A_malformed_or_wrong_response_is_a_tool_failure_never_a_result(string mode, string expected)
    {
        var e = ExtractFails(mode, TinyLimits());

        Assert.Contains(expected, e.Message);
        Assert.DoesNotContain("TOP_SECRET_FILE_CONTENT", e.Message);
    }

    [Fact]
    public void An_empty_but_valid_response_succeeds()
    {
        var response = Extract("ok-empty", TinyLimits());

        Assert.Equal(CppSidecar.ResponseSchema, response.Schema);
        Assert.Equal("fake", response.TreeSitter);
        Assert.Empty(response.Files!);
    }

    [Fact]
    public void A_child_that_ignores_a_large_manifest_and_exits_at_once_neither_deadlocks_nor_throws_an_IOException()
    {
        // 32 MiB of input dwarfs any pipe buffer, so the writer is mid-write when the child exits (C18, C30).
        var large = new SidecarInput("src/large.h", new byte[32 * 1024 * 1024]);
        var watch = Stopwatch.StartNew();

        var response = Extract("ok-empty", TinyLimits(), [large]);

        Assert.Empty(response.Files!);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), $"took {watch.Elapsed}");
    }

    [Fact]
    public void A_child_that_ignores_a_large_manifest_and_fails_is_decided_by_its_exit_code()
    {
        var large = new SidecarInput("src/large.h", new byte[32 * 1024 * 1024]);

        var e = ExtractFails("exit3", TinyLimits(), [large]);

        Assert.Contains("exited with code 3", e.Message);
    }

    [Theory]
    [InlineData("ok-timeout-file", "big.h", "timeout", 0)]
    [InlineData("ok-error-file", "bad.h", "error", 3)]
    public void A_file_the_sidecar_labels_timeout_or_error_is_handed_to_the_caller_with_that_status(
        string mode, string path, string status, int errorNodes)
    {
        var response = Extract(mode, TinyLimits());

        var file = Assert.Single(response.Files!);
        Assert.Equal(path, file.Path);
        Assert.Equal(status, file.Status);
        Assert.Equal(errorNodes, file.ErrorNodes);
        Assert.Empty(file.Types);
    }
}
