---
phase: 01-offline-tracer
plan: 06
subsystem: extraction
tags: [rust, tree-sitter, tree-sitter-cpp, sidecar, enums, nested-types, macro-blanking, depth-cap, robustness]

requires:
  - phase: 01-offline-tracer
    provides: "plan 01-01 sidecar (protocol v0, blanking, deadline-bounded parse, class/struct extraction); plan 01-03 CLI pipeline, SyntheticRepo, sidecar collection fixture"
provides:
  - "enum extraction (enumerators as members of kind enumerator with access none, underlying type in bases)"
  - "nested class, struct and enum types with the enclosing type in scope"
  - "alias members (using, typedef), member templates with the template header in the signature, conversion operators"
  - "depth cap of 128 on every recursive walk, with each skipped subtree added to the file's error_nodes"
  - "hardened blanking tests and two CLI-level test classes (Fixture B, blanking end to end)"
affects: [01-07, 01-08, 01-10]

plan_head_before: 510bac9046800016e9b8575fc567d5db179fa27a
plan_head_after: 56b75ea43fb6916119e171becadd121319957ec1

actuals:
  tokens: 14400
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Walk struct (src, scope, out, skipped) for the type descent; free functions with an explicit depth and skip counter for member and enumerator walks"
    - "the sidecar reports depth-cap skips through the existing error_nodes field, so no protocol or C# change was needed"
    - "characterisation tests named known_gap_* record labelled limitations without asserting the grammar's bogus output"

key-files:
  created:
    - tests/Vdiff.Tests/Extraction/RichFixtureEndToEndTests.cs
    - tests/Vdiff.Tests/Extraction/BlankingEndToEndTests.cs
  modified:
    - extractors/cpp-ts/src/extract.rs
    - extractors/cpp-ts/src/blank.rs
    - extractors/cpp-ts/src/main.rs
    - extractors/cpp-ts/src/protocol.rs

key-decisions:
  - "Depth counts one level per container descent (namespace body, type body, template, linkage block, preprocessor block, ERROR); a declaration or field declaration is entered without counting a level, so the members of a type sitting at the cap are not each reported as a skipped subtree"
  - "A type found exactly at the cap is kept with its own members, but its body is not searched for nested types, so the file gains one error node"
  - "Skips are added to error_nodes in main.rs (count_error_nodes + skipped) instead of adding a protocol field, keeping the JSON shape of plan 01-01 unchanged"
  - "A conversion operator is a method named from `operator` through the target type up to its function declarator (`operator bool`, `operator int*`), key adds parameters and cv-qualifiers"
  - "A member template yields the members of its wrapped declaration with the template's span and the header text in front of the signature; the key does not include the header"
  - "A variable declared together with a nested type (`struct In { } inst;`) has the type body elided from its signature (`struct In inst`)"

patterns-established:
  - "New member kinds reuse the existing kind string (alias, enumerator) and access `none`; the C# side needed no change (Mermaid draws `none` as public)"

requirements-completed: [TRC-02, SPK-02]

coverage:
  - id: D1
    description: "The sidecar extracts enums with enumerators and underlying type, nested types, aliases, member templates, conversion operators, multi-declarator and function-pointer fields, with access sections"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml extract::tests (39 passed)"
        status: pass
      - kind: e2e
        ref: "tests/Vdiff.Tests/Extraction/RichFixtureEndToEndTests.cs (2 tests, real CLI and sidecar)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Length-preserving whole-word macro blanking proven at byte level and through --blank: type missing without it, found with it, macros_blanked sorted and distinct, UTF-8 byte spans exact, BOM offset of exactly 3"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml blank::tests (20 passed)"
        status: pass
      - kind: e2e
        ref: "tests/Vdiff.Tests/Extraction/BlankingEndToEndTests.cs (5 tests)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Two known gaps are labelled, not fixed: typedef of an anonymous struct yields no type; a macro call with arguments in a class body can yield a bogus member that --blank cannot fix"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "extract::tests::known_gap_a_typedef_of_an_anonymous_struct_yields_no_type, extract::tests::known_gap_a_macro_call_with_arguments_in_a_class_body_keeps_the_class_and_the_real_member"
        status: pass
    human_judgment: false
  - id: D4
    description: "Honest labelling and robustness: error nodes labelled, depth cap of 128 labels the file, 10,000 nested namespaces, 200,000 parentheses, 20,000 template arguments, random bytes, CRLF, non-ASCII identifiers and Latin-1 comments complete without a crash or content leak"
    requirement: SPK-02
    verification:
      - kind: unit
        ref: "cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml extract::robustness (14 passed)"
        status: pass
    human_judgment: false

duration: 15min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 06: Sidecar depth and hardening Summary

**The Rust sidecar now extracts enums, nested types, aliases, member templates and conversion operators, caps every recursive walk at 128 levels with each skipped subtree labelling the file through `error_nodes`, and macro blanking is proven length-preserving at byte level and through the real CLI.**

## Performance

- **Duration:** 15 min
- **Started:** 2026-11-17T05:52:35Z (approximate; exploration began earlier)
- **Completed:** 2026-11-17T06:07:00Z
- **Tasks:** 3
- **Files modified:** 6 (2 created, 4 modified)

