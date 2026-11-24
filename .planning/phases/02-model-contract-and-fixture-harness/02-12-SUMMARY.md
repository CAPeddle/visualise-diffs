---
phase: 02-model-contract-and-fixture-harness
plan: 12
subsystem: diff
tags: [csharp, vdiff-diff-v1, canonical-order, edge-identity, determinism]

requires:
  - phase: 02-model-contract-and-fixture-harness
    provides: "02-03 v1 model records, Canonical, BaseResolver; 02-11 RunMeta and run.meta.json (commit ids and tool versions kept out of the canonical files)"
provides:
  - "vdiff-diff/v1 records in contract declaration order (EdgeCounts, PackageDiff, RenameSource, RenameScore, MemberJaccard, EdgeDiff with to_before/to_after); DiffFile.SchemaName = vdiff-diff/v1; the v0-tracer diff name is deleted"
  - "States.Renamed and States.BodyChanged constants"
  - "ModelDiffer keyed by (language, id); a member is modified by access, signature or sorted modifiers; before/after presence rules; no type-level attribute change (D-20)"
  - "Canonical.Canonicalise(DiffFile) over every v1 diff collection and Canonical.EdgeIdentity (D-23)"
  - "MermaidWriter ordered by language then id and using BaseResolver.Strip"
  - "DeterminismTests.Same_trees_committed_twice_give_identical_canonical_files_and_differ_only_in_run_meta"
affects: [02-05, 02-06, 02-07, 02-08, 02-10, 05, 06, 07]

actuals:
  tokens: 15053
  tasks: 2
  commits: 4
plan_head_before: 2d0feae4022309ded3b4eee28ba99e80fa59b687
plan_head_after: e0542205801d98cf77a3f1b4ec66ccc2f33c400b

tech-stack:
  added: []
  patterns:
    - "The differ indexes with Canonical comparers and returns Canonical.Canonicalise(diff); OutputWriter.Serialize canonicalises again"
    - "Edge identity is a comparer in Canonical (kind, from, to_name, via); the full edge order is that identity, then the resolved target"
    - "Separation proof compares canonical files as raw bytes with no field stripped"

key-files:
  created: []
  modified:
    - src/Vdiff.Core/Model/ModelTypes.cs
    - src/Vdiff.Core/Model/Canonical.cs
    - src/Vdiff.Core/Diff/ModelDiffer.cs
    - src/Vdiff.Core/Output/MermaidWriter.cs
    - tests/Vdiff.Tests/Diff/ModelDifferTests.cs
    - tests/Vdiff.Tests/Diff/ModelBuilderTests.cs
    - tests/Vdiff.Tests/Output/MermaidWriterTests.cs
    - tests/Vdiff.Tests/Output/HtmlWriterTests.cs
    - tests/Vdiff.Tests/Output/DeterminismTests.cs
    - tests/Vdiff.Tests/Extraction/RichFixtureEndToEndTests.cs
    - tests/Vdiff.Tests/Scope/IncludeGlobEndToEndTests.cs
    - tests/Vdiff.Tests/TracerEndToEndTests.cs

key-decisions:
  - "TypeDiff has no access or modifiers pair and a type is modified only by bases or members (D-20); the differ never compares a type's access, modifiers, partial or files"
  - "Canonical.Edges is built from Canonical.EdgeIdentity (identity, then to.language, to.id, tie-break); EdgeDiff rows sort by the same identity through a private projection, then to, then state"
  - "ModelDiffer's member order now includes modifiers, so two members that differ only in modifiers are never a tie"

patterns-established:
  - "Serialize and the differ both canonicalise; a hand-built diff in any order serialises to one byte string"

requirements-completed: [R-10, R-23]

