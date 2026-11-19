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

    // ---- operational settings: the timeout and the per-file parse budget ----

    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);
    static readonly TimeSpan DefaultParseBudget = TimeSpan.FromSeconds(10);

    static SidecarLimits LimitsFrom(string name, string? value) =>
        SidecarLimits.FromEnvironment(n => n == name ? value : null);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1201")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("2.5")]
    [InlineData(" 5")]
    public void A_timeout_override_outside_1_to_1200_or_not_an_integer_is_ignored(string value)
    {
        var limits = LimitsFrom("VDIFF_SIDECAR_TIMEOUT_SECONDS", value);

        Assert.Equal(DefaultTimeout, limits.Timeout);
        Assert.Equal(DefaultParseBudget, limits.ParseBudget);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1200", 1200)]
    public void A_timeout_override_from_1_to_1200_is_accepted(string value, int seconds)
    {
        var limits = LimitsFrom("VDIFF_SIDECAR_TIMEOUT_SECONDS", value);

        Assert.Equal(TimeSpan.FromSeconds(seconds), limits.Timeout);
        Assert.Equal(DefaultParseBudget, limits.ParseBudget);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("600001")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("0.5")]
    public void A_parse_budget_override_outside_1_to_600000_or_not_an_integer_is_ignored(string value)
    {
        var limits = LimitsFrom("VDIFF_PARSE_BUDGET_MS", value);

        Assert.Equal(DefaultParseBudget, limits.ParseBudget);
        Assert.Equal(DefaultTimeout, limits.Timeout);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("600000", 600000)]
    public void A_parse_budget_override_from_1_to_600000_is_accepted(string value, int milliseconds)
    {
        var limits = LimitsFrom("VDIFF_PARSE_BUDGET_MS", value);

        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), limits.ParseBudget);
        Assert.Equal(DefaultTimeout, limits.Timeout);
    }

    [Fact]
    public void An_override_never_changes_the_output_or_stderr_caps()
    {
        var limits = SidecarLimits.FromEnvironment(n => n is "VDIFF_SIDECAR_TIMEOUT_SECONDS" or "VDIFF_PARSE_BUDGET_MS" ? "5" : null);

        Assert.Equal(SidecarLimits.Default.MaxStdoutBytes, limits.MaxStdoutBytes);
        Assert.Equal(SidecarLimits.Default.MaxStderrBytes, limits.MaxStderrBytes);
        Assert.Equal(64L * OneMiB, limits.MaxStdoutBytes);
    }

    [Fact]
    public void The_default_limits_read_the_process_environment()
    {
        var limits = WithEnvironment(
            new Dictionary<string, string?> { ["VDIFF_SIDECAR_TIMEOUT_SECONDS"] = "7", ["VDIFF_PARSE_BUDGET_MS"] = "250" },
            () => SidecarLimits.Default);

        Assert.Equal(TimeSpan.FromSeconds(7), limits.Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(250), limits.ParseBudget);
    }

    [Fact]
    public void The_not_found_error_names_both_operational_settings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vdiff-sc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var e = Assert.Throws<VdiffException>(() => CppSidecar.Locate(dir, _ => null));

            Assert.Contains("VDIFF_SIDECAR_TIMEOUT_SECONDS", e.Message);
            Assert.Contains("VDIFF_PARSE_BUDGET_MS", e.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---- CLI level: a failing sidecar is a tool failure with nothing written ----

    static (string BaseId, string HeadId) BuildSmallRepo(SyntheticRepo repo)
    {
        repo.Write("include/shop/gadget.h", "#pragma once\nnamespace shop {\nclass Gadget {\npublic:\n    int size() const;\n};\n}\n");
        var baseId = repo.Commit("base");
        repo.Write("include/shop/gadget.h", "#pragma once\nnamespace shop {\nclass Gadget {\npublic:\n    long size() const;\n    void grow(int by);\n};\n}\n");
        var headId = repo.Commit("head");
        return (baseId, headId);
    }

    static int RunCli(IReadOnlyDictionary<string, string?> environment, string[] args, out string stdout, out string stderr)
    {
        var result = WithEnvironment(environment, () =>
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            var exit = Vdiff.Core.Cli.CliApp.Run(args, outWriter, errWriter);
            return (exit, outWriter.ToString(), errWriter.ToString());
        });
        stdout = result.Item2;
        stderr = result.Item3;
        return result.exit;
    }

    static Dictionary<string, string?> FakeSidecarEnvironment(string mode, string? pidFile = null)
    {
        var variables = Mode(mode, pidFile);
        variables["VDIFF_CPP_TS"] = FakeSidecarPath;
        variables["VDIFF_SIDECAR_TIMEOUT_SECONDS"] = null;
        variables["VDIFF_PARSE_BUDGET_MS"] = null;
        return variables;
    }

    static string[] CliArgs(SyntheticRepo repo, string baseId, string headId, string outDir) =>
        ["--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", outDir];

    [Fact]
    public void A_hanging_sidecar_makes_the_cli_exit_1_within_seconds_naming_the_timeout_and_writes_nothing()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallRepo(repo);
        var outDir = Path.Combine(repo.Root, "failed-out");
        var before = RepoSnapshot.Take(repo.Root);
        var pidFile = NewPidFile();
        try
        {
            var environment = FakeSidecarEnvironment("hang", pidFile);
            environment["VDIFF_SIDECAR_TIMEOUT_SECONDS"] = "2";

            var watch = Stopwatch.StartNew();
            var exit = RunCli(environment, CliArgs(repo, baseId, headId, outDir), out _, out var stderr);
            watch.Stop();

            Assert.Equal(1, exit);
            Assert.StartsWith("vdiff: ", stderr);
            Assert.Contains("time limit of 2", stderr);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
            Assert.False(Directory.Exists(outDir));
            AssertChildIsGone(pidFile);
            Assert.Equal(before, RepoSnapshot.Take(repo.Root));
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    [Theory]
    [InlineData("exit3", "exited with code 3")]
    [InlineData("garbage", "not valid JSON")]
    [InlineData("truncated", "not valid JSON")]
    [InlineData("wrong-schema", "unexpected schema")]
    [InlineData("flood-stdout", "bytes to stdout")]
    public void A_failing_sidecar_makes_the_cli_exit_1_and_writes_nothing(string mode, string expected)
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildSmallRepo(repo);
        var outDir = Path.Combine(repo.Root, "failed-out");
        var before = RepoSnapshot.Take(repo.Root);

        var exit = RunCli(FakeSidecarEnvironment(mode), CliArgs(repo, baseId, headId, outDir), out var stdout, out var stderr);

        Assert.Equal(1, exit);
        Assert.StartsWith("vdiff: ", stderr);
        Assert.Contains(expected, stderr);
        Assert.DoesNotContain("wrote", stdout);
        Assert.False(Directory.Exists(outDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root));
    }

    // ---- deadline end to end with the real sidecar (C31, T-01-32) ----

    /// <summary>
    /// A generated header of about 3 MiB (repeated small class declarations, below the 4 MiB
    /// per-blob cap so it can never become too-large) beside one small normal header.
    /// Margin: parsing a 4 MB generated header took about one second in the planning probe, so
    /// the 3 MiB header needs several hundred milliseconds, at least ten times a 50 ms budget,
    /// while a header of a few hundred bytes parses in well under a millisecond, far below it.
    /// </summary>
    static (string BaseId, string HeadId) BuildDeadlineRepo(SyntheticRepo repo)
    {
        repo.Write("README.md", "# Deadline\n");
        var baseId = repo.Commit("base");

        var generated = new StringBuilder("#pragma once\nnamespace gen {\n");
        for (var i = 0; generated.Length < 3 * 1024 * 1024; i++)
        {
            generated.Append("class Generated").Append(i).Append(" { public: int value").Append(i).Append("() const; };\n");
        }

        generated.Append("}\n");
        repo.Write("include/gen/big.h", generated.ToString());
        repo.Write("include/gen/small.h", "#pragma once\nnamespace gen {\nclass Small {\npublic:\n    int id() const;\n};\n}\n");
        var headId = repo.Commit("head");
        return (baseId, headId);
    }

    static readonly System.Text.Json.JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower };

    static T ReadJson<T>(string path) => System.Text.Json.JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReadOptions)!;

    static Dictionary<string, string?> RealSidecarEnvironment(SidecarFixture sidecar, string? parseBudgetMs) => new()
    {
        ["VDIFF_CPP_TS"] = sidecar.Command.File,
        ["VDIFF_SIDECAR_TIMEOUT_SECONDS"] = null,
        ["VDIFF_PARSE_BUDGET_MS"] = parseBudgetMs,
    };

    readonly SidecarFixture _sidecar;

    public SidecarBoundsTests(SidecarFixture sidecar) => _sidecar = sidecar;

    [Fact]
    public void A_file_the_real_sidecar_labels_timeout_is_unmodelled_with_the_run_still_succeeding()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildDeadlineRepo(repo);
        var outDir = Path.Combine(repo.Root, "result");

        var exit = RunCli(RealSidecarEnvironment(_sidecar, "50"), CliArgs(repo, baseId, headId, outDir), out _, out var stderr);

        Assert.Equal(0, exit);
        Assert.Equal("", stderr);
        var head = ReadJson<Vdiff.Core.Model.ModelFile>(Path.Combine(outDir, "head.model.json"));
        var big = Assert.Single(head.Unmodelled, u => u.Path == "include/gen/big.h");
        Assert.Equal("parse-timeout", big.Reason);
        Assert.DoesNotContain(head.Files, f => f.Path == "include/gen/big.h");

        // The small header is expected to be modelled; on a heavily loaded machine it may
        // itself be labelled parse-timeout, which is tolerated, but never a crash.
        Assert.All(head.Unmodelled, u => Assert.Equal("parse-timeout", u.Reason));
        Assert.True(head.Files.Any(f => f.Path == "include/gen/small.h") || head.Unmodelled.Any(u => u.Path == "include/gen/small.h"));

        var diff = ReadJson<Vdiff.Core.Model.DiffFile>(Path.Combine(outDir, "diff.json"));
        Assert.True(diff.Summary.Unmodelled.Head >= 1, $"summary.unmodelled.head is {diff.Summary.Unmodelled.Head}");
    }

    [Fact]
    public void With_the_default_parse_budget_the_same_large_file_is_modelled()
    {
        using var repo = new SyntheticRepo();
        var (baseId, headId) = BuildDeadlineRepo(repo);
        var outDir = Path.Combine(repo.Root, "result");

        var exit = RunCli(RealSidecarEnvironment(_sidecar, null), CliArgs(repo, baseId, headId, outDir), out _, out var stderr);

        Assert.Equal(0, exit);
        Assert.Equal("", stderr);
        var head = ReadJson<Vdiff.Core.Model.ModelFile>(Path.Combine(outDir, "head.model.json"));
        Assert.Contains(head.Files, f => f.Path == "include/gen/big.h");
        Assert.Empty(head.Unmodelled);
        var diff = ReadJson<Vdiff.Core.Model.DiffFile>(Path.Combine(outDir, "diff.json"));
        Assert.Equal(0, diff.Summary.Unmodelled.Head);
    }
}