## Accomplishments

- Enums (`enum` and `enum class`, with the `base` field's own text as `bases`) and their enumerators; nested class, struct and enum types with the enclosing type in `scope`, parents before nested types, found under `#if`/`#ifdef`/`#else` too.
- Alias members for `using` and `typedef`, member templates (header kept in `signature`, key unchanged), conversion operators (`operator bool() const`), type-with-variable declarations without the type body in the signature.
- Depth cap `MAX_DEPTH = 128` on the type descent, the member walk, the enumerator walk and declarator resolution; `extract_with_skips` returns the skip count and `main.rs` adds it to the file's `error_nodes`. A mutation check (cap set to 128,000,000) makes `ten_thousand_nested_namespaces_complete_and_label_the_file` overflow the stack, so the test guards the cap.
- 14 `extract::robustness` tests: error-node labelling, the cap boundary (127 levels clean, 128 keeps the type but labels, 129 skips and labels), 10,000 nested namespaces, 5,000 nested types and `#if` blocks, 200,000 nested parentheses (parses `ok`, 0 error nodes), 20,000 nested template arguments (parses `ok`, 1 error node), 40 random-byte files through `run()` (always one JSON object), empty file, CRLF line numbers, non-ASCII identifiers with exact byte offsets, a Latin-1 byte in a comment, fixed-message failures.
- `BlankingEndToEndTests` (5): without `--blank` `class shop::Foo` is missing and the file shows `error_nodes >= 1` plus a diff warning; with it the type, its base `Base` and its members are found; repeated and unordered `--blank` flags give sorted distinct `macros_blanked` in both models; member and type spans equal the true UTF-8 offsets in the committed bytes (a multi-byte comment precedes them); a BOM shifts every span by exactly 3.
- `RichFixtureEndToEndTests` (2): Fixture B (generic `shop::Box` class template, `enum class Mode : unsigned char`, nested `Slot`, operators, aliases). The head adds `Medium`, drops `const` from `operator bool` and adds an alias; `diff.json` reports `Size` added, `operator bool()` added, `operator bool() const` removed, `Medium` added (access `none`), `Slot` unchanged.

## Task Commits

1. **Task 1 RED: failing tests for enum, nested type, alias and special member extraction** - `d718f6d` (test) - 11 cargo tests and 2 CLI tests failed on the planned assertions
2. **Task 1 GREEN: enum, nested type, alias, member template and conversion operator extraction** - `0290cdb` (feat)
3. **Task 2: macro blanking at byte level and through the CLI** - `385f903` (test)
4. **Task 3: robustness tests and exact depth-cap labelling** - `56b75ea` (feat)

**Plan metadata:** recorded in the docs commit that follows this file.

## TDD Notes

- Task 1 followed RED then GREEN. RED evidence: against the 01-01 code, 11 of 39 `extract::tests` failed and both Fixture B tests failed on assertions (`left: []` style type and member list mismatches, `Types` missing `enum shop::Box::Mode`), not on compile errors; semantic assessment: each failure was the planned assertion. The plan is `type: execute`, so the `check tdd-red-evidence` classifier does not apply (and has no Rust adapter).
- Task 2 had no RED phase in the strict sense: the blanking behaviour was built in plan 01-01, so the new unit and CLI tests passed on first run. They are characterisation and hardening tests; the `without --blank` test shows the contrast the plan asks for. No production change was needed in `blank.rs`.
- Task 3: the depth parameter and skip counting were written during the Task 1 rewrite (the descent had to be restructured anyway), so the robustness tests were not RED against the old code. Two checks stand in for RED: the first run of the new module failed one test (`nesting_up_to_the_cap...`, left 1 right 0) which exposed a real labelling bug (see Deviations), and the mutation of `MAX_DEPTH` described above overflows the stack.

## Files Created/Modified

- `extractors/cpp-ts/src/extract.rs` - enum, nested type, alias, template-member and conversion-operator extraction; `Walk` struct; depth cap; `extract_with_skips`; 14 new `robustness` and 16 new `tests` cargo tests (one existing test renamed and corrected)
- `extractors/cpp-ts/src/blank.rs` - ten new tests (no behaviour change)
- `extractors/cpp-ts/src/main.rs` - `error_nodes = count_error_nodes(root) + skipped`
- `extractors/cpp-ts/src/protocol.rs` - doc comments only (kinds now include enum, alias, enumerator; access `none`)
- `tests/Vdiff.Tests/Extraction/RichFixtureEndToEndTests.cs` - Fixture B through `CliApp.Run`
- `tests/Vdiff.Tests/Extraction/BlankingEndToEndTests.cs` - `--blank` and span proofs through `CliApp.Run`

## Decisions Made

See `key-decisions`. Two behaviours the plan left open: an enumerator's span is its own node (not the enum's), and a member template takes the span of the whole `template_declaration`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `main.rs` edited, outside `files_modified`**
- **Found during:** Task 3 (and needed from the Task 1 GREEN build)
- **Issue:** The plan requires each depth-skipped subtree to add 1 to the file's `error_nodes`, but `error_nodes` is assembled in `main.rs` from `count_error_nodes` alone, and `extract_types` returned only types.
- **Fix:** Added `extract_with_skips` returning `(types, skipped)` and changed `process_file` to report `count_error_nodes(root) + skipped`. `extract_types` is now test-only. No protocol field was added.
- **Files modified:** `extractors/cpp-ts/src/main.rs`
- **Committed in:** `0290cdb`

