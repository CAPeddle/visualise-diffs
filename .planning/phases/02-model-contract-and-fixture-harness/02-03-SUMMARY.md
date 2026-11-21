---
phase: 02-model-contract-and-fixture-harness
plan: 03
subsystem: model
tags: [csharp, vdiff-model-v1, canonical-order, base-edges, determinism]

requires:
  - phase: 02-model-contract-and-fixture-harness
    provides: "02-11 RunMeta and run.meta.json (commit ids and tool versions out of the model); 02-01 OutputWriter.FileNames"
provides:
  - "vdiff-model/v1 records (ModelFile, ExtractionInfo, LanguageExtraction, ModelFileEntry, TypeModel with language/access/partial/files/modifiers, MemberModel with modifiers, EdgeModel, TypeRef) in contract declaration order"
  - "Languages, Modes and EdgeKinds constants; ModelFile.SchemaName = vdiff-model/v1; the v0-tracer model name is gone"
  - "Canonical.Canonicalise(ModelFile|DiffFile|RunMeta) and the Types/Members/Edges comparers; OutputWriter.Serialize canonicalises every ModelFile, DiffFile and RunMeta"
  - "BaseResolver.Strip and BaseResolver.Resolve (D-16 exact/'::'-suffix rule of the syntax-only C++ extractors)"
  - "ModelBuilder.Build(response, unmodelled, macros) with D-21 path-suffixed repeated ids and one base edge per written base"
affects: [02-05, 02-06, 02-07, 02-08, 02-10, 02-12]

actuals:
  tokens: 10200
  tasks: 2
  commits: 5
plan_head_before: 914c3f70191f5dd6e2597466cd2866084d32faf2
plan_head_after: 06e50ac89c574f338af6511a9cd3a10f0a984696

tech-stack:
  added: []
  patterns:
    - "One canonical ordering module; comparers are total and tie-break on the element serialised after its nested collections are canonicalised"
    - "Repeated ids: '#<path>' on every definition once an id repeats, '@n' only for a repeat inside one file (D-21)"
    - "Base edge resolution is the syntax-only C++ rule, kept out of the frozen schema (D-16 clarification)"

key-files:
  created:
    - src/Vdiff.Core/Model/Canonical.cs
    - src/Vdiff.Core/Model/BaseResolver.cs
  modified:
    - src/Vdiff.Core/Model/ModelTypes.cs
    - src/Vdiff.Core/Model/ModelBuilder.cs
    - src/Vdiff.Core/Pipeline/TracerPipeline.cs
    - src/Vdiff.Core/Output/OutputWriter.cs
    - tests/Vdiff.Tests/Diff/ModelBuilderTests.cs
    - tests/Vdiff.Tests/ModelAssemblyTests.cs
    - tests/Vdiff.Tests/Diff/ModelDifferTests.cs
    - tests/Vdiff.Tests/Extraction/BlankingEndToEndTests.cs
    - tests/Vdiff.Tests/Extraction/RichFixtureEndToEndTests.cs
    - tests/Vdiff.Tests/TracerEndToEndTests.cs

key-decisions:
  - "BaseResolver.Resolve returns null for a match whose id equals the source type's id, so a self reference gives an edge without a target"
  - "Canonical tie-break serialises the element after canonicalising its nested collections, so a full tie never falls back to input order"
  - "The builder keeps only source-order sorting (which repeat takes @2); every output ordering is Canonical's"

patterns-established:
  - "Serialize<T> is the single canonicalising entry point; no caller can skip Canonical"

requirements-completed: [R-10, R-23]

coverage:
  - id: D1
    description: "The tracer writes vdiff-model/v1 models (language cpp, extractor cpp-ts, mode syntax-only, partial false, access and files omitted) with no commit id or tool version; the v0-tracer model name is deleted"
    requirement: "R-10"
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs#Cli_run_on_fixture_A_writes_every_output_file_and_leaves_the_repository_untouched"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelBuilderTests.cs#The_tracer_labels_everything_as_syntax_only_cpp_and_omits_what_it_cannot_fill"
        status: pass
    human_judgment: false
  - id: D2
    description: "Repeated type ids and member keys take #<path> and @<n> suffixes; adding an earlier-sorting file leaves the other ids unchanged (D-21)"
    requirement: "R-10"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelBuilderTests.cs#Adding_an_earlier_sorting_file_with_a_repeated_type_leaves_the_other_ids_unchanged, #Repeated_types_get_path_suffixed_ids_and_at_n_only_for_a_repeat_in_one_file"
        status: pass
    human_judgment: false
  - id: D3
    description: "One base edge per written base, resolved by a unique exact or '::'-suffix match, never to the type itself (D-16)"
    requirement: "R-10"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelBuilderTests.cs (five A_base_*/Two_exact_*/A_base_that_resolves_* facts)"
        status: pass
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs#Pipeline_on_fixture_A_returns_the_expected_diff_through_the_real_sidecar"
        status: pass
    human_judgment: false
  - id: D4
    description: "Every model, diff and run-meta collection is ordered by Canonical; serialised bytes do not depend on input order"
    requirement: "R-23"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelBuilderTests.cs#Canonicalising_a_reverse_ordered_model_gives_the_bytes_of_the_forward_ordered_one, #The_serialiser_is_the_single_entry_point_and_writes_canonical_order_by_itself, #Edges_sort_by_their_identity_before_the_resolved_target"
        status: pass
      - kind: unit
        ref: "dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter Category!=MermaidCli (634 passed, 0 failed)"
        status: pass
    human_judgment: false

