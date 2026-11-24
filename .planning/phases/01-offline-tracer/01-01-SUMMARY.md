---
phase: 01-offline-tracer
plan: 01
subsystem: extraction
tags: [rust, tree-sitter, tree-sitter-cpp, sidecar, json-protocol, macro-blanking]

requires:
  - phase: none
    provides: ADR-001 (accepted) and the S2 trial crate in docs/spikes/S2-trial/B
provides:
  - extractors/cpp-ts Rust sidecar crate (vdiff-cpp-ts) built from the ADR-001 pinned graph
  - protocol v0 (stdin manifest in, one JSON object out) with per-file status and error-node count
  - length-preserving macro blanking
  - deadline-bounded parse with parser reset
  - class and struct extraction with scope, bases, access-annotated members, UTF-8 byte spans
  - repository hygiene (.gitignore, .gitattributes)
affects: [01-02, 01-03, 01-05, 01-06]

plan_head_before: df400451e74d6c46cbe5a60af376eb2c66d8dd8a
plan_head_after: d661028b224ee6204a073298263b57c83d7dbbfd

actuals:
  tokens: 14600
  tasks: 2
  commits: 3

tech-stack:
  added: [tree-sitter =0.27.0, tree-sitter-cpp =0.23.4, rayon 1, serde 1, serde_json 1]
  patterns:
    - "stdin manifest protocol: one JSON header line, then raw file bytes; paths are echoed labels, never opened"
    - "per-thread parser (rayon map_init) with progress-callback deadline and reset after cancel"
    - "whitelist descent over declaration containers instead of walking every node"

key-files:
  created:
    - .gitignore
    - .gitattributes
    - extractors/cpp-ts/Cargo.toml
    - extractors/cpp-ts/Cargo.lock
    - extractors/cpp-ts/src/main.rs
    - extractors/cpp-ts/src/protocol.rs
    - extractors/cpp-ts/src/blank.rs
    - extractors/cpp-ts/src/extract.rs
  modified: []

key-decisions:
  - "Cargo.lock is the S2 trial lock with only the root package name changed (33 packages, five direct); `--locked` accepts it and a diff that ignores the root name line is empty"
  - "A subtree with no error is skipped while counting ERROR and MISSING nodes (node.has_error), keeping the iterative walk cheap on clean files"
  - "Several declarators in one declaration (int n_, m_;) each get the shared specifiers plus their own declarator as signature; all members of one declaration share the whole declaration's span"
  - "Internal failures (pool or serialisation) exit 1; only rejected requests exit 2"

patterns-established:
  - "Rejections are Failure { exit_code, message: &'static str }, so no header text or file content can reach stderr by type"
  - "RED tests are committed against a stub that returns an empty list, so they fail on the planned assertion rather than on a compile error"

requirements-completed: [SPK-02, TRC-02]

coverage:
  - id: D1
    description: "Sidecar crate builds locked from the ADR-001 pinned graph and reports tree_sitter 0.27.0 and tree_sitter_cpp 0.23.4, guarded by a test against Cargo.toml and Cargo.lock"
    requirement: SPK-02
    verification:
      - kind: unit
        ref: "cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml tests::version_constants_match_the_pins_in_cargo_toml_and_cargo_lock"
        status: pass
      - kind: other
        ref: "diff of both Cargo.lock files ignoring the root name line, exit 0"
        status: pass
    human_judgment: false
  - id: D2
    description: "Bounded stdin protocol: malformed, oversized, overflowing or short requests exit 2 with fixed messages; empty files list gives a valid response"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "cargo test protocol::tests (16 tests) and tests::a_rejected_request_is_a_failure_with_exit_code_two"
        status: pass
    human_judgment: false
  - id: D3
    description: "Length-preserving whole-word macro blanking that skips preprocessor lines"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "cargo test blank::tests (10 tests)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Per-file deadline: a timed-out file is labelled timeout with no types and the parser is reset so the next file on the same thread parses correctly"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "cargo test extract::deadline (5 tests), including the reset test, which fails when parser.reset() is removed"
        status: pass
    human_judgment: false
  - id: D5
    description: "Class and struct extraction on the measured tree-sitter-cpp 0.23.4 shapes (names, scope, bases, access, keys, signatures, spans, preprocessor conditionals, ERROR nodes)"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "cargo test extract::tests (23 tests)"
        status: pass
    human_judgment: false

