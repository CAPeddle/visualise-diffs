using System.Security.Cryptography;
using System.Text;
using Vdiff.Core.Process;

namespace Vdiff.Tests.Support;

/// <summary>
/// Everything a run must leave unchanged in a sandbox: a SHA-256 over the whole sandbox
/// (paths and file bytes, work tree and .git included) except the output directory, plus the
/// git-visible state of each repository inside it. Two snapshots are equal by value, so a
/// test takes one before a run and one after and compares them.
/// </summary>
public sealed record RepoSnapshot(string TreeHash, string GitState)
{
    /// <summary>The read-only git questions asked of every repository (each starts with the shared prefix).</summary>
    static readonly string[][] Questions =
    [
        ["status", "--porcelain=v1", "--untracked-files=all"],
        ["rev-parse", "HEAD"],
        ["symbolic-ref", "-q", "HEAD"],
        ["rev-parse", "--all"],
        ["for-each-ref"],
        ["worktree", "list", "--porcelain"],
        ["stash", "list"],
    ];

    /// <summary>
    /// The full argument lists (after "git") the snapshot runs for one repository directory.
    /// Public so a test can prove each one starts with <see cref="GitCommand.Prefix"/>
    /// (<c>-c core.fsmonitor=false --no-optional-locks</c>): without the fsmonitor override
    /// even a snapshot's own <c>status</c> would run a repository's fsmonitor hook or start
    /// its daemon, and without no-optional-locks it would refresh the index.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> BuildCommands(string gitDir)
    {
        var commands = new List<IReadOnlyList<string>>();
        foreach (var question in Questions)
        {
            var args = new List<string>(GitCommand.Prefix) { "-C", gitDir };
            args.AddRange(question);
            commands.Add(args);
        }

        return commands;
    }

    /// <param name="sandboxRoot">The sandbox directory.</param>
    /// <param name="excludeDir">The output directory a run may write to; left out of the hash.</param>
    /// <param name="gitDirs">
    /// Repositories to ask git about: a work tree, a subdirectory, a linked worktree or a bare
    /// repository inside the sandbox, absolute or relative to the sandbox. Default: <c>repo</c>.
    /// </param>
    public static RepoSnapshot Take(string sandboxRoot, string? excludeDir = null, IEnumerable<string>? gitDirs = null)
    {
        var root = Path.GetFullPath(sandboxRoot);
        // Hash first: if one of the git questions below ever modified the sandbox, the next
        // snapshot's hash would show it instead of both snapshots absorbing the change.
        var treeHash = HashTree(root, excludeDir is null ? null : Path.GetFullPath(excludeDir));

        var dirs = (gitDirs ?? [Path.Combine(root, "repo")]).Select(d => Path.GetFullPath(Path.Combine(root, d)));
        var state = new StringBuilder();
        foreach (var dir in dirs.OrderBy(d => d, StringComparer.Ordinal))
        {
            state.Append("== ").Append(Path.GetRelativePath(root, dir).Replace('\\', '/')).Append('\n');
            foreach (var args in BuildCommands(dir))
            {
                var question = string.Join(' ', args.Skip(GitCommand.Prefix.Count + 2));
                var result = RunReadOnlyGit(args);
                state.Append("$ ").Append(question).Append(" -> exit ").Append(result.ExitCode).Append('\n');
                state.Append(result.StdoutText);
                if (result.Stdout.Length > 0 && result.Stdout[^1] != (byte)'\n')
                {
                    state.Append('\n');
                }
            }
        }

        return new RepoSnapshot(treeHash, state.ToString());
    }

    static ProcessResult RunReadOnlyGit(IReadOnlyList<string> args)
    {
        var limits = new ProcessLimits(TimeSpan.FromSeconds(120), 64L * 1024 * 1024, 1024 * 1024);
        // Never throws on a non-zero exit: a detached HEAD (symbolic-ref) and a bare
        // repository (status) legitimately fail, and the exit code is part of the state.
        return BoundedProcess.Run("git", args, limits, null, GitCommand.BuildEnvironment());
    }

    static string HashTree(string root, string? excludeDir)
    {
        var entries = new List<(string Rel, string Full, bool IsDir)>();
        foreach (var full in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            if (excludeDir is not null && IsSameOrUnder(full, excludeDir))
            {
                continue;
            }

            var rel = Path.GetRelativePath(root, full).Replace('\\', '/');
            entries.Add((rel, full, Directory.Exists(full)));
        }

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (rel, full, isDir) in entries.OrderBy(e => e.Rel, StringComparer.Ordinal))
        {
            sha.AppendData(Encoding.UTF8.GetBytes((isDir ? "D:" : "F:") + rel + "\0"));
            if (!isDir)
            {
                var bytes = File.ReadAllBytes(full);
                sha.AppendData(Encoding.UTF8.GetBytes(bytes.Length + "\0"));
                sha.AppendData(bytes);
            }
        }

        return Convert.ToHexString(sha.GetHashAndReset());
    }

    static bool IsSameOrUnder(string path, string dir)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var trimmed = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(path, trimmed, comparison)
            || path.StartsWith(trimmed + Path.DirectorySeparatorChar, comparison);
    }
}
