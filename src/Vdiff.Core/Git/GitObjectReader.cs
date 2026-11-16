using Vdiff.Core.Process;

namespace Vdiff.Core.Git;

/// <summary>A git version, compared by major, minor and patch.</summary>
public readonly record struct GitVersion(int Major, int Minor, int Patch)
{
    /// <summary>The lowest supported git: 2.45 introduced <c>GIT_NO_LAZY_FETCH</c> (D-11).</summary>
    public static GitVersion Minimum => new(2, 45, 0);

    public static GitVersion Parse(string text) => throw new NotImplementedException();

    public static void EnsureMinimum(GitVersion version) => throw new NotImplementedException();

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/// <summary>A regular file that differs between two commits; a null blob id means absent on that side.</summary>
public sealed record ChangedFile(string Path, string? BaseBlob, string? HeadBlob);

/// <summary>A file that could not be modelled, with the reason (no file content).</summary>
public sealed record UnmodelledFile(string Path, string Reason);

public sealed record ChangeList(IReadOnlyList<ChangedFile> Files, IReadOnlyList<UnmodelledFile> Unmodelled);

public sealed record BlobRequest(string Path, string Oid);

public sealed record BlobContent(string Path, string Oid, byte[] Bytes);

public sealed record BlobReadResult(IReadOnlyList<BlobContent> Blobs, IReadOnlyList<UnmodelledFile> Unmodelled);

/// <summary>Absolute locations of a repository; <see cref="Root"/> is the work-tree root, or the git directory for a bare repository.</summary>
public sealed record RepoLocation(string Root, string GitDir, string CommonDir, bool IsBare);

public sealed class GitObjectReader
{
    public const long DefaultMaxBlobBytes = 4L * 1024 * 1024;
    public const long DefaultMaxTotalBytes = 256L * 1024 * 1024;

    public GitObjectReader(string repoPath) => throw new NotImplementedException();

    public static void ValidateCommitInput(string hex) => throw new NotImplementedException();

    public static IReadOnlyList<string> VersionArguments() => throw new NotImplementedException();

    public static IReadOnlyList<string> ResolveCommitArguments(string repoPath, string hex) => throw new NotImplementedException();

    public static IReadOnlyList<string> DiffTreeArguments(string repoPath, string baseOid, string headOid) => throw new NotImplementedException();

    public static IReadOnlyList<string> CatFileBatchCheckArguments(string repoPath) => throw new NotImplementedException();

    public static IReadOnlyList<string> CatFileBatchArguments(string repoPath) => throw new NotImplementedException();

    public string ResolveCommit(string hex) => throw new NotImplementedException();

    public ChangeList ListChanged(string baseOid, string headOid) => throw new NotImplementedException();

    public static ChangeList ParseDiffTree(byte[] raw) => throw new NotImplementedException();

    public BlobReadResult ReadBlobs(
        IReadOnlyList<BlobRequest> entries,
        long maxBlobBytes = DefaultMaxBlobBytes,
        long maxTotalBytes = DefaultMaxTotalBytes)
        => throw new NotImplementedException();

    public RepoLocation Locate() => throw new NotImplementedException();

    public string ToplevelOrGitDir() => throw new NotImplementedException();
}
