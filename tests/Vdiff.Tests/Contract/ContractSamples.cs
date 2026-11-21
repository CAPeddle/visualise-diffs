using Vdiff.Core.Model;

namespace Vdiff.Tests.Contract;

/// <summary>
/// Record instances with every optional property set and every list holding at least two distinct
/// elements, given out of canonical order. Names are synthetic and generic. They validate against the v1
/// schemas, so a record field missing from a schema fails additionalProperties (D-01), and they give the
/// plan 02-06 shuffle something to permute in every list (R-23). The raw <see cref="FullModel"/> is
/// deliberately not canonical (one type lists its files in reverse order); canonicalise it before the
/// model presence rules are applied.
/// </summary>
public static class ContractSamples
{
    static SourceSpan Span(int line) => new(line * 100, line * 100 + 60, line, line + 3);

    static MemberModel Member(string key, string name, string kind, string access, string[] modifiers, string signature, int line) =>
        new(key, name, kind, access, modifiers, signature, Span(line));

    public static ModelFile FullModel()
    {
        var beta = new TypeModel(
            Languages.CSharp,
            "class Synthetic.Beta",
            "Synthetic.Beta",
            "class",
            "Beta",
            "Synthetic",
            "internal",
            true,
            "src/Beta.a.cs",
            ["src/Beta.b.cs", "src/Beta.a.cs"],
            ["sealed", "partial"],
            ["Synthetic.IZ", "Synthetic.IA"],
            Span(4),
            [
                Member("Submit(Order order)", "Submit", "method", "public", ["virtual", "async"], "public virtual async Task Submit(Order order)", 6),
                Member("Count", "Count", "property", "public", ["static", "readonly"], "public static int Count { get; }", 5),
                Member("Changed", "Changed", "event", "protected-internal", [], "protected internal event Action Changed", 7),
            ]);
        var gamma = new TypeModel(
            Languages.CppCli,
            "class Synthetic::Interop::Gamma",
            "Synthetic::Interop::Gamma",
            "class",
            "Gamma",
            "Synthetic::Interop",
            null,
            false,
            "src/interop/gamma.h",
            null,
            ["sealed", "abstract"],
            ["Synthetic::Interop::Zeta", "Synthetic::Interop::Alpha"],
            Span(10),
            [
                Member("Spin(int turns)", "Spin", "method", "public", ["static", "inline"], "static void Spin(int turns)", 12),
                Member("Halt()", "Halt", "method", "private-protected", [], "void Halt()", 11),
                Member("Gamma(int seed)", "Gamma", "constructor", "public", [], "Gamma(int seed)", 13),
            ]);
        var alpha = new TypeModel(
            Languages.Cpp,
            "class synthetic::Alpha",
            "synthetic::Alpha",
            "class",
            "Alpha",
            "synthetic",
            null,
            false,
            "include/synthetic/alpha.h",
            null,
            ["sealed", "abstract"],
            ["synthetic::Zed", "synthetic::Base"],
            Span(20),
            [
                Member("width() const", "width", "method", "public", ["virtual", "const"], "virtual int width() const", 22),
                Member("height_", "height_", "field", "protected", [], "int height_ = 0", 21),
                Member("Mode", "Mode", "alias", "private", [], "using Mode = int", 23),
                Member("kRed", "kRed", "enumerator", "none", [], "kRed = 1", 24),
            ]);

        return new ModelFile(
            ModelFile.SchemaName,
            new ExtractionInfo(
            [
                new LanguageExtraction(Languages.CSharp, "roslyn", Modes.Semantic, null, null, []),
                new LanguageExtraction(Languages.Cpp, "cpp-ts", Modes.SyntaxOnly, "parse-failed", 3, ["LIB_CALL", "EXPORT_API"]),
            ]),
            [
                new ModelFileEntry("src/Beta.b.cs", Languages.CSharp, Modes.Semantic, null, 0),
                new ModelFileEntry("src/Beta.a.cs", Languages.CSharp, Modes.Semantic, null, 0),
                new ModelFileEntry("include/synthetic/alpha.h", Languages.Cpp, Modes.SyntaxOnly, "parse-failed", 2),
            ],
            [
                new UnmodelledEntry("src/blob.bin", "binary-file"),
                new UnmodelledEntry("include/synthetic/odd.h", "parse-failed"),
            ],
            [beta, gamma, alpha],
            [
                new EdgeModel(EdgeKinds.Base, new TypeRef(Languages.CSharp, "class Synthetic.Beta"), new TypeRef(Languages.CSharp, "class Synthetic.Orders.Document"), "Document", null),
                new EdgeModel(EdgeKinds.Association, new TypeRef(Languages.Cpp, "class synthetic::Alpha"), null, "Widget", "height_"),
            ],
            [
                "member width() const of class synthetic::Alpha (2 definitions)",
                "class synthetic::Dup (2 definitions)",
            ]);
    }

    public static ModelFile EmptyModel() =>
        new(ModelFile.SchemaName, new ExtractionInfo([]), [], [], [], [], []);

