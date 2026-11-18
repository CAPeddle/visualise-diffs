using System.Text;
using System.Text.RegularExpressions;
using Vdiff.Core.Process;
using Vdiff.Tests.Support;

namespace Vdiff.Tests.Trial;

/// <summary>
/// Proof, on synthetic repositories only (D-09), that scripts/trace-run.ps1 passes on a healthy
/// run, fails closed with a reason from the fixed list, prints nothing but its six lines and
/// never touches the analysed repository (TRC-03, T-01-23 to T-01-26, C47, C48). The script is
/// started as a child process under PowerShell 7 (required) and, on Windows, under Windows
/// PowerShell 5.1 as well. A missing host fails the test; it never skips.
/// </summary>
[Collection("sidecar")]
public class TraceRunScriptTests
{
    static readonly string[] Reasons =
    [
        "none", "bad-arguments", "out-not-outside", "tool-failed", "repo-changed", "outputs-missing", "over-budget", "script-error",
    ];

    static readonly Regex[] LineGrammar =
    [
        new("^result: (pass|fail)$"),
        new("^reason: (" + string.Join('|', Reasons) + ")$"),
        new("^wall_clock: (under-1-min|1-to-5-min|5-to-20-min|over-20-min)$"),
        new("^repo_unchanged: (yes|no|unknown)$"),
        new("^outputs_present: (yes|no)$"),
        new("^mermaid_syntax: (ok|fail|skipped)$"),
    ];

    readonly SidecarFixture _sidecar;

    public TraceRunScriptTests(SidecarFixture sidecar) => _sidecar = sidecar;

    public static IEnumerable<object[]> Hosts()
    {
        yield return ["pwsh"];
        if (OperatingSystem.IsWindows())
        {
            yield return ["powershell"];
        }
    }

    // ---- plumbing ----