coverage:
  - id: D1
    description: "The tracer writes vdiff-diff/v1 (language on every type, empty packages and edges, Phase 5 counts zero) and the v0-tracer diff name is gone from src and tests"
    requirement: "R-10"
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs#Cli_run_on_fixture_A_writes_every_output_file_and_leaves_the_repository_untouched"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelDifferTests.cs#Packages_and_edges_are_empty_and_the_phase_five_counts_are_zero"
        status: pass
      - kind: other
        ref: "grep -rn v0-tracer src tests --include=*.cs prints nothing"
        status: pass
    human_judgment: false
  - id: D2
    description: "Types are keyed by (language, id): a cpp and a csharp type with one id diff separately and changing one leaves the other unchanged"
    requirement: "R-10"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelDifferTests.cs#A_cpp_and_a_csharp_type_with_the_same_id_are_two_types_and_changing_one_leaves_the_other_unchanged"
        status: pass
    human_judgment: false
  - id: D3
    description: "Member presence rules and modifiers-only modification; no type-level attribute change is reported (D-20)"
    requirement: "R-10"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelDifferTests.cs#A_member_whose_only_change_is_its_modifiers_is_modified, #An_added_member_has_only_after_fields_a_removed_one_only_before_fields_and_a_modified_one_all_six, #A_type_whose_only_change_is_its_access_or_modifiers_or_partial_files_stays_unchanged_and_unlisted, #A_type_whose_access_and_a_member_both_change_is_modified_because_of_the_member_and_lists_no_type_level_attribute"
        status: pass
    human_judgment: false
  - id: D4
    description: "Canonical.EdgeIdentity leaves the resolved to out of an edge's identity, proven on two real ModelBuilder models (D-23)"
    requirement: "R-23"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelBuilderTests.cs#An_unrelated_type_with_the_same_suffix_changes_an_edges_target_but_not_its_identity, #EdgeIdentity_covers_kind_from_to_name_and_via_but_not_to"
        status: pass
    human_judgment: false
  - id: D5
    description: "Every v1 diff collection is ordered by Canonical; warnings are ordinal; MermaidWriter shares the language-then-id order and BaseResolver.Strip"
    requirement: "R-23"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelDifferTests.cs#Canonicalising_a_reverse_ordered_diff_gives_the_bytes_of_the_forward_ordered_one_and_orders_each_collection, #Warnings_come_out_in_ordinal_order_across_both_sides_and_all_kinds"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Output/MermaidWriterTests.cs#Types_are_ordered_by_language_then_id_so_a_cpp_type_is_listed_before_a_csharp_type_with_the_same_id"
        status: pass
    human_judgment: false
  - id: D6
    description: "The same trees committed in two repositories under different commits give byte-identical canonical files; run.meta.json differs only in the two commit ids; no canonical file holds a commit id or a tool version (D-04)"
    requirement: "R-23"
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Output/DeterminismTests.cs#Same_trees_committed_twice_give_identical_canonical_files_and_differ_only_in_run_meta"
        status: pass
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs#The_canonical_files_hold_neither_commit_id_nor_a_tool_version"
        status: pass
    human_judgment: false

duration: 31min
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 12: v1 diff records, language-keyed differ and canonical diff order Summary

**The tracer now writes `vdiff-diff/v1` with types matched by (language, id), members modified by access, signature or modifiers, no type-level attribute change (D-20), every diff collection ordered by `Canonical`, the D-23 edge identity as `Canonical.EdgeIdentity`, and a byte-level proof that identical trees under different commits give identical canonical files.**

## Performance

- **Duration:** about 31 min of wall clock, of which the full suite took 14 min 24 s
- **Started:** 2026-11-21T07:46:00Z (approximate; the ledger file was created at 07:53Z, at the first commit)
- **Completed:** 2026-11-21T08:20:00Z (approximate)
- **Tasks:** 2 (each as RED, GREEN)
- **Files modified:** 12 (none created)

## Accomplishments

- Diff records rewritten in the Diff contract table's declaration order: `TypeCounts` and `MemberCounts` gained renamed and body_changed, `EdgeCounts`, `PackageDiff`, `RenameSource`, `RenameScore`, `MemberJaccard` and `EdgeDiff` (with `ToBefore`/`ToAfter`) were added, `MemberDiff` gained modifiers and `RenamedFromKey`, `TypeDiff` gained language and the rename fields and has no access or modifiers pair. `DiffFile.SchemaName` is `vdiff-diff/v1`; no `v0-tracer` string is left in `src` or `tests`.
- `ModelDiffer` indexes types by language plus id with `Canonical.Types`, members with `Canonical.Members`, fills `*_before` and `*_after` by the presence rules, leaves `packages`, `edges`, renamed and body_changed empty or zero, and returns `Canonical.Canonicalise(diff)`. The class doc comment now states the D-20 rule and the Phase 5 boundary, and says `#path`/`@n` (D-21) instead of `#n`.
- `Canonical.Canonicalise(DiffFile)` orders warnings, bases and member modifiers by value, packages by name then state, types by language then id, members by key and edges by identity, target and state. `Canonical.EdgeIdentity` is kind, from, to_name and via only.
- `MermaidWriter` orders shown types by language then id and calls `BaseResolver.Strip`; its private `BaseName` copy is deleted.
- The separation proof and a canonical-files check (no commit id, no `0.27.0`) are in place.

