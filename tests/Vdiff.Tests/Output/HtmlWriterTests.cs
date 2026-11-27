using System.Text.RegularExpressions;
using Vdiff.Core.Model;
using Vdiff.Core.Output;

namespace Vdiff.Tests.Output;

/// <summary>
/// The report page (D-08, T-01-06): self-contained and encoded. No browser is needed; the
/// "opens offline" claim is covered by the assertions that nothing external is referenced.
/// </summary>
public class HtmlWriterTests
{
    const string Hostile1 = "</title><b>x";
    const string Hostile2 = "a<b>&\"c'";

    static DiffFile Diff(
        IReadOnlyList<TypeDiff> types,
        IReadOnlyList<string>? warnings = null,
        DiffSummary? summary = null) =>
        new(
            DiffFile.SchemaName,
            summary ?? new DiffSummary(new TypeCounts(0, 0, 0, 0, 0), new MemberCounts(0, 0, 0, 0, 0), new EdgeCounts(0, 0, 0, 0, 0), new UnmodelledCounts(0, 0)),
            warnings ?? [],
            [],
            types,
            []);

    static TypeDiff Type(string fqn, string state, params MemberDiff[] members) =>
        new(Languages.Cpp, "class " + fqn, fqn, "class", state, "include/a.h", "include/a.h", null, null, new BaseChanges([], []), members);

    static MemberDiff Member(string key, string state, string? before, string? after, string? accessBefore = null, string? accessAfter = null) =>
        new(key, "m", state, accessBefore, accessAfter, before, after, null, null, null);

    static readonly RunMeta DefaultMeta = Meta("1111111111111111111111111111111111111111", "2222222222222222222222222222222222222222");

    static string Page(DiffFile diff) => HtmlWriter.Render(diff, MermaidWriter.Render(diff), DefaultMeta);

    static RunMeta Meta(string baseCommit, string headCommit) =>
        new(
            RunMeta.SchemaName,
            "0.0.0",
            baseCommit,
            headCommit,
            [new ExtractorRun("cpp-ts", [new ToolVersion("tree-sitter", "0.27.0"), new ToolVersion("tree-sitter-cpp", "0.23.4")])]);

    static DiffFile Typical() => Diff(
        [
            Type(
                "shop::Gadget",
                "modified",
                Member("size() const", "modified", "int size() const", "long size() const", "public", "public"),
                Member("grow(int)", "added", null, "void grow(int by)", null, "public")),
            Type("shop::Legacy", "removed", Member("id", "removed", "int id", null, "public", null)),
        ],
        ["head: 1 file(s) not modelled"]);

    [Fact]
    public void The_page_begins_with_the_doctype_has_a_language_one_charset_and_one_style_block()
    {
        var html = Page(Typical());

        Assert.StartsWith("<!DOCTYPE html>\n", html);
        Assert.Matches("^<!DOCTYPE html>\n<html lang=\"[a-z]+\">", html);
        Assert.Single(Regex.Matches(html, "<meta charset=\"utf-8\">"));
        Assert.Single(Regex.Matches(html, "<meta "));
        Assert.Single(Regex.Matches(html, "<style>"));
        Assert.Single(Regex.Matches(html, "</style>"));
    }

