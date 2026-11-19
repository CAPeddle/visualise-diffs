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

/// Containers are descended at most this many levels deep. Deeper subtrees are not entered; each
/// one adds 1 to the skip count that [`extract_with_skips`] returns, so the caller can label the
/// file as incomplete instead of presenting it as fully extracted (T-01-14, T-01-16).
pub const MAX_DEPTH: usize = 128;

/// Extracts classes, structs and enums, with their members, from a parsed C++ file.
///
/// Types are reached by a whitelist descent over declaration containers, never by walking every
/// node, so a function body is not entered and a local class cannot appear with a wrong scope.
/// Unions are not extracted. Use [`extract_with_skips`] when the depth-cap skip count matters.
#[cfg(test)]
pub fn extract_types(root: Node, src: &[u8]) -> Vec<TypeInfo> {
    extract_with_skips(root, src).0
}

/// Like `extract_types`, and also returns how many subtrees the depth cap ([`MAX_DEPTH`]) left
/// unvisited. A non-zero count means the file's type list may be incomplete.
pub fn extract_with_skips(root: Node, src: &[u8]) -> (Vec<TypeInfo>, usize) {
    let mut walk = Walk { src, scope: Vec::new(), out: Vec::new(), skipped: 0 };
    walk.collect(root, 0);
    (walk.out, walk.skipped)
}

struct Walk<'s> {
    src: &'s [u8],
    scope: Vec<String>,
    out: Vec<TypeInfo>,
    skipped: usize,
}

impl Walk<'_> {
    fn collect(&mut self, container: Node, depth: usize) {
        if depth > MAX_DEPTH {
            self.skipped += 1;
            return;
        }
        let mut cursor = container.walk();
        for child in container.children(&mut cursor) {
            match child.kind() {
                "class_specifier" | "struct_specifier" => self.collect_type(child, depth),
                "enum_specifier" => self.collect_enum(child),
                "namespace_definition" => {
                    if let Some(body) = child.child_by_field_name("body") {
                        let name = child
                            .child_by_field_name("name")
                            .map(|n| collapse(&text(n, self.src)))
                            .unwrap_or_else(|| "(anonymous)".to_string());
                        self.scope.push(name);
                        self.collect(body, depth + 1);
                        self.scope.pop();
                    }
                }
                // A declaration is entered only to find a type specifier inside it; it cannot nest
                // in itself, and a type specifier it holds is counted by `collect_type`, so it is
                // not a level. Without this a member of a type sitting at the cap would count as a
                // skipped subtree of its own.
                "declaration" | "type_definition" | "field_declaration" => self.collect(child, depth),
                "declaration_list" | "field_declaration_list" | "template_declaration" | "linkage_specification"
                | "ERROR" => self.collect(child, depth + 1),
                kind if PREPROC_KINDS.contains(&kind) => self.collect(child, depth + 1),
                _ => {}
            }
        }
    }

    fn collect_type(&mut self, spec: Node, depth: usize) {
        // A forward declaration has no body and an unnamed struct has no name: neither is a type.
        let (Some(name_node), Some(body)) = (spec.child_by_field_name("name"), spec.child_by_field_name("body"))
        else {
            return;
        };
        let is_class = spec.kind() == "class_specifier";
        let name = collapse(&text(name_node, self.src));
        let members = members_of(body, self.src, if is_class { "private" } else { "public" }, &mut self.skipped);
        self.out.push(TypeInfo {
            kind: if is_class { "class" } else { "struct" },
            name: name.clone(),
            scope: self.scope.join("::"),
            bases: base_names(spec, self.src),
            start_byte: spec.start_byte(),
            end_byte: spec.end_byte(),
            start_line: spec.start_position().row + 1,
            end_line: spec.end_position().row + 1,
            members,
        });
        // Nested types follow their parent, with the parent's name added to their scope.
        self.scope.push(name);
        self.collect(body, depth + 1);
        self.scope.pop();
    }

    /// An enum with a name and a body. Its `bases` hold the underlying type (empty when none) and
    /// each enumerator is a member of kind `enumerator` with access `none`.
    fn collect_enum(&mut self, spec: Node) {
        let (Some(name_node), Some(body)) = (spec.child_by_field_name("name"), spec.child_by_field_name("body"))
        else {
            return;
        };
        let bases = spec
            .child_by_field_name("base")
            .map(|b| vec![collapse(&text(b, self.src))])
            .unwrap_or_default();
        let mut members = Vec::new();
        enumerators(body, self.src, 0, &mut self.skipped, &mut members);
        self.out.push(TypeInfo {
            kind: "enum",
            name: collapse(&text(name_node, self.src)),
            scope: self.scope.join("::"),
            bases,
            start_byte: spec.start_byte(),
            end_byte: spec.end_byte(),
            start_line: spec.start_position().row + 1,
            end_line: spec.end_position().row + 1,
            members,
        });
    }
}