duration: 38min
completed: 2026-11-16
status: complete
---

# Phase 1 Plan 01: Rust tree-sitter sidecar Summary

**A pinned Rust sidecar (`vdiff-cpp-ts`, tree-sitter 0.27.0 + tree-sitter-cpp 0.23.4) that reads a bounded stdin manifest, blanks export macros length-preservingly, parses each file under a wall-clock deadline and returns classes and structs with access-annotated members, byte spans and a per-file status.**

## Performance

- **Duration:** 38 min
- **Started:** 2026-11-16T20:27:00Z (approximate)
- **Completed:** 2026-11-16T21:05:00Z
- **Tasks:** 2
- **Files created:** 8 (6 under `extractors/cpp-ts`, plus `.gitignore` and `.gitattributes`)
- **Tests:** 65 cargo tests, all passing, no compiler warnings

## Accomplishments

- Crate seeded from the S2 trial with the ADR-001 dependency graph unchanged; `cargo build --release --locked` and `cargo test --locked` pass (release build 50 s with a warm registry cache).
- Protocol v0 as planned: header cap 8 MiB, total cap 512 MiB, checked length sum, `parse_budget_ms` 1 to 600000 (default 10000), fixed-message exit 2, fixed-line panic hook, response with `schema`, both versions, sorted distinct `macros_blanked` and `files` in request order. A real-binary run over stdin returned the expected JSON for `namespace shop { class EXPORT_API Foo : public Base { int x; }; }` with `EXPORT_API` blanked, and a garbage request exited 2 with `request header is not valid`.
- `parse_bounded` implements the progress-callback deadline and resets the parser after every cancel. The reset-then-correct-parse test was shown to fail with the `reset()` call commented out, so it guards the behaviour it names.
- Extraction follows the measured grammar shapes (research Pitfall 3 and corrections C2 to C7): destructor and operator names from their node text, `reference_declarator` without a `declarator` field, function-pointer fields, members and types under `#if`/`#ifdef`/`#else`, `extern "C++"`, types directly under `ERROR`, local classes never entered, lossy UTF-8, 4096-byte signature cut on a character boundary, exact UTF-8 spans including after a byte-order mark.

## Task Commits

1. **Task 1: scaffold, pinned lock, protocol, blanking, deadline-bounded parse** - `6861335` (feat)
2. **Task 2 RED: failing extraction tests** - `e97cb8c` (test)
3. **Task 2 GREEN: type and member extraction** - `d661028` (feat)

## TDD Notes

- Task 2 followed RED then GREEN. RED evidence: 24 of the extraction-related tests failed against an `extract_types` stub returning an empty list, on assertions such as `left: []` / `right: ["class C", "struct S"]`, not on compile errors or zero discovered tests (the semantic assessment is that each failure was the planned assertion on the planned behaviour). The `gsd_run check tdd-red-evidence` classifier was not run: the plan is `type: execute`, so the plan-level gate does not apply, and Rust has no TAP/JUnit adapter for it.
- Task 1 is a single `feat` commit with its tests written alongside the implementation (scaffold, protocol and blanking tests, no separate RED commit). One RED probe was done by hand: removing `parser.reset()` makes the reset test fail.
- One RED test expectation was wrong and was corrected in GREEN: `a_type_that_parsed_beside_a_syntax_error_is_still_returned` used `@@@ ### $$$` as the garbage, which makes the grammar swallow the next class as a function definition. It now uses three measured shapes (a broken declaration between two classes, the same inside a namespace, and a class sitting directly under an `ERROR` node). The trees were printed while developing and only assertions were kept, as the plan instructs.

