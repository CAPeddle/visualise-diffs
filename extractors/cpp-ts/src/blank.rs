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

    #[test]
    fn a_name_that_is_a_prefix_of_another_blanks_each_whole_word_correctly() {
        let text = "EXPORT EXPORT_API EXPORT_APIX XEXPORT";
        let out = run(text, &["EXPORT", "EXPORT_API"]);
        assert_eq!(out, "                  EXPORT_APIX XEXPORT");
        // The order in which the names are given does not matter.
        assert_eq!(run(text, &["EXPORT_API", "EXPORT"]), out);
    }

    #[test]
    fn matching_is_case_sensitive() {
        let text = "export_api Export_Api EXPORT_API";
        assert_eq!(run(text, &["EXPORT_API"]), "export_api Export_Api           ");
    }

    #[test]
    fn repeated_macros_on_one_line_are_all_blanked() {
        let out = run("EXPORT_API EXPORT_API int x; EXPORT_API", &["EXPORT_API"]);
        assert_eq!(out, "                      int x;           ");
    }

    #[test]
    fn punctuation_is_a_word_boundary_but_digits_and_underscores_are_not() {
        let text = "(EXPORT_API) <EXPORT_API> ::EXPORT_API, EXPORT_API1 1EXPORT_API EXPORT_API_";
        let out = run(text, &["EXPORT_API"]);
        let gap = " ".repeat("EXPORT_API".len());
        assert_eq!(out, format!("({gap}) <{gap}> ::{gap}, EXPORT_API1 1EXPORT_API EXPORT_API_"));
    }

    #[test]
    fn a_hash_in_the_middle_of_a_line_does_not_make_it_a_preprocessor_line() {
        let out = run("int a; # EXPORT_API\n  \t #define EXPORT_API\n", &["EXPORT_API"]);
        assert_eq!(out, "int a; #           \n  \t #define EXPORT_API\n");
    }

    #[test]
    fn bytes_that_are_not_utf8_are_left_alone_and_never_block_a_match() {
        let mut bytes = vec![0xFF, 0x00, b' '];
        bytes.extend_from_slice(b"EXPORT_API");
        bytes.extend_from_slice(&[0xC3, 0x00, 0xFE]);
        let before = bytes.clone();
        super::blank(&mut bytes, &["EXPORT_API".to_string()]);
        assert_eq!(bytes.len(), before.len());
        assert_eq!(&bytes[..3], &before[..3]);
        assert_eq!(&bytes[3..13], b"          ");
        assert_eq!(&bytes[13..], &before[13..]);
    }

    #[test]
    fn a_byte_order_mark_is_kept_and_every_offset_after_it_is_unchanged() {
        let mut bytes = vec![0xEF, 0xBB, 0xBF];
        bytes.extend_from_slice(b"class EXPORT_API Foo {};\n");
        let foo_before = bytes.windows(3).position(|w| w == b"Foo").expect("Foo");
        super::blank(&mut bytes, &["EXPORT_API".to_string()]);
        assert_eq!(&bytes[..3], &[0xEF, 0xBB, 0xBF]);
        assert_eq!(bytes.windows(3).position(|w| w == b"Foo"), Some(foo_before));
        assert_eq!(foo_before, 3 + "class EXPORT_API ".len());
    }

    #[test]
    fn a_name_longer_than_the_input_and_an_empty_name_change_nothing() {
        assert_eq!(run("EX", &["EXPORT_API"]), "EX");
        assert_eq!(run("class A {};", &[""]), "class A {};");
        assert_eq!(run("", &["EXPORT_API"]), "");
    }

    #[test]
    fn blanking_twice_gives_the_same_bytes_as_blanking_once() {
        let once = run("class EXPORT_API A : public EXPORT_BASE B {};\n#define EXPORT_API\n", &["EXPORT_API", "EXPORT_BASE"]);
        let twice = run(&once, &["EXPORT_API", "EXPORT_BASE"]);
        assert_eq!(once, twice);
    }

    #[test]
    fn for_any_input_the_length_is_kept_and_only_bytes_inside_a_named_macro_become_spaces() {
        // A small deterministic generator: no extra crate, the same inputs on every run.
        let mut state: u64 = 0x2545_F491_4F6C_DD1D;
        let mut next = move || {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            state
        };
        let fragments: [&[u8]; 14] = [
            b"EXPORT_API", b"EXPORT", b"EXPORT_APIS", b"class ", b" ", b"\n", b"\r\n", b"#", b"\t", b"x", b"_",
            b"\xC3\xA9", b"\xFF", b"\x00",
        ];
        let names = vec!["EXPORT_API".to_string(), "EXPORT".to_string()];
        for _ in 0..300 {
            let mut input = Vec::new();
            for _ in 0..(next() % 40) {
                input.extend_from_slice(fragments[(next() % fragments.len() as u64) as usize]);
            }
            let mut output = input.clone();
            super::blank(&mut output, &names);
            assert_eq!(output.len(), input.len());
            for (i, (a, b)) in input.iter().zip(&output).enumerate() {
                if a != b {
                    assert_eq!(*b, b' ', "byte {i} was changed to something other than a space");
                    assert!(
                        a.is_ascii_uppercase() || *a == b'_',
                        "byte {i} was part of no macro name: {a:#x}"
                    );
                }
            }
        }
    }
}
