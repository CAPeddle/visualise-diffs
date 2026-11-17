namespace Vdiff.Core.Model;

// Records of the provisional tracer model and diff shapes (D-08). They are serialised with
// snake_case names, in declaration order, with null values omitted (see OutputWriter).

public sealed record SourceSpan(int StartByte, int EndByte, int StartLine, int EndLine);

public sealed record MemberModel(
    string Key, string Name, string Kind, string Access, string Signature, SourceSpan Span);

public sealed record TypeModel(
    string Id,
    string Fqn,
    string Kind,
    string Name,
    string Scope,
    string File,
    IReadOnlyList<string> Bases,
    SourceSpan Span,
    IReadOnlyList<MemberModel> Members);

public sealed record ExtractionInfo(
    string Extractor,
    string Mode,
    string TreeSitter,
    string TreeSitterCpp,
    IReadOnlyList<string> MacrosBlanked);

public sealed record ModelFileEntry(string Path, int ErrorNodes);

public sealed record UnmodelledEntry(string Path, string Reason);

public sealed record ModelFile(
    string Schema,
    string Commit,
    ExtractionInfo Extraction,
    IReadOnlyList<ModelFileEntry> Files,
    IReadOnlyList<UnmodelledEntry> Unmodelled,
    IReadOnlyList<TypeModel> Types)
{
    public const string SchemaName = "vdiff-model/v0-tracer";
}

public sealed record TypeCounts(int Added, int Removed, int Modified, int Unchanged);

public sealed record MemberCounts(int Added, int Removed, int Modified);

public sealed record UnmodelledCounts(int Base, int Head);

public sealed record DiffSummary(TypeCounts Types, MemberCounts Members, UnmodelledCounts Unmodelled);

public sealed record BaseChanges(IReadOnlyList<string> Added, IReadOnlyList<string> Removed);

/// <summary>One changed member; values that do not apply to the state are null and omitted from the JSON.</summary>
public sealed record MemberDiff(
    string Key,
    string Name,
    string State,
    string? AccessBefore,
    string? AccessAfter,
    string? SignatureBefore,
    string? SignatureAfter);

public sealed record TypeDiff(
    string Id,
    string Fqn,
    string Kind,
    string State,
    string? BaseFile,
    string? HeadFile,
    BaseChanges Bases,
    IReadOnlyList<MemberDiff> Members);

/// <summary>Exactly the content of diff.json: the summary and the changed types only.</summary>
public sealed record DiffFile(
    string Schema,
    string Base,
    string Head,
    DiffSummary Summary,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<TypeDiff> Types)
{
    public const string SchemaName = "vdiff-diff/v0-tracer";
}

/// <summary>The states a type or member can be in (D-07).</summary>
public static class States
{
    public const string Added = "added";
    public const string Removed = "removed";
    public const string Modified = "modified";
    public const string Unchanged = "unchanged";
}
