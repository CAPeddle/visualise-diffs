---
phase: 01-offline-tracer
plan: 07
subsystem: differ-and-scope
tags: [dotnet, differ, model-builder, scope-filter, include-globs, determinism, xunit]

requires:
  - phase: 01-offline-tracer
    provides: "plan 01-02 ScopeFilter and git object reader (mode-only changes already dropped), plan 01-03 ModelBuilder, ModelDiffer, CliApp and the sidecar collection"
provides:
  - "ModelFile.Duplicates: sorted list of repeated type and member definitions, always written (provisional vdiff-model/v0-tracer field)"
  - "ModelBuilder duplicate policy: #2, #3 suffixes in (file, start_byte) order for repeated types and, per type, repeated member keys"
  - "ModelDiffer with total ordinal ordering and input-order independence; per-side warnings split into parse errors, not modelled and duplicate <entry>"
  - "ScopeFilter that normalises a leading ./ and rejects empty patterns"
  - "83 new test cases: ModelDifferTests 26, ModelBuilderTests 8, ScopeFilterTests 38, IncludeGlobEndToEndTests 11 (theory rows counted individually)"
affects: [01-08, 01-09, 01-10]

plan_head_before: 231c4005ae339662081296666dedb0e5314bcadd
plan_head_after: b48282f82f9b854e97e1b0f7f304bdc20fe550ca

actuals:
  tokens: 13700
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Every ordering is a stable OrderBy with a total ordinal comparer; List.Sort and Array.Sort are not used for output"
    - "Keyed lookup in the differ orders its input first, so the first entry of a repeated key is chosen canonically"
    - "Duplicates on the model are strings (class shop::X (2 definitions); member f() of class shop::Y (3 definitions)), ordinal sorted"

key-files:
  created:
    - tests/Vdiff.Tests/Diff/ModelDifferTests.cs
    - tests/Vdiff.Tests/Diff/ModelBuilderTests.cs
    - tests/Vdiff.Tests/Scope/ScopeFilterTests.cs
    - tests/Vdiff.Tests/Scope/IncludeGlobEndToEndTests.cs
  modified:
    - src/Vdiff.Core/Diff/ModelDiffer.cs
    - src/Vdiff.Core/Model/ModelBuilder.cs
    - src/Vdiff.Core/Model/ModelTypes.cs
    - src/Vdiff.Core/Scope/ScopeFilter.cs
    - tests/Vdiff.Tests/ModelAssemblyTests.cs

key-decisions:
  - "Duplicate member entries read 'member <key> of <type id> (N definitions)', using the suffixed id of the owning type; type entries read '<type id> (N definitions)'"
  - "ModelFile.Duplicates is an init property with an empty default, so every existing ModelFile constructor call still compiles; in JSON it follows types"
  - "A modified member always carries both the access pair and the signature pair, not only the pair that changed, because plan 01-08 draws signature_after for every member that still exists"
  - "The differ chooses the first entry of a repeated id or key in a total ordinal order (id/key, file, start byte, end byte, then content); the builder already suffixes, so this only matters for hand-built models"
  - "A parameter rename stays removed plus added until Phase 5"
  - "Empty include patterns throw VdiffException from ScopeFilter; the command line already rejects them first"

patterns-established:
  - "Warnings per side in a fixed order (parse errors, not modelled, duplicates) rather than a final global sort"

requirements-completed: [TRC-02]

