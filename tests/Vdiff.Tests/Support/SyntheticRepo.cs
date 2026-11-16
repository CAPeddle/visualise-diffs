using System.Text;
using Vdiff.Core.Process;

namespace Vdiff.Tests.Support;

/// <summary>
/// A synthetic git sandbox in a temp directory (D-09: every test repository is built by
/// script, never cloned, and carries only generic content). Layout: <c>Root/repo</c> is the
/// work tree, <c>Root/out</c> is where a run may write. Git configuration is isolated from
/// the machine, so results do not depend on whoever runs the tests; this isolation belongs
/// here and never in production code.
/// </summary>
public sealed class SyntheticRepo : IDisposable
{
    static readonly DateTimeOffset FirstCommitTime = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    readonly Dictionary<string, string?> _env;
    int _commitCount;

    public string Root { get; }
    public string RepoDir { get; }
    public string OutDir { get; }

    public SyntheticRepo()
    {
        Root = Path.Combine(Path.GetTempPath(), "vdiff-" + Guid.NewGuid().ToString("N"));
        RepoDir = Path.Combine(Root, "repo");
        OutDir = Path.Combine(Root, "out");
        Directory.CreateDirectory(RepoDir);
        Directory.CreateDirectory(OutDir);

        // An empty file, because /dev/null is not valid from .NET on Windows.
        var emptyConfig = Path.Combine(Root, "empty.gitconfig");
        File.WriteAllBytes(emptyConfig, []);

        _env = GitCommand.BuildEnvironment();
        _env["GIT_CONFIG_NOSYSTEM"] = "1";
        _env["GIT_CONFIG_GLOBAL"] = emptyConfig;
        _env["GIT_AUTHOR_NAME"] = "Synthetic Author";
        _env["GIT_AUTHOR_EMAIL"] = "author@example.invalid";
        _env["GIT_COMMITTER_NAME"] = "Synthetic Committer";
        _env["GIT_COMMITTER_EMAIL"] = "committer@example.invalid";

        Git("init", "-q", "-b", "main");
        Git("config", "core.autocrlf", "false");
        Git("config", "core.longpaths", "true");
    }

    /// <summary>Runs git in the work tree and returns stdout; a non-zero exit throws.</summary>
    public string Git(params string[] args)
    {
        var result = RunGit(RepoDir, args, null);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} exited {result.ExitCode}: {result.StderrTail}");
        }

        return result.StdoutText;
    }

    ProcessResult RunGit(string dir, IReadOnlyList<string> args, byte[]? stdin)
    {
        var full = new List<string>(GitCommand.Prefix) { "-C", dir };
        full.AddRange(args);
        var limits = new ProcessLimits(TimeSpan.FromSeconds(120), 256L * 1024 * 1024, 1024 * 1024);
        return BoundedProcess.Run("git", full, limits, stdin, _env);
    }

    /// <summary>Writes a file (LF content is stored as given) and stages it.</summary>
    public void Write(string relativePath, string content) =>
        Write(relativePath, new UTF8Encoding(false).GetBytes(content));

    public void Write(string relativePath, byte[] content)
    {
        var full = Path.Combine(RepoDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
        Git("add", "--", relativePath);
    }

    /// <summary>Deletes a tracked file and stages the deletion.</summary>
    public void Delete(string relativePath)
    {
        File.Delete(Path.Combine(RepoDir, relativePath));
        Git("add", "-A", "--", relativePath);
    }

    /// <summary>
    /// Commits what is staged with fixed synthetic dates: two sandboxes built by the same
    /// calls get the same commit ids. Returns the full commit id.
    /// </summary>
    public string Commit(string message)
    {
        _commitCount++;
        var when = FirstCommitTime.AddMinutes(_commitCount).ToString("yyyy-MM-ddTHH:mm:sszzz");
        _env["GIT_AUTHOR_DATE"] = when;
        _env["GIT_COMMITTER_DATE"] = when;
        Git("commit", "-q", "--allow-empty", "-m", message);
        return Git("rev-parse", "HEAD").Trim();
    }

    /// <summary>Checks out a ref or commit id; detached when asked.</summary>
    public void Checkout(string refOrId, bool detach = false)
    {
        if (detach)
        {
            Git("checkout", "-q", "--detach", refOrId);
        }
        else
        {
            Git("checkout", "-q", refOrId);
        }
    }

    /// <summary>Stores a blob without a work tree (hash-object -w --stdin) and returns its id.</summary>
    public string WriteBlob(string content)
    {
        var result = RunGit(RepoDir, ["hash-object", "-w", "--stdin"], new UTF8Encoding(false).GetBytes(content));
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"hash-object exited {result.ExitCode}: {result.StderrTail}");
        }

        return result.StdoutText.Trim();
    }

    /// <summary>
    /// Adds an index entry by plumbing, with no file in the work tree. This is how symlink
    /// (120000), gitlink (160000) and over-long-path entries are made, because the Windows
    /// work tree cannot make them reliably.
    /// </summary>
    public void AddCacheInfo(string mode, string oid, string path) =>
        Git("update-index", "--add", "--cacheinfo", $"{mode},{oid},{path}");

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(entry, FileAttributes.Normal);
                }

                Directory.Delete(Root, recursive: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