duration: 21min
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 03: v1 model records, canonical ordering and base edges Summary

**The tracer now writes `vdiff-model/v1` (language, per-file mode, type access/partial/files fields, modifiers, a top-level edges array with one resolved-or-open base edge per written base, D-21 path-suffixed repeated ids), every collection is ordered by one `Canonical` module that `OutputWriter.Serialize` applies itself, and the `v0-tracer` model name and the model's commit and tool-version fields are gone.**

## Performance

- **Duration:** 21 min of wall clock, of which the full suite took 13 min 25 s
- **Started:** 2026-11-21T07:28:15Z
- **Completed:** 2026-11-21T07:49:24Z
- **Tasks:** 2 (each as RED, GREEN), plus one test added after GREEN
- **Files modified:** 12 (2 created, 10 modified)

## Accomplishments

- Model records rewritten in the Model contract table's declaration order. `ModelFile` has no commit, `ExtractionInfo` holds only `Languages`, `Duplicates` is positional. `Languages`, `Modes` and `EdgeKinds` constants added.
- `ModelBuilder.Build` lost its commit parameter, counts each id per language first and suffixes every definition of a repeated id with `#<path>` (and `@2`, `@3` for repeats inside one file); member keys follow the same rule. It emits one `EdgeModel` per written base through `BaseResolver.Resolve` and returns a canonical model.
- `Canonical` orders extraction languages, macros, files, unmodelled entries, types, members, edges (D-23 identity first), duplicates, modifiers, bases, type-level files, today's diff collections and run-meta extractors and tools. `OutputWriter.JsonOptions` is internal for its tie-break; `Serialize<T>` canonicalises `ModelFile`, `DiffFile` and `RunMeta`; `TracerPipeline` canonicalises both models before the differ.

## Task Commits

1. **Task 1 RED: failing tests for path-suffixed repeated ids (D-21)** - `4dcb5bc` (test)
2. **Task 1 GREEN: v1 model records with language, mode, type access and path-suffixed ids** - `c5c066f` (feat)
3. **Task 2 RED: failing tests for base edges and canonical order** - `73c0d7a` (test)
4. **Task 2 GREEN: canonical ordering module, base resolver and base edges** - `67c3f0b` (feat)
5. **Task 1 behaviour added after GREEN: tracer labelling and omitted type fields** - `06e50ac` (test)

**Plan metadata:** committed separately as `docs(02-03): complete v1 model plan`.

## TDD Gate Compliance