coverage:
  - id: D1
    description: "A member is modified on a changed signature or access; overloads, a const qualifier and a changed parameter list are different members; a changed base list or enum underlying type modifies the type; same name with a different kind is one removed and one added type"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelDifferTests.cs (members, bases, kind and file tests)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Repeated type and member definitions get stable #n ids, are recorded on ModelFile.duplicates and become 'base:'/'head:' duplicate warnings; they never stop a run"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelBuilderTests.cs#Repeated_types_get_hash_n_ids_in_file_then_start_byte_order_and_are_recorded and #Duplicates_on_either_side_surface_as_diff_warnings_and_do_not_stop_the_diff"
        status: pass
    human_judgment: false
  - id: D3
    description: "Models and diff.json are canonical: shuffled input gives byte-identical serialisation, including equal-key and equal-start-byte ties"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Diff/ModelDifferTests.cs#Shuffling_the_types_and_members_of_either_model_never_changes_the_diff and #Types_and_members_that_share_an_id_or_key_are_ordered_totally_so_every_input_order_gives_one_diff; tests/Vdiff.Tests/Diff/ModelBuilderTests.cs#Shuffling_the_answer_never_changes_the_serialised_model"
        status: pass
    human_judgment: false
  - id: D4
    description: "D-10 scope: only changed C++ files are extracted; unchanged and mode-only files are skipped; --include narrows the set; two --include flags in either order give byte-identical output; over-limit globs are refused before anything runs"
    requirement: TRC-02
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Scope/IncludeGlobEndToEndTests.cs"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Scope/ScopeFilterTests.cs"
        status: pass
    human_judgment: false

duration: 9min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 07: Differ and scope hardening Summary

**The differ now gives one byte-identical diff for any input order, repeated definitions get `#n` ids recorded on a new `duplicates` array and surfaced as warnings, and the scope filter rejects empty globs and ignores a leading `./`, with D-10 proven through the CLI on a synthetic repository.**

## Performance

- **Duration:** about 9 min (06:12:51Z to about 06:22Z), including one full .NET and cargo run of about 3.5 min
- **Tasks:** 2 (both TDD, each RED then GREEN)
- **Commits:** 4, measured from `plan_head_before` to `plan_head_after`
- **Tests:** .NET 342 passed (259 before this plan), cargo 105 passed (unchanged)

## Accomplishments

- Task 1: `ModelBuilder` records repeated type ids and repeated member keys per type on `ModelFile.Duplicates` and gives them `#2`, `#3` suffixes in (file, start_byte) order; all builder sorts are total (start byte, end byte, key, signature, kind, access). `ModelDiffer` orders both inputs with total ordinal comparers before indexing, so a repeated key cannot make the output depend on input order, and members of added and removed types now come out in key order instead of input order. Warnings are three separate per-side lines.
- Task 2: `ScopeFilter` builds one `Matcher(StringComparison.Ordinal)`, strips leading `./`, and rejects empty patterns (`""`, `./`, `.//`). End-to-end tests prove changed-only extraction (an unchanged file and a mode-only change appear in neither model, a deleted file only in the base model, an added file only in the head model), narrowing by one glob, case sensitivity, a glob that matches nothing (exit 0, zero types, empty `files`), byte-identical output for the two `--include` orders and for `./` against plain, and refusal of 65 globs or a 513-character glob before anything is written.

## Task Commits

1. **Task 1 RED:** `50389bb` (test) - 34 unit tests, 17 fail on the planned assertions.
2. **Task 1 GREEN:** `39e3ec8` (feat) - builder, differ and the warning-wording update in `ModelAssemblyTests`.
3. **Task 2 RED:** `05f1550` (test) - 49 tests, 3 fail (empty-pattern rejection).
4. **Task 2 GREEN:** `b48282f` (feat) - `ScopeFilter`.

## TDD Notes

