//! vdiff-cpp-ts: the syntax-only C++ type extractor sidecar (ADR-001).
//!
//! Reads one request on stdin (see `protocol`), blanks the listed macros, parses each file with a
//! per-file deadline and writes one UTF-8 JSON object on stdout. It never opens a repository path.

mod blank;
mod extract;
mod protocol;

use protocol::{FileResult, Failure, Response};
use rayon::prelude::*;
use std::collections::BTreeSet;
use std::io::{Read, Write};
use std::time::Duration;
use tree_sitter::Parser;

/// The tree-sitter runtime version this binary is built against (pinned with `=` in Cargo.toml).
pub const TREE_SITTER_VERSION: &str = "0.27.0";
/// The tree-sitter-cpp grammar version this binary is built against (pinned with `=` in Cargo.toml).
pub const TREE_SITTER_CPP_VERSION: &str = "0.23.4";

/// Worker threads parse with a deep stack: tree-sitter itself is iterative, but the extractor
/// descends nested namespaces and types recursively.
const WORKER_STACK_BYTES: usize = 64 * 1024 * 1024;

fn process_file(
    parser: &mut Parser,
    file: &protocol::InputFile<'_>,
    macros: &[String],
    budget: Duration,
) -> FileResult {
    let mut bytes = file.bytes.to_vec();
    blank::blank(&mut bytes, macros);
    match extract::parse_bounded(parser, &bytes, budget) {
        (extract::Status::Ok, Some(tree)) => {
            let root = tree.root_node();
            FileResult {
                path: file.path.clone(),
                status: extract::Status::Ok.as_str(),
                error_nodes: extract::count_error_nodes(root),
                types: extract::extract_types(root, &bytes),
            }
        }
        (status, _) => FileResult {
            path: file.path.clone(),
            status: status.as_str(),
            error_nodes: 0,
            types: Vec::new(),
        },
    }
}

/// Runs one request to one response, without touching stdin, stdout or the file system.
pub fn run(input: &[u8]) -> Result<Vec<u8>, Failure> {
    let request = protocol::parse_request(input)?;

    let language: tree_sitter::Language = tree_sitter_cpp::LANGUAGE.into();
    Parser::new()
        .set_language(&language)
        .map_err(|_| Failure::internal("the C++ grammar could not be loaded"))?;

    let files: Vec<FileResult> = if request.files.is_empty() {
        Vec::new()
    } else {
        let pool = rayon::ThreadPoolBuilder::new()
            .stack_size(WORKER_STACK_BYTES)
            .build()
            .map_err(|_| Failure::internal("the worker pool could not be created"))?;
        pool.install(|| {
            request
                .files
                .par_iter()
                .map_init(
                    || {
                        let mut parser = Parser::new();
                        // Checked once above; a parser without a language reports status "error".
                        let _ = parser.set_language(&language);
                        parser
                    },
                    |parser, file| process_file(parser, file, &request.blank, request.budget),
                )
                .collect()
        })
    };

    let macros_blanked: Vec<String> = request
        .blank
        .iter()
        .cloned()
        .collect::<BTreeSet<String>>()
        .into_iter()
        .collect();

    protocol::write_response(&Response {
        schema: protocol::RESPONSE_SCHEMA,
        tree_sitter: TREE_SITTER_VERSION,
        tree_sitter_cpp: TREE_SITTER_CPP_VERSION,
        macros_blanked,
        files,
    })
}

