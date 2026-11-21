using System.Text.RegularExpressions;
using Vdiff.Core.Model;
using Vdiff.Core.Output;

namespace Vdiff.Tests.Output;

/// <summary>
/// The Mermaid summary (D-08, T-01-20). Every writer output in this class is passed through
/// <see cref="MermaidLint"/>. What that proves: the structure (header, class lines, state tags,
/// edges, classDef lines, balanced braces) is the writer's own and cannot be changed by
/// repository text. What it does not prove: that Mermaid draws every glyph well. Measured with
/// mermaid-cli 12.2.0 during planning, a raw brace or a raw double quote inside a label breaks
/// parsing, while a tilde, a trailing asterisk or dollar sign, angle brackets, percent signs,
/// a hash, a semicolon and a double colon in member text only parse without error and their
/// rendering was not inspected, so the escapes stay. The trailing member-tag format is the
/// plan's provisional one until spike S1 is run.
/// </summary>
public class MermaidWriterTests
{
    const string ClassDefAdded = "  classDef added fill:#d4f8d4,stroke:#2e7d32,color:#111\n";
    const string ClassDefRemoved = "  classDef removed fill:#fde0e0,stroke:#c62828,color:#111,stroke-dasharray:4 3\n";
    const string ClassDefModified = "  classDef modified fill:#fff4cc,stroke:#b58900,color:#111\n";
    const string ClassDefs = ClassDefAdded + ClassDefRemoved + ClassDefModified;

    static DiffFile Diff(params TypeDiff[] types) =>
        new(
            DiffFile.SchemaName,
            new DiffSummary(new TypeCounts(0, 0, 0, 0), new MemberCounts(0, 0, 0), new UnmodelledCounts(0, 0)),
            [],
            types);

    static TypeDiff Type(
        string fqn,
        string state = "modified",
        string kind = "class",
        string[]? basesAdded = null,
        string[]? basesRemoved = null,
        MemberDiff[]? members = null) =>
        new(
            kind + " " + fqn,
            fqn,
            kind,
            state,
            state == "added" ? null : "a.h",
            state == "removed" ? null : "a.h",
            new BaseChanges(basesAdded ?? [], basesRemoved ?? []),
            members ?? []);

    static MemberDiff Member(
        string key,
        string state = "added",
        string? after = null,
        string? before = null,
        string? accessAfter = "public",
        string? accessBefore = null,
        string name = "m") =>
        new(key, name, state, accessBefore, accessAfter, before, after);

    static string Render(DiffFile diff)
    {
        var text = MermaidWriter.Render(diff);
        Assert.Empty(MermaidLint.Check(text));
        return text;
    }

    static string[] Lines(string text) => text.Split('\n');

    static readonly Regex ClassIdOnLine = new("^  class (\\S+?)\\[", RegexOptions.CultureInvariant);

    static readonly Regex EdgeEndpoints = new("^  (\\S+) <\\|-- (\\S+)$", RegexOptions.CultureInvariant);

    [Fact]
    public void Output_starts_with_classDiagram_uses_LF_and_ends_with_exactly_one_LF()
    {
        var text = Render(Diff(Type("shop::A", members: [Member("f()", after: "void f()")])));

        Assert.StartsWith("classDiagram\n", text);
        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("\n", text);
        Assert.DoesNotContain("\n\n", text);
    }

    [Fact]
    public void Class_blocks_come_first_in_id_order_then_sorted_edges_then_exactly_three_classDef_lines()
    {
        var types = new[]
        {
            Type("shop::Zed", "added", basesAdded: ["shop::Base"]),
            Type("shop::Base", "modified", members: [Member("f()", "added", "void f()")]),
            Type("shop::Alpha", "removed", basesRemoved: ["shop::Base"]),
        };

        var text = Render(Diff(types));

        Assert.Equal(
            "classDiagram\n" +
            "  class C_shop__Alpha[\"shop::Alpha\"]:::removed\n" +
            "  class C_shop__Base[\"shop::Base\"]:::modified {\n" +
            "    +void f() [added]\n" +
            "  }\n" +
            "  class C_shop__Zed[\"shop::Zed\"]:::added\n" +
            "  C_shop__Base <|-- C_shop__Alpha\n" +
            "  C_shop__Base <|-- C_shop__Zed\n" +
            ClassDefs,
            text);
    }

