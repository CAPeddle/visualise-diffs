---
phase: 02-model-contract-and-fixture-harness
plan: 10
subsystem: testing
tags: [fixtures, golden-files, csharp, cpp-cli, cross-stack, mermaid, identity-grammar, schema-v1]

requires:
  - phase: 02-model-contract-and-fixture-harness
    provides: "02-08 three C++ fixtures, golden conventions, FixtureGoldenTests, CommittedFixtureTests, MermaidCliSyntaxTests golden theory; 02-06 frozen v1 schemas and the README Identity grammar; 02-07 harness"
provides:
  - "csharp fixture: SDK-style project and six C# files, hand-written diff.json and summary.mmd that spell the unbound-generic id Foo<> and the nested id OrderService.Receipt by the README Identity grammar"
  - "cross-stack fixture: native C++ class, C++/CLI ref class plus native class with a gcroot member (language cpp-cli), C# caller and a package pin update, with hand-written goldens"
  - "CommittedFixtureTests.All_five_shapes_have_a_fixture"
  - "All five shape directories under tests/fixtures, ten outputs reported RED, suite green"
affects: [04, 05, 06, 07]

actuals:
  tokens: 21000
  tasks: 3
  commits: 3
plan_head_before: 7f3953f687abe04cd6ec65bb1353f1936f8ce80d
plan_head_after: 28521362e6139af1cdd0c44154e565714f39751a

tech-stack:
  added: []
  patterns:
    - "A package node is written as a class line (class C_package_<name>[label]:::<state>), which the existing lint and the pinned mermaid-cli accept"
    - "A cpp-cli type renders the managed-wrapper stereotype first in its block, whether the type is added or modified"

key-files:
  created:
    - tests/fixtures/csharp/fixture.yml
    - tests/fixtures/csharp/base/src/Synthetic.Orders/Synthetic.Orders.csproj
    - tests/fixtures/csharp/base/src/Synthetic.Orders/OrderService.cs
    - tests/fixtures/csharp/base/src/Synthetic.Orders/Document.cs
    - tests/fixtures/csharp/base/src/Synthetic.Orders/Foo.cs
    - tests/fixtures/csharp/head/src/Synthetic.Orders/Synthetic.Orders.csproj
    - tests/fixtures/csharp/head/src/Synthetic.Orders/OrderService.cs
    - tests/fixtures/csharp/head/src/Synthetic.Orders/Document.cs
    - tests/fixtures/csharp/head/src/Synthetic.Orders/Foo.cs
    - tests/fixtures/csharp/head/src/Synthetic.Orders/Invoice.cs
    - tests/fixtures/csharp/expected/diff.json
    - tests/fixtures/csharp/expected/summary.mmd
    - tests/fixtures/cross-stack/fixture.yml
    - tests/fixtures/cross-stack/base/Directory.Packages.props
    - tests/fixtures/cross-stack/base/native/include/synthetic/engine.h
    - tests/fixtures/cross-stack/base/interop/Synthetic.Interop.vcxproj
    - tests/fixtures/cross-stack/base/interop/EngineWrapper.h
    - tests/fixtures/cross-stack/base/app/Synthetic.App/Synthetic.App.csproj
    - tests/fixtures/cross-stack/base/app/Synthetic.App/Dashboard.cs
    - tests/fixtures/cross-stack/head/Directory.Packages.props
    - tests/fixtures/cross-stack/head/native/include/synthetic/engine.h
    - tests/fixtures/cross-stack/head/interop/Synthetic.Interop.vcxproj
    - tests/fixtures/cross-stack/head/interop/EngineWrapper.h
    - tests/fixtures/cross-stack/head/app/Synthetic.App/Synthetic.App.csproj
    - tests/fixtures/cross-stack/head/app/Synthetic.App/Dashboard.cs
    - tests/fixtures/cross-stack/expected/diff.json
    - tests/fixtures/cross-stack/expected/summary.mmd
  modified:
    - tests/Vdiff.Tests/Fixtures/CommittedFixtureTests.cs

key-decisions:
  - "The Foo<> class label passes the pinned mermaid-cli as written, so the plan's fallback for a rejected generic label was not used and no format changed"
  - "Head-only EngineWrapper.h carries the vcclr.h include; base has none, since the include only serves the added native class"
  - "A package node is a class line, so MermaidLint needed no new rule"

patterns-established:
  - "A fixture directory is written complete before its first test run (carried over from 02-08)"

requirements-completed: []
requirements-advanced: [ACC-01]