fn enumerators(container: Node, src: &[u8], depth: usize, skipped: &mut usize, out: &mut Vec<MemberInfo>) {
    if depth > MAX_DEPTH {
        *skipped += 1;
        return;
    }
    let mut cursor = container.walk();
    for child in container.children(&mut cursor) {
        match child.kind() {
            "enumerator" => {
                let Some(name_node) = child.child_by_field_name("name") else { continue };
                let name = collapse(&text(name_node, src));
                out.push(MemberInfo {
                    key: name.clone(),
                    name,
                    kind: "enumerator",
                    access: "none",
                    signature: cut_on_char_boundary(collapse(&text(child, src)), MAX_SIGNATURE_BYTES),
                    start_byte: child.start_byte(),
                    end_byte: child.end_byte(),
                    start_line: child.start_position().row + 1,
                    end_line: child.end_position().row + 1,
                });
            }
            kind if PREPROC_KINDS.contains(&kind) => enumerators(child, src, depth + 1, skipped, out),
            _ => {}
        }
    }
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

fn members_of(body: Node, src: &[u8], default_access: &'static str, skipped: &mut usize) -> Vec<MemberInfo> {
    let mut out = Vec::new();
    let mut access = default_access;
    member_items(body, src, &mut access, &mut out, 0, skipped);
    out
}

fn member_items(
    container: Node,
    src: &[u8],
    access: &mut &'static str,
    out: &mut Vec<MemberInfo>,
    depth: usize,
    skipped: &mut usize,
) {
    if depth > MAX_DEPTH {
        *skipped += 1;
        return;
    }
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
                declaration_members(child, child, "", false, src, *access, out)
            }
            "type_definition" => declaration_members(child, child, "", true, src, *access, out),
            "alias_declaration" => alias_member(child, child, "", src, *access, out),
            "template_declaration" => template_member(child, src, *access, out),
            kind if PREPROC_KINDS.contains(&kind) => member_items(child, src, access, out, depth + 1, skipped),
            _ => {}
        }
    }
}

/// A member template: the declaration wrapped by `template <...>`. It yields the members of that
/// declaration, spanning the whole template, with the template header in front of the signature.
/// A nested class template has no member form here: it is a type, reached by the type descent.
fn template_member(template: Node, src: &[u8], access: &'static str, out: &mut Vec<MemberInfo>) {
    let mut cursor = template.walk();
    let inner = template.named_children(&mut cursor).find(|c| {
        matches!(c.kind(), "declaration" | "field_declaration" | "function_definition" | "alias_declaration")
    });
    let Some(inner) = inner else { return };
    let header = collapse(&lossy(src, template.start_byte(), inner.start_byte()));
    if inner.kind() == "alias_declaration" {
        alias_member(inner, template, &header, src, access, out);
    } else {
        declaration_members(inner, template, &header, false, src, access, out);
    }
}