## Task Commits

1. **Task 1 RED: failing tests for the language-keyed diff, member modifiers and ordinal warnings** - `596dde2` (test)
2. **Task 1 GREEN: v1 diff records and a differ keyed by language and id** - `141a823` (feat)
3. **Task 2 RED: failing tests for canonical diff order, edge identity and writer order** - `e1e4490` (test)
4. **Task 2 GREEN: canonical diff order, edge identity and a writer on the shared order** - `e054220` (feat)

**Plan metadata:** committed separately as `docs(02-12): complete v1 diff plan`.

## Files Created/Modified

- `src/Vdiff.Core/Model/ModelTypes.cs` - the v1 diff records, `States.Renamed`, `States.BodyChanged`
- `src/Vdiff.Core/Model/Canonical.cs` - diff canonicalisation, `EdgeIdentity`, edge-diff and package comparers
- `src/Vdiff.Core/Diff/ModelDiffer.cs` - language-keyed differ on the shared comparers
- `src/Vdiff.Core/Output/MermaidWriter.cs` - language-then-id order, `BaseResolver.Strip`
- Eight test files moved to the v1 constructors, with the new facts added to `ModelDifferTests`, `ModelBuilderTests`, `MermaidWriterTests`, `DeterminismTests` and `TracerEndToEndTests`.

## TDD Gate Compliance

- **Task 1 RED** (`596dde2`): three facts on the existing API so the assembly compiled. Run: `Failed: 3, Passed: 26, Total: 29`. Cross-language: `Assert.Equal` Expected 1, Actual 0 listed types. Modifiers-only: the type was unchanged, so `Assert.Single` failed. Warnings: `base: ... with parse errors` came before `base: ... not modelled`. Semantic assessment: each target ran and failed on the planned behaviour, not on compile, discovery or setup.
- **Task 1 GREEN** (`141a823`): records, differ, `Canonical` type order by language, the eight classes' constructor updates and four new facts (presence, D-20 guard, D-20 member-modified, packages and edges). Eight edited classes: `Passed: 136, Failed: 0`.
- **Task 2 RED** (`e1e4490`): six facts plus a placeholder `Canonical.EdgeIdentity = Edges`. Run: `Failed: 4, Passed: 2, Total: 6`. The four failures were assertions: `EdgeIdentity` compare gave -25 and 1 instead of 0, the Mermaid output listed `removed` before `added`, and the reversed diff serialised differently. The other two (separation and canonical-files) passed at once, which the plan predicted because 02-11 and 02-03 had already removed the commit ids.
- **Task 2 GREEN** (`e054220`): real `EdgeIdentity`, diff canonicalisation, differ and writer changes. Nine classes: `Passed: 146, Failed: 0`.
- **REFACTOR:** none.
- `workflow.tdd_mode` is not enabled and the plan is `type: execute`, so `gsd_run check tdd-red-evidence` was not run and no evidence file was written.
- The D-20 facts, the presence rules, the packages/edges fact and the TracerEndToEnd schema assertions landed with GREEN because they need the v1 records to compile. The two D-20 facts are guards (they pass against the old differ too), not RED evidence.

## Verification