    [Fact]
    public void Types_given_in_any_order_render_identically()
    {
        var a = Type("shop::A", members: [Member("g()", "added", "void g()"), Member("f()", "added", "void f()")]);
        var b = Type("shop::B", "added", basesAdded: ["A"]);
        var c = Type("other::C", "removed");

        Assert.Equal(Render(Diff(a, b, c)), Render(Diff(c, b, a)));
    }

    [Fact]
    public void Members_are_written_one_per_line_in_key_order()
    {
        var text = Render(Diff(Type("A", members: [Member("zeta()", "added", "void zeta()"), Member("alpha()", "added", "void alpha()")])));

        var members = Lines(text).Where(l => l.StartsWith("    ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(["    +void alpha() [added]", "    +void zeta() [added]"], members);
    }

    [Fact]
    public void Unchanged_types_and_members_are_never_drawn()
    {
        var text = Render(Diff(
            Type("shop::Same", "unchanged"),
            Type("shop::Changed", members: [Member("f()", "unchanged", "void f()"), Member("g()", "added", "void g()")])));

        Assert.DoesNotContain("Same", text);
        Assert.DoesNotContain("void f()", text);
        Assert.Contains("void g()", text);
    }

    [Theory]
    [InlineData("shop::Gadget", "C_shop__Gadget")]
    [InlineData("::a", "C___a")]
    [InlineData("9lives", "C_9lives")]
    [InlineData("café::Ü", "C_caf____")]
    [InlineData("a b-c", "C_a_b_c")]
    public void Class_id_is_C_underscore_then_the_fqn_with_other_characters_replaced(string fqn, string expectedId)
    {
        var text = Render(Diff(Type(fqn, "added")));

        Assert.Equal(expectedId, new string(Lines(text)[1].Skip("  class ".Length).TakeWhile(c => c != '[').ToArray()));
    }

    [Theory]
    [InlineData("link")]
    [InlineData("note")]
    [InlineData("click")]
    [InlineData("callback")]
    [InlineData("href")]
    [InlineData("cssClass")]
    public void Keyword_named_global_types_get_a_prefixed_class_id_and_keep_their_readable_label(string name)
    {
        var text = Render(Diff(Type(name, "added")));

        var classLines = Lines(text).Where(l => l.StartsWith("  class ", StringComparison.Ordinal)).ToArray();
        Assert.Equal([$"  class C_{name}[\"{name}\"]:::added"], classLines);
        Assert.DoesNotContain($"  class {name}[", text);
    }

    [Fact]
    public void A_keyword_named_base_is_drawn_with_prefixed_ids_on_the_edge()
    {
        var text = Render(Diff(
            Type("link", "modified"),
            Type("derived", "added", basesAdded: ["link"])));

        Assert.Contains("  C_link <|-- C_derived\n", text);
        Assert.DoesNotContain("  link <|--", text);
    }

    [Fact]
    public void Every_class_id_and_edge_endpoint_starts_with_the_C_prefix()
    {
        var types = new[]
        {
            Type("link", "added"),
            Type("note", "added"),
            Type("click", "added"),
            Type("callback", "added"),
            Type("href", "added"),
            Type("cssClass", "added"),
            Type("", "added"),
            Type("::a", "added"),
            Type("9lives", "added"),
            Type("a b", "added"),
            Type("shop::Widget", "modified"),
            Type("derived", "added", basesAdded: ["link", "shop::Widget"]),
        };

        var text = Render(Diff(types));

        var ids = new List<string>();
        foreach (var line in Lines(text))
        {
            var classMatch = ClassIdOnLine.Match(line);
            if (classMatch.Success)
            {
                ids.Add(classMatch.Groups[1].Value);
            }

            var edgeMatch = EdgeEndpoints.Match(line);
            if (edgeMatch.Success)
            {
                ids.Add(edgeMatch.Groups[1].Value);
                ids.Add(edgeMatch.Groups[2].Value);
            }
        }

        // At least one id per class line and two per edge line were read, so an empty read cannot pass.
        Assert.True(ids.Count >= types.Length + 2, $"only {ids.Count} ids were read back");
        Assert.All(ids, id => Assert.StartsWith("C_", id, StringComparison.Ordinal));
    }

    [Fact]
    public void Colliding_ids_get_numbered_suffixes_in_canonical_order_and_every_class_line_carries_its_state()
    {
        var text = Render(Diff(
            Type("a__b", "added"),
            Type("a::b", "removed"),
            Type("a b", "modified")));

        var classLines = Lines(text).Where(l => l.StartsWith("  class ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(
            [
                "  class C_a_b[\"a b\"]:::modified",
                "  class C_a__b[\"a::b\"]:::removed",
                "  class C_a__b_2[\"a__b\"]:::added",
            ],
            classLines);
    }

    [Fact]
    public void A_changed_type_without_changed_members_is_a_single_class_line_without_a_body()
    {
        var text = Render(Diff(Type("shop::Widget", basesAdded: ["Gadget"])));

        Assert.Contains("  class C_shop__Widget[\"shop::Widget\"]:::modified\n", text);
        Assert.DoesNotContain("{", text);
        Assert.DoesNotContain("}", text);
    }

    [Theory]
    [InlineData("public", "+")]
    [InlineData("protected", "#")]
    [InlineData("private", "-")]
    [InlineData("none", "+")]
    public void Member_visibility_is_drawn_from_the_access(string access, string expected)
    {
        var text = Render(Diff(Type("A", members: [Member("f()", "added", "void f()", accessAfter: access)])));

        Assert.Contains($"    {expected}void f() [added]\n", text);
    }

    [Fact]
    public void A_removed_member_shows_its_before_signature_and_access_and_a_modified_one_its_after_signature()
    {
        var text = Render(Diff(Type(
            "A",
            members:
            [
                Member("a()", "removed", before: "int a()", accessAfter: null, accessBefore: "private"),
                Member("b()", "modified", before: "int b()", after: "long b()", accessBefore: "private", accessAfter: "protected"),
            ])));

        Assert.Contains("    -int a() [removed]\n", text);
        Assert.Contains("    #long b() [modified]\n", text);
        Assert.DoesNotContain("int b()", text);
    }

    [Fact]
    public void An_empty_signature_falls_back_to_the_member_name()
    {
        var text = Render(Diff(Type("A", members: [Member("count_", "added", after: "", name: "count_")])));

        Assert.Contains("    +count_ [added]\n", text);
    }

    [Theory]
    [InlineData("operator<<", "operator<<")]
    [InlineData("std::map<int, std::vector<char*>> m", "std::map<int, std::vector<char*>> m")]
    [InlineData("T&& take(T&& value)", "T&& take(T&& value)")]
    [InlineData("~Foo()", "∼Foo()")]
    [InlineData("int x{5}", "int x❴5❵")]
    [InlineData("void (*callback)(int, char*)", "void (*callback)(int, char*)")]
    [InlineData("const char* s = \"a\" `tick`", "const char* s = ″a″ `tick`")]
    [InlineData("café 中文 name", "café 中文 name")]
    [InlineData("a\r\nb\tc\u0007d\u007Fe", "a  b c d e")]
    [InlineData("char* ptr*", "char* ptr*")]
    public void Member_text_is_escaped_by_the_table_and_other_C_plus_plus_text_is_left_alone(string signature, string expected)
    {
        var text = Render(Diff(Type("A", members: [Member("k", "added", signature)])));

        Assert.Contains($"    +{expected} [added]\n", text);
    }

    [Fact]
    public void A_member_text_ending_in_a_classifier_character_is_still_followed_by_the_state_tag()
    {
        var text = Render(Diff(Type("A", members: [Member("p", "added", "static int* p*"), Member("q", "added", "static int q$")])));

        Assert.Contains("    +static int* p* [added]\n", text);
        Assert.Contains("    +static int q$ [added]\n", text);
    }

    [Fact]
    public void Repository_text_in_a_member_or_a_type_name_cannot_add_a_class_or_close_a_block()
    {
        const string injection = "a() } class Evil:::added {  +b()";
        var text = Render(Diff(
            Type("shop::A", members: [Member("k", "added", injection)]),
            Type("shop::B\"]:::removed\n  class Evil", "added")));

        var classLines = Lines(text).Count(l => l.StartsWith("  class ", StringComparison.Ordinal));
        Assert.Equal(2, classLines);
        Assert.DoesNotContain("\n  class Evil", text);
        Assert.Equal(1, text.Count(c => c == '{'));
        Assert.Equal(1, text.Count(c => c == '}'));
    }

    [Theory]
    [InlineData("Base", "shop::Base", "shop::Base")]
    [InlineData("shop::Base", "shop::Base", "shop::Base")]
    [InlineData("::shop::Base", "shop::Base", "shop::Base")]
    [InlineData("Base<int, std::vector<char>>", "shop::Base", "shop::Base")]
    [InlineData("shop::Base<Base<int>>", "shop::Base", "shop::Base")]
    [InlineData("  Base  ", "shop::Base", "shop::Base")]
    public void A_base_is_resolved_by_exact_fqn_or_by_a_unique_displayed_suffix(string written, string displayedFqn, string expectedFqn)
    {
        var text = Render(Diff(
            Type(displayedFqn, "added"),
            Type("shop::Derived", "added", basesAdded: [written])));

        var expected = $"  C_{expectedFqn.Replace("::", "__", StringComparison.Ordinal)} <|-- C_shop__Derived\n";
        Assert.Contains(expected, text);
    }

    [Fact]
    public void An_unresolved_ambiguous_or_self_base_makes_no_edge_and_no_node()
    {
        var text = Render(Diff(
            Type("a::Base", "added"),
            Type("b::Base", "added"),
            Type("shop::Derived", "added", basesAdded: ["Base", "std::exception", "Missing", "Derived"])));

        Assert.DoesNotContain("<|--", text);
        Assert.DoesNotContain("exception", text);
        Assert.DoesNotContain("Missing", text);
        Assert.Equal(3, Lines(text).Count(l => l.StartsWith("  class ", StringComparison.Ordinal)));
    }

    [Fact]
    public void An_exact_fqn_wins_over_a_suffix_match_and_a_pair_is_drawn_once()
    {
        var text = Render(Diff(
            Type("Base", "added"),
            Type("shop::Base", "added"),
            Type("shop::Derived", "modified", basesAdded: ["Base", "Base<int>"], basesRemoved: ["Base"])));

        var edges = Lines(text).Where(l => l.Contains("<|--", StringComparison.Ordinal)).ToArray();
        Assert.Equal(["  C_Base <|-- C_shop__Derived"], edges);
    }

    [Fact]
    public void A_diff_with_no_changed_types_is_classDiagram_followed_by_the_three_classDef_lines_only()
    {
        var text = Render(Diff());

        Assert.Equal("classDiagram\n" + ClassDefs, text);
    }

    [Fact]
    public void The_lint_rejects_hand_made_broken_input_and_names_the_problem()
    {
        const string defs = ClassDefs;
        var broken = new Dictionary<string, string>
        {
            ["missing header"] = "graph TD\n" + defs,
            ["unbalanced braces"] = "classDiagram\n  class C_A[\"A\"]:::added {\n    +f() [added]\n" + defs,
            ["no state"] = "classDiagram\n  class C_A[\"A\"]\n" + defs,
            ["state without classDef"] = "classDiagram\n  class C_A[\"A\"]:::removed\n" + ClassDefAdded,
            ["member without a tag"] = "classDiagram\n  class C_A[\"A\"]:::added {\n    +f()\n  }\n" + defs,
            ["brace inside a member"] = "classDiagram\n  class C_A[\"A\"]:::added {\n    +f() { [added]\n  }\n" + defs,
            ["bare classDiagram"] = "classDiagram\n",
            ["edge to an undeclared class"] = "classDiagram\n  class C_A[\"A\"]:::added\n  C_A <|-- C_B\n" + defs,
            ["carriage return"] = "classDiagram\r\n" + defs,
            ["bare keyword class id"] = "classDiagram\n  class link[\"link\"]:::added\n" + defs,
            ["unprefixed edge endpoint"] =
                "classDiagram\n  class C_A[\"A\"]:::added\n  class C_B[\"B\"]:::added\n  A <|-- C_B\n" + defs,
        };

        foreach (var (name, text) in broken)
        {
            Assert.True(MermaidLint.Check(text).Count > 0, "the lint accepted: " + name);
        }

        Assert.Empty(MermaidLint.Check("classDiagram\n  class C_A[\"A\"]:::added {\n    +f() [added]\n  }\n" + defs));
    }
}
