//! Deadline-bounded parsing, error-node counting and (task 2) type extraction.

use crate::protocol::{MemberInfo, TypeInfo};
use std::ops::ControlFlow;
use std::time::{Duration, Instant};
use tree_sitter::{Node, ParseOptions, Parser, Tree};

/// Outcome of parsing one file (D-11): a timed-out or failed file is labelled, never silently empty.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Status {
    Ok,
    Timeout,
    Error,
}

impl Status {
    pub fn as_str(self) -> &'static str {
        match self {
            Status::Ok => "ok",
            Status::Timeout => "timeout",
            Status::Error => "error",
        }
    }
}

/// Parses `bytes` with a wall-clock deadline of `budget` (T-01-32).
///
/// tree-sitter 0.27.0 has no timeout setter, only `parse_with_options` with a progress callback;
/// the callback breaks once the deadline has passed. A cancelled parse leaves the parser holding
/// the abandoned parse, which it would resume on its next use, so the parser is reset here before
/// it is returned to the caller. The parse result is never unwrapped.
pub fn parse_bounded(parser: &mut Parser, bytes: &[u8], budget: Duration) -> (Status, Option<Tree>) {
    let deadline = Instant::now() + budget;
    let mut deadline_passed = false;
    let mut on_progress = |_: &tree_sitter::ParseState| {
        if Instant::now() >= deadline {
            deadline_passed = true;
            ControlFlow::Break(())
        } else {
            ControlFlow::Continue(())
        }
    };
    let tree = parser.parse_with_options(
        &mut |offset, _| if offset < bytes.len() { &bytes[offset..] } else { &[][..] },
        None,
        Some(ParseOptions::new().progress_callback(&mut on_progress)),
    );
    match tree {
        Some(tree) => (Status::Ok, Some(tree)),
        None => {
            parser.reset();
            let status = if deadline_passed { Status::Timeout } else { Status::Error };
            (status, None)
        }
    }
}

/// Counts ERROR and MISSING nodes, anonymous missing tokens included, with an iterative cursor
/// walk (no recursion, so a deeply nested file cannot overflow the stack). A subtree whose root
/// reports no error cannot contain one, so it is skipped.
pub fn count_error_nodes(root: Node) -> usize {
    let mut cursor = root.walk();
    let mut count = 0;
    loop {
        let node = cursor.node();
        if node.is_error() || node.is_missing() {
            count += 1;
        }
        if node.has_error() && cursor.goto_first_child() {
            continue;
        }
        loop {
            if cursor.goto_next_sibling() {
                break;
            }
            if !cursor.goto_parent() {
                return count;
            }
        }
    }
}

const MAX_SIGNATURE_BYTES: usize = 4096;

/// Node kinds that can name a declarator directly.
const NAME_KINDS: &[&str] = &[
    "identifier",
    "field_identifier",
    "type_identifier",
    "qualified_identifier",
    "destructor_name",
    "operator_name",
    "template_function",
    "template_method",
];

/// Preprocessor conditionals: their children are ordinary declarations, one level down.
const PREPROC_KINDS: &[&str] = &["preproc_if", "preproc_ifdef", "preproc_else", "preproc_elif", "preproc_elifdef"];

/// Text of `src[start..end]`, with invalid UTF-8 replaced by U+FFFD, never dropped.
fn lossy(src: &[u8], start: usize, end: usize) -> String {
    String::from_utf8_lossy(&src[start..end]).into_owned()
}

fn text(node: Node, src: &[u8]) -> String {
    lossy(src, node.start_byte(), node.end_byte())
}

/// Collapses every run of whitespace to one space and trims both ends.
fn collapse(s: &str) -> String {
    s.split_whitespace().collect::<Vec<_>>().join(" ")
}

/// Cuts `s` to at most `max` bytes without splitting a character.
fn cut_on_char_boundary(mut s: String, max: usize) -> String {
    if s.len() > max {
        let mut end = max;
        while !s.is_char_boundary(end) {
            end -= 1;
        }
        s.truncate(end);
    }
    s
}

