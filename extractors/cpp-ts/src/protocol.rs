//! Request parsing and response writing for protocol v0.
//!
//! Stdin is one UTF-8 JSON header line ended by a line feed, followed by the raw bytes of each
//! listed file back to back. The sidecar never opens a repository path: `path` in the header is an
//! echoed label. Every rejection carries a fixed message that never includes the header text or any
//! file content (T-01-27), which the `&'static str` type of [`Failure::message`] guarantees.

use serde::{Deserialize, Serialize};
use std::time::Duration;

pub const REQUEST_SCHEMA: &str = "vdiff-cpp-ts-request/v0";
pub const RESPONSE_SCHEMA: &str = "vdiff-cpp-ts/v0";

/// Largest accepted header line, in bytes (excluding the line feed).
pub const MAX_HEADER_BYTES: usize = 8 * 1024 * 1024;
/// Largest accepted total of the promised file lengths, in bytes.
pub const MAX_TOTAL_BYTES: u64 = 512 * 1024 * 1024;
/// Per-file parse deadline used when the header has no `parse_budget_ms`.
pub const DEFAULT_PARSE_BUDGET_MS: u64 = 10_000;
pub const MIN_PARSE_BUDGET_MS: u64 = 1;
pub const MAX_PARSE_BUDGET_MS: u64 = 600_000;

/// A failed run. `exit_code` 2 is a rejected request; 1 is an internal failure.
#[derive(Debug, PartialEq, Eq)]
pub struct Failure {
    pub exit_code: i32,
    pub message: &'static str,
}

impl Failure {
    pub fn rejected(message: &'static str) -> Self {
        Failure { exit_code: 2, message }
    }

    pub fn internal(message: &'static str) -> Self {
        Failure { exit_code: 1, message }
    }
}

#[derive(Deserialize)]
struct Header {
    schema: String,
    #[serde(default)]
    blank: Vec<String>,
    parse_budget_ms: Option<u64>,
    files: Vec<HeaderFile>,
}

#[derive(Deserialize)]
struct HeaderFile {
    path: String,
    len: u64,
}

/// One file of a request: the echoed label and the bytes, borrowed from the input buffer.
#[derive(Debug)]
pub struct InputFile<'a> {
    pub path: String,
    pub bytes: &'a [u8],
}

#[derive(Debug)]
pub struct Request<'a> {
    /// Macro names to blank, sorted and distinct.
    pub blank: Vec<String>,
    pub budget: Duration,
    pub files: Vec<InputFile<'a>>,
}

fn is_ascii_identifier(name: &str) -> bool {
    let mut bytes = name.bytes();
    match bytes.next() {
        Some(b) if b.is_ascii_alphabetic() || b == b'_' => {}
        _ => return false,
    }
    bytes.all(|b| b.is_ascii_alphanumeric() || b == b'_')
}

pub fn parse_request(input: &[u8]) -> Result<Request<'_>, Failure> {
    let window = input.len().min(MAX_HEADER_BYTES + 1);
    let newline = match input[..window].iter().position(|&b| b == b'\n') {
        Some(position) => position,
        None if input.len() > MAX_HEADER_BYTES => {
            return Err(Failure::rejected("request header is too large"))
        }
        None => return Err(Failure::rejected("request header line is missing or unterminated")),
    };

    let header: Header = serde_json::from_slice(&input[..newline])
        .map_err(|_| Failure::rejected("request header is not valid"))?;

    if header.schema != REQUEST_SCHEMA {
        return Err(Failure::rejected("request schema is not supported"));
    }

    if !header.blank.iter().all(|name| is_ascii_identifier(name)) {
        return Err(Failure::rejected("a blank name is not an ASCII identifier"));
    }
    let mut blank = header.blank;
    blank.sort();
    blank.dedup();

    let budget_ms = header.parse_budget_ms.unwrap_or(DEFAULT_PARSE_BUDGET_MS);
    if !(MIN_PARSE_BUDGET_MS..=MAX_PARSE_BUDGET_MS).contains(&budget_ms) {
        return Err(Failure::rejected("parse_budget_ms is out of range"));
    }

    let mut total: u64 = 0;
    for file in &header.files {
        total = total
            .checked_add(file.len)
            .ok_or_else(|| Failure::rejected("file lengths overflow"))?;
    }
    if total > MAX_TOTAL_BYTES {
        return Err(Failure::rejected("total file length is too large"));
    }

    let body = &input[newline + 1..];
    if (body.len() as u64) < total {
        return Err(Failure::rejected("request body is shorter than promised"));
    }

    let mut files = Vec::with_capacity(header.files.len());
    let mut offset = 0usize;
    for file in header.files {
        // Each length is at most `total`, which is at most 512 MiB, so it fits in usize.
        let len = file.len as usize;
        files.push(InputFile { path: file.path, bytes: &body[offset..offset + len] });
        offset += len;
    }

    Ok(Request { blank, budget: Duration::from_millis(budget_ms), files })
}

