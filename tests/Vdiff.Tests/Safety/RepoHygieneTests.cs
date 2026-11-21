using System.Text;
using System.Text.RegularExpressions;
using Vdiff.Core.Output;
using Vdiff.Core.Process;

namespace Vdiff.Tests.Safety;

/// <summary>
/// Public-repository guards (D-09, T-01-03, T-01-11, T-01-12). This repository is public and the
/// tool's real targets are private, so the rules that keep generated output and remote access out
/// of it are failing tests rather than conventions. These tests run against THIS repository: if
/// its root or git cannot be found they fail with a message and never skip.
/// </summary>
public partial class RepoHygieneTests
{
    static readonly string[] OutputPathsThatMustBeIgnored =
    [
        "out/diff.json",
        "vdiff-out-1/summary.mmd",
        "run.vdiff-out/report.html",
        "src/Vdiff.Core/bin/x.dll",
        "extractors/cpp-ts/target/x",
        "tools/mermaid-check/node_modules/x",
    ];

    static readonly IReadOnlyList<string> GeneratedFileNames = OutputWriter.FileNames;

    static readonly string[] GeneratedDirectoryNames = ["out", "bin", "obj", "target", "node_modules"];

    static string FindRoot()
    {
        for (var dir = Path.GetFullPath(AppContext.BaseDirectory); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "vdiff.sln")))
            {
                return dir;
            }
        }

        throw new Xunit.Sdk.XunitException(
            "could not find vdiff.sln above the test binaries; the public-repository guards cannot run (they never skip)");
    }

    static ProcessResult Git(string root, params string[] args)
    {
        var full = new List<string> { "-C", root };
        full.AddRange(args);
        var env = GitCommand.BuildEnvironment();
        env["GIT_CONFIG_NOSYSTEM"] = "1";
        var limits = new ProcessLimits(TimeSpan.FromSeconds(120), 64L * 1024 * 1024, 1024 * 1024);
        try
        {
            return BoundedProcess.Run("git", [.. GitCommand.Prefix, .. full], limits, null, env);
        }
        catch (Exception e)
        {
            throw new Xunit.Sdk.XunitException($"git could not be run, so the public-repository guards cannot run (they never skip): {e.Message}");
        }
    }

    // ---- (1) ignore rules ----

    [Fact]
    public void Output_and_build_directories_are_ignored_by_a_committed_ignore_file()
    {
        var root = FindRoot();
        var problems = new List<string>();
        foreach (var path in OutputPathsThatMustBeIgnored)
        {
            // -v names the rule that matched; a rule that only exists in a personal exclude file
            // does not protect anyone else's clone, so only a .gitignore file counts.
            var result = Git(root, "check-ignore", "-v", "--", path);
            var line = result.StdoutText.Trim();
            if (result.ExitCode != 0)
            {
                problems.Add($"{path}: not ignored");
            }
            else if (!line.Split(':')[0].EndsWith(".gitignore", StringComparison.Ordinal))
            {
                problems.Add($"{path}: ignored only by '{line.Split('\t')[0]}', not by a .gitignore in the repository");
            }
        }

        Assert.True(problems.Count == 0, "missing ignore rules (T-01-11):\n" + string.Join('\n', problems));
    }

    // ---- (2) tracked-file guard ----

    [Fact]
    public void No_generated_output_or_build_directory_is_tracked()
    {
        var root = FindRoot();
        var listing = Git(root, "ls-files", "-z");
        Assert.True(listing.ExitCode == 0, "git ls-files failed: " + listing.StderrTail);

        var offenders = new List<string>();
        foreach (var path in Encoding.UTF8.GetString(listing.Stdout).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            offenders.AddRange(TrackedPathProblems(path));
        }

        Assert.True(
            offenders.Count == 0,
            "tracked paths that look like generated output (T-01-11); a later phase that adds golden files with these names must change this guard in the same commit:\n"
            + string.Join('\n', offenders));
    }

    /// <summary>The reasons one tracked path breaks the guard; empty when it is fine.</summary>
    public static IReadOnlyList<string> TrackedPathProblems(string path)
    {
        var problems = new List<string>();
        var segments = path.Replace('\\', '/').Split('/');
        if (GeneratedFileNames.Contains(segments[^1], StringComparer.Ordinal))
        {
            problems.Add($"{path}: generated file name '{segments[^1]}'");
        }

        foreach (var segment in segments[..^1].Where(s => GeneratedDirectoryNames.Contains(s, StringComparer.Ordinal)))
        {
            problems.Add($"{path}: generated directory '{segment}'");
        }

        return problems;
    }

    // ---- (3) synthetic-only guard ----

    // A remote verb as the FIRST argument of a call: Git("fetch", ...), ["fetch", ...], ["git", "pull"],
    // ["-C", dir, "push"], UnprotectedGit(dir, "fetch") or a literal such as "fetch origin". A verb further
    // along is a different command's argument (`stash push` is local), so it is not flagged.
    const string Verbs = "(fetch|pull|push|remote|ls-remote|submodule)";

    [GeneratedRegex(
        @"(?:[(\[]\s*|""git""\s*,\s*|""-C""\s*,\s*[^,]+,\s*|Git\(\s*[^"",()]+,\s*)""" + Verbs + @"\b[^""]*""",
        RegexOptions.CultureInvariant)]
    private static partial Regex RemoteVerbLiteral();

    // A command line: git fetch, git -C x push, git --no-pager remote ...
    [GeneratedRegex(@"\bgit(\.exe)?\s+(-\S+\s+(\S+\s+)?)*(fetch|pull|push|remote|ls-remote|submodule)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RemoteCommandLine();

    /// <summary>
    /// What in one source text could reach a remote repository. Comment lines are skipped for the
    /// verb checks (a comment may talk about remotes) but not for the address checks.
    /// </summary>
    public static IReadOnlyList<string> RemoteAccessProblems(string text)
    {
        var problems = new List<string>();
        var lineNumber = 0;
        foreach (var line in text.Split('\n'))
        {
            lineNumber++;
            var trimmed = line.TrimStart();
            var isComment = trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('#') && !trimmed.StartsWith("#!", StringComparison.Ordinal);
            if (!isComment)
            {
                if (RemoteVerbLiteral().IsMatch(line) || RemoteCommandLine().IsMatch(line))
                {
                    problems.Add($"line {lineNumber}: a git call that can reach a remote");
                }
            }

            if (line.Contains("git@", StringComparison.Ordinal) || line.Contains("ssh://", StringComparison.Ordinal))
            {
                problems.Add($"line {lineNumber}: a remote address");
            }
        }

        return problems;
    }

    static IEnumerable<string> SourceFilesToScan(string root)
    {
        var tests = Path.Combine(root, "tests");
        if (Directory.Exists(tests))
        {
            foreach (var file in Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(tests, file).Replace('\\', '/');
                if (relative.Split('/').Any(s => s is "bin" or "obj"))
                {
                    continue;
                }

                // This file is the guard itself and has to contain the forbidden strings as samples.
                if (!string.Equals(relative, "Vdiff.Tests/Safety/RepoHygieneTests.cs", StringComparison.Ordinal))
                {
                    yield return file;
                }
            }
        }

        var scripts = Path.Combine(root, "scripts");
        if (Directory.Exists(scripts))
        {
            foreach (var file in Directory.EnumerateFiles(scripts, "*", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

    }

    [Fact]
    public void Test_and_script_sources_cannot_reach_a_remote()
    {
        var root = FindRoot();
        var findings = new List<string>();
        var scanned = 0;
        foreach (var file in SourceFilesToScan(root))
        {
            scanned++;
            foreach (var problem in RemoteAccessProblems(File.ReadAllText(file)))
            {
                findings.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}: {problem}");
            }
        }

        Assert.True(scanned > 0, "the synthetic-only guard found no test sources to scan, so it proves nothing");
        Assert.True(
            findings.Count == 0,
            "test or script sources that could reach a remote (T-01-12); a local `clone --bare` of a sandbox repository is allowed:\n"
            + string.Join('\n', findings));
    }

    // ---- (4) fixture and schema bytes: no carriage return, no byte-order mark ----

    /// <summary>
    /// The reasons one file's bytes break the fixture rule: a CR changes spans and commit ids when the
    /// file is copied into a repository, and a BOM changes the first token.
    /// </summary>
    public static IReadOnlyList<string> TextFileProblems(string path, byte[] bytes)
    {
        var problems = new List<string>();
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            problems.Add($"{path}: starts with a UTF-8 byte-order mark");
        }

        if (Array.IndexOf(bytes, (byte)'\r') >= 0)
        {
            problems.Add($"{path}: contains a carriage return");
        }

        return problems;
    }

    [Fact]
    public void Fixture_and_schema_files_have_no_carriage_return_or_bom()
    {
        var root = FindRoot();
        var listing = Git(root, "ls-files", "-z", "--", "tests/fixtures", "docs/schema");
        Assert.True(listing.ExitCode == 0, "git ls-files failed: " + listing.StderrTail);

        var problems = new List<string>();
        var scanned = 0;
        foreach (var path in Encoding.UTF8.GetString(listing.Stdout).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var isFixture = path.StartsWith("tests/fixtures/", StringComparison.Ordinal);
            var isSchema = path.StartsWith("docs/schema/", StringComparison.Ordinal)
                && path.EndsWith(".json", StringComparison.Ordinal)
                && path.IndexOf('/', "docs/schema/".Length) < 0;
            if (!isFixture && !isSchema)
            {
                continue;
            }

            // The working-tree bytes are what the harness copies into a repository.
            scanned++;
            problems.AddRange(TextFileProblems(path, File.ReadAllBytes(Path.Combine(root, path))));
        }

        Assert.True(scanned >= 3, "fewer than three fixture or schema files were scanned, so the guard proves nothing");
        Assert.True(
            problems.Count == 0,
            "fixture or schema files with a carriage return or a byte-order mark (RESEARCH Pitfall 3); check .gitattributes:\n"
            + string.Join('\n', problems));
    }

    [Theory]
    [InlineData(new byte[] { 0x61, 0x0D, 0x0A, 0x62 }, 1)]
    [InlineData(new byte[] { 0x0D }, 1)]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x7B, 0x7D, 0x0A }, 1)]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0x0D }, 2)]
    [InlineData(new byte[] { 0x7B, 0x7D, 0x0A }, 0)]
    [InlineData(new byte[] { 0xEF, 0xBB, 0x7B }, 0)]
    [InlineData(new byte[0], 0)]
    public void The_text_file_guard_flags_a_carriage_return_or_a_byte_order_mark(byte[] bytes, int expectedProblems)
    {
        var problems = TextFileProblems("tests/fixtures/small-cpp/head/include/a.h", bytes);

        Assert.Equal(expectedProblems, problems.Count);
        Assert.All(problems, p => Assert.StartsWith("tests/fixtures/small-cpp/head/include/a.h: ", p, StringComparison.Ordinal));
    }

    // ---- the guards must fail when their condition breaks (both directions) ----

    [Theory]
    [InlineData("repo.Git(\"fetch\", \"origin\");")]
    [InlineData("repo.Git(\"pull\");")]
    [InlineData("repo.Git(\"push\", \"-f\");")]
    [InlineData("repo.Git(\"remote\", \"add\", \"o\", \"x\");")]
    [InlineData("repo.Git(\"ls-remote\", \"x\");")]
    [InlineData("repo.Git(\"submodule\", \"update\");")]
    [InlineData("Run(\"git\", \"fetch --all\");")]
    [InlineData("echo go; git -C \"$d\" push origin main")]
    [InlineData("git fetch")]
    [InlineData("var u = \"git@host:org/repo.git\";")]
    [InlineData("var u = \"ssh://host/repo\";")]
    [InlineData("// see git@host for the address")]
    public void The_synthetic_only_scanner_flags_remote_access(string source)
    {
        Assert.NotEmpty(RemoteAccessProblems(source));
    }

    [Theory]
    [InlineData("repo.Git(\"clone\", \"--bare\", \"--quiet\", repo.RepoDir, bare);")]
    [InlineData("repo.Git(\"checkout\", \"-q\", \"main\");")]
    [InlineData("repo.Git(\"stash\", \"push\", \"-q\");")]
    [InlineData("// a comment about fetch, pull and remote repositories")]
    [InlineData("var name = \"remoteness\";")]
    public void The_synthetic_only_scanner_accepts_local_work(string source)
    {
        Assert.Empty(RemoteAccessProblems(source));
    }

    [Theory]
    [InlineData("out/diff.json")]
    [InlineData("tests/fixtures/base.model.json")]
    [InlineData("golden/summary.mmd")]
    [InlineData("golden/report.html")]
    [InlineData("src/Vdiff.Core/bin/Vdiff.Core.dll")]
    [InlineData("src/Vdiff.Core/obj/x.cs")]
    [InlineData("extractors/cpp-ts/target/release/x")]
    [InlineData("tools/mermaid-check/node_modules/pkg/index.js")]
    public void The_tracked_file_guard_flags_generated_output(string path)
    {
        Assert.NotEmpty(TrackedPathProblems(path));
    }

    [Theory]
    [InlineData("src/Vdiff.Core/Cli/CliApp.cs")]
    [InlineData("tests/Vdiff.Tests/Safety/RepoHygieneTests.cs")]
    [InlineData("README.md")]
    [InlineData("docs/outline.md")]
    public void The_tracked_file_guard_accepts_ordinary_sources(string path)
    {
        Assert.Empty(TrackedPathProblems(path));
    }
}