- Task 1 RED `50389bb`: the 17 failures were assertion failures, never build or discovery errors. They covered `duplicates` (empty instead of the expected entries), the split warnings (old combined text), members of added and removed types in input order, equal-start-byte member ties, and the shuffled and equal-key diff tests (output depended on input order). To keep RED compiling, `ModelFile.Duplicates` was added in the RED commit as an empty-default property; that is not behaviour. Semantic assessment: valid RED.
- The other 17 tests in that commit (overloads, const qualifier, changed parameter list, base list, enum underlying type, kind change, files, unchanged counts, empty models, ordinal order, unmodelled reasons) passed before any change: the 01-03 code already had that behaviour. They are pinning tests, not RED evidence.
- Task 2 RED `05f1550`: only the three empty-pattern cases failed (no exception thrown). `./src/**`, a non-recursive `*.h`, `Include/A` against `include/a`, the no-glob bypass and every end-to-end test already passed, so they too are pinning tests. `Matcher` already accepts a leading `./`; the explicit normalisation makes that independent of the library.
- `gsd_run check tdd-red-evidence` was not run: the plan is `type: execute` and `workflow.tdd_mode` is not enabled; the semantic assessment is recorded here.
- No REFACTOR commit was needed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing test asserted the old combined warning text**
- **Found during:** Task 1 GREEN
- **Issue:** `tests/Vdiff.Tests/ModelAssemblyTests.cs` (plan 01-03, not in this plan's `files_modified`) asserted `"base: 0 file(s) with parse errors, 1 file(s) not modelled"`. The plan requires separate, conditional warnings.
- **Fix:** updated the one assertion to `"base: 1 file(s) not modelled"` and `"head: 2 file(s) not modelled"`. `BlankingEndToEndTests` uses `StartsWith("head: 1 file(s) with parse errors")`, which still holds.
- **Files modified:** `tests/Vdiff.Tests/ModelAssemblyTests.cs`
- **Commit:** `39e3ec8`

### Judgement calls on points the plan left open

- **Duplicate member format:** `member f() of class shop::Y (3 definitions)`; the plan fixes only the type format.
- **Both pairs on a modified member:** the plan lists the access pair for an access-only change and the signature pair for a signature change. I kept both pairs filled for every modified member, because plan 01-08 draws `signature_after` for each member that still exists, and an access-only change would otherwise lose its signature in the diagram. The cost is the known cosmetic `int count_ -> int count_` in `report.html`, which belongs to 01-08.
- **No change needed in other plans' files:** mode-only skipping is done by `GitObjectReader.ParseDiffTree` (plan 01-02) and the glob caps already live in `CliOptions` (64 globs, 512 characters), so the over-limit assertions passed without edits there.
- **Parameter renames** stay removed plus added until Phase 5, as the plan documents.

**Total deviations:** 1 auto-fixed (Rule 3), 3 judgement calls. **Impact:** none on scope; one extra file edited (a one-line test assertion).

## Authentication Gates

None.

## Issues Encountered

- The dispatch asked for a `Claude Opus 5.5` commit trailer while the harness attribution says `Claude Sonnet 5.5`; the harness line was used on all four commits.

## Known Stubs

None.

## Known Limitations (for later plans)

- Reordering a base list without adding or removing a base is not reported (the diff compares base names as sets).
- A repeated id or key in a hand-built model silently loses all but the first (in canonical order) in the diff; the builder never produces one.
- `report.html` and `summary.mmd` still read member values as in 01-03; plan 01-08 owns any change there (a `duplicate` warning is plain text through the existing warnings list).

## Threat Flags

None. T-01-17 is mitigated by the `#n` policy, the `duplicates` warnings and the shuffled and repeated-definition tests; T-01-19 by the existing caps in `CliOptions`, asserted at and one past each limit, and by the empty-pattern refusal in `ScopeFilter`. T-01-SC: no packages added.

## Next Phase Readiness

Ready for 01-08: `ModelFile.Duplicates` exists and serialises as `"duplicates": []` after `types`; diff warnings read `base: ...` and `head: ...`; modified members keep both before and after values.

## Self-Check: PASSED

- All 4 created test files and 5 modified files exist on disk.
- Commits `50389bb`, `39e3ec8`, `05f1550` and `b48282f` are reachable from HEAD (`git rev-list --count` from the base gives 4).
- `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo`: 342 passed, 0 failed. `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: 105 passed. Task 1 filter 34 passed (needs at least 20), Task 2 filter 49 passed (needs at least 10; 65 with the existing scope tests in `GitObjectReaderTests`).
- `git diff --stat` for the plan touches the eight listed files plus `ModelAssemblyTests.cs` (deviation 1).
- `check-public-diff.sh` reported no target names before every commit.
