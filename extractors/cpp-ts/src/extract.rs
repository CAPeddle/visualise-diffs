//! Deadline-bounded parsing, error-node counting and (task 2) type extraction.

use crate::protocol::TypeInfo;
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

/// Extracts classes and structs with their members (tracer subset). Implemented in task 2.
pub fn extract_types(_root: Node, _src: &[u8]) -> Vec<TypeInfo> {
    Vec::new()
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