    [Fact]
    public void The_page_loads_nothing_external_and_runs_no_script()
    {
        var html = Page(Typical());

        Assert.DoesNotMatch("(?i)<script", html);
        Assert.DoesNotMatch("(?i)https?:", html);
        Assert.DoesNotContain("http", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("(?i)\\bsrc\\s*=", html);
        Assert.DoesNotMatch("(?i)\\bhref\\s*=", html);
        Assert.DoesNotMatch("(?i)<(link|iframe|img|object|embed|base)\\b", html);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("url(", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("(?i)\\bon[a-z]+\\s*=", html);
    }

    [Fact]
    public void Hostile_values_in_a_type_name_a_member_signature_and_a_warning_are_encoded_never_live_markup()
    {
        var diff = Diff(
            [Type(Hostile1, "added", Member(Hostile2, "added", null, Hostile2, null, "public"))],
            [Hostile1, Hostile2]);

        var html = Page(diff);

        Assert.Contains("&lt;/title&gt;&lt;b&gt;x", html);
        Assert.Contains("a&lt;b&gt;&amp;&quot;c&#39;", html);
        Assert.DoesNotContain("<b>x", html);
        Assert.DoesNotContain("<b>&", html);
        Assert.DoesNotContain("a<b>", html);
        Assert.Single(Regex.Matches(html, "</title>"));
        Assert.DoesNotMatch("<b[ >/]", html);
    }

    [Fact]
    public void Hostile_values_cannot_break_out_of_an_attribute_or_smuggle_a_url_into_a_tag()
    {
        var diff = Diff(
            [new TypeDiff(Languages.Cpp, "class x", "x", "class", "added\" onclick=\"alert(1)", null, null, null, null, new BaseChanges([], []), [])],
            ["http://example.invalid/x"]);

        var html = Page(diff);

        Assert.DoesNotContain("\" onclick=", html);
        Assert.Contains("added&quot; onclick=&quot;alert(1)", html);
        Assert.DoesNotMatch("<[^>]*https?:", html);
        Assert.DoesNotMatch("<[^>]* onclick=\"", html);
    }

    [Fact]
    public void The_commit_ids_come_from_the_run_meta()
    {
        var diff = Typical();
        var meta = Meta("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var html = HtmlWriter.Render(diff, MermaidWriter.Render(diff), meta);

        Assert.Contains("<p>Base <code>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</code>, head <code>bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb</code></p>", html);
        Assert.DoesNotContain(DefaultMeta.BaseCommit, html);
        Assert.DoesNotContain(DefaultMeta.HeadCommit, html);
    }

    [Fact]
    public void Two_run_metas_with_different_ids_give_pages_that_differ_only_in_the_commit_text()
    {
        var diff = Typical();
        var mermaid = MermaidWriter.Render(diff);
        var first = Meta("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var second = Meta("cccccccccccccccccccccccccccccccccccccccc", "dddddddddddddddddddddddddddddddddddddddd");

        var pageOne = HtmlWriter.Render(diff, mermaid, first);
        var pageTwo = HtmlWriter.Render(diff, mermaid, second);

        Assert.NotEqual(pageOne, pageTwo);
        Assert.Equal(
            pageOne.Replace(first.BaseCommit, "BASE").Replace(first.HeadCommit, "HEAD"),
            pageTwo.Replace(second.BaseCommit, "BASE").Replace(second.HeadCommit, "HEAD"));
    }

    [Fact]
    public void The_mermaid_text_appears_encoded_inside_one_pre_block()
    {
        var diff = Typical();
        var mermaid = MermaidWriter.Render(diff);

        var html = HtmlWriter.Render(diff, mermaid, DefaultMeta);

        var pre = Regex.Match(html, "<pre>(.*?)</pre>", RegexOptions.Singleline);
        Assert.True(pre.Success);
        Assert.Single(Regex.Matches(html, "<pre>"));
        Assert.DoesNotContain('<', pre.Groups[1].Value);
        Assert.StartsWith("classDiagram\n", System.Net.WebUtility.HtmlDecode(pre.Groups[1].Value));
        Assert.Equal(mermaid, System.Net.WebUtility.HtmlDecode(pre.Groups[1].Value));
    }

    [Fact]
    public void The_summary_counts_and_the_warnings_are_shown()
    {
        var summary = new DiffSummary(new TypeCounts(1, 2, 3, 0, 4), new MemberCounts(5, 6, 7, 0, 0), new EdgeCounts(0, 0, 0, 0, 0), new UnmodelledCounts(8, 9));
        var html = Page(Diff([], ["base: 2 file(s) not modelled", "head: duplicate"], summary));

        Assert.Contains(
            "Types: 1 added, 2 removed, 3 modified, 4 unchanged. Members: 5 added, 6 removed, 7 modified. " +
            "Not modelled: 8 on the base side, 9 on the head side.",
            html);
        Assert.Contains("<li>base: 2 file(s) not modelled</li>", html);
        Assert.Contains("<li>head: duplicate</li>", html);
        Assert.Contains("1111111111111111111111111111111111111111", html);
    }

    [Fact]
    public void There_is_no_warnings_section_when_there_are_no_warnings()
    {
        var html = Page(Diff([Type("A", "added")]));

        Assert.DoesNotContain("Warnings", html);
        Assert.DoesNotContain("<ul>", html);
    }

    [Fact]
    public void One_table_row_per_changed_type_and_per_changed_member_in_diff_json_order_with_the_state_text()
    {
        var diff = Diff(
        [
            Type("zeta::Z", "added", Member("b()", "added", null, "void b()", null, "public"), Member("a()", "added", null, "void a()", null, "public")),
            Type("alpha::A", "removed", Member("c()", "removed", "void c()", null, "public", null)),
        ]);

        var html = Page(diff);

        var rows = Regex.Matches(html, "<tr(?: class=\"type\")?><td>([^<]*)</td><td class=\"([a-z]+)\">([a-z]+)</td>")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value))
            .ToArray();
        Assert.Equal(
            [
                ("class zeta::Z", "added", "added"),
                ("b()", "added", "added"),
                ("a()", "added", "added"),
                ("class alpha::A", "removed", "removed"),
                ("c()", "removed", "removed"),
            ],
            rows);
    }

    [Fact]
    public void A_member_whose_signature_did_not_change_is_not_shown_as_an_arrow_from_itself()
    {
        var diff = Diff([Type("A", "modified", Member("count_", "modified", "int count_", "int count_", "private", "protected"))]);

        var html = Page(diff);

        Assert.DoesNotContain("int count_ -&gt; int count_", html);
        Assert.Contains("int count_; private -&gt; protected", html);
    }

    [Fact]
    public void A_changed_signature_is_shown_as_before_arrow_after()
    {
        var diff = Diff([Type("A", "modified", Member("size() const", "modified", "int size() const", "long size() const", "public", "public"))]);

        var html = Page(diff);

        Assert.Contains("int size() const -&gt; long size() const", html);
    }

    [Fact]
    public void The_page_uses_line_feeds_only_and_ends_with_a_line_feed()
    {
        var html = Page(Typical());

        Assert.DoesNotContain('\r', html);
        Assert.EndsWith("</html>\n", html);
    }
}