    static string RepositoryRoot()
    {
        for (var dir = Path.GetFullPath(AppContext.BaseDirectory); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "vdiff.sln")))
            {
                return dir;
            }
        }

        throw new InvalidOperationException("vdiff.sln was not found above the test assembly");
    }

    static string VdiffDll() => Path.Combine(AppContext.BaseDirectory, "vdiff.dll");

    /// <summary>The fixed argument list: everything but -Repo, -Base, -Head, -Out, which the case adds.</summary>
    static ProcessResult RunScript(string host, IEnumerable<string> scriptArguments, bool skipMermaid, TimeSpan timeout)
    {
        var args = new List<string>
        {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", Path.Combine(RepositoryRoot(), "scripts", "trace-run.ps1"),
        };
        args.AddRange(scriptArguments);
        args.AddRange(["-VdiffDll", VdiffDll()]);
        if (skipMermaid)
        {
            args.Add("-SkipMermaid");
        }

        return BoundedProcess.Run(host, args, new ProcessLimits(timeout, 1024 * 1024, 1024 * 1024));
    }

    static List<string> CoreArguments(string repoPath, string baseId, string headId, string outPath) =>
        ["-Repo", repoPath, "-Base", baseId, "-Head", headId, "-Out", outPath];

    /// <summary>Exactly six lines, in order, in the fixed vocabulary; returns the value after each "name: ".</summary>
    static string[] ParseSixLines(string stdout)
    {
        var lines = stdout.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        Assert.Equal(LineGrammar.Length, lines.Count);
        var values = new string[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            Assert.Matches(LineGrammar[i], lines[i]);
            values[i] = lines[i][(lines[i].IndexOf(": ", StringComparison.Ordinal) + 2)..];
        }

        return values;
    }

    /// <summary>T-01-23: stdout and stderr hold no path, commit id (full or 7 characters), user or machine name.</summary>
    static void AssertNothingLeaks(ProcessResult result, params string[] secrets)
    {
        Assert.Equal("", result.StderrTail.Trim());
        var all = new List<string>(secrets) { Environment.UserName, Environment.MachineName };
        foreach (var secret in secrets.Where(s => Regex.IsMatch(s, "^[0-9a-f]{40}$")))
        {
            all.Add(secret[..7]);
        }

        foreach (var secret in all.Where(s => s.Length >= 3))
        {
            foreach (var variant in new[] { secret, secret.Replace('\\', '/'), secret.Replace('/', '\\') })
            {
                Assert.DoesNotContain(variant, result.StdoutText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(variant, result.StderrTail, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// History: C1 (base) on main, C2 (head) on feature, C3 on main. Tracked: .gitignore (ignores
    /// *.log), README.md, include/a.h and include/b.h; C2 changes a.h and adds other/c.h.
    /// </summary>
    static (string C1, string C2, string C3) BuildHistory(SyntheticRepo repo)
    {
        repo.Write(".gitignore", "*.log\n");
        repo.Write("README.md", "# Sample\n");
        repo.Write("include/a.h", "class Alpha {\npublic:\n    int size() const;\n};\n");
        repo.Write("include/b.h", "struct Beta {\n    int id;\n};\n");
        var c1 = repo.Commit("base");

        repo.Git("checkout", "-q", "-b", "feature");
        repo.Write("include/a.h", "class Alpha {\npublic:\n    long size() const;\n    void grow(int by);\n};\n");
        repo.Write("other/c.h", "class Gamma {\npublic:\n    int weight;\n};\n");
        var c2 = repo.Commit("head");

        repo.Git("checkout", "-q", "main");
        repo.Write("docs.txt", "notes\n");
        var c3 = repo.Commit("other");
        return (c1, c2, c3);
    }

    /// <summary>One stash entry, then a staged edit, an unstaged edit, an untracked file and an ignored file.</summary>
    static void MakeDirty(SyntheticRepo repo)
    {
        File.WriteAllText(Path.Combine(repo.RepoDir, "README.md"), "# Sample\n\nstashed edit\n");
        repo.Git("stash", "push", "-q", "-m", "synthetic stash");

        repo.Write("README.md", "# Sample\n\nstaged edit\n");
        File.WriteAllText(Path.Combine(repo.RepoDir, "include", "b.h"), "struct Beta {\n    int id;\n    int unstaged;\n};\n");
        File.WriteAllText(Path.Combine(repo.RepoDir, "notes.txt"), "untracked\n");
        File.WriteAllText(Path.Combine(repo.RepoDir, "build.log"), "ignored\n");
    }

    static void AssertPassed(ProcessResult result, string[] values)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("pass", values[0]);
        Assert.Equal("none", values[1]);
        Assert.Equal("under-1-min", values[2]);
        Assert.Equal("yes", values[3]);
        Assert.Equal("yes", values[4]);
    }

    static string[] OutputFiles(string dir) =>
        Directory.GetFiles(dir).Select(f => Path.GetFileName(f)!).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    static readonly string[] FiveFiles = ["base.model.json", "diff.json", "head.model.json", "report.html", "summary.mmd"];

    // ---- (a) healthy runs ----

    [Theory]
    [MemberData(nameof(Hosts))]
    public void A_clean_run_passes_with_the_repository_unchanged(string host)
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        repo.Git("checkout", "-q", "feature");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var result = RunScript(host, CoreArguments(repo.RepoDir, c1, c2, repo.OutDir), skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        AssertPassed(result, values);
        Assert.Equal("skipped", values[5]);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, c1, c2);
        Assert.Equal(FiveFiles, OutputFiles(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void A_dirty_working_tree_with_neither_commit_checked_out_passes_and_stays_unchanged(string host)
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        MakeDirty(repo);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var result = RunScript(host, CoreArguments(repo.RepoDir, c1, c2, repo.OutDir), skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        AssertPassed(result, values);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, c1, c2);
        Assert.Equal(FiveFiles, OutputFiles(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    // ---- (b) and (c) failures: the right reason, six lines, nothing leaked ----

    [Theory]
    [InlineData("out-inside-repository", "out-not-outside", "unknown")]
    [InlineData("out-inside-this-repository", "out-not-outside", "unknown")]
    [InlineData("non-hex-base", "bad-arguments", "unknown")]
    [InlineData("unknown-commit", "tool-failed", "yes")]
    [InlineData("missing-repo-directory", "bad-arguments", "unknown")]
    [InlineData("repo-omitted", "bad-arguments", "unknown")]
    [InlineData("out-omitted", "bad-arguments", "unknown")]
    public void Every_failure_case_fails_closed_with_a_fixed_reason_and_leaks_nothing(string scenario, string expectedReason, string expectedRepoUnchanged)
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        var unknownId = "0badc0de0badc0de0badc0de0badc0de0badc0de";
        var insideRepo = Path.Combine(repo.RepoDir, "out-here");
        var insideThisRepo = Path.Combine(RepositoryRoot(), "vdiff-trace-out-" + Guid.NewGuid().ToString("N"));
        var missingRepo = Path.Combine(repo.Root, "no-such-repository");

        var arguments = scenario switch
        {
            "out-inside-repository" => CoreArguments(repo.RepoDir, c1, c2, insideRepo),
            "out-inside-this-repository" => CoreArguments(repo.RepoDir, c1, c2, insideThisRepo),
            "non-hex-base" => CoreArguments(repo.RepoDir, "not-a-commit-id", c2, repo.OutDir),
            "unknown-commit" => CoreArguments(repo.RepoDir, c1, unknownId, repo.OutDir),
            "missing-repo-directory" => CoreArguments(missingRepo, c1, c2, repo.OutDir),
            "repo-omitted" => ["-Base", c1, "-Head", c2, "-Out", repo.OutDir],
            "out-omitted" => ["-Repo", repo.RepoDir, "-Base", c1, "-Head", c2],
            _ => throw new InvalidOperationException(scenario),
        };

        var result = RunScript("pwsh", arguments, skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("fail", values[0]);
        Assert.Equal(expectedReason, values[1]);
        Assert.Equal(expectedRepoUnchanged, values[3]);
        Assert.Equal("no", values[4]);
        Assert.Equal("skipped", values[5]);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, insideRepo, insideThisRepo, missingRepo, c1, c2, unknownId);
        Assert.False(Directory.Exists(insideRepo), "the script wrote into the analysed repository");
        Assert.False(Directory.Exists(insideThisRepo), "the script wrote into this repository");
        Assert.Empty(Directory.GetFiles(repo.OutDir));
    }

    // Precedence (01-14): bad-arguments (blank, unbound, non-hex, missing repo), then out-not-outside, then a bad -LogPath.
    [Theory]
    [MemberData(nameof(CombinedCases))]
    public void Several_bad_inputs_at_once_report_the_highest_precedence_reason(string host, string scenario, string expectedReason)
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        var outInside = Path.Combine(repo.RepoDir, "out-here");
        var logInside = Path.Combine(repo.RepoDir, "log-here.txt");
        var missingRepo = Path.Combine(repo.Root, "no-such-repository");

        var arguments = scenario switch
        {
            "non-hex-base+out-inside" => CoreArguments(repo.RepoDir, "not-a-commit-id", c2, outInside),
            "out-inside+log-inside" => [.. CoreArguments(repo.RepoDir, c1, c2, outInside), "-LogPath", logInside],
            "non-hex-base+out-inside+log-inside" => [.. CoreArguments(repo.RepoDir, "not-a-commit-id", c2, outInside), "-LogPath", logInside],
            "missing-repo+out-inside+log-inside" => [.. CoreArguments(missingRepo, c1, c2, outInside), "-LogPath", logInside],
            _ => throw new InvalidOperationException(scenario),
        };

        var result = RunScript(host, arguments, skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("fail", values[0]);
        Assert.Equal(expectedReason, values[1]);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, outInside, logInside, missingRepo, c1, c2);
        Assert.False(Directory.Exists(outInside), "the script wrote into the analysed repository");
        Assert.False(File.Exists(logInside), "the script wrote a log into the analysed repository");
    }

    public static IEnumerable<object[]> CombinedCases()
    {
        foreach (var host in new[] { "pwsh", "powershell" })
        {
            if (host == "powershell" && !OperatingSystem.IsWindows())
            {
                continue;
            }

            yield return [host, "non-hex-base+out-inside", "bad-arguments"];
            yield return [host, "out-inside+log-inside", "out-not-outside"];
            yield return [host, "non-hex-base+out-inside+log-inside", "bad-arguments"];
            yield return [host, "missing-repo+out-inside+log-inside", "bad-arguments"];
        }
    }

    // T-01-46 backstop: a swap between the first check and the write cannot be provoked deterministically,
    // so the order of the statements inside Invoke-Trace is what is pinned.
    [Fact]
    public void Log_write_is_preceded_by_a_second_containment_check_in_source_order()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "scripts", "trace-run.ps1"));
        var start = text.IndexOf("function Invoke-Trace", StringComparison.Ordinal);
        Assert.True(start >= 0, "Invoke-Trace not found");
        var body = text[start..];

        var run = body.IndexOf("Invoke-Native 'dotnet'", StringComparison.Ordinal);
        Assert.True(run >= 0, "the vdiff invocation was not found in Invoke-Trace");
        var write = body.IndexOf("Write-LogFile $logFull", StringComparison.Ordinal);
        Assert.True(write > run, "the log write must come after the vdiff run");

        var between = body[run..write];
        Assert.Contains("Test-NonDriveRoot $logFull", between);
        Assert.Contains("Test-SameOrUnder $logFull", between);

        // The first checks (before the run) must still exist.
        var before = body[..run];
        Assert.Contains("Test-NonDriveRoot $logFull", before);
        Assert.Contains("Test-SameOrUnder $logFull", before);

        // Nothing writes the log earlier than the second check.
        Assert.Equal(write, body.LastIndexOf("Write-LogFile $logFull", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unknown_commit_id_leaves_the_repository_unchanged_and_reports_it()
    {
        using var repo = new SyntheticRepo();
        var (c1, _, _) = BuildHistory(repo);
        MakeDirty(repo);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var result = RunScript(
            "pwsh",
            CoreArguments(repo.RepoDir, c1, "0badc0de0badc0de0badc0de0badc0de0badc0de", repo.OutDir),
            skipMermaid: true,
            TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        Assert.Equal("tool-failed", values[1]);
        Assert.Equal("yes", values[3]);
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    // ---- (c) pass-through of --blank and --include ----

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Blank_names_and_an_include_glob_reach_vdiff(string host)
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);

        var arguments = CoreArguments(repo.RepoDir, c1, c2, repo.OutDir);
        arguments.AddRange(["-Blank", "SHOP_API,OTHER_API", "-Include", "include/**"]);
        var result = RunScript(host, arguments, skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        AssertPassed(result, values);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, c1, c2);

        var head = File.ReadAllText(Path.Combine(repo.OutDir, "head.model.json"));
        Assert.Contains("SHOP_API", head, StringComparison.Ordinal);
        Assert.Contains("OTHER_API", head, StringComparison.Ordinal);

        var diff = File.ReadAllText(Path.Combine(repo.OutDir, "diff.json"));
        Assert.Contains("Alpha", diff, StringComparison.Ordinal);
        Assert.DoesNotContain("Gamma", diff, StringComparison.Ordinal);
    }

    // ---- (c2) links: -Out and -LogPath reached through a symbolic link or junction (WR-01, T-01-37) ----

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_out_through_a_directory_link_into_the_repository_is_refused_with_out_not_outside(string host)
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var rootLink = links.Link("to-root", repo.RepoDir);
        var includeLink = links.Link("to-include", Path.Combine(repo.RepoDir, "include"));
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        foreach (var link in new[] { rootLink, includeLink })
        {
            var result = RunScript(host, CoreArguments(repo.RepoDir, c1, c2, Path.Combine(link, "out-here")), skipMermaid: true, TimeSpan.FromMinutes(5));

            var values = ParseSixLines(result.StdoutText);
            Assert.Equal(1, result.ExitCode);
            Assert.Equal("fail", values[0]);
            Assert.Equal("out-not-outside", values[1]);
            Assert.Equal("unknown", values[3]);
            Assert.Equal("no", values[4]);
            Assert.Equal("skipped", values[5]);
            AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, rootLink, includeLink, c1, c2);
            Assert.Empty(Directory.EnumerateDirectories(repo.RepoDir, "out-here", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
            Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
        }
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_log_path_through_a_directory_link_into_the_repository_is_refused_and_no_log_is_written(string host)
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var link = links.Link("to-root", repo.RepoDir);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var arguments = CoreArguments(repo.RepoDir, c1, c2, repo.OutDir);
        arguments.AddRange(["-LogPath", Path.Combine(link, "vdiff.log")]);
        var result = RunScript(host, arguments, skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("fail", values[0]);
        Assert.Equal("bad-arguments", values[1]);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, link, c1, c2);
        Assert.False(File.Exists(Path.Combine(repo.RepoDir, "vdiff.log")), "the script wrote its log into the analysed repository");
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_out_through_a_directory_link_to_a_directory_outside_the_repository_still_passes(string host)
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var link = links.Link("to-out", repo.OutDir);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var result = RunScript(host, CoreArguments(repo.RepoDir, c1, c2, Path.Combine(link, "run")), skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        AssertPassed(result, values);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, link, c1, c2);
        Assert.Equal(FiveFiles, OutputFiles(Path.Combine(repo.OutDir, "run")));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_out_through_a_link_loop_fails_closed_with_script_error(string host)
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var first = links.Link("loop-a", Path.Combine(links.Root, "loop-b"));
        var second = links.Link("loop-b", first);

        var result = RunScript(host, CoreArguments(repo.RepoDir, c1, c2, Path.Combine(first, "x")), skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("fail", values[0]);
        Assert.Equal("script-error", values[1]);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, first, second, c1, c2);
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
    }

    // ---- (c3) other Windows namespaces and volume GUID links (CR-01 on the script side, T-01-41, T-01-44) ----

    static readonly string[] NonDriveKinds = ["extended", "device", "loopback-unc", "extended-unc"];

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_out_in_a_non_drive_namespace_is_refused_with_out_not_outside(string host)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        var spellings = NonDriveKinds.Select(k => PathSpellings.NonDriveNamespace(k, Path.Combine(repo.RepoDir, "out-here"))).ToArray();

        foreach (var spelled in spellings)
        {
            var result = RunScript(host, CoreArguments(repo.RepoDir, c1, c2, spelled), skipMermaid: true, TimeSpan.FromMinutes(5));

            var values = ParseSixLines(result.StdoutText);
            Assert.Equal(1, result.ExitCode);
            Assert.Equal("fail", values[0]);
            Assert.Equal("out-not-outside", values[1]);
            Assert.Equal("unknown", values[3]);
            Assert.Equal("no", values[4]);
            Assert.Equal("skipped", values[5]);
            AssertNothingLeaks(result, [repo.RepoDir, repo.OutDir, c1, c2, .. spellings]);
            Assert.Empty(Directory.EnumerateDirectories(repo.RepoDir, "out-here", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
            Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
        }
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_log_path_in_a_non_drive_namespace_is_refused_with_bad_arguments(string host)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
        var spellings = new[] { "extended", "loopback-unc" }
            .Select(k => PathSpellings.NonDriveNamespace(k, Path.Combine(repo.RepoDir, "vdiff.log")))
            .ToArray();

        foreach (var spelled in spellings)
        {
            var arguments = CoreArguments(repo.RepoDir, c1, c2, repo.OutDir);
            arguments.AddRange(["-LogPath", spelled]);
            var result = RunScript(host, arguments, skipMermaid: true, TimeSpan.FromMinutes(5));

            var values = ParseSixLines(result.StdoutText);
            Assert.Equal(1, result.ExitCode);
            Assert.Equal("fail", values[0]);
            Assert.Equal("bad-arguments", values[1]);
            AssertNothingLeaks(result, [repo.RepoDir, repo.OutDir, c1, c2, .. spellings]);
            Assert.False(File.Exists(Path.Combine(repo.RepoDir, "vdiff.log")), "the script wrote its log into the analysed repository");
            Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
            Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
        }
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_out_through_a_junction_with_a_volume_guid_target_fails_closed_with_script_error(string host)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var alias = links.Link("alias", PathSpellings.VolumeGuidPath(repo.RepoDir));
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var result = RunScript(host, CoreArguments(repo.RepoDir, c1, c2, Path.Combine(alias, "out-here")), skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("fail", values[0]);
        Assert.Equal("script-error", values[1]);
        Assert.Equal("no", values[4]);
        Assert.Equal("skipped", values[5]);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, alias, c1, c2);
        Assert.Empty(Directory.EnumerateDirectories(repo.RepoDir, "out-here", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFileSystemEntries(repo.OutDir));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_out_in_a_sibling_directory_whose_name_extends_the_repository_name_still_passes(string host)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var sibling = Path.Combine(repo.Root, "repo-out");
        var before = RepoSnapshot.Take(repo.Root, sibling);

        var result = RunScript(host, CoreArguments(repo.RepoDir, c1, c2, sibling), skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        AssertPassed(result, values);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, sibling, c1, c2);
        Assert.Equal(FiveFiles, OutputFiles(sibling));
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, sibling));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_repo_spelled_in_the_extended_namespace_still_refuses_an_out_inside_it(string host)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var extendedRepo = PathSpellings.NonDriveNamespace("extended", repo.RepoDir);
        var insideRepo = Path.Combine(repo.RepoDir, "out-here");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var result = RunScript(host, CoreArguments(extendedRepo, c1, c2, insideRepo), skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("fail", values[0]);
        Assert.Equal("out-not-outside", values[1]);
        Assert.Equal("unknown", values[3]);
        Assert.Equal("no", values[4]);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, extendedRepo, insideRepo, c1, c2);
        Assert.False(Directory.Exists(insideRepo), "the script wrote into the analysed repository");
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    // ---- (c4) the log is written without following a planted hard link (WR-01 on the script side, T-01-45) ----

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Aliased_log_path_hard_linked_to_a_repository_file_is_replaced_and_the_file_is_unchanged(string host)
    {
        using var repo = new SyntheticRepo();
        using var links = new LinkScratch();
        var (c1, c2, _) = BuildHistory(repo);
        var readme = Path.Combine(repo.RepoDir, "README.md");
        var log = links.HardLink(Path.Combine(repo.OutDir, "vdiff.log"), readme);
        var outRun = Path.Combine(repo.OutDir, "run");
        var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

        var arguments = CoreArguments(repo.RepoDir, c1, c2, outRun);
        arguments.AddRange(["-LogPath", log]);
        var result = RunScript(host, arguments, skipMermaid: true, TimeSpan.FromMinutes(5));

        var values = ParseSixLines(result.StdoutText);
        AssertPassed(result, values);
        AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, outRun, log, c1, c2);
        Assert.Equal("# Sample\n", File.ReadAllText(readme));
        Assert.Equal(FiveFiles, OutputFiles(outRun));
        Assert.Contains("wrote 5 files", File.ReadAllText(log), StringComparison.Ordinal);
        Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
    }

    // ---- (d) fsmonitor (C47) ----

    static ProcessResult UnprotectedGit(string repoDir, params string[] args)
    {
        // Deliberately without the read-only prefix: this is how an unguarded tool would call git.
        var full = new List<string> { "-C", repoDir };
        full.AddRange(args);
        return BoundedProcess.Run("git", full, new ProcessLimits(TimeSpan.FromSeconds(120), 64L * 1024 * 1024, 1024 * 1024), null, GitCommand.BuildEnvironment());
    }

    static string ForwardSlashes(string path) => path.Replace('\\', '/');

    [Fact]
    public void A_core_fsmonitor_hook_is_never_run_by_the_script()
    {
        using var repo = new SyntheticRepo();
        var (c1, c2, _) = BuildHistory(repo);
        var markerDir = Path.Combine(Path.GetTempPath(), "vdiff-trace-marker-" + Guid.NewGuid().ToString("N"));
        try
        {
            var hookDir = Path.Combine(repo.Root, "hooks");
            Directory.CreateDirectory(hookDir);
            var hook = Path.Combine(hookDir, "fsmonitor.sh");
            // LF line ends: Git's shell cannot run a CRLF script.
            File.WriteAllBytes(
                hook,
                new UTF8Encoding(false).GetBytes(
                    "#!/bin/sh\n" +
                    $"mkdir -p \"{ForwardSlashes(markerDir)}\"\n" +
                    $"echo ran >> \"{ForwardSlashes(markerDir)}/ran.txt\"\n" +
                    "exit 0\n"));
            repo.Git("config", "core.fsmonitor", ForwardSlashes(hook));
            MakeDirty(repo);

            // Positive control: an unguarded git run does start the hook, so the assertions below prove something.
            UnprotectedGit(repo.RepoDir, "status", "--porcelain=v1");
            Assert.True(
                Directory.Exists(markerDir),
                "positive control failed: the fsmonitor hook did not run for an unguarded git status, so this test would prove nothing");
            Directory.Delete(markerDir, recursive: true);
            var before = RepoSnapshot.Take(repo.Root, repo.OutDir);

            var result = RunScript("pwsh", CoreArguments(repo.RepoDir, c1, c2, repo.OutDir), skipMermaid: true, TimeSpan.FromMinutes(5));

            var values = ParseSixLines(result.StdoutText);
            AssertPassed(result, values);
            Assert.False(Directory.Exists(markerDir), "the script ran the repository's fsmonitor hook");
            Assert.False(Directory.Exists(Path.Combine(repo.RepoDir, ".git", "fsmonitor--daemon")));
            Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
            Assert.False(Directory.Exists(markerDir), "the snapshot ran the repository's fsmonitor hook");
        }
        finally
        {
            if (Directory.Exists(markerDir))
            {
                Directory.Delete(markerDir, recursive: true);
            }
        }
    }

    // ---- (e) a diagram over 50,000 characters is really parsed, never reported as a failure (C48) ----

    [Fact]
    public void A_diagram_over_fifty_thousand_characters_reports_ok_or_skipped_and_never_fail()
    {
        using var repo = new SyntheticRepo();
        repo.Write("include/seed.h", "class Seed {\npublic:\n    int id;\n};\n");
        var c1 = repo.Commit("base");

        var header = new StringBuilder("#pragma once\n");
        for (var i = 0; i < 1500; i++)
        {
            header.Append("class Gen").Append(i).Append(" {\npublic:\n    int v;\n};\n");
        }

        repo.Write("include/gen/many.h", header.ToString());
        var c2 = repo.Commit("head with many classes");

        var result = RunScript("pwsh", CoreArguments(repo.RepoDir, c1, c2, repo.OutDir), skipMermaid: false, TimeSpan.FromMinutes(15));

        var values = ParseSixLines(result.StdoutText);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("pass", values[0]);
        Assert.Equal("yes", values[3]);
        Assert.Equal("yes", values[4]);
        var summary = File.ReadAllText(Path.Combine(repo.OutDir, "summary.mmd"));
        Assert.True(summary.Length > 50_000, $"the test premise failed: summary.mmd has only {summary.Length} characters");

        var installed = Directory.Exists(Path.Combine(RepositoryRoot(), "tools", "mermaid-check", "node_modules"));
        Assert.NotEqual("fail", values[5]);
        if (installed)
        {
            // Installed packages and a machine browser: the raised text-size limit means the text was parsed.
            Assert.Equal("ok", values[5]);
        }
        else
        {
            Assert.Equal("skipped", values[5]);
        }
    }
}