#[derive(Serialize, Debug)]
pub struct Response {
    pub schema: &'static str,
    pub tree_sitter: &'static str,
    pub tree_sitter_cpp: &'static str,
    pub macros_blanked: Vec<String>,
    pub files: Vec<FileResult>,
}

#[derive(Serialize, Debug)]
pub struct FileResult {
    pub path: String,
    /// "ok", "timeout" or "error".
    pub status: &'static str,
    pub error_nodes: usize,
    pub types: Vec<TypeInfo>,
}

#[derive(Serialize, Debug, PartialEq, Eq)]
pub struct TypeInfo {
    /// "class", "struct" or "enum".
    pub kind: &'static str,
    pub name: String,
    /// Enclosing namespaces then enclosing types, joined with `::`; empty at global scope.
    pub scope: String,
    pub bases: Vec<String>,
    pub start_byte: usize,
    pub end_byte: usize,
    pub start_line: usize,
    pub end_line: usize,
    pub members: Vec<MemberInfo>,
}

#[derive(Serialize, Debug, PartialEq, Eq)]
pub struct MemberInfo {
    pub name: String,
    /// "method", "field", "alias" or "enumerator".
    pub kind: &'static str,
    /// "public", "protected" or "private"; "none" for an enumerator.
    pub access: &'static str,
    pub key: String,
    pub signature: String,
    pub start_byte: usize,
    pub end_byte: usize,
    pub start_line: usize,
    pub end_line: usize,
}