coverage:
  - id: D1
    description: "csharp and cross-stack goldens validate against the frozen diff schema, keep the presence rules, round-trip byte for byte through OutputWriter and pass MermaidLint"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Fixtures/FixtureGoldenTests.cs#Golden_diff_validates_round_trips_and_keeps_the_presence_rules, #Golden_summary_is_well_formed, #Fixture_file_loads"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every one of the five golden summaries, including Foo<> and the managed-wrapper and package lines, parses in the pinned mermaid-cli (local Category=MermaidCli run); CI does not enforce this in Phase 2"
    requirement: "ACC-01"
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs#Golden_summary_passes_the_real_syntax_check"
        status: pass
    human_judgment: false
  - id: D3
    description: "tests/fixtures holds exactly the five shape directories, and the harness reports all ten outputs RED with the run passing"
    requirement: "ACC-01"
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Fixtures/CommittedFixtureTests.cs#All_five_shapes_have_a_fixture, #Committed_fixtures_pass_the_expected_failure_ratchet, #Every_fixture_directory_is_a_shape"
        status: pass
    human_judgment: false
  - id: D4
    description: "The C# and C++/CLI goldens state the member signature text, the cpp-cli lane, the managed-wrapper stereotype and the package node that Phases 4 to 7 must produce"
    requirement: "ACC-01"
    verification: []
    human_judgment: true
    rationale: "The tests prove the goldens are well formed, canonical and parseable, not that these proposed conventions are what the owner wants; they are listed under Flagged assumptions"
  - id: D5
    description: "The fixtures, goldens and project files hold no target-organisation or target-product name, identifier, path or code"
    requirement: "ACC-01"
    verification: []
    human_judgment: true
    rationale: "check-public-diff.sh printed clean on every staged diff, but the prohibition is judged by reading; a script cannot know every target name"

duration: about 50min
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 10: csharp and cross-stack fixtures, five-shape pin Summary

