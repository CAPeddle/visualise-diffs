namespace Vdiff.Core.Model;

// The model records are the vdiff-model/v1 contract, mirrored by docs/schema/ (plan 02-05). They are
// serialised with snake_case names, in declaration order, with null values omitted (see OutputWriter),
// so the declaration order of the positional parameters is the JSON key order. A nullable property
// is optional in the schema; every other property, and every list, is always written. The diff
// records further down are still the provisional tracer shape (plan 02-12 rewrites them).

/// <summary>The language values a type or file can carry; the managed-wrapper lane is <c>cpp-cli</c> (D-17).</summary>
public static class Languages
{
    public const string Cpp = "cpp";
    public const string CSharp = "csharp";
    public const string CppCli = "cpp-cli";
}

/// <summary>How a language, or one file of it, was extracted.</summary>
public static class Modes
{
    public const string Semantic = "semantic";
    public const string SyntaxOnly = "syntax-only";
    public const string Scanner = "scanner";
}

/// <summary>The kinds of edge in the model's <c>edges</c> array.</summary>
public static class EdgeKinds
{
    public const string Base = "base";
    public const string Association = "association";
}

public sealed record SourceSpan(int StartByte, int EndByte, int StartLine, int EndLine);

public sealed record MemberModel(
    string Key,
    string Name,
    string Kind,
    string Access,
    IReadOnlyList<string> Modifiers,
    string Signature,
    SourceSpan Span);

/// <summary>
/// One type of one language, identified by (<c>Language</c>, <c>Id</c>). Type rules (D-19):
/// <c>Access</c> is the declared or language-default type-level accessibility and is absent when the
/// extractor has none; it is never <c>none</c>, absence being the only encoding of "no type-level access".
/// <c>Partial</c> is true only for a type declared in parts. <c>Files</c> is present exactly when
/// <c>Partial</c> is true: every file that declares a part, sorted ordinally and without repeats, with
/// <c>File</c> as its first entry and <c>Span</c> the span of the earliest part in that file.
/// </summary>
public sealed record TypeModel(
    string Language,
    string Id,
    string Fqn,
    string Kind,
    string Name,
    string Scope,
    string? Access,
    bool Partial,
    string File,
    IReadOnlyList<string>? Files,
    IReadOnlyList<string> Modifiers,
    IReadOnlyList<string> Bases,
    SourceSpan Span,
    IReadOnlyList<MemberModel> Members);

/// <summary>A type named by language and id; the endpoint of an edge.</summary>
public sealed record TypeRef(string Language, string Id);

/// <summary>
/// One edge between types. <c>To</c> is present only when the extractor resolved <c>ToName</c> to one
/// modelled type; <c>ToName</c> is the name as written. <c>Via</c> holds the member key that creates an
/// association edge and is absent on a base edge (D-16).
/// </summary>
public sealed record EdgeModel(string Kind, TypeRef From, TypeRef? To, string ToName, string? Via);

/// <summary>How one language was extracted in this run.</summary>
public sealed record LanguageExtraction(
    string Language,
    string Extractor,
    string Mode,
    string? FallbackReason,
    int? UnresolvedSymbols,
    IReadOnlyList<string> MacrosBlanked);

/// <summary>Extraction metadata that is stable across runs; tool versions live in run.meta.json (D-04).</summary>
public sealed record ExtractionInfo(IReadOnlyList<LanguageExtraction> Languages);

public sealed record ModelFileEntry(string Path, string Language, string Mode, string? FallbackReason, int ErrorNodes);

public sealed record UnmodelledEntry(string Path, string Reason);

/// <summary>
/// The content of a base or head model file. <c>Duplicates</c> lists the definitions that share an id
/// (a repeated type, or a repeated member key inside one type), ordinal sorted and always written.
/// </summary>
public sealed record ModelFile(
    string Schema,
    ExtractionInfo Extraction,
    IReadOnlyList<ModelFileEntry> Files,
    IReadOnlyList<UnmodelledEntry> Unmodelled,
    IReadOnlyList<TypeModel> Types,
    IReadOnlyList<EdgeModel> Edges,
    IReadOnlyList<string> Duplicates)
{
    public const string SchemaName = "vdiff-model/v1";
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
