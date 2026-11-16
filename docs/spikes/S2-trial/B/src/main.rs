// ADR-001 trial (B): native Rust sidecar (tree-sitter 0.27.0 + tree-sitter-cpp 0.23.4).
// usage: s2b <dir> [--blank NAME]... [--threads N] > out.json ; timings on stderr
use rayon::prelude::*;
use serde::Serialize;
use std::{fs, path::Path, time::Instant};
use tree_sitter::{Node, Parser};

#[derive(Serialize)]
struct Ty {
    file: String,
    kind: &'static str,
    name: String,
    bases: Vec<String>,
    members: usize,
    nested: usize,
    start_byte: usize,
    end_byte: usize,
    start_line: usize,
}

#[derive(Serialize)]
struct Out {
    schema: &'static str,
    files: usize,
    types: Vec<Ty>,
}

fn is_id(c: u8) -> bool {
    c.is_ascii_alphanumeric() || c == b'_'
}

/// Length-preserving blanking on raw bytes: each whole-word occurrence of a macro name becomes spaces.
/// Lines whose first non-blank byte is '#' are left alone. Multi-byte UTF-8 is never touched
/// because macro names are ASCII and matches are whole-word.
fn blank(src: &mut [u8], macros: &[String]) {
    if macros.is_empty() {
        return;
    }
    let mut ls = 0;
    while ls < src.len() {
        let le = src[ls..].iter().position(|&b| b == b'\n').map_or(src.len(), |p| ls + p);
        let first = src[ls..le].iter().find(|&&b| b != b' ' && b != b'\t').copied();
        if first != Some(b'#') {
            for m in macros {
                let mb = m.as_bytes();
                let mut p = ls;
                while p + mb.len() <= le {
                    if &src[p..p + mb.len()] == mb
                        && (p == 0 || !is_id(src[p - 1]))
                        && (p + mb.len() >= src.len() || !is_id(src[p + mb.len()]))
                    {
                        for b in &mut src[p..p + mb.len()] {
                            *b = b' ';
                        }
                        p += mb.len();
                    } else {
                        p += 1;
                    }
                }
            }
        }
        ls = le + 1;
    }
}

fn text<'a>(n: Node, src: &'a [u8]) -> &'a str {
    std::str::from_utf8(&src[n.start_byte()..n.end_byte()]).unwrap_or("")
}

fn walk(n: Node, file: &str, src: &[u8], out: &mut Vec<Ty>) {
    let k = n.kind();
    if k == "class_specifier" || k == "struct_specifier" {
        if let (Some(body), Some(name)) = (n.child_by_field_name("body"), n.child_by_field_name("name")) {
            let mut bases = Vec::new();
            let mut cur = n.walk();
            for c in n.children(&mut cur) {
                if c.kind() == "base_class_clause" {
                    let mut c2 = c.walk();
                    for b in c.named_children(&mut c2) {
                        if !matches!(b.kind(), "access_specifier" | "virtual" | "comment") {
                            bases.push(text(b, src).to_string());
                        }
                    }
                }
            }
            let (mut members, mut nested) = (0, 0);
            let mut c3 = body.walk();
            for c in body.named_children(&mut c3) {
                match c.kind() {
                    "field_declaration" | "function_definition" | "declaration" | "template_declaration"
                    | "alias_declaration" => members += 1,
                    _ => {}
                }
                if c.kind() == "field_declaration" {
                    let mut c4 = c.walk();
                    if c.children(&mut c4).any(|x| matches!(x.kind(), "class_specifier" | "struct_specifier" | "enum_specifier")) {
                        nested += 1;
                    }
                }
            }
            out.push(Ty {
                file: file.to_string(),
                kind: if k == "class_specifier" { "class" } else { "struct" },
                name: text(name, src).to_string(),
                bases,
                members,
                nested,
                start_byte: n.start_byte(),
                end_byte: n.end_byte(),
                start_line: n.start_position().row + 1,
            });
        }
    }
    let mut cur = n.walk();
    for c in n.children(&mut cur) {
        walk(c, file, src, out);
    }
}

fn collect(dir: &Path, root: &Path, v: &mut Vec<String>) {
    for e in fs::read_dir(dir).unwrap() {
        let p = e.unwrap().path();
        if p.is_dir() {
            collect(&p, root, v);
        } else if matches!(p.extension().and_then(|x| x.to_str()), Some("h" | "cpp" | "hpp" | "cc")) {
            v.push(p.strip_prefix(root).unwrap().to_string_lossy().replace(std::path::MAIN_SEPARATOR, "/"));
        }
    }
}

fn main() {
    let a: Vec<String> = std::env::args().collect();
    let root = Path::new(&a[1]).to_path_buf();
    let (mut macros, mut threads) = (Vec::<String>::new(), 0usize);
    let mut i = 2;
    while i < a.len() {
        match a[i].as_str() {
            "--blank" => { i += 1; macros.push(a[i].clone()); }
            "--threads" => { i += 1; threads = a[i].parse().unwrap(); }
            _ => {}
        }
        i += 1;
    }
    if threads > 0 {
        rayon::ThreadPoolBuilder::new().num_threads(threads).build_global().unwrap();
    }
    let t0 = Instant::now();
    let mut files = Vec::new();
    collect(&root, &root, &mut files);
    files.sort(); // byte-wise ordinal, same as the C# trial
    let t1 = Instant::now();
    let lang: tree_sitter::Language = tree_sitter_cpp::LANGUAGE.into();
    let results: Vec<Vec<Ty>> = files
        .par_iter()
        .map_init(
            || { let mut p = Parser::new(); p.set_language(&lang).unwrap(); p },
            |parser, f| {
                let mut src = fs::read(root.join(f)).unwrap();
                blank(&mut src, &macros);
                let tree = parser.parse(&src, None).unwrap();
                let mut v = Vec::new();
                walk(tree.root_node(), f, &src, &mut v);
                v
            },
        )
        .collect();
    let parse_ms = t1.elapsed().as_secs_f64() * 1000.0;
    let types: Vec<Ty> = results.into_iter().flatten().collect();
    let members: usize = types.iter().map(|t| t.members).sum();
    eprintln!(
        "B threads={} files={} types={} members={} parse_extract_ms={:.1} total_ms={:.1} abi={}",
        if threads > 0 { threads } else { rayon::current_num_threads() }, files.len(), types.len(), members, parse_ms,
        t0.elapsed().as_secs_f64() * 1000.0, lang.abi_version()
    );
    let n = files.len();
    serde_json::to_writer(std::io::stdout().lock(), &Out { schema: "s2-trial", files: n, types }).unwrap();
}