/// `using Name = type;` as a member of kind `alias`, keyed by its name.
fn alias_member(alias: Node, span: Node, header: &str, src: &[u8], access: &'static str, out: &mut Vec<MemberInfo>) {
    let Some(name_node) = alias.child_by_field_name("name") else { return };
    let name = collapse(&text(name_node, src));
    let whole = lossy(src, alias.start_byte(), alias.end_byte());
    let whole = whole.trim_end();
    let whole = collapse(whole.strip_suffix(';').unwrap_or(whole));
    out.push(member(name.clone(), name, "alias", access, with_header(header, whole), span));
}

fn with_header(header: &str, signature: String) -> String {
    if header.is_empty() {
        signature
    } else {
        format!("{header} {signature}")
    }
}

fn member(
    name: String,
    key: String,
    kind: &'static str,
    access: &'static str,
    signature: String,
    span: Node,
) -> MemberInfo {
    MemberInfo {
        name,
        kind,
        access,
        key,
        signature: cut_on_char_boundary(signature, MAX_SIGNATURE_BYTES),
        start_byte: span.start_byte(),
        end_byte: span.end_byte(),
        start_line: span.start_position().row + 1,
        end_line: span.end_position().row + 1,
    }
}

/// The text of `src[start..end]` with the range `skip` left out, collapsed to single spaces.
fn collapsed_without(src: &[u8], start: usize, end: usize, skip: Option<(usize, usize)>) -> String {
    match skip {
        Some((from, to)) if start <= from && from <= to && to <= end => {
            collapse(&format!("{} {}", lossy(src, start, from), lossy(src, to, end)))
        }
        _ => collapse(&lossy(src, start, end)),
    }
}