/// Extracts classes and structs, with their members, from a parsed C++ file (tracer subset).
///
/// Types are reached by a whitelist descent over declaration containers, never by walking every
/// node, so a function body is not entered and a local class cannot appear with a wrong scope.
/// Depth handling for pathological nesting belongs to plan 01-06.
pub fn extract_types(root: Node, src: &[u8]) -> Vec<TypeInfo> {
    let mut out = Vec::new();
    let mut scope = Vec::new();
    collect(root, src, &mut scope, &mut out);
    out
}

fn collect(container: Node, src: &[u8], scope: &mut Vec<String>, out: &mut Vec<TypeInfo>) {
    let mut cursor = container.walk();
    for child in container.children(&mut cursor) {
        match child.kind() {
            "class_specifier" | "struct_specifier" => collect_type(child, src, scope, out),
            "namespace_definition" => {
                if let Some(body) = child.child_by_field_name("body") {
                    let name = child
                        .child_by_field_name("name")
                        .map(|n| collapse(&text(n, src)))
                        .unwrap_or_else(|| "(anonymous)".to_string());
                    scope.push(name);
                    collect(body, src, scope, out);
                    scope.pop();
                }
            }
            "declaration_list" | "field_declaration_list" | "template_declaration" | "linkage_specification"
            | "declaration" | "type_definition" | "field_declaration" | "ERROR" => collect(child, src, scope, out),
            kind if PREPROC_KINDS.contains(&kind) => collect(child, src, scope, out),
            _ => {}
        }
    }
}

fn collect_type(spec: Node, src: &[u8], scope: &mut Vec<String>, out: &mut Vec<TypeInfo>) {
    // A forward declaration has no body and an unnamed struct has no name: neither is a type.
    let (Some(name_node), Some(body)) = (spec.child_by_field_name("name"), spec.child_by_field_name("body")) else {
        return;
    };
    let is_class = spec.kind() == "class_specifier";
    let name = collapse(&text(name_node, src));
    out.push(TypeInfo {
        kind: if is_class { "class" } else { "struct" },
        name: name.clone(),
        scope: scope.join("::"),
        bases: base_names(spec, src),
        start_byte: spec.start_byte(),
        end_byte: spec.end_byte(),
        start_line: spec.start_position().row + 1,
        end_line: spec.end_position().row + 1,
        members: members_of(body, src, if is_class { "private" } else { "public" }),
    });
    // Nested types follow their parent, with the parent's name added to their scope.
    scope.push(name);
    collect(body, src, scope, out);
    scope.pop();
}

fn base_names(spec: Node, src: &[u8]) -> Vec<String> {
    let mut bases = Vec::new();
    let mut cursor = spec.walk();
    for clause in spec.children(&mut cursor) {
        if clause.kind() != "base_class_clause" {
            continue;
        }
        let mut inner = clause.walk();
        for base in clause.named_children(&mut inner) {
            if !matches!(base.kind(), "access_specifier" | "virtual" | "comment") {
                bases.push(collapse(&text(base, src)));
            }
        }
    }
    bases
}

fn members_of(body: Node, src: &[u8], default_access: &'static str) -> Vec<MemberInfo> {
    let mut out = Vec::new();
    let mut access = default_access;
    member_items(body, src, &mut access, &mut out);
    out
}

fn member_items(container: Node, src: &[u8], access: &mut &'static str, out: &mut Vec<MemberInfo>) {
    let mut cursor = container.walk();
    for child in container.children(&mut cursor) {
        match child.kind() {
            "access_specifier" => {
                *access = match text(child, src).trim() {
                    "public" => "public",
                    "protected" => "protected",
                    "private" => "private",
                    _ => *access,
                };
            }
            "field_declaration" | "declaration" | "function_definition" => {
                declaration_members(child, src, *access, out)
            }
            kind if PREPROC_KINDS.contains(&kind) => member_items(child, src, access, out),
            _ => {}
        }
    }
}