    static MemberDiff ModifiedMember(string key, string name, string before, string after) =>
        new(key, name, States.Modified, "private", "public", before, after, [], [], null);

    public static DiffFile FullDiff()
    {
        var added = new TypeDiff(
            Languages.Cpp, "class synthetic::Added", "synthetic::Added", "class", States.Added,
            null, "include/synthetic/added.h", null, null, new BaseChanges([], []), []);
        var removed = new TypeDiff(
            Languages.Cpp, "class synthetic::Removed", "synthetic::Removed", "class", States.Removed,
            "include/synthetic/removed.h", null, null, null, new BaseChanges([], []), []);
        var renamed = new TypeDiff(
            Languages.Cpp, "class synthetic::Disc", "synthetic::Disc", "class", States.Renamed,
            "include/synthetic/circle.h", "include/synthetic/disc.h",
            new RenameSource("class synthetic::Circle", "synthetic::Circle"),
            new RenameScore(775, new MemberJaccard(3, 4), true, true, false),
            new BaseChanges([], []), []);
        var modifiedCpp = new TypeDiff(
            Languages.Cpp, "class synthetic::ModA", "synthetic::ModA", "class", States.Modified,
            "include/synthetic/moda.h", "include/synthetic/moda.h", null, null,
            new BaseChanges(["synthetic::Zed", "synthetic::Alpha"], ["synthetic::Yak", "synthetic::Beta"]),
            [
                new MemberDiff("show()", "show", States.Added, null, "public", null, "void show()", null, [], null),
                new MemberDiff("Disc(double r)", "Disc", States.Renamed, "public", "public", "Circle(double r)", "Disc(double r)", [], [], "Circle(double r)"),
                new MemberDiff("width() const", "width", States.Modified, "protected", "public", "int width() const", "long width() const", ["virtual", "static"], ["override", "inline"], null),
                new MemberDiff("paint()", "paint", States.Removed, "public", null, "void paint()", null, [], null, null),
                new MemberDiff("area() const", "area", States.BodyChanged, "public", "public", "double area() const", "double area() const", [], [], null),
            ]);
        var modifiedCSharp = new TypeDiff(
            Languages.CSharp, "class Synthetic.ModB", "Synthetic.ModB", "class", States.Modified,
            "src/ModB.cs", "src/ModB.cs", null, null, new BaseChanges([], []),
            [ModifiedMember("Submit(Order order)", "Submit", "void Submit(Order order)", "Receipt Submit(Order order)")]);

        return new DiffFile(
            DiffFile.SchemaName,
            new DiffSummary(
                new TypeCounts(1, 1, 2, 1, 4),
                new MemberCounts(1, 1, 2, 1, 1),
                new EdgeCounts(1, 1, 1, 0, 2),
                new UnmodelledCounts(1, 2)),
            [
                "head: include/synthetic/b.h with parse errors",
                "base: include/synthetic/a.h not modelled",
            ],
            [
                new PackageDiff("synthetic-pkg-b", "updated", "1.0.0", "1.1.0"),
                new PackageDiff("synthetic-pkg-a", "added", null, "2.0.0"),
            ],
            [modifiedCSharp, removed, modifiedCpp, renamed, added],
            [
                new EdgeDiff(EdgeKinds.Base, new TypeRef(Languages.Cpp, "class synthetic::Added"), new TypeRef(Languages.Cpp, "class synthetic::ModA"), null, null, "ModA", null, States.Added),
                new EdgeDiff(EdgeKinds.Association, new TypeRef(Languages.Cpp, "class synthetic::Removed"), null, null, null, "Widget", "owner_", States.Removed),
                new EdgeDiff(
                    EdgeKinds.Base,
                    new TypeRef(Languages.Cpp, "class synthetic::ModA"),
                    To: null,
                    ToBefore: new TypeRef(Languages.Cpp, "class synthetic::Base"),
                    ToAfter: new TypeRef(Languages.Cpp, "class other::Base"),
                    ToName: "Base",
                    Via: null,
                    State: States.Modified),
            ]);
    }

    public static DiffFile EmptyDiff() =>
        new(
            DiffFile.SchemaName,
            new DiffSummary(new TypeCounts(0, 0, 0, 0, 0), new MemberCounts(0, 0, 0, 0, 0), new EdgeCounts(0, 0, 0, 0, 0), new UnmodelledCounts(0, 0)),
            [],
            [],
            [],
            []);

    public static RunMeta FullMeta() =>
        new(
            RunMeta.SchemaName,
            "0.1.0",
            new string('a', 40),
            new string('b', 40),
            [
                new ExtractorRun("roslyn", [new ToolVersion("roslyn", "4.12.0"), new ToolVersion("dotnet", "10.0.0")]),
                new ExtractorRun("cpp-ts", [new ToolVersion("tree-sitter-cpp", "0.23.4"), new ToolVersion("tree-sitter", "0.27.0")]),
            ]);
}