- **Task 1 RED** (`4dcb5bc`): the six D-21 assertions (rewritten repeated-type, repeated-member, same-byte-member, differ-warning tests, the new adjacency fact and `ModelAssemblyTests`' repeated-definition test) written against the current signature so the assembly compiled. Run: `Failed: 6, Passed: 7, Total: 13`; each target failed on its id or count assertion, not on a build error or at zero discovery. Semantic assessment: the tests ran and failed for the planned reason (old `#2` ids).
- **Task 1 GREEN** (`c5c066f`): records, builder, pipeline and the six edited test classes; `Passed: 49, Failed: 0`.
- **Task 2 RED** (`73c0d7a`): eight new `ModelBuilderTests` facts (five D-16 cases, three canonical-order checks) plus a placeholder `Canonical.Canonicalise(ModelFile)` that returns its input, so the suite compiled. Run: `Failed: 8, Passed: 9, Total: 17`, all eight on assertions. Semantic assessment: edges were `[]` and nothing was reordered, which is the planned failure.
- **Task 2 GREEN** (`67c3f0b`): real `Canonical`, `BaseResolver`, edges; filtered run `Passed: 58, Failed: 0`.
- **REFACTOR:** none.
- `workflow.tdd_mode` is not enabled and the plan is `type: execute`, so `gsd_run check tdd-red-evidence` was not run and no evidence file was written. The plan asked for task 1's tests to fail to compile; they were made to fail at run time instead so the RED is valid under the TDD reference.

## Verification

- Full suite `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "Category!=MermaidCli"` on `67c3f0b`: `Passed: 634, Failed: 0, Total: 634` (13 m 25 s). That is 625 from 02-11 plus nine new facts. The last commit (`06e50ac`) adds one test file edit and no source change; `ModelBuilderTests` reran alone after it: `Passed: 18, Failed: 0`. The `TraceRunScriptTests` flake did not recur.
- `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: 105 passed, 0 failed (no Rust change).
- `grep -rn "vdiff-model/v0-tracer" src tests --include=*.cs`: nothing. No `#n` id assertions remain in `tests`, and `scripts/` holds no `v0-tracer`, `tree_sitter` or `extraction.mode` reference.
- All Task 1 and Task 2 acceptance greps returned their expected counts. Public-diff guard printed `clean` before all six commits. Every edited file holds LF only.

## Decisions Made

- `BaseResolver.Resolve` compares ids, not references, to detect "the type itself".
- Canonical's tie-break canonicalises nested collections before serialising, so the final tie cannot depend on input order.
- The builder keeps only the source-order sorts that decide which repeat takes `@n`; all output ordering moved to `Canonical`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing coverage] Task 1's first behaviour bullet had no test**
- **Found during:** pre-completion review
- **Issue:** the bullet (every type language `cpp`, `Partial` false, `Access`/`Files` null, empty modifiers; files `cpp`/`syntax-only`; one `cpp-ts` language entry with sorted macros) was covered only indirectly.
- **Fix:** added `The_tracer_labels_everything_as_syntax_only_cpp_and_omits_what_it_cannot_fill`, including a nested type and unsorted macros, after GREEN.
- **Files modified:** tests/Vdiff.Tests/Diff/ModelBuilderTests.cs
- **Committed in:** `06e50ac`

**2. [Rule 3 - Consistency] Fixture A edge assertion in `TracerEndToEndTests` landed in Task 2's commit**
- **Found during:** Task 2
- **Issue:** `TracerEndToEndTests.cs` is in Task 1's file list, not Task 2's. A one-edge assertion (Widget to Gadget, `to_name` Gadget, none in base) belongs to the edge behaviour.
- **Fix:** added it in `67c3f0b`.
- **Committed in:** `67c3f0b`

**3. [Rule 3 - Consistency] Task 1 RED made to fail at run time**
- **Found during:** Task 1
- **Issue:** the plan says tests should fail to compile; tdd.md rejects a build failure as RED.
- **Fix:** see TDD Gate Compliance.

---

**Total deviations:** 3 (1 missing coverage, 2 consistency). **Impact on plan:** no source change beyond the plan, no scope creep.

## Issues Encountered

- A bash heredoc mangled `\n` in a patch script; the `OutputWriter` edit was made with the Edit tool instead. No effect on the result.

## Known Stubs

None. The RED-phase `Canonical` placeholder was replaced in `67c3f0b`.

## Deferred Items

- `ModelDiffer`'s class doc comment still says the builder makes ids unique with `#n` suffixes (D-21 replaced that). `ModelDiffer.cs` is rewritten in plan 02-12; fix the comment there.
- `MermaidWriter.BaseName` keeps its private copy until 02-12 points it at `BaseResolver.Strip`, as planned.

## Threat Flags

None. T-02-07 (paths stay repository-relative; the local-identity determinism test runs over every output file) and T-02-09 (one `Canonical` module used by the builder, the pipeline and the serialiser) are covered; the seeded-shuffle proof is plan 02-06's.

## Next Phase Readiness

- 02-12 can rewrite the diff side on this model: `TypeModel.Language`, `EdgeModel`, `Canonical.Edges`, `Canonical.Canonicalise(DiffFile)` (to extend) and `BaseResolver.Strip` are in place. `ModelDiffer` still keys types by id alone, which is correct while every type is `cpp`.
- 02-05 can transcribe the Model contract table; the record order here is the JSON key order. 02-06's freeze review has the D-19 fields and D-21 ids as shipped.
- No builds or tests are running; `obj/` and `bin/` are free.

## Self-Check: PASSED

- `src/Vdiff.Core/Model/Canonical.cs` and `src/Vdiff.Core/Model/BaseResolver.cs` exist.
- Commits `4dcb5bc`, `c5c066f`, `73c0d7a`, `67c3f0b`, `06e50ac` are on the branch; `git rev-list --count` from the recorded base gives 5.
- Plan verification run: full suite 634 of 634, cargo 105 of 105, no `v0-tracer` in source or tests, no tracked `bin/` or `obj/` files.

---
*Phase: 02-model-contract-and-fixture-harness*
*Completed: 2026-11-21*