**The csharp fixture (derived, nested and generic-pair types spelled by the README Identity grammar) and the cross-stack fixture (native C++, a `cpp-cli` wrapper with a `gcroot<>` member, a C# caller and a package pin update) with hand-written goldens, plus a test that pins `tests/fixtures` at exactly the five shapes; all ten outputs report RED, the suite is green and all five summary goldens parse in the pinned mermaid-cli.**

## Performance

- **Duration:** about 50 min wall clock (start not recorded, approximate); 22 min 27 s of it the full suite
- **Tasks:** 3 (task 1 tracer)
- **Files:** 28 in the plan's list (27 created, 1 modified)

## Accomplishments

- csharp: `Invoice : Document` added, `OrderService` modified (`Cancel(int id)` removed, `Refund(int id)` added, `RetryCount` public to protected, `Submit(Order order)` body-changed), nested `OrderService.Receipt` added as a type of its own, and `Foo` and `Foo<>` each modified by one member. The golden holds `class Synthetic.Orders.Foo`, `class Synthetic.Orders.Foo<>` and `class Synthetic.Orders.OrderService.Receipt` as plain ids with no repeat suffix, one resolved base edge `Invoice` to `class Synthetic.Orders.Document`, and `Document` counted as the one unchanged type. The summary lists Foo, Foo<>, Invoice, OrderService, Receipt in (language, id) order and `Document` as context.
- cross-stack: `synthetic::Engine` (cpp) gains `boost(int amount)`; `Synthetic::Interop::EngineWrapper` (cpp-cli) gains `Boost(int amount)`; the native `Synthetic::Interop::EngineEvents` (cpp-cli) is added with a `gcroot<System::Object^> payload` field; `Synthetic.App.Dashboard.Refresh()` (csharp) is body-changed; `Synthetic.Telemetry` is an updated package 1.2.0 to 1.3.0. No edges: the gcroot field resolves to no modelled type of its language. The summary gives both cpp-cli blocks the `<<managed wrapper>>` stereotype and draws the package as `C_package_Synthetic_Telemetry` with `:::modified`.
- `All_five_shapes_have_a_fixture` compares the ordinal-sorted directory names under `tests/fixtures` with `Shapes.All` sorted the same way and names any missing or extra directory.

## Task Commits

1. **Task 1 (tracer): csharp fixture** - `52898f7` (feat)
2. **Task 2: cross-stack fixture** - `8619c4a` (feat)
3. **Task 3: five-shape pin** - `2852136` (test)

**Plan metadata:** recorded in the final docs commit that carries this file.

## Tracer gate

Task 1 is `type="tracer"`, `human_verify_mode` is `end-of-phase` and its `<verify>` carries only `<automated>` checks, so the gate was a re-run. The filtered run (FixtureGoldenTests, CommittedFixtureTests, RepoHygieneTests) passed with 65 tests and the report showed csharp diff and summary RED. Tracer verified end to end, expansion followed. The tracer models no C# today, so the csharp rows are RED because the run is empty, as the plan expected, not FAIL.

## Verification

- After task 1: `Passed!  - Failed: 0, Passed: 65` (the filtered classes); `out/fixture-report.md` has two csharp RED rows.
- After task 2: `Passed!  - Failed: 0, Passed: 68`; two cross-stack RED rows. The cross-stack row is RED, not FAIL: today's C++ sidecar parses the C++/CLI header as plain C++ without error, the CLI exits 0 with no stderr and the output is schema-valid and differs from the golden. There is no tracer or schema finding.
- After task 3: `CommittedFixtureTests` `Passed!  - Failed: 0, Passed: 3`. `out/fixture-report.md` through `VDIFF_FIXTURE_REPORT`: ten RED rows (small-cpp, csharp, cross-stack, extract-class, rename, each diff due 5 and summary due 7), no GREEN or FAIL row.
- Full suite once after the last code commit, `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "Category!=MermaidCli"`: `Passed!  - Failed: 0, Passed: 901, Skipped: 0, Total: 901, Duration: 22 m 27 s`. 861 at the end of 02-08, the rest from plan 02-09's tests and this plan's one new fact plus the golden theory rows over the two new fixtures. The `TraceRunScriptTests` flake did not recur.
- `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: `test result: ok. 105 passed; 0 failed`.
- Local Mermaid parseability proof, `Golden_summary_passes_the_real_syntax_check` over all five goldens: `Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 6 s`. The whole `Category=MermaidCli` run: `Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 28 s`. The pinned checker was already installed, so no package was installed. CI does not enforce this in Phase 2: the pipeline's Mermaid step is advisory.
- Acceptance greps: csharp `"language": "csharp"` 7, plain `Foo` id 1, `Foo<>` id 1, `Receipt` id 1, `class Foo<T>` in head 1, `class Receipt` in head 1, `"key": "Submit(Order order)"` 1, the inheritance line 1, `Synthetic.Orders.Foo<>` in the summary 1; cross-stack `"language": "cpp-cli"` 2, `"old_version": "1.2.0"` 1, `<<managed wrapper>>` 2, `CLRSupport` in the head vcxproj 1.
- `check-public-diff.sh --cached` printed `clean` before every commit. Every new file is LF only; no fixture holds a `bin`, `obj`, `target`, `out` or `node_modules` directory.

## Decisions Made

- The `Foo<>` label, the managed-wrapper stereotype, the `gcroot<System::Object^>` member text and the package label all pass the real mermaid-cli parse, so the plan's fallback (change README, MermaidLint and goldens) was not needed. No design change to record.
- The package node is a class line, so it satisfies the existing `ClassLine` rule of `MermaidLint` with no new rule.

## Deviations from Plan

None - plan executed exactly as written.

## Flagged assumptions (surfaced, not resolved)

- The C# signature text (`void Reset(int to)`, `T Value`, `int RetryCount`, a property signature without its accessor list) and the cpp-cli signature text (`gcroot<System::Object^> payload`) are the plan's proposals for Phase 4 to confirm or revise through accept mode and a reviewed diff. The ids and keys follow the frozen Identity grammar and are not proposals.
- The managed-wrapper stereotype for every `cpp-cli` type, the package-node label with U+2192, and "a gcroot field to an unmodelled type makes no edge" are plan proposals. The lane selection and the package-pin rule become `fixture.yml` config keys when Phase 4 adds them to the `.visualise-diffs.yml` reader; two comment lines in `tests/fixtures/cross-stack/fixture.yml` say so.
- The C# `Invoice` base edge has `to` resolved to a full id while `to_name` stays `Document`, per the D-16 clarification; the C++ exact/suffix rule is not applied.

## Issues Encountered

None.

## Known Stubs

None. The only placeholders are the two `pending` outputs of each new fixture, which are the plan's purpose (due phases 5 and 7).

## Threat Flags

None. No new endpoint, auth path or trust boundary: T-02-33 is mitigated (synthetic names only, clean public-diff check on every commit), T-02-34 (no solution file includes the fixture projects, no build-output directories) and T-02-35 (the cross-stack row is RED, no golden, config or ratchet was adjusted).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 2's fixture set is complete: five shapes, ten RED outputs, ACC-01 delivered red. Phases 4 to 7 flip `state` to `green` per output in the same commit as the code that makes it match.
- Phase 4 should confirm the C# and cpp-cli signature text and the cross-stack lane and package conventions; any change goes through accept mode and a reviewed diff.

## Self-Check: PASSED

- All 28 plan files exist on disk and are tracked.
- Commits `52898f7`, `8619c4a`, `2852136` are ancestors of HEAD; `git rev-list --count` from the recorded base gives 3.

---
*Phase: 02-model-contract-and-fixture-harness*
*Completed: 2026-11-21*