/// One member per declarator of a field, declaration or function definition. A declarator shape
/// this plan does not handle (a conversion operator, say) yields no member rather than a wrong one.
fn declaration_members(decl: Node, src: &[u8], access: &'static str, out: &mut Vec<MemberInfo>) {
    let mut cursor = decl.walk();
    let declarators: Vec<Node> = decl.children_by_field_name("declarator", &mut cursor).collect();
    if declarators.is_empty() {
        return;
    }

    // The signature stops where a body or a constructor initializer list starts, so `= 0`,
    // `= default`, `= delete` and field default values stay in.
    let mut end = decl.end_byte();
    let mut children = decl.walk();
    for child in decl.children(&mut children) {
        if matches!(child.kind(), "compound_statement" | "field_initializer_list" | "function_try_block") {
            end = end.min(child.start_byte());
        }
    }
    let whole = lossy(src, decl.start_byte(), end);
    let whole = whole.trim_end();
    let whole = collapse(whole.strip_suffix(';').unwrap_or(whole));

    // With several declarators (`int n_, m_;`) each member is the shared specifiers plus its own
    // declarator, so `n_` and `m_` do not both carry the whole declaration as their signature.
    let shared_prefix = collapse(&lossy(src, decl.start_byte(), declarators[0].start_byte()));

    for declarator in &declarators {
        let Some(resolved) = resolve(*declarator, src) else { continue };
        let signature = if declarators.len() == 1 {
            whole.clone()
        } else {
            let mut segment_end = decl.end_byte();
            let mut next = declarator.next_sibling();
            while let Some(sibling) = next {
                if matches!(sibling.kind(), "," | ";") {
                    segment_end = sibling.start_byte();
                    break;
                }
                next = sibling.next_sibling();
            }
            let own = collapse(&lossy(src, declarator.start_byte(), segment_end));
            format!("{shared_prefix} {own}")
        };
        out.push(MemberInfo {
            name: resolved.name,
            kind: resolved.kind,
            access,
            key: resolved.key,
            signature: cut_on_char_boundary(signature, MAX_SIGNATURE_BYTES),
            start_byte: decl.start_byte(),
            end_byte: decl.end_byte(),
            start_line: decl.start_position().row + 1,
            end_line: decl.end_position().row + 1,
        });
    }
}

struct Resolved {
    name: String,
    key: String,
    kind: &'static str,
}

/// The declarator wrapped by a pointer, array, reference, parenthesised or initialised declarator.
/// A `reference_declarator` has no `declarator` field, so fall back to its first named child.
fn inner_declarator(node: Node) -> Option<Node> {
    if let Some(inner) = node.child_by_field_name("declarator") {
        return Some(inner);
    }
    let mut cursor = node.walk();
    let found = node
        .named_children(&mut cursor)
        .find(|c| !matches!(c.kind(), "comment" | "type_qualifier"));
    found
}

/// Follows declarators down to the member's name. For `destructor_name` and `operator_name` the
/// name is that node's own text (`~Gadget`, `operator==`), not its innermost leaf.
fn resolve(declarator: Node, src: &[u8]) -> Option<Resolved> {
    let mut node = declarator;
    loop {
        let kind = node.kind();
        if NAME_KINDS.contains(&kind) {
            let name = collapse(&text(node, src));
            return Some(Resolved { key: name.clone(), name, kind: "field" });
        }
        match kind {
            "function_declarator" => {
                let inner = node.child_by_field_name("declarator")?;
                if !NAME_KINDS.contains(&inner.kind()) {
                    // `int (*cb)(int);`: the name is whatever the wrapped declarator resolves to.
                    return resolve(inner, src);
                }
                let name = collapse(&text(inner, src));
                let parameters = node
                    .child_by_field_name("parameters")
                    .map(|p| collapse(&text(p, src)))
                    .unwrap_or_default();
                let mut key = format!("{name}{parameters}");
                let mut cursor = node.walk();
                for qualifier in node.children(&mut cursor).filter(|c| c.kind() == "type_qualifier") {
                    key.push(' ');
                    key.push_str(&collapse(&text(qualifier, src)));
                }
                return Some(Resolved { name, key, kind: "method" });
            }
            "init_declarator" | "pointer_declarator" | "array_declarator" | "attributed_declarator"
            | "parenthesized_declarator" | "reference_declarator" => {
                node = inner_declarator(node)?;
            }
            _ => return None,
        }
    }
}

#[cfg(test)]
pub(crate) fn cpp_parser() -> Parser {
    let mut parser = Parser::new();
    let language: tree_sitter::Language = tree_sitter_cpp::LANGUAGE.into();
    parser.set_language(&language).expect("the pinned grammar loads");
    parser
}

#[cfg(test)]
mod deadline {
    use super::*;

    const SMALL: &str = "class A { int x; };\n";