- Full suite `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "Category!=MermaidCli"` on `e054220`: `Passed: 648, Failed: 0, Total: 648` (14 m 24 s). 02-03 ended at 635 (634 plus the fact added in `02d9a2c`); this plan added 13 facts. The `TraceRunScriptTests` flake did not recur.
- `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: 105 passed, 0 failed (no Rust change).
- The D-23 fact and the separation fact each pass alone (`Passed: 1`).
- `grep -rn "v0-tracer" src tests --include=*.cs`: nothing. Task acceptance greps: `vdiff-diff/v1` 1, the three records 3, `BodyChanged` 1, `ToBefore` 1, `ToAfter` 1, `string? AccessBefore` declarations 1, separation fact 1, `BaseResolver.Strip` 1, `Canonical.` in the differ 7, `static int CompareTypes` in the differ 0, `EdgeIdentity` in `Canonical.cs` 5, D-23 fact 1.
- Public-diff guard printed `clean` before all four commits. Every edited file holds LF only.

## Decisions Made

- `Canonical.cs` is touched in both tasks although only task 2 lists it: task 1's GREEN made the diff types order by language then id (otherwise the v1 diff would be mis-ordered between the two commits), and task 2's RED commit holds the `EdgeIdentity` placeholder.
- The differ's member order includes sorted modifiers, so two members that differ only in modifiers are never a tie.
- The D-20 doc comment lives on `ModelDiffer`, as the plan asked, so Phase 5 finds it where the rule is applied.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing coverage] A canonical-order fact for the whole diff**
- **Found during:** Task 2
- **Issue:** the plan's behaviours cover the edge identity and the writer, but nothing proved that `Canonicalise(DiffFile)` orders every v1 collection (packages, edges, warnings, modifiers) beyond the differ's own output, and the plan lists no such test although T-02-37 depends on it.
- **Fix:** added `Canonicalising_a_reverse_ordered_diff_gives_the_bytes_of_the_forward_ordered_one_and_orders_each_collection` on a hand-built diff in both orders.
- **Files modified:** tests/Vdiff.Tests/Diff/ModelDifferTests.cs
- **Committed in:** `e1e4490` (RED), `e054220` (GREEN)

**2. [Rule 3 - Consistency] Task 1 RED and Task 2 RED made to fail at run time**
- **Found during:** Tasks 1 and 2
- **Issue:** tdd.md rejects a build failure as RED. Task 1's tests would not compile against the new records, and task 2's D-23 facts need `EdgeIdentity`.
- **Fix:** Task 1's RED tests use only the existing API (the cross-language check reads the serialised JSON); Task 2's RED carries a placeholder `EdgeIdentity` equal to `Edges`, replaced in GREEN.
- **Committed in:** `596dde2`, `e1e4490`

---

**Total deviations:** 2 (1 missing coverage, 1 consistency). **Impact on plan:** no source change beyond the plan, no scope creep.

## Issues Encountered

- A bash heredoc containing C# `'\u0000'` literals failed to parse (the same class of problem 02-03 hit); the differ was rewritten with the Write tool and the remaining edits used script files.

## Known Stubs

None. Empty `packages` and `edges` and the zero renamed, body_changed and edge counts are the plan's deliberate Phase 5 deferral (R-13 to R-15), documented on `ModelDiffer`, not stubs.

## Deferred Items

- `DiffFile`'s doc comment still reads "the summary and the changed types only"; it predates packages and edges. Fix it with the next edit to `ModelTypes.cs` (committing it now would have put a source change after the full-suite run).
- Edge diffing, rename matching, body-changed members and package pins are Phase 5; it keys edges on `Canonical.EdgeIdentity`.

## Threat Flags

None. T-02-02 (separation test plus the canonical-files fact), T-02-08 (cross-language fact) and T-02-37 (`Canonicalise(DiffFile)` over every collection, applied by the differ and by `OutputWriter.Serialize`) are covered; the seeded-shuffle proof over the full diff sample is plan 02-06's.

## Next Phase Readiness

- 02-05 can write the diff schema from the Diff contract table; the record order here is the JSON key order. 02-06's freeze review has D-20 and D-23 as built.
- No builds or tests are running; `obj/` and `bin/` are free.

## Self-Check: PASSED

- Commits `596dde2`, `141a823`, `e1e4490`, `e054220` are ancestors of HEAD; `git rev-list --count` from the recorded base `2d0feae` gives 4.
- No file was created by this plan; every modified file exists and holds LF only.
- Acceptance greps and the plan's verification (full suite 648 of 648, cargo 105 of 105, no `v0-tracer` in source or tests) were re-run on `e054220`.

---
*Phase: 02-model-contract-and-fixture-harness*
*Completed: 2026-11-21*