fn main() {
    // A panic message could quote file content; print one fixed line instead (T-01-27).
    std::panic::set_hook(Box::new(|_| {
        eprintln!("vdiff-cpp-ts: internal error");
    }));

    let limit = (protocol::MAX_HEADER_BYTES as u64) + 1 + protocol::MAX_TOTAL_BYTES + 1;
    let mut input = Vec::new();
    if std::io::stdin().lock().take(limit).read_to_end(&mut input).is_err() {
        eprintln!("vdiff-cpp-ts: stdin could not be read");
        std::process::exit(1);
    }

    match run(&input) {
        Ok(output) => {
            let mut stdout = std::io::stdout().lock();
            if stdout.write_all(&output).and_then(|_| stdout.flush()).is_err() {
                eprintln!("vdiff-cpp-ts: stdout could not be written");
                std::process::exit(1);
            }
        }
        Err(failure) => {
            eprintln!("vdiff-cpp-ts: {}", failure.message);
            std::process::exit(failure.exit_code);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn request(blank: &[&str], budget_ms: Option<u64>, files: &[(&str, &[u8])]) -> Vec<u8> {
        let blank_json: Vec<String> = blank.iter().map(|b| format!("\"{b}\"")).collect();
        let files_json: Vec<String> = files
            .iter()
            .map(|(path, bytes)| format!("{{\"path\":\"{path}\",\"len\":{}}}", bytes.len()))
            .collect();
        let budget = budget_ms.map(|ms| format!(",\"parse_budget_ms\":{ms}")).unwrap_or_default();
        let header = format!(
            "{{\"schema\":\"vdiff-cpp-ts-request/v0\",\"blank\":[{}]{budget},\"files\":[{}]}}\n",
            blank_json.join(","),
            files_json.join(",")
        );
        let mut input = header.into_bytes();
        for (_, bytes) in files {
            input.extend_from_slice(bytes);
        }
        input
    }

    fn response_text(input: &[u8]) -> String {
        String::from_utf8(run(input).expect("the run succeeds")).expect("UTF-8 output")
    }

    #[test]
    fn version_constants_match_the_pins_in_cargo_toml_and_cargo_lock() {
        let toml = include_str!("../Cargo.toml").replace("\r\n", "\n");
        let lock = include_str!("../Cargo.lock").replace("\r\n", "\n");
        assert!(toml.contains(&format!("tree-sitter = \"={TREE_SITTER_VERSION}\"")));
        assert!(toml.contains(&format!("tree-sitter-cpp = \"={TREE_SITTER_CPP_VERSION}\"")));
        assert!(lock.contains(&format!("name = \"tree-sitter\"\nversion = \"{TREE_SITTER_VERSION}\"\n")));
        assert!(lock.contains(&format!(
            "name = \"tree-sitter-cpp\"\nversion = \"{TREE_SITTER_CPP_VERSION}\"\n"
        )));
    }

    #[test]
    fn an_empty_files_list_gives_a_valid_empty_response_with_the_version_metadata() {
        let text = response_text(&request(&["B", "A", "B"], None, &[]));
        assert_eq!(
            text,
            "{\"schema\":\"vdiff-cpp-ts/v0\",\"tree_sitter\":\"0.27.0\",\"tree_sitter_cpp\":\"0.23.4\",\"macros_blanked\":[\"A\",\"B\"],\"files\":[]}"
        );
    }

    #[test]
    fn files_come_back_in_request_order_with_status_and_error_node_count() {
        let files: Vec<(String, Vec<u8>)> = (0..40)
            .map(|i| (format!("f{i:02}.h"), format!("class C{i} {{ int x; }};\n").into_bytes()))
            .collect();
        let refs: Vec<(&str, &[u8])> = files.iter().map(|(p, b)| (p.as_str(), b.as_slice())).collect();
        let text = response_text(&request(&[], None, &refs));
        let value: serde_json::Value = serde_json::from_str(&text).expect("JSON");
        let listed = value["files"].as_array().expect("files array");
        assert_eq!(listed.len(), 40);
        for (i, file) in listed.iter().enumerate() {
            assert_eq!(file["path"], format!("f{i:02}.h"));
            assert_eq!(file["status"], "ok");
            assert_eq!(file["error_nodes"], 0);
        }
    }

    #[test]
    fn a_file_with_a_syntax_error_reports_a_positive_error_node_count() {
        let text = response_text(&request(&[], None, &[("bad.h", b"class A { int x }\n")]));
        let value: serde_json::Value = serde_json::from_str(&text).expect("JSON");
        assert_eq!(value["files"][0]["status"], "ok");
        assert!(value["files"][0]["error_nodes"].as_u64().expect("number") >= 1);
    }

    #[test]
    fn a_file_past_its_deadline_is_labelled_timeout_and_the_next_file_still_parses() {
        let big = "class A { int x; };\n".repeat(200_000).into_bytes();
        let small: &[u8] = b"struct Small { int y; };\n";
        // One worker would make the reset visible; two files in order exercise both paths either way.
        let text = response_text(&request(&[], Some(1), &[("big.h", &big), ("small.h", small)]));
        let value: serde_json::Value = serde_json::from_str(&text).expect("JSON");
        assert_eq!(value["files"][0]["status"], "timeout");
        assert_eq!(value["files"][0]["error_nodes"], 0);
        assert_eq!(value["files"][0]["types"].as_array().expect("types").len(), 0);
        assert_eq!(value["files"][1]["path"], "small.h");
    }

    #[test]
    fn a_rejected_request_is_a_failure_with_exit_code_two() {
        let failure = run(b"not json\n").expect_err("rejected");
        assert_eq!(failure.exit_code, 2);
    }

    #[test]
    fn macros_are_blanked_before_parsing() {
        let source = b"class EXPORT_API Foo : public Base { int x; };\n";
        let with = response_text(&request(&["EXPORT_API"], None, &[("a.h", source)]));
        let value: serde_json::Value = serde_json::from_str(&with).expect("JSON");
        assert_eq!(value["files"][0]["error_nodes"], 0, "blanked source parses cleanly: {with}");
        let without = response_text(&request(&[], None, &[("a.h", source)]));
        let value: serde_json::Value = serde_json::from_str(&without).expect("JSON");
        assert!(value["files"][0]["error_nodes"].as_u64().expect("number") >= 1, "{without}");
    }
}