## Files Created

- `.gitignore`, `.gitattributes` - build and run-output ignore rules (`/out/`, `/vdiff-out*/`, `*.vdiff-out/`); seven `eol=lf` pins, no `text=auto`.
- `extractors/cpp-ts/Cargo.toml`, `Cargo.lock` - pinned graph (lock differs from the trial only in the root package name).
- `extractors/cpp-ts/src/main.rs` - `run(&[u8]) -> Result<Vec<u8>, Failure>`, rayon pool with 64 MiB stacks, version constants, version-guard test.
- `extractors/cpp-ts/src/protocol.rs` - request parsing, response types and writer.
- `extractors/cpp-ts/src/blank.rs` - macro blanking.
- `extractors/cpp-ts/src/extract.rs` - `parse_bounded`, `count_error_nodes`, `extract_types`.

## Decisions Made

See `key-decisions` in the frontmatter. Wire shape for plan 01-02 (matches its stated record layout): type has `kind`, `name`, `scope`, `bases`, `start_byte`, `end_byte`, `start_line`, `end_line`, `members`; member has `name`, `kind`, `access`, `key`, `signature` and the same four span numbers. Member spans are the whole declaration's span (so `n_` and `m_` in `int n_, m_;` share one span and differ by `key`).

## Deviations from Plan

None - plan executed exactly as written.

Behaviour the plan left open and this plan fixed (not deviations): the signature of a member from a multi-declarator declaration (shared specifiers plus own declarator), the exit code of internal failures (1), and the span of multi-declarator members (the whole declaration). Plan 01-02/01-06 should treat these as the protocol's behaviour.

## Authentication Gates

None.

## Issues Encountered

- Three test-only borrow errors in `protocol::tests` on the first compile (a request buffer dropped while a borrowing `Request` was still in use); fixed by binding the buffers. No production code was affected.
- A hand-written request in a manual run had a wrong `len` (58 instead of 65), which truncated the file and gave `error_nodes: 1`; the corrected request returned the full type. Not a defect.

## Known Stubs

None. `extract_types` was a stub only between the RED and GREEN commits.

## Limitations for later plans

- Recursion depth is not capped in `extract_types` (the depth cap belongs to plan 01-06); worker threads have 64 MiB stacks and the error-node count is iterative.
- Macro calls with arguments inside class bodies (`DECLARE_X(Foo)`) produce bogus members that `--blank` cannot fix (C36, measured). Conversion operators, enums, templates members, aliases and nested-type members yield no member here (plans 01-06).
- `Cargo.toml` is not pinned to LF in `.gitattributes` (the plan lists specific entries); the version guard test normalises CRLF so a CRLF checkout does not break it.

## Threat Flags

None. The only new surface is stdin parsing, already covered by T-01-04, T-01-27 and T-01-32 in the plan's threat model; paths are never opened and the panic hook prints no payload.

## Next Phase Readiness

Ready for plans 01-02 and 01-03: the binary builds with `cargo build --release --locked --manifest-path extractors/cpp-ts/Cargo.toml` and speaks protocol v0 as described above. `git ls-files` shows no `target/` path.

## Self-Check: PASSED

- Files present: `.gitignore`, `.gitattributes`, `extractors/cpp-ts/Cargo.toml`, `Cargo.lock`, `src/main.rs`, `src/protocol.rs`, `src/blank.rs`, `src/extract.rs` (all tracked, none under `target/`).
- Commits present on this branch: `6861335`, `e97cb8c`, `d661028` (3 measured from the plan-start ledger `df40045..HEAD` before this SUMMARY's own commit).
- Plan verification re-run: full cargo suite 65 passed, 0 failed; `extract::deadline` 5 passed; `extract::tests` 23 passed; lock diff ignoring root name exit 0; `grep -c "^/out/" .gitignore` = 1; `grep -c "eol=lf" .gitattributes` = 7; release build `Finished`.
