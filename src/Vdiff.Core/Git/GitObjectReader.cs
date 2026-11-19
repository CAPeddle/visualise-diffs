using System.Text;
using System.Text.RegularExpressions;
using Vdiff.Core.Process;

namespace Vdiff.Core.Git;

/// <summary>A git version, compared by major, minor and patch.</summary>
public readonly partial record struct GitVersion(int Major, int Minor, int Patch)
{
    /// <summary>The lowest supported git: 2.45 introduced <c>GIT_NO_LAZY_FETCH</c> (D-11).</summary>
    public static GitVersion Minimum => new(2, 45, 0);

    /// <summary>Reads <c>git version 2.55.0.windows.3</c> as 2.55.0.</summary>
    public static GitVersion Parse(string text)
    {
        var match = VersionPattern().Match(text);
        if (!match.Success)
        {
            throw new VdiffException("could not read the git version from the output of 'git --version'");
        }

        var patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        return new GitVersion(
            int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
            patch);
    }

    /// <summary>
    /// Refuses a git older than <see cref="Minimum"/>: older versions ignore
    /// <c>GIT_NO_LAZY_FETCH</c>, so a partial clone could fetch objects into the repository (T-01-08).
    /// </summary>
    public static void EnsureMinimum(GitVersion version)
    {
        if (version.CompareKey() < Minimum.CompareKey())
        {
            throw new VdiffException(
                $"git {version} is too old: vdiff needs git {Minimum.Major}.{Minimum.Minor} or newer, " +
                "because older versions cannot be told not to lazy-fetch missing objects into the repository (lazy-fetch protection)");
        }
    }

    long CompareKey() => ((long)Major * 1_000_000 + Minor) * 1_000_000 + Patch;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    [GeneratedRegex(@"git version (\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}

/// <summary>A regular file that differs between two commits; a null blob id means absent on that side.</summary>
public sealed record ChangedFile(string Path, string? BaseBlob, string? HeadBlob);

/// <summary>A file that could not be modelled, with the reason (never file content).</summary>
public sealed record UnmodelledFile(string Path, string Reason);

public sealed record ChangeList(IReadOnlyList<ChangedFile> Files, IReadOnlyList<UnmodelledFile> Unmodelled);

public sealed record BlobRequest(string Path, string Oid);

public sealed record BlobContent(string Path, string Oid, byte[] Bytes);

public sealed record BlobReadResult(IReadOnlyList<BlobContent> Blobs, IReadOnlyList<UnmodelledFile> Unmodelled);

/// <summary>Absolute locations of a repository; <see cref="Root"/> is the work-tree root, or the git directory for a bare repository.</summary>
public sealed record RepoLocation(string Root, string GitDir, string CommonDir, bool IsBare);

/// <summary>
/// Reads commits from git objects only (D-02): <c>rev-parse</c>, <c>diff-tree</c> and
/// <c>cat-file</c>, every call an argument array through <see cref="BoundedProcess"/> with the
/// shared read-only prefix and environment of <see cref="GitCommand"/>. Nothing is checked
/// out and nothing is written to the repository.
/// </summary>
public sealed class GitObjectReader
{
    public const long DefaultMaxBlobBytes = 4L * 1024 * 1024;
    public const long DefaultMaxTotalBytes = 256L * 1024 * 1024;

    const long MaxListingBytes = 256L * 1024 * 1024;
    const int PerObjectOutputOverhead = 160;
    static readonly ProcessLimits SmallLimits = new(TimeSpan.FromSeconds(60), 1024 * 1024, 256 * 1024);
    static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    readonly string _repoPath;

    /// <summary>Reads <c>git --version</c> once, before any other call, and refuses git older than 2.45.</summary>
    public GitObjectReader(string repoPath)
    {
        _repoPath = repoPath ?? throw new ArgumentNullException(nameof(repoPath));
        var result = BoundedProcess.Run("git", VersionArguments(), SmallLimits, null, GitCommand.BuildEnvironment());
        if (result.ExitCode != 0)
        {
            throw new VdiffException("'git --version' failed; is git installed and on the PATH?");
        }

        GitVersion.EnsureMinimum(GitVersion.Parse(result.StdoutText));
    }

    // ---- pure argument builders (every list starts with GitCommand.Prefix) ----

    public static IReadOnlyList<string> VersionArguments() => [.. GitCommand.Prefix, "--version"];

    public static IReadOnlyList<string> ResolveCommitArguments(string repoPath, string hex)
    {
        ValidateCommitInput(hex);
        return GitCommand.Build(repoPath, ["rev-parse", "--verify", "--quiet", "--end-of-options", hex + "^{commit}"]);
    }

    public static IReadOnlyList<string> DiffTreeArguments(string repoPath, string baseOid, string headOid)
    {
        RequireFullId(baseOid);
        RequireFullId(headOid);
        return GitCommand.Build(repoPath, ["diff-tree", "-r", "-z", "--raw", "--no-renames", "--no-abbrev", baseOid, headOid]);
    }

    public static IReadOnlyList<string> CatFileBatchCheckArguments(string repoPath) =>
        GitCommand.Build(repoPath, ["cat-file", "--batch-check"]);

    public static IReadOnlyList<string> CatFileBatchArguments(string repoPath) =>
        GitCommand.Build(repoPath, ["cat-file", "--batch"]);

    // ---- commit ids ----

    /// <summary>A commit id is 7 to 64 hexadecimal characters, nothing else (T-01-01).</summary>
    public static void ValidateCommitInput(string hex)
    {
        if (hex is null || hex.Length < 7 || hex.Length > 64 || !hex.All(char.IsAsciiHexDigit))
        {
            throw new VdiffException("a commit id must be 7 to 64 hexadecimal characters (no branch names, no options)");
        }
    }

    /// <summary>
    /// Resolves a validated hexadecimal id to the full commit id. A branch or tag named like
    /// an abbreviation would otherwise win over the commit, so the full id must start with
    /// the input (T-01-31).
    /// </summary>
    public string ResolveCommit(string hex)
    {
        ValidateCommitInput(hex);
        var result = Run(ResolveCommitArguments(_repoPath, hex), SmallLimits);
        var full = result.StdoutText.Trim();
        if (result.ExitCode != 0 || full.Length == 0)
        {
            throw new VdiffException("the commit id does not name a commit in this repository");
        }

        if (!IsFullId(full))
        {
            throw new VdiffException("git returned something other than a commit id");
        }

        if (!full.StartsWith(hex, StringComparison.OrdinalIgnoreCase))
        {
            throw new VdiffException(
                "the commit id is shadowed by a branch or tag of the same name; pass the full 40-character id");
        }

        return full.ToLowerInvariant();
    }

    // ---- changes between two commits ----

    /// <summary>Added, modified and deleted regular files between two full commit ids, in ordinal path order.</summary>
    public ChangeList ListChanged(string baseOid, string headOid)
    {
        var limits = new ProcessLimits(TimeSpan.FromMinutes(5), MaxListingBytes, 256 * 1024);
        var result = Run(DiffTreeArguments(_repoPath, baseOid, headOid), limits);
        if (result.ExitCode != 0)
        {
            throw Failure("diff-tree", result);
        }

        return ParseDiffTree(result.Stdout);
    }

    /// <summary>
    /// Parses <c>diff-tree -r -z --raw</c> output: records <c>:mode mode oid oid status NUL path NUL</c>.
    /// Each side is judged by its own mode (only 100644 and 100755 are regular files), a change
    /// with the same blob on both sides (a mode-only change) is dropped, and the result is sorted
    /// by ordinal path here rather than trusting git's order (diff.orderFile can change it).
    /// </summary>
    public static ChangeList ParseDiffTree(byte[] raw)
    {
        var files = new List<ChangedFile>();
        var unmodelled = new List<UnmodelledFile>();
        var position = 0;
        while (position < raw.Length)
        {
            var metaEnd = Array.IndexOf(raw, (byte)0, position);
            if (metaEnd < 0)
            {
                throw new VdiffException("truncated diff-tree record");
            }

            var meta = Encoding.ASCII.GetString(raw, position, metaEnd - position);
            position = metaEnd + 1;
            var pathEnd = Array.IndexOf(raw, (byte)0, position);
            if (pathEnd < 0)
            {
                throw new VdiffException("truncated diff-tree record");
            }

            var pathBytes = raw.AsSpan(position, pathEnd - position);
            position = pathEnd + 1;

            var fields = meta.StartsWith(':') ? meta[1..].Split(' ') : Array.Empty<string>();
            if (fields.Length != 5 || !IsFullId(fields[2]) || !IsFullId(fields[3]))
            {
                throw new VdiffException("unexpected diff-tree record");
            }

            var baseBlob = RegularBlob(fields[0], fields[2]);
            var headBlob = RegularBlob(fields[1], fields[3]);
            if (baseBlob is null && headBlob is null)
            {
                continue; // gitlink, symlink or nothing on both sides
            }

            if (baseBlob is not null && headBlob is not null && string.Equals(baseBlob, headBlob, StringComparison.Ordinal))
            {
                continue; // mode-only change: same blob on both sides (D-10)
            }

            string path;
            try
            {
                path = StrictUtf8.GetString(pathBytes);
            }
            catch (DecoderFallbackException)
            {
                unmodelled.Add(new UnmodelledFile(Encoding.UTF8.GetString(pathBytes), "non-utf8-path"));
                continue;
            }

            files.Add(new ChangedFile(path, baseBlob, headBlob));
        }

        return new ChangeList(
            files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList(),
            unmodelled.OrderBy(u => u.Path, StringComparer.Ordinal).ToList());
    }

    static string? RegularBlob(string mode, string oid) =>
        (mode == "100644" || mode == "100755") && !IsZeroId(oid) ? oid : null;

    // ---- blob contents ----

    /// <summary>
    /// Reads blob bytes exactly as stored (no filters, so core.autocrlf is irrelevant). Sizes
    /// come first from <c>cat-file --batch-check</c>: a blob over <paramref name="maxBlobBytes"/>
    /// is never read, a missing object is reported, and once the running total of accepted blobs
    /// would pass <paramref name="maxTotalBytes"/> every later entry in ordinal path order is
    /// reported too, so one large pull request cannot make the sidecar refuse the whole run (C20).
    /// </summary>
    public BlobReadResult ReadBlobs(
        IReadOnlyList<BlobRequest> entries,
        long maxBlobBytes = DefaultMaxBlobBytes,
        long maxTotalBytes = DefaultMaxTotalBytes)
    {
        foreach (var entry in entries)
        {
            RequireFullId(entry.Oid);
            if (IsZeroId(entry.Oid))
            {
                throw new VdiffException("an all-zero blob id is never read");
            }
        }

        var ordered = entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToList();
        if (ordered.Count == 0)
        {
            return new BlobReadResult([], []);
        }

        // Pass 1: type and size of every object, nothing read.
        var checkInput = Encoding.ASCII.GetBytes(string.Concat(ordered.Select(e => e.Oid + "\n")));
        var checkLimits = new ProcessLimits(
            TimeSpan.FromMinutes(5), ordered.Count * (long)PerObjectOutputOverhead + 4096, 256 * 1024);
        var check = Run(CatFileBatchCheckArguments(_repoPath), checkLimits, checkInput);
        if (check.ExitCode != 0)
        {
            throw Failure("cat-file --batch-check", check);
        }

        var lines = check.StdoutText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != ordered.Count)
        {
            throw new VdiffException("unexpected output from 'git cat-file --batch-check'");
        }

        var skipReason = new string?[ordered.Count];
        var sizes = new long[ordered.Count];
        var total = 0L;
        var budgetExhausted = false;
        for (var i = 0; i < ordered.Count; i++)
        {
            var parts = lines[i].TrimEnd('\r').Split(' ');
            if (parts.Length < 2 || !string.Equals(parts[0], ordered[i].Oid, StringComparison.OrdinalIgnoreCase))
            {
                throw new VdiffException("unexpected output from 'git cat-file --batch-check'");
            }

            if (parts[1] == "missing")
            {
                skipReason[i] = "object-missing";
                continue;
            }

            if (parts.Length != 3 || !long.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var size))
            {
                throw new VdiffException("unexpected output from 'git cat-file --batch-check'");
            }

            if (parts[1] != "blob")
            {
                skipReason[i] = "not-a-blob";
            }
            else if (size > maxBlobBytes)
            {
                skipReason[i] = "too-large";
            }
            else if (budgetExhausted || total + size > maxTotalBytes)
            {
                budgetExhausted = true;
                skipReason[i] = "total-too-large";
            }
            else
            {
                total += size;
                sizes[i] = size;
            }
        }

        // Pass 2: the accepted blobs only.
        var accepted = Enumerable.Range(0, ordered.Count).Where(i => skipReason[i] is null).ToList();
        var contents = new byte[ordered.Count][];
        if (accepted.Count > 0)
        {
            var batchInput = Encoding.ASCII.GetBytes(string.Concat(accepted.Select(i => ordered[i].Oid + "\n")));
            var batchLimits = new ProcessLimits(
                TimeSpan.FromMinutes(5), total + accepted.Count * (long)PerObjectOutputOverhead + 4096, 256 * 1024);
            var batch = Run(CatFileBatchArguments(_repoPath), batchLimits, batchInput);
            if (batch.ExitCode != 0)
            {
                throw Failure("cat-file --batch", batch);
            }

            var position = 0;
            foreach (var i in accepted)
            {
                var headerEnd = Array.IndexOf(batch.Stdout, (byte)'\n', position);
                if (headerEnd < 0)
                {
                    throw new VdiffException("unexpected output from 'git cat-file --batch'");
                }

                var header = Encoding.ASCII.GetString(batch.Stdout, position, headerEnd - position).TrimEnd('\r').Split(' ');
                position = headerEnd + 1;
                if (header.Length < 2 || !string.Equals(header[0], ordered[i].Oid, StringComparison.OrdinalIgnoreCase))
                {
                    throw new VdiffException("unexpected output from 'git cat-file --batch'");
                }

                if (header[1] == "missing")
                {
                    skipReason[i] = "object-missing"; // vanished between the two passes
                    continue;
                }

                if (header.Length != 3 || header[1] != "blob" || !long.TryParse(header[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var size)
                    || size != sizes[i] || position + size + 1 > batch.Stdout.Length || batch.Stdout[position + size] != (byte)'\n')
                {
                    throw new VdiffException("unexpected output from 'git cat-file --batch'");
                }

                contents[i] = batch.Stdout.AsSpan(position, (int)size).ToArray();
                position += (int)size + 1;
            }
        }

        var blobs = new List<BlobContent>();
        var skipped = new List<UnmodelledFile>();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (skipReason[i] is { } reason)
            {
                skipped.Add(new UnmodelledFile(ordered[i].Path, reason));
            }
            else
            {
                blobs.Add(new BlobContent(ordered[i].Path, ordered[i].Oid, contents[i]));
            }
        }

        return new BlobReadResult(blobs, skipped);
    }

    // ---- repository location ----

    /// <summary>
    /// The absolute work-tree root, or the git directory of a bare repository: the root a
    /// <c>--out</c> directory must stay clear of (plan 01-03).
    /// </summary>
    public string ToplevelOrGitDir() => Locate().Root;

    /// <summary>
    /// Absolute locations from <c>rev-parse --path-format=absolute</c> (without the flag a
    /// subdirectory gets relative answers, C16), normalised with <see cref="Path.GetFullPath(string)"/>.
    /// </summary>
    public RepoLocation Locate()
    {
        var info = Run(
            GitCommand.Build(_repoPath, ["rev-parse", "--path-format=absolute", "--is-bare-repository", "--absolute-git-dir", "--git-common-dir"]),
            SmallLimits);
        if (info.ExitCode != 0)
        {
            throw Failure("rev-parse", info);
        }

        var lines = info.StdoutText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        if (lines.Length != 3 || (lines[0] != "true" && lines[0] != "false"))
        {
            throw new VdiffException("unexpected output from 'git rev-parse'");
        }

        var isBare = lines[0] == "true";
        var gitDir = Path.GetFullPath(lines[1]);
        var commonDir = Path.GetFullPath(lines[2]);
        if (isBare)
        {
            return new RepoLocation(gitDir, gitDir, commonDir, true);
        }

        var top = Run(GitCommand.Build(_repoPath, ["rev-parse", "--path-format=absolute", "--show-toplevel"]), SmallLimits);
        var root = top.StdoutText.Trim();
        if (top.ExitCode != 0 || root.Length == 0)
        {
            throw Failure("rev-parse --show-toplevel", top);
        }

        return new RepoLocation(Path.GetFullPath(root), gitDir, commonDir, false);
    }

    // ---- helpers ----

    ProcessResult Run(IReadOnlyList<string> args, ProcessLimits limits, byte[]? stdin = null) =>
        BoundedProcess.Run("git", args, limits, stdin, GitCommand.BuildEnvironment());

    static VdiffException Failure(string what, ProcessResult result)
    {
        var tail = result.StderrTail.Trim();
        return new VdiffException(
            $"git {what} failed (exit {result.ExitCode})" + (tail.Length > 0 ? ": " + (tail.Length > 500 ? tail[^500..] : tail) : "."));
    }

    static bool IsFullId(string id) => (id.Length == 40 || id.Length == 64) && id.All(char.IsAsciiHexDigit);

    static bool IsZeroId(string id) => id.All(c => c == '0');

    static void RequireFullId(string id)
    {
        if (id is null || !IsFullId(id))
        {
            throw new VdiffException("expected a full 40- or 64-character hexadecimal object id");
        }
    }
}