    fn big_input() -> Vec<u8> {
        SMALL.repeat(200_000).into_bytes()
    }

    #[test]
    fn a_parse_past_its_deadline_is_labelled_timeout_with_no_tree() {
        let mut parser = cpp_parser();
        let (status, tree) = parse_bounded(&mut parser, &big_input(), Duration::from_millis(1));
        assert_eq!(status, Status::Timeout);
        assert!(tree.is_none());
        assert_eq!(status.as_str(), "timeout");
    }

    #[test]
    fn the_parser_is_reset_after_a_cancel_so_the_next_file_parses_correctly() {
        let mut parser = cpp_parser();
        let (status, _) = parse_bounded(&mut parser, &big_input(), Duration::from_millis(1));
        assert_eq!(status, Status::Timeout);

        // Same parser, same thread, a small unrelated file: without the reset it would resume the
        // abandoned parse of the large one.
        let source = b"struct Small { int y; };\n";
        let (status, tree) = parse_bounded(&mut parser, source, Duration::from_secs(10));
        assert_eq!(status, Status::Ok);
        let tree = tree.expect("a tree");
        assert_eq!(tree.root_node().end_byte(), source.len());
        assert_eq!(count_error_nodes(tree.root_node()), 0);
        let sexp = tree.root_node().to_sexp();
        assert_eq!(sexp.matches("struct_specifier").count(), 1, "{sexp}");
        assert_eq!(sexp.matches("class_specifier").count(), 0, "{sexp}");
        let types = extract_types(tree.root_node(), source);
        assert_eq!(types.len(), 1, "the small file's type list is returned: {types:?}");
        assert_eq!(types[0].name, "Small");
    }

    #[test]
    fn a_generous_budget_returns_ok() {
        let mut parser = cpp_parser();
        let (status, tree) = parse_bounded(&mut parser, SMALL.as_bytes(), Duration::from_secs(10));
        assert_eq!(status, Status::Ok);
        assert!(tree.is_some());
        assert_eq!(status.as_str(), "ok");
    }

    #[test]
    fn an_empty_input_parses_ok() {
        let mut parser = cpp_parser();
        let (status, tree) = parse_bounded(&mut parser, b"", Duration::from_secs(10));
        assert_eq!(status, Status::Ok);
        assert_eq!(count_error_nodes(tree.expect("a tree").root_node()), 0);
    }

    #[test]
    fn a_parser_without_a_language_is_an_error_not_a_timeout() {
        let mut parser = Parser::new();
        let (status, tree) = parse_bounded(&mut parser, SMALL.as_bytes(), Duration::from_secs(10));
        assert_eq!(status, Status::Error);
        assert!(tree.is_none());
        assert_eq!(status.as_str(), "error");
    }
}

#[cfg(test)]
mod error_nodes {
    use super::*;

    fn count(source: &str) -> usize {
        let mut parser = cpp_parser();
        let (_, tree) = parse_bounded(&mut parser, source.as_bytes(), Duration::from_secs(10));
        count_error_nodes(tree.expect("a tree").root_node())
    }

    #[test]
    fn clean_source_has_no_error_nodes() {
        assert_eq!(count("class A { int x; };\n"), 0);
    }

    #[test]
    fn a_missing_semicolon_counts_as_a_missing_node() {
        assert!(count("class A { int x }\n") >= 1);
    }

    #[test]
    fn a_syntax_error_counts_error_nodes() {
        assert!(count("class A { int x; };\n@@@ ### $$$\nclass B { int y; };\n") >= 1);
    }