/// One member per declarator of a field, declaration, function definition or typedef (`alias`
/// asks for kind `alias`). A declarator shape not handled here yields no member rather than a
/// wrong one. `span` is the node whose span the members take (the declaration, or the enclosing
/// template); `header` is the template header text, empty outside a member template.
fn declaration_members(
    decl: Node,
    span: Node,
    header: &str,
    alias: bool,
    src: &[u8],
    access: &'static str,
    out: &mut Vec<MemberInfo>,
) {
    let mut cursor = decl.walk();
    let declarators: Vec<Node> = decl.children_by_field_name("declarator", &mut cursor).collect();
    if declarators.is_empty() {
        return;
    }

    // A type declared in the same declaration (`struct In { ... } inst;`) is its own type, so its
    // body stays out of the signature of the variable.
    let skip = decl
        .child_by_field_name("type")
        .filter(|t| matches!(t.kind(), "class_specifier" | "struct_specifier" | "enum_specifier"))
        .and_then(|t| t.child_by_field_name("body"))
        .map(|b| (b.start_byte(), b.end_byte()));

    // The signature stops where a body or a constructor initializer list starts, so `= 0`,
    // `= default`, `= delete` and field default values stay in.
    let mut end = decl.end_byte();
    let mut children = decl.walk();
    for child in decl.children(&mut children) {
        if matches!(child.kind(), "compound_statement" | "field_initializer_list" | "function_try_block") {
            end = end.min(child.start_byte());
        }
    }
    let whole = collapsed_without(src, decl.start_byte(), end, skip);
    let whole = whole.strip_suffix(';').map(str::trim_end).unwrap_or(&whole).to_string();

    // With several declarators (`int n_, m_;`) each member is the shared specifiers plus its own
    // declarator, so `n_` and `m_` do not both carry the whole declaration as their signature.
    let shared_prefix = collapsed_without(src, decl.start_byte(), declarators[0].start_byte(), skip);

    for declarator in &declarators {
        let Some(resolved) = resolve(*declarator, src, 0) else { continue };
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
        let (kind, key) = if alias { ("alias", resolved.name.clone()) } else { (resolved.kind, resolved.key) };
        out.push(member(resolved.name, key, kind, access, with_header(header, signature), span));
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

/// `name`, then the parameter list text, then each trailing cv-qualifier, of a function
/// declarator or the abstract one of a conversion operator.
fn function_key(name: &str, declarator: Node, src: &[u8]) -> String {
    let parameters = declarator
        .child_by_field_name("parameters")
        .map(|p| collapse(&text(p, src)))
        .unwrap_or_default();
    let mut key = format!("{name}{parameters}");
    let mut cursor = declarator.walk();
    for qualifier in declarator.children(&mut cursor).filter(|c| c.kind() == "type_qualifier") {
        key.push(' ');
        key.push_str(&collapse(&text(qualifier, src)));
    }
    key
}

/// Follows declarators down to the member's name. For `destructor_name` and `operator_name` the
/// name is that node's own text (`~Gadget`, `operator==`), not its innermost leaf; a conversion
/// operator is named from `operator` through its target type (`operator bool`).
fn resolve(declarator: Node, src: &[u8], depth: usize) -> Option<Resolved> {
    if depth > MAX_DEPTH {
        return None;
    }
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
                    return resolve(inner, src, depth + 1);
                }
                let name = collapse(&text(inner, src));
                let key = function_key(&name, node, src);
                return Some(Resolved { name, key, kind: "method" });
            }
            "operator_cast" => {
                let mut function = node.child_by_field_name("declarator")?;
                while function.kind() != "abstract_function_declarator" {
                    function = function.child_by_field_name("declarator")?;
                }
                let name = collapse(&lossy(src, node.start_byte(), function.start_byte()));
                let key = function_key(&name, function, src);
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
    fn friend_using_and_static_assert_children_yield_nothing() {
        let t = the_only_type(
            "struct S { friend bool operator==(const S&, const S&); friend class Other;\n\
             using Base::f; static_assert(true, \"x\"); int ok; };\n",
        );
        assert_eq!(members(&t), ["public field ok"]);
    }

    #[test]
    fn enums_have_enumerators_and_the_underlying_type_in_bases() {
        let types = extract("enum Color { Red, Green = 2 };\nenum class E : unsigned char { X };\n");
        assert_eq!(ids(&types), ["enum Color", "enum E"]);
        assert!(types[0].bases.is_empty());
        assert_eq!(types[1].bases, ["unsigned char"]);
        assert_eq!(members(&types[0]), ["none enumerator Red", "none enumerator Green"]);
        assert_eq!(members(&types[1]), ["none enumerator X"]);
        let green = member(&types[0], "Green");
        assert_eq!((green.name.as_str(), green.kind, green.access), ("Green", "enumerator", "none"));
        assert_eq!(green.signature, "Green = 2");
        assert_eq!(member(&types[0], "Red").signature, "Red");
        assert_eq!(types[0].start_byte, 0);
        assert_eq!(types[0].end_byte, "enum Color { Red, Green = 2 }".len());
    }

    #[test]
    fn enum_forward_declarations_and_anonymous_enums_are_not_types() {
        let types = extract("enum class F : int;\nenum { Z };\ntypedef enum { Y } YT;\nenum Real { W };\n");
        assert_eq!(ids(&types), ["enum Real"]);
    }

    #[test]
    fn enumerators_under_preprocessor_conditionals_are_found() {
        let types = extract("enum E2 { P,\n#if X\n Q,\n#endif\n R };\n");
        assert_eq!(members(&types[0]), ["none enumerator P", "none enumerator Q", "none enumerator R"]);
    }

    #[test]
    fn nested_class_struct_and_enum_are_types_with_the_enclosing_type_in_scope() {
        let types = extract(
            "namespace shop { class Box { public: enum Mode { A, B }; struct In { int v; };\n\
             class Deep { enum class K { Z }; }; int own; }; }\n",
        );
        assert_eq!(
            ids(&types),
            [
                "class shop::Box",
                "enum shop::Box::Mode",
                "struct shop::Box::In",
                "class shop::Box::Deep",
                "enum shop::Box::Deep::K"
            ]
        );
        // A type-only declaration is not a member of the enclosing type.
        assert_eq!(members(&types[0]), ["public field own"]);
    }

    #[test]
    fn a_nested_type_declared_with_a_variable_yields_the_variable_without_the_body() {
        let types = extract("struct S { struct In { int v; } inst; enum Mode { M } mode; };\n");
        assert_eq!(ids(&types), ["struct S", "struct S::In", "enum S::Mode"]);
        assert_eq!(members(&types[0]), ["public field inst", "public field mode"]);
        assert_eq!(member(&types[0], "inst").signature, "struct In inst");
        assert_eq!(member(&types[0], "mode").signature, "enum Mode mode");
    }

    #[test]
    fn using_and_typedef_inside_a_type_are_alias_members() {
        let types = extract(
            "class A { public: using Item = std::pair<int, int>; typedef int Handle;\n\
             typedef struct Tag { int q; } Tag_t; int x; };\n",
        );
        let t = &types[0];
        assert_eq!(ids(&types), ["class A", "struct A::Tag"]);
        assert_eq!(
            members(t),
            ["public alias Item", "public alias Handle", "public alias Tag_t", "public field x"]
        );
        assert_eq!(member(t, "Item").signature, "using Item = std::pair<int, int>");
        assert_eq!(member(t, "Handle").signature, "typedef int Handle");
        assert_eq!(member(t, "Tag_t").signature, "typedef struct Tag Tag_t");
    }

    #[test]
    fn a_typedef_that_names_a_struct_inside_a_type_still_yields_that_struct() {
        let types = extract("class A { typedef struct Tag { int q; } Tag_t; };\n");
        assert_eq!(ids(&types), ["class A", "struct A::Tag"]);
    }

    #[test]
    fn a_member_template_is_one_member_whose_signature_has_the_template_header() {
        let t = the_only_type(
            "class A { public: template <class T> void put(T t);\n\
             template <typename U> using V = std::vector<U>; template <class W> W get() const { return w_; } };\n",
        );
        assert_eq!(members(&t), ["public method put(T t)", "public alias V", "public method get() const"]);
        assert_eq!(member(&t, "put(T t)").signature, "template <class T> void put(T t)");
        assert_eq!(member(&t, "V").signature, "template <typename U> using V = std::vector<U>");
        assert_eq!(member(&t, "get() const").signature, "template <class W> W get() const");
    }

    #[test]
    fn a_nested_class_template_is_a_type_in_the_scope_of_its_enclosing_class() {
        let types = extract(
            "template <class T> class Outer { template <class U> struct Inner { U u; }; T t; };\n",
        );
        assert_eq!(ids(&types), ["class Outer", "struct Outer::Inner"]);
        assert_eq!(members(&types[0]), ["private field t"]);
    }

    #[test]
    fn conversion_operators_are_methods_named_from_operator_through_the_type() {
        let t = the_only_type(
            "class C { public: operator bool() const; operator std::string() const { return s_; }\n\
             explicit operator int*(); };\n",
        );
        assert_eq!(
            members(&t),
            [
                "public method operator bool() const",
                "public method operator std::string() const",
                "public method operator int*()"
            ]
        );
        let b = member(&t, "operator bool() const");
        assert_eq!((b.name.as_str(), b.kind), ("operator bool", "method"));
        assert_eq!(b.signature, "operator bool() const");
        assert_eq!(member(&t, "operator std::string() const").name, "operator std::string");
        // The body of an in-class definition is not part of the signature.
        assert_eq!(member(&t, "operator std::string() const").signature, "operator std::string() const");
        assert_eq!(member(&t, "operator int*()").signature, "explicit operator int*()");
    }

    #[test]
    fn a_const_and_a_non_const_overload_have_different_keys() {
        let t = the_only_type("class A { int get(); int get() const; operator bool(); operator bool() const; };\n");
        assert_eq!(
            members(&t),
            [
                "private method get()",
                "private method get() const",
                "private method operator bool()",
                "private method operator bool() const"
            ]
        );
    }

    #[test]
    fn a_nested_enum_under_a_preprocessor_conditional_in_a_class_body_is_found() {
        let types = extract("class A {\n#ifdef X\n enum Flavour { Sweet, Sour };\n#else\n struct Plain {};\n#endif\n int a; };\n");
        assert_eq!(ids(&types), ["class A", "enum A::Flavour", "struct A::Plain"]);
        assert_eq!(members(&types[1]), ["none enumerator Sweet", "none enumerator Sour"]);
    }

    #[test]
    fn unions_are_not_extracted() {
        let types = extract("union U { int a; float b; };\nstruct S { union { int c; float d; } u; int e; };\n");
        assert_eq!(ids(&types), ["struct S"]);
    }

    #[test]
    fn output_order_is_source_order_with_each_parent_before_its_nested_types() {
        let types = extract(
            "struct First { struct Nested {}; };\nenum Second { S1 };\nnamespace n { class Third { enum E3 { X }; }; }\n",
        );
        assert_eq!(
            ids(&types),
            ["struct First", "struct First::Nested", "enum Second", "class n::Third", "enum n::Third::E3"]
        );
    }

    #[test]
    fn known_gap_a_typedef_of_an_anonymous_struct_yields_no_type() {
        // D-11 / C36: labelled limitation, not fixed in Phase 1. The rest of the file is extracted.
        let types = extract("typedef struct { int a; } Name;\nclass After { int z; };\n");
        assert_eq!(ids(&types), ["class After"]);
        assert!(types.iter().all(|t| t.name != "Name"));
    }

    #[test]
    fn known_gap_a_macro_call_with_arguments_in_a_class_body_keeps_the_class_and_the_real_member() {
        // D-11 / C36: the grammar may produce a bogus member for `DECLARE_X(Foo)`; that member is
        // deliberately not asserted, so a grammar change in either direction is not a failure here.
        let t = the_only_type("class Foo { public: DECLARE_X(Foo) int real; };\n");
        assert_eq!(t.name, "Foo");
        assert!(t.members.iter().any(|m| m.key == "real" && m.kind == "field"), "{:?}", members(&t));
    }
}

#[cfg(test)]
mod robustness {
    use super::*;

    /// Parses and extracts one source; returns the status, the file's error-node count (parse
    /// errors plus depth-cap skips, as the response reports it) and the types.
    fn analyse(source: &[u8]) -> (Status, usize, Vec<TypeInfo>) {
        let mut parser = cpp_parser();
        let (status, tree) = parse_bounded(&mut parser, source, Duration::from_secs(30));
        match tree {
            Some(tree) => {
                let root = tree.root_node();
                let (types, skipped) = extract_with_skips(root, source);
                (status, count_error_nodes(root) + skipped, types)
            }
            None => (status, 0, Vec::new()),
        }
    }

    fn nested_namespaces(depth: usize, inner: &str) -> String {
        format!("{}{}{}\n", "namespace n { ".repeat(depth), inner, "} ".repeat(depth))
    }

    fn names(types: &[TypeInfo]) -> Vec<&str> {
        types.iter().map(|t| t.name.as_str()).collect()
    }

    #[test]
    fn a_syntax_error_labels_the_file_and_the_types_that_parsed_are_kept_and_a_clean_file_reports_zero() {
        let (status, errors, types) = analyse(b"class A { int a; };\nint f( ;\nclass B { int b; };\n");
        assert_eq!(status, Status::Ok);
        assert!(errors >= 1, "error nodes: {errors}");
        assert_eq!(names(&types), ["A", "B"]);

        let (_, clean_errors, clean_types) = analyse(b"class A { int a; };\nclass B { int b; };\n");
        assert_eq!(clean_errors, 0);
        assert_eq!(clean_types.len(), 2);
    }

    #[test]
    fn nesting_below_the_cap_is_extracted_in_full_and_not_labelled() {
        let source = nested_namespaces(MAX_DEPTH - 1, "class Deep { int d; };");
        let (status, errors, types) = analyse(source.as_bytes());
        assert_eq!(status, Status::Ok);
        assert_eq!(errors, 0);
        assert_eq!(names(&types), ["Deep"]);
        assert_eq!(types[0].scope.matches("::").count(), MAX_DEPTH - 2);
    }

    #[test]
    fn a_type_at_the_cap_is_kept_but_its_body_is_not_searched_for_nested_types_so_the_file_is_labelled() {
        let source = nested_namespaces(MAX_DEPTH, "class Deep { class Hidden {}; int d; };");
        let (status, errors, types) = analyse(source.as_bytes());
        assert_eq!(status, Status::Ok);
        assert_eq!(names(&types), ["Deep"]);
        assert_eq!(types[0].members.len(), 1, "the type's own members are still read");
        assert_eq!(errors, 1, "the nested type may have been missed: the file is labelled");
    }

    #[test]
    fn nesting_one_level_past_the_cap_is_not_descended_and_adds_one_error_node() {
        let source = nested_namespaces(MAX_DEPTH + 1, "class TooDeep { int d; };");
        let (status, errors, types) = analyse(source.as_bytes());
        assert_eq!(status, Status::Ok);
        assert!(types.is_empty(), "{:?}", names(&types));
        assert_eq!(errors, 1, "one skipped subtree, and a parse with no error of its own");
    }

    #[test]
    fn ten_thousand_nested_namespaces_complete_and_label_the_file() {
        // This test thread has the default 2 MiB stack: an uncapped recursive descent would overflow it.
        let source = nested_namespaces(10_000, "class Bottom {};");
        let (status, errors, types) = analyse(source.as_bytes());
        assert_eq!(status, Status::Ok);
        assert!(types.is_empty());
        assert!(errors >= 1, "the file must be labelled: {errors}");
    }

    #[test]
    fn deeply_nested_types_and_preprocessor_blocks_are_capped_too() {
        let classes = format!("{}{}", "class C { ".repeat(5_000), "}; ".repeat(5_000));
        let (status, errors, types) = analyse(classes.as_bytes());
        assert_eq!(status, Status::Ok);
        assert!(errors >= 1, "nested types past the cap label the file: {errors}");
        assert!(types.len() >= MAX_DEPTH / 2 && types.len() <= MAX_DEPTH + 1, "{}", types.len());

        let conditionals = format!("class P {{\n{}int x;\n{}}};\n", "#if A\n".repeat(5_000), "#endif\n".repeat(5_000));
        let (status, errors, types) = analyse(conditionals.as_bytes());
        assert_eq!(status, Status::Ok);
        assert_eq!(names(&types), ["P"]);
        assert!(errors >= 1, "conditionals nested past the cap label the file: {errors}");
    }

    #[test]
    fn two_hundred_thousand_nested_parentheses_complete_and_report_their_error_nodes() {
        let source = format!("int x = {}1{};\nclass After {{ int a; }};\n", "(".repeat(200_000), ")".repeat(200_000));
        let (status, errors, _) = analyse(source.as_bytes());
        // The parse finishes, or is labelled a timeout; it never crashes this thread.
        assert!(matches!(status, Status::Ok | Status::Timeout | Status::Error), "{status:?}");
        println!("nested parentheses: status {status:?}, error nodes {errors}");
    }

    #[test]
    fn twenty_thousand_nested_template_arguments_complete_and_report_their_error_nodes() {
        let source = format!("{}int{} x;\nclass After {{ int a; }};\n", "std::vector<".repeat(20_000), ">".repeat(20_000));
        let (status, errors, types) = analyse(source.as_bytes());
        assert!(matches!(status, Status::Ok | Status::Timeout | Status::Error), "{status:?}");
        println!("nested template arguments: status {status:?}, error nodes {errors}, types {}", types.len());
    }

    #[test]
    fn random_bytes_with_nuls_and_0xff_never_panic_and_the_response_stays_one_json_object() {
        let mut state: u64 = 0x9E37_79B9_7F4A_7C15;
        let mut next = move || {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            state
        };
        for round in 0..40 {
            let len = (next() % 3000) as usize;
            let mut bytes: Vec<u8> = (0..len).map(|_| (next() >> 24) as u8).collect();
            if round % 4 == 0 {
                bytes.splice(0..0, [0x00, 0xFF, 0xFE, 0x00]);
            }
            let _ = analyse(&bytes);

            let header = format!(
                "{{\"schema\":\"vdiff-cpp-ts-request/v0\",\"blank\":[\"X\"],\"files\":[{{\"path\":\"r.h\",\"len\":{}}}]}}\n",
                bytes.len()
            );
            let mut input = header.into_bytes();
            input.extend_from_slice(&bytes);
            let output = crate::run(&input).expect("hostile file bytes never fail the request");
            let value: serde_json::Value = serde_json::from_slice(&output).expect("one JSON object");
            assert!(value.is_object());
            assert!(matches!(value["files"][0]["status"].as_str(), Some("ok" | "timeout" | "error")));
        }
    }

    #[test]
    fn an_empty_file_gives_no_types_and_no_error_nodes() {
        let (status, errors, types) = analyse(b"");
        assert_eq!(status, Status::Ok);
        assert_eq!(errors, 0);
        assert!(types.is_empty());
    }

    #[test]
    fn a_crlf_file_has_correct_one_based_line_numbers() {
        let source = b"// header\r\nclass A {\r\n  int x;\r\n  void f();\r\n};\r\n";
        let (_, errors, types) = analyse(source);
        assert_eq!(errors, 0);
        assert_eq!(types.len(), 1);
        assert_eq!((types[0].start_line, types[0].end_line), (2, 5));
        let x = types[0].members.iter().find(|m| m.key == "x").expect("x");
        assert_eq!((x.start_line, x.end_line), (3, 3));
        let f = types[0].members.iter().find(|m| m.key == "f()").expect("f()");
        assert_eq!(f.start_line, 4);
        // The line break is two bytes, so the byte offset of the class counts both.
        assert_eq!(types[0].start_byte, "// header\r\n".len());
    }

    #[test]
    fn non_ascii_identifiers_extract_with_correct_names_bases_and_byte_offsets() {
        let source = "class Caf\u{e9}Type : public B\u{e4}se { public: int \u{e9}x; };\n";
        let (_, errors, types) = analyse(source.as_bytes());
        assert_eq!(errors, 0);
        assert_eq!(types.len(), 1);
        assert_eq!(types[0].name, "Caf\u{e9}Type");
        assert_eq!(types[0].bases, ["B\u{e4}se"]);
        let member = &types[0].members[0];
        assert_eq!(member.key, "\u{e9}x");
        assert_eq!(member.start_byte, source.find("int \u{e9}x;").unwrap());
        assert_eq!(member.end_byte, member.start_byte + "int \u{e9}x;".len());
        assert_eq!(types[0].end_byte, source.find("};").unwrap() + 1);
    }

    #[test]
    fn a_latin_1_byte_inside_a_comment_leaves_every_span_exact() {
        let mut source = b"// caf".to_vec();
        source.push(0xE9);
        source.extend_from_slice(b"\nclass A { int x; };\n");
        let (_, errors, types) = analyse(&source);
        assert_eq!(errors, 0);
        assert_eq!(types.len(), 1);
        assert_eq!(types[0].start_byte, "// caf".len() + 1 + 1);
        assert_eq!(types[0].start_line, 2);
        let x = &types[0].members[0];
        assert_eq!(x.start_byte, types[0].start_byte + "class A { ".len());
    }

    #[test]
    fn a_failed_request_prints_a_fixed_message_never_file_content() {
        let failure = crate::run(b"SECRET-CONTENT\n").expect_err("not a request");
        assert_eq!(failure.exit_code, 2);
        assert!(!failure.message.contains("SECRET"));
    }
}