/// Serialises the response as one UTF-8 JSON object (serde_json writes non-ASCII text as raw UTF-8
/// and escapes control characters, so the bytes are valid UTF-8 by construction).
pub fn write_response(response: &Response) -> Result<Vec<u8>, Failure> {
    serde_json::to_vec(response).map_err(|_| Failure::internal("response could not be written"))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn request(header: &str, body: &[u8]) -> Vec<u8> {
        let mut input = header.as_bytes().to_vec();
        input.push(b'\n');
        input.extend_from_slice(body);
        input
    }

    fn rejected(input: &[u8]) -> &'static str {
        let failure = parse_request(input).expect_err("the request should be rejected");
        assert_eq!(failure.exit_code, 2, "a rejected request exits with 2");
        failure.message
    }

    const OK_HEADER: &str = r#"{"schema":"vdiff-cpp-ts-request/v0","blank":[],"files":[]}"#;

    #[test]
    fn an_empty_files_list_is_a_valid_request() {
        let input = request(OK_HEADER, b"");
        let parsed = parse_request(&input).expect("valid");
        assert!(parsed.files.is_empty());
        assert!(parsed.blank.is_empty());
    }

    #[test]
    fn two_files_are_sliced_back_to_back_in_request_order() {
        let header = r#"{"schema":"vdiff-cpp-ts-request/v0","files":[{"path":"a.h","len":3},{"path":"b.h","len":2}]}"#;
        let input = request(header, b"abcde");
        let parsed = parse_request(&input).expect("valid");
        assert_eq!(parsed.files.len(), 2);
        assert_eq!(parsed.files[0].path, "a.h");
        assert_eq!(parsed.files[0].bytes, b"abc");
        assert_eq!(parsed.files[1].path, "b.h");
        assert_eq!(parsed.files[1].bytes, b"de");
    }

    #[test]
    fn a_wrong_schema_is_rejected() {
        let input = request(r#"{"schema":"other/v1","files":[]}"#, b"");
        assert_eq!(rejected(&input), "request schema is not supported");
    }

    #[test]
    fn a_header_over_eight_mib_is_rejected() {
        let mut input = vec![b'x'; MAX_HEADER_BYTES + 1];
        input.push(b'\n');
        assert_eq!(rejected(&input), "request header is too large");
        let unterminated = vec![b'x'; MAX_HEADER_BYTES + 1];
        assert_eq!(rejected(&unterminated), "request header is too large");
    }

    #[test]
    fn a_header_of_exactly_eight_mib_is_read_not_rejected_as_too_large() {
        // Not valid JSON, so it is rejected for that reason instead; the point is the limit itself.
        let mut input = vec![b'x'; MAX_HEADER_BYTES];
        input.push(b'\n');
        assert_eq!(rejected(&input), "request header is not valid");
    }

    #[test]
    fn a_missing_line_feed_is_rejected() {
        assert_eq!(rejected(OK_HEADER.as_bytes()), "request header line is missing or unterminated");
        assert_eq!(rejected(b""), "request header line is missing or unterminated");
    }

    #[test]
    fn a_blank_name_that_is_not_an_ascii_identifier_is_rejected() {
        for bad in ["", "1ABC", "A-B", "A B", "\u{e9}tat", "A\u{e9}"] {
            let header = format!(
                r#"{{"schema":"vdiff-cpp-ts-request/v0","blank":["{}"],"files":[]}}"#,
                bad
            );
            assert_eq!(rejected(&request(&header, b"")), "a blank name is not an ASCII identifier", "name {bad:?}");
        }
    }

    #[test]
    fn blank_names_are_returned_sorted_and_distinct() {
        let header = r#"{"schema":"vdiff-cpp-ts-request/v0","blank":["ZED","_A","ZED","ABC"],"files":[]}"#;
        let input = request(header, b"");
        let parsed = parse_request(&input).expect("valid");
        assert_eq!(parsed.blank, vec!["ABC", "ZED", "_A"]);
    }

    #[test]
    fn parse_budget_defaults_to_ten_seconds_and_accepts_its_bounds() {
        let default_input = request(OK_HEADER, b"");
        let parsed = parse_request(&default_input).expect("valid");
        assert_eq!(parsed.budget, Duration::from_millis(10_000));
        for ms in [1u64, 600_000] {
            let header = format!(
                r#"{{"schema":"vdiff-cpp-ts-request/v0","parse_budget_ms":{ms},"files":[]}}"#
            );
            let input = request(&header, b"");
            let parsed = parse_request(&input).expect("valid");
            assert_eq!(parsed.budget, Duration::from_millis(ms));
        }
    }

    #[test]
    fn parse_budget_outside_one_to_600000_is_rejected() {
        for ms in ["0", "600001", "-5", "1.5", "\"10\""] {
            let header = format!(
                r#"{{"schema":"vdiff-cpp-ts-request/v0","parse_budget_ms":{ms},"files":[]}}"#
            );
            let message = rejected(&request(&header, b""));
            assert!(
                message == "parse_budget_ms is out of range" || message == "request header is not valid",
                "budget {ms}: {message}"
            );
        }
        let zero = r#"{"schema":"vdiff-cpp-ts-request/v0","parse_budget_ms":0,"files":[]}"#;
        assert_eq!(rejected(&request(zero, b"")), "parse_budget_ms is out of range");
    }

    #[test]
    fn a_total_over_512_mib_is_rejected() {
        let header = r#"{"schema":"vdiff-cpp-ts-request/v0","files":[{"path":"a","len":536870913}]}"#;
        assert_eq!(rejected(&request(header, b"")), "total file length is too large");
    }

    #[test]
    fn a_total_of_exactly_512_mib_with_a_short_body_is_a_short_body() {
        let header = r#"{"schema":"vdiff-cpp-ts-request/v0","files":[{"path":"a","len":536870912}]}"#;
        assert_eq!(rejected(&request(header, b"x")), "request body is shorter than promised");
    }

    #[test]
    fn lengths_whose_sum_overflows_64_bits_are_rejected() {
        let header = r#"{"schema":"vdiff-cpp-ts-request/v0","files":[{"path":"a","len":10000000000000000000},{"path":"b","len":10000000000000000000}]}"#;
        assert_eq!(rejected(&request(header, b"")), "file lengths overflow");
    }

    #[test]
    fn fewer_bytes_than_promised_are_rejected() {
        let header = r#"{"schema":"vdiff-cpp-ts-request/v0","files":[{"path":"a","len":5}]}"#;
        assert_eq!(rejected(&request(header, b"abcd")), "request body is shorter than promised");
    }

    #[test]
    fn a_rejection_message_never_contains_header_text() {
        let header = r#"{"schema":"vdiff-cpp-ts-request/v0","files":"SECRET-PAYLOAD"}"#;
        let message = rejected(&request(header, b"SECRET-PAYLOAD"));
        assert!(!message.contains("SECRET"));
        let bad = rejected(b"SECRET-PAYLOAD\n");
        assert!(!bad.contains("SECRET"));
    }

    #[test]
    fn the_response_is_one_utf8_json_object_with_the_documented_fields() {
        let response = Response {
            schema: RESPONSE_SCHEMA,
            tree_sitter: "0.27.0",
            tree_sitter_cpp: "0.23.4",
            macros_blanked: vec!["A".to_string()],
            files: vec![FileResult {
                path: "caf\u{e9}.h".to_string(),
                status: "ok",
                error_nodes: 0,
                types: vec![],
            }],
        };
        let bytes = write_response(&response).expect("serialises");
        let text = String::from_utf8(bytes).expect("valid UTF-8");
        assert!(text.starts_with('{') && text.ends_with('}'));
        assert!(text.contains("\"schema\":\"vdiff-cpp-ts/v0\""));
        assert!(text.contains("\"macros_blanked\":[\"A\"]"));
        assert!(text.contains("caf\u{e9}.h"), "non-ASCII is written as raw UTF-8");
        assert!(text.contains("\"status\":\"ok\""));
    }
}