    #[test]
    fn a_deeply_nested_file_is_counted_without_recursion() {
        let depth = 20_000;
        let source = format!("{}{}\n", "namespace n { ".repeat(depth), "} ".repeat(depth));
        assert_eq!(count(&source), 0);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::protocol::MemberInfo;

    fn extract_bytes(source: &[u8]) -> Vec<TypeInfo> {
        let mut parser = cpp_parser();
        let (status, tree) = parse_bounded(&mut parser, source, Duration::from_secs(10));
        assert_eq!(status, Status::Ok);
        extract_types(tree.expect("a tree").root_node(), source)
    }

    fn extract(source: &str) -> Vec<TypeInfo> {
        extract_bytes(source.as_bytes())
    }

    /// "<kind> <scope::name>" for each type, in the order returned.
    fn ids(types: &[TypeInfo]) -> Vec<String> {
        types
            .iter()
            .map(|t| {
                if t.scope.is_empty() {
                    format!("{} {}", t.kind, t.name)
                } else {
                    format!("{} {}::{}", t.kind, t.scope, t.name)
                }
            })
            .collect()
    }

    /// "<access> <kind> <key>" for each member, in source order.
    fn members(t: &TypeInfo) -> Vec<String> {
        t.members.iter().map(|m| format!("{} {} {}", m.access, m.kind, m.key)).collect()
    }

    fn member<'a>(t: &'a TypeInfo, key: &str) -> &'a MemberInfo {
        t.members
            .iter()
            .find(|m| m.key == key)
            .unwrap_or_else(|| panic!("no member with key {key:?} in {:?}", members(t)))
    }

    fn the_only_type(source: &str) -> TypeInfo {
        let mut types = extract(source);
        assert_eq!(types.len(), 1, "expected one type in {source:?}, got {:?}", ids(&types));
        types.remove(0)
    }

    #[test]
    fn class_and_struct_members_get_their_default_and_explicit_access() {
        let types = extract(
            "class C { int a; public: int b; };\n\
             struct S { int c; private: int d; protected: void m(); };\n",
        );
        assert_eq!(ids(&types), ["class C", "struct S"]);
        assert_eq!(members(&types[0]), ["private field a", "public field b"]);
        assert_eq!(
            members(&types[1]),
            ["public field c", "private field d", "protected method m()"]
        );
        assert!(types[0].bases.is_empty());
        assert_eq!(types[0].scope, "");
    }

    #[test]
    fn spans_are_utf8_byte_offsets_and_one_based_lines() {
        let source = "// caf\u{e9} \u{4e2d}\u{6587}\n\nclass A {\n  int x;\n};\n";
        let t = the_only_type(source);
        assert_eq!(t.start_byte, source.find("class A").unwrap());
        assert_eq!(t.end_byte, source.find("};").unwrap() + 1);
        assert_eq!((t.start_line, t.end_line), (3, 5));
        let x = member(&t, "x");
        assert_eq!(x.start_byte, source.find("int x;").unwrap());
        assert_eq!(x.end_byte, x.start_byte + "int x;".len());
        assert_eq!((x.start_line, x.end_line), (4, 4));
    }

    #[test]
    fn spans_stay_exact_after_a_byte_order_mark() {
        let mut source = vec![0xEF, 0xBB, 0xBF];
        source.extend_from_slice(b"class A { int x; };\n");
        let types = extract_bytes(&source);
        assert_eq!(ids(&types), ["class A"]);
        assert_eq!(types[0].start_byte, 3);
        assert_eq!(types[0].start_line, 1);
        assert_eq!(member(&types[0], "x").start_byte, 3 + "class A { ".len());
    }

    #[test]
    fn namespaces_form_the_scope_and_an_anonymous_namespace_is_named() {
        let types = extract(
            "namespace shop::billing { class Invoice {}; }\n\
             namespace a { namespace b { struct Deep {}; } }\n\
             namespace { class Hidden {}; }\n\
             inline namespace v1 { class V {}; }\n",
        );
        assert_eq!(
            ids(&types),
            [
                "class shop::billing::Invoice",
                "struct a::b::Deep",
                "class (anonymous)::Hidden",
                "class v1::V"
            ]
        );
    }

    #[test]
    fn bases_drop_access_and_virtual_and_collapse_whitespace() {
        let t = the_only_type("class D final : public virtual C,\n    protected   N::E<int>, B {};\n");
        assert_eq!(t.bases, ["C", "N::E<int>", "B"]);
    }

    #[test]
    fn nested_types_carry_the_enclosing_type_in_scope_and_follow_their_parent() {
        let types = extract(
            "namespace n { class Outer { public: struct Inner { int v; }; int w; }; }\n\
             class After {};\n",
        );
        assert_eq!(ids(&types), ["class n::Outer", "struct n::Outer::Inner", "class After"]);
        assert_eq!(members(&types[0]), ["public field w"]);
        assert_eq!(members(&types[1]), ["public field v"]);
    }

    #[test]
    fn destructors_and_operators_take_the_names_of_their_own_nodes() {
        let t = the_only_type(
            "class Gadget { public: virtual ~Gadget() = default;\n\
             bool operator==(const Gadget& o) const;\n\
             int operator()(int a); };\n",
        );
        let d = member(&t, "~Gadget()");
        assert_eq!((d.name.as_str(), d.kind), ("~Gadget", "method"));
        assert_eq!(d.signature, "virtual ~Gadget() = default");
        let eq = member(&t, "operator==(const Gadget& o) const");
        assert_eq!((eq.name.as_str(), eq.kind), ("operator==", "method"));
        assert_eq!(eq.signature, "bool operator==(const Gadget& o) const");
        let call = member(&t, "operator()(int a)");
        assert_eq!((call.name.as_str(), call.kind), ("operator()", "method"));
    }

    #[test]
    fn reference_returning_and_pointer_returning_methods_are_methods() {
        let t = the_only_type(
            "class F { Foo& ref(); const Foo& cref() const; Foo&& mv(); int* p(); };\n",
        );
        assert_eq!(
            members(&t),
            [
                "private method ref()",
                "private method cref() const",
                "private method mv()",
                "private method p()"
            ]
        );
        assert_eq!(member(&t, "ref()").name, "ref");
        assert_eq!(member(&t, "ref()").signature, "Foo& ref()");
    }

    #[test]
    fn a_function_pointer_member_is_a_field_and_arrays_and_pointers_are_fields() {
        let t = the_only_type("struct S { int (*cb)(int); int arr[3]; int* ptr; };\n");
        assert_eq!(members(&t), ["public field cb", "public field arr", "public field ptr"]);
        assert_eq!(member(&t, "cb").signature, "int (*cb)(int)");
    }

    #[test]
    fn a_multi_line_parameter_list_has_the_same_key_and_signature_as_the_single_line_form() {
        let single = the_only_type("class A { void grow(int by, long times); };\n");
        let multi = the_only_type("class A { void grow(int by,\n      long   times); };\n");
        assert_eq!(single.members[0].key, "grow(int by, long times)");
        assert_eq!(multi.members[0].key, single.members[0].key);
        assert_eq!(multi.members[0].signature, single.members[0].signature);
        assert_eq!(single.members[0].signature, "void grow(int by, long times)");
        assert_eq!(multi.members[0].name, "grow");
    }

    #[test]
    fn members_under_preprocessor_conditionals_inside_a_class_body_are_found() {
        let t = the_only_type(
            "class A {\n int a;\n#if X\n int b;\n#else\n int c;\n#endif\n public:\n#ifdef Y\n void d();\n#endif\n};\n",
        );
        assert_eq!(
            members(&t),
            ["private field a", "private field b", "private field c", "public method d()"]
        );
    }

    #[test]
    fn types_under_preprocessor_conditionals_at_namespace_level_are_found() {
        let types = extract(
            "#ifdef X\nclass A { int a; };\n#else\nclass B { int b; };\n#endif\n\
             namespace n {\n#if Z\nstruct C {};\n#endif\n}\n",
        );
        assert_eq!(ids(&types), ["class A", "class B", "struct n::C"]);
    }

    #[test]
    fn a_class_inside_a_linkage_specification_is_found() {
        let types = extract("extern \"C++\" { class L { int l; }; }\n");
        assert_eq!(ids(&types), ["class L"]);
        assert_eq!(members(&types[0]), ["private field l"]);
    }

    #[test]
    fn a_type_that_parsed_beside_a_syntax_error_is_still_returned() {
        // Types on either side of a broken statement, at file level and inside a namespace.
        let beside = extract("class A { int a; };\nint x = ;\nclass B { int b; };\n");
        assert_eq!(ids(&beside), ["class A", "class B"]);
        let in_namespace = extract("namespace n { class A { int a; }; int f( ; class B { int b; }; }\n");
        assert_eq!(ids(&in_namespace), ["class n::A", "class n::B"]);
        // A type the grammar leaves directly under an ERROR node (measured shape) is reached too.
        let under_error = extract("int f( { class B { int b; }; \n");
        assert_eq!(ids(&under_error), ["class B"]);
        assert_eq!(members(&under_error[0]), ["private field b"]);
    }

    #[test]
    fn a_local_class_in_a_function_body_is_not_a_type() {
        let types = extract(
            "void ff() { struct Local { int z; }; }\n\
             class W { void m() { struct Inner {}; } };\n\
             class After { int a; };\n",
        );
        assert_eq!(ids(&types), ["class W", "class After"]);
        assert_eq!(members(&types[0]), ["private method m()"]);
    }

    #[test]
    fn forward_declarations_and_anonymous_structs_are_not_types() {
        let types = extract(
            "class Fwd;\nstruct Opaque;\nstruct { int a; } anon;\n\
             class Real { struct { int in; } inner; };\n",
        );
        assert_eq!(ids(&types), ["class Real"]);
        assert_eq!(members(&types[0]), ["private field inner"]);
    }

    #[test]
    fn a_named_struct_in_a_typedef_is_a_type() {
        let types = extract("typedef struct Tagged { int a; } Tagged_t;\n");
        assert_eq!(ids(&types), ["struct Tagged"]);
    }

    #[test]
    fn templates_yield_the_type_under_its_plain_or_specialised_name() {
        let types = extract(
            "template <class T> struct Box { T v; };\n\
             template <class T> struct Spec<T*> { };\n",
        );
        assert_eq!(ids(&types), ["struct Box", "struct Spec<T*>"]);
    }

    #[test]
    fn one_member_per_declarator() {
        let t = the_only_type("struct P { int n_, m_; int a = 1, b = 2; };\n");
        assert_eq!(
            members(&t),
            ["public field n_", "public field m_", "public field a", "public field b"]
        );
        assert_eq!(member(&t, "n_").signature, "int n_");
        assert_eq!(member(&t, "m_").signature, "int m_");
        assert_eq!(member(&t, "a").signature, "int a = 1");
        assert_eq!(member(&t, "b").signature, "int b = 2");
    }

    #[test]
    fn signatures_keep_specifiers_and_drop_bodies_initializers_and_semicolons() {
        let t = the_only_type(
            "class W {\n public:\n\
             W() : a_(1), b_(2) { }\n\
             virtual void g() override = 0;\n\
             int size() const { return n_; }\n\
             void h() = delete;\n\
             static const int k = 5;\n\
             int a_ = 3;\n int b_;\n int n_;\n};\n",
        );
        assert_eq!(member(&t, "W()").signature, "W()");
        let g = member(&t, "g()");
        assert_eq!(g.signature, "virtual void g() override = 0");
        assert_eq!(g.kind, "method");
        assert_eq!(member(&t, "size() const").signature, "int size() const");
        assert_eq!(member(&t, "h()").signature, "void h() = delete");
        assert_eq!(member(&t, "k").signature, "static const int k = 5");
        assert_eq!(member(&t, "a_").signature, "int a_ = 3");
        assert_eq!(member(&t, "n_").kind, "field");
    }

    #[test]
    fn invalid_utf8_inside_a_member_is_converted_lossily_not_dropped() {
        let mut source = b"class A { int /* caf".to_vec();
        source.push(0xE9);
        source.extend_from_slice(b" */ x; };\n");
        let types = extract_bytes(&source);
        assert_eq!(ids(&types), ["class A"]);
        let x = member(&types[0], "x");
        assert_eq!(x.signature, "int /* caf\u{FFFD} */ x");
        assert_eq!(x.start_byte, "class A { ".len());
    }

    #[test]
    fn signatures_are_cut_at_4096_bytes_on_a_character_boundary() {
        let long = "\u{e9}".repeat(3000);
        let t = the_only_type(&format!("struct L {{ const char* s = \"{long}\"; }};\n"));
        let signature = &member(&t, "s").signature;
        assert!(signature.starts_with("const char* s = \""));
        assert!(signature.len() <= 4096, "{}", signature.len());
        assert!(signature.len() >= 4093, "{}", signature.len());
    }

    #[test]
    fn a_conversion_operator_or_other_unhandled_child_yields_no_member() {
        let t = the_only_type(
            "struct S { operator bool() const; friend bool operator==(const S&, const S&);\n\
             using Base::f; static_assert(true, \"x\"); int ok; };\n",
        );
        assert_eq!(members(&t), ["public field ok"]);
    }
}
