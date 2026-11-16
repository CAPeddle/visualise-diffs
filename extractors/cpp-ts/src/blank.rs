//! Length-preserving macro blanking (D-05).
//!
//! Export and decoration macros such as `EXPORT_API` in `class EXPORT_API Foo` make the C++ grammar
//! lose the class. Blanking replaces each whole-word occurrence with the same number of spaces on
//! the raw bytes, so every byte offset and line number found later in the blanked text is also true
//! in the original file.

fn is_id(c: u8) -> bool {
    c.is_ascii_alphanumeric() || c == b'_'
}

/// Replaces every whole-word occurrence of each macro name with spaces, in place.
///
/// * The length never changes.
/// * A line whose first non-blank byte is `#` is a preprocessor line and is left untouched, so a
///   `#define EXPORT_API` or `#ifdef EXPORT_API` keeps its meaning.
/// * A match inside a longer identifier (`EXPORT_API_X`, `MY_EXPORT_API`) is left untouched.
/// * Macro names are ASCII (the protocol rejects anything else), so multi-byte UTF-8 sequences are
///   never altered: every byte of such a sequence is at least 0x80 and is neither part of a name
///   nor an identifier byte.
pub fn blank(src: &mut [u8], macros: &[String]) {
    if macros.is_empty() {
        return;
    }
    let mut line_start = 0;
    while line_start < src.len() {
        let line_end = src[line_start..]
            .iter()
            .position(|&b| b == b'\n')
            .map_or(src.len(), |p| line_start + p);
        let first = src[line_start..line_end]
            .iter()
            .find(|&&b| b != b' ' && b != b'\t')
            .copied();
        if first != Some(b'#') {
            for m in macros {
                blank_in_line(src, line_start, line_end, m.as_bytes());
            }
        }
        line_start = line_end + 1;
    }
}

fn blank_in_line(src: &mut [u8], line_start: usize, line_end: usize, name: &[u8]) {
    if name.is_empty() {
        return;
    }
    let mut p = line_start;
    while p + name.len() <= line_end {
        let whole_word = &src[p..p + name.len()] == name
            && (p == line_start || !is_id(src[p - 1]))
            && (p + name.len() >= line_end || !is_id(src[p + name.len()]));
        if whole_word {
            for b in &mut src[p..p + name.len()] {
                *b = b' ';
            }
            p += name.len();
        } else {
            p += 1;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::blank;

    fn run(text: &str, macros: &[&str]) -> String {
        let mut bytes = text.as_bytes().to_vec();
        let names: Vec<String> = macros.iter().map(|m| m.to_string()).collect();
        blank(&mut bytes, &names);
        assert_eq!(bytes.len(), text.len(), "blanking must preserve the length");
        String::from_utf8(bytes).expect("blanking must keep valid UTF-8 valid")
    }

    #[test]
    fn length_is_preserved_and_the_macro_becomes_spaces() {
        let out = run("class EXPORT_API Foo {};", &["EXPORT_API"]);
        assert_eq!(out, "class            Foo {};");
    }

    #[test]
    fn preprocessor_lines_are_untouched_including_an_indented_one() {
        let text = "#define EXPORT_API\n   # ifdef EXPORT_API\n\t#if defined(EXPORT_API)\nint EXPORT_API x;";
        let out = run(text, &["EXPORT_API"]);
        assert_eq!(
            out,
            "#define EXPORT_API\n   # ifdef EXPORT_API\n\t#if defined(EXPORT_API)\nint            x;"
        );
    }

    #[test]
    fn partial_words_are_untouched() {
        let text = "EXPORT_API_X a; MY_EXPORT_API b; EXPORT_APIS c; _EXPORT_API d;";
        assert_eq!(run(text, &["EXPORT_API"]), text);
    }

    #[test]
    fn a_macro_at_the_start_of_the_input_is_blanked() {
        assert_eq!(run("EXPORT_API int x;", &["EXPORT_API"]), "           int x;");
    }

    #[test]
    fn a_macro_at_the_end_of_the_input_is_blanked() {
        assert_eq!(run("int x; EXPORT_API", &["EXPORT_API"]), "int x;           ");
    }

    #[test]
    fn a_macro_filling_the_whole_input_is_blanked() {
        assert_eq!(run("EXPORT_API", &["EXPORT_API"]), "          ");
    }

    #[test]
    fn adjacent_macros_are_both_blanked() {
        assert_eq!(run("A B C", &["A", "B", "C"]), "     ");
        assert_eq!(run("class A B Foo", &["A", "B"]), "class     Foo");
    }

    #[test]
    fn multi_byte_utf8_next_to_a_macro_is_untouched() {
        let text = "/* caf\u{e9} */ EXPORT_API \u{4e2d}\u{6587}EXPORT_API\u{1f600} class X {};";
        let out = run(text, &["EXPORT_API"]);
        assert_eq!(
            out,
            "/* caf\u{e9} */            \u{4e2d}\u{6587}          \u{1f600} class X {};"
        );
    }

    #[test]
    fn an_empty_macro_list_changes_nothing() {
        let text = "class EXPORT_API Foo {};";
        assert_eq!(run(text, &[]), text);
    }

    #[test]
    fn every_line_of_a_multi_line_input_is_handled() {
        let text = "EXPORT_API a;\n#define EXPORT_API\r\nEXPORT_API b;\r\n";
        let out = run(text, &["EXPORT_API"]);
        assert_eq!(out, "           a;\n#define EXPORT_API\r\n           b;\r\n");
    }
}