**2. [Rule 3 - Blocking] `protocol.rs` doc comments updated, outside `files_modified`**
- **Found during:** Task 1
- **Issue:** The comments on `TypeInfo.kind`, `MemberInfo.kind` and `MemberInfo.access` named only the tracer values and would now mislead.
- **Fix:** Comment text only; no code change.
- **Files modified:** `extractors/cpp-ts/src/protocol.rs`
- **Committed in:** `0290cdb`

**3. [Rule 1 - Bug] A declaration counted as a depth level**
- **Found during:** Task 3 (`nesting_below_the_cap...` failed on first run)
- **Issue:** Descending into each `field_declaration` incremented the depth, so a type sitting just inside the cap reported one skipped subtree per member and labelled the file wrongly.
- **Fix:** `declaration`, `type_definition` and `field_declaration` are entered at the same depth; a type specifier inside them is still counted by `collect_type`.
- **Files modified:** `extractors/cpp-ts/src/extract.rs`
- **Verification:** the cap boundary tests (127 clean, 128 keeps the type and labels, 129 skips) and the full suite pass
- **Committed in:** `56b75ea`

**4. [Rule 1 - Bug] An existing 01-01 test asserted the gap this plan closes**
- **Found during:** Task 1
- **Issue:** `a_conversion_operator_or_other_unhandled_child_yields_no_member` expected a conversion operator to yield no member.
- **Fix:** Renamed to `friend_using_and_static_assert_children_yield_nothing` and dropped the conversion operator from it; conversion operators have their own tests now.
- **Committed in:** `d718f6d`

---

**Total deviations:** 4 (2 blocking-file scope, 2 bugs)
**Impact on plan:** All necessary for correctness; no scope creep, no dependency change (`Cargo.lock` and `Cargo.toml` diff empty).

## Issues Encountered

- The plan's stated baselines were off (it said twelve `extract::tests` and six `blank::tests` exist; there were 23 and 10). The verify thresholds (24, 14, 8) are met with margin: 39, 20 and 14.
- The plan's Fixture B text says the head "changes a conversion operator's constness"; because the member key includes cv-qualifiers, that shows in the diff as one member removed and one added, not as modified. The test asserts that behaviour.
- The Mermaid writer draws access `none` (enumerators) as `+`; the output plan (01-08) may want to special-case it.

## Known Limitations (labelled, D-11)

- `typedef struct { int a; } Name;` yields no type (anonymous struct) - `known_gap_a_typedef_of_an_anonymous_struct_yields_no_type`.
- A macro call with arguments inside a class body (`DECLARE_X(Foo)`) makes the grammar emit a bogus field named after the argument; `--blank` cannot fix it because the call is not a bare name. The class and the real members are still extracted - `known_gap_a_macro_call_with_arguments_in_a_class_body_keeps_the_class_and_the_real_member`.
- Unions are not extracted; a union inside a type is not searched for nested types.
- Template parameters of a class template are not tracked; a member template's overloads that differ only in the template header share a key (the C# side disambiguates with `#2`).

## Known Stubs

None.

## Authentication Gates

None.

## Threat Flags

None. No new network, auth or file-access surface; T-01-14, T-01-15 and T-01-16 are covered by the depth cap, the unchanged blank-name validation and the skip-to-`error_nodes` labelling. `git diff --stat 510bac9 -- extractors/cpp-ts/Cargo.lock extractors/cpp-ts/Cargo.toml` is empty (T-01-SC).

## Verification Run

- `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: 105 passed, 0 failed (baseline 65); `extract::tests` 39, `blank::tests` 20, `extract::robustness` 14.
- `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo`: 259 passed, 0 failed (baseline 252; +5 blanking, +2 Fixture B), 3 min 20 s, against a freshly rebuilt release sidecar.

## Next Phase Readiness

Ready for 01-07 and 01-08. The sidecar JSON shape is unchanged; new values are `kind` `enum`, member kinds `alias` and `enumerator`, and access `none`. Output code (01-08) should expect them.

## Self-Check: PASSED

- Files present: `extractors/cpp-ts/src/extract.rs`, `blank.rs`, `main.rs`, `protocol.rs`, `tests/Vdiff.Tests/Extraction/RichFixtureEndToEndTests.cs`, `BlankingEndToEndTests.cs`.
- Commits present on this branch: `d718f6d`, `0290cdb`, `385f903`, `56b75ea` (4 measured from the plan-start ledger `510bac9..HEAD` before this SUMMARY's own commit).
- Plan verification re-run: full cargo suite 105 passed; `extract::tests` 39, `blank::tests` 20, `extract::robustness` 14; full dotnet suite 259 passed; `Cargo.lock` and `Cargo.toml` unchanged.
