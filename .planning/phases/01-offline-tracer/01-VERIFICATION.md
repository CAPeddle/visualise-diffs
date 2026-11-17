---
phase: 01-offline-tracer
verified: 2026-11-17T13:00:00Z
status: gaps_found
score: 5/7 must-haves verified
covered_files:
  - ".gitattributes"
  - ".gitignore"
  - ".planning/phases/01-offline-tracer/01-01-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-01-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-02-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-02-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-03-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-03-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-04-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-04-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-05-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-05-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-06-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-06-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-07-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-07-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-08-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-08-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-09-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-09-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-10-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-10-SUMMARY.md"
  - "docs/decisions/ADR-001-cpp-tree-sitter-packaging.md"
  - "docs/trials/phase-1-offline-tracer.md"
  - "extractors/cpp-ts/Cargo.lock"
  - "extractors/cpp-ts/Cargo.toml"
  - "extractors/cpp-ts/src/blank.rs"
  - "extractors/cpp-ts/src/extract.rs"
  - "extractors/cpp-ts/src/main.rs"
  - "extractors/cpp-ts/src/protocol.rs"
  - "global.json"
  - "scripts/trace-run.ps1"
  - "src/Vdiff.Cli/Program.cs"
  - "src/Vdiff.Core/Cli/CliApp.cs"
  - "src/Vdiff.Core/Cli/CliOptions.cs"
  - "src/Vdiff.Core/Diff/ModelDiffer.cs"
  - "src/Vdiff.Core/Extraction/CppSidecar.cs"
  - "src/Vdiff.Core/Git/GitObjectReader.cs"
  - "src/Vdiff.Core/Model/ModelBuilder.cs"
  - "src/Vdiff.Core/Model/ModelTypes.cs"
  - "src/Vdiff.Core/Output/HtmlWriter.cs"
  - "src/Vdiff.Core/Output/MermaidWriter.cs"
  - "src/Vdiff.Core/Output/OutputWriter.cs"
  - "src/Vdiff.Core/Pipeline/TracerPipeline.cs"
  - "src/Vdiff.Core/Process/BoundedProcess.cs"
  - "src/Vdiff.Core/Scope/ScopeFilter.cs"
  - "src/Vdiff.Core/VdiffException.cs"
  - "tests/Vdiff.FakeSidecar/Program.cs"
  - "tools/mermaid-check/check.mjs"
  - "tools/mermaid-check/package-lock.json"
  - "tools/mermaid-check/package.json"
  - "vdiff.sln"
covered_digest: "v4:sha256:37da7e220ee8c022e549210914360b23f1bf479f91bf605a5e7c84ce58c00efd"
behavior_unverified: 0
overrides_applied: 0
gaps:
  - truth: "The Mermaid text vdiff writes passes a real Mermaid syntax check, and repository text can never change the diagram's structure (01-08 and 01-09 must-haves; intent of roadmap criterion 3)"
    status: failed
    reason: "A changed global-scope C++ type whose name is a Mermaid class-diagram keyword (link, note, click, callback, href, cssClass) is emitted as a bare class id, so the whole summary.mmd is unparseable. Reproduced end to end in this verification: a two-commit synthetic repository with `struct link` produced a summary.mmd that the repository's own mermaid-cli check rejects (exit 1, 'Parse error on line 7 ... got LINK'). Code review finding CR-01 is confirmed. The test suite cannot catch it: the syntax-check test only uses `shop::Cell` and `Gen<n>`, and the structural lint accepts any identifier."
    artifacts:
      - path: "src/Vdiff.Core/Output/MermaidWriter.cs"
        issue: "ClassId (lines 91-116) uses the fqn-derived identifier bare; line 52 emits `class <id>[...]`"
      - path: "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs"
        issue: "No case with a keyword-named global type"
    missing:
      - "Prefix every emitted class id (for example `C_`) so it can never equal a Mermaid keyword; keep the readable name in the label"
      - "Add a test that diffs a header declaring struct link, note, click, callback, href and cssClass and asserts the local check exits 0, plus the same names in a MermaidWriter unit test"
  - truth: "An --out inside the analysed repository is refused; everything vdiff writes lands under --out and nowhere else (01-03 must-have; roadmap criterion 2: no change to the repository that was read)"
    status: partial
    reason: "The containment check is lexical (Path.GetFullPath against paths git reports). Reproduced in this verification on Windows: with a directory junction pointing at the analysed repository, `--out <junction>/outx` exited 0 and wrote the five files into the analysed work tree, which then showed `?? outx/` in git status. The default and normal cases (a path outside the repository) are refused or accepted correctly, so this affects only an aliased --out, but the guard is described as refusing any --out inside the repository. Code review finding WR-01 is confirmed."
    artifacts:
      - path: "src/Vdiff.Core/Cli/CliApp.cs"
        issue: "RefuseUnsafeOut / IsSameOrUnder compare lexical full paths; no symlink, junction, subst or short-name resolution"
      - path: "scripts/trace-run.ps1"
        issue: "Test-SameOrUnder has the same lexical-only comparison"
    missing:
      - "Resolve the deepest existing ancestor of --out (and the three git roots) to its final target before comparing, in both CliApp and trace-run.ps1"
      - "Add a test that passes an --out reached through a directory link into the repository and asserts exit 1 and an unchanged repository"
advisory: []
---

# Phase 1: Offline Tracer Verification Report

**Phase Goal:** A user can run `vdiff --repo <path> --base <sha> --head <sha> --out <dir>` offline against a local git repository and get a C++ type- and member-level diff in the output directory, with no Azure DevOps contact and no change to the repository that was read.
**Verified:** 2026-11-17
**Status:** gaps_found
**Re-verification:** No, initial verification

## Summary of the verdict

The core goal is met and evidenced by fresh runs: the tool runs offline, reads both commits from git objects, extracts C++ through the ADR-001 Rust sidecar, writes the five output files, and leaves the repository untouched in normal use. Both suites were re-run in this verification and are green. Two defects found by the code review were reproduced and each falsifies a stated must-have, so the status is `gaps_found` rather than `passed`. Both fixes are small. Neither blocks the model JSON or the diff JSON, which were correct in every run.

## Goal Achievement

### Observable Truths (roadmap success criteria first, then the plan must-haves that matter)

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | SC1: a test script builds a synthetic two-commit C++ repository; `vdiff --repo --base --head --out` writes model JSON, diff JSON, a Mermaid summary and an HTML page; diff JSON lists exactly the expected added, removed and modified types and members | VERIFIED | Manual run on a fresh synthetic repository (head not checked out, dirty work tree): exit 0, five files written (`base.model.json`, `head.model.json`, `diff.json`, `summary.mmd`, `report.html`). diff.json listed Cell modified with `extra()` added, `reset()` removed, `reset(bool hard)` added; `Fresh` added with base `Cell`; `Gone` removed. `TracerEndToEndTests` and the rich-fixture tests pass in the 421-test run. |
| 2  | SC2: a run never changes the repository it reads (status, HEAD, branch, index, refs, worktree list), including when neither commit is checked out; everything written lands in `--out` | PARTIAL (see gap 2) | Matrix of states covered by `RepoUnchangedMatrixTests` (neither commit checked out, dirty tree, stash, linked worktree, detached HEAD, bare repo, fsmonitor hook), all passing. Manual run: a hash over every file under the repository directory, including `.git`, was identical before and after. Git reads are argument arrays with fsmonitor off, no optional locks, no lazy fetch (read in `GitObjectReader` and `BoundedProcess`). Falls short only for an `--out` that reaches the repository through a junction (reproduced, gap 2). |
| 3  | SC3: Mermaid summary is a `classDiagram` with a `classDef` per state and trailing member tags and passes a Mermaid syntax check in the test run; HTML is one self-contained file with no network | PARTIAL (see gap 1) | Output has `classDiagram`, three `classDef` lines (added, removed, modified), `:::state` per class and `[added]`/`[removed]` tags. Syntax check passes on the synthetic fixtures in the test run. `report.html` has inline CSS only, no script element, all repository text passed through `HtmlEncode` (read in `HtmlWriter`). Fails the real syntax check when a global-scope type is named like a Mermaid keyword (reproduced, gap 1). |
| 4  | SC4: run on a small real C++ PR on a private clone completes well inside the R26 budget with output outside this repository; recorded here only as pass or fail | VERIFIED (orchestrator-attested) | `docs/trials/phase-1-offline-tracer.md` carries the six-line block: result pass, wall clock under 1 minute, repo unchanged yes, outputs present yes, Mermaid syntax ok. I could not and did not re-run it, by instruction. The record holds no path, commit id, count or target name. The wrapper's snapshot covers the items SC2 names (status, HEAD, branch, refs, worktree list, stash, index) but not the object database or config (review WR-03, advisory). |
| 5  | SC5: ADR-001 records the packaging choice with the comparison and the tracer extracts through the sidecar it chose | VERIFIED | ADR-001 status line reads `Accepted (PO, 2026-11-16)` and holds package size, Linux install, speed and macro-blanking sections. The tracer calls the sidecar through `CppSidecar.Extract`; the pinned crate versions (tree-sitter 0.27.0, tree-sitter-cpp 0.23.4) are in the manifest and lock and asserted in the sidecar tests. |
| 6  | Plan 03: no Azure DevOps contact and no network use anywhere in the tool | VERIFIED | No network type or URL in `src` or `extractors/cpp-ts/src` (a search found only generated global-using lines and binaries). Mermaid check runs a local headless browser. |
| 7  | Plan 09: npm install is pinned and reproducible, with the human checkpoint recorded | VERIFIED | `package.json` pins mermaid-cli 12.2.0 and puppeteer 25.17.0; `01-03-SUMMARY.md` carries `package-checkpoint: approved mermaid-cli=12.2.0 puppeteer=25.17.0`; an integrity-hashed lockfile is committed (user-approved exception for the target-name guard). |

**Score:** 5/7 truths verified (truths 2 and 3 are partial and are carried as gaps; 0 behavior-unverified).

### Behavior-dependent truths

The "repository unchanged" invariant and the sidecar timeout and process-kill invariants are behavior-dependent. Both are exercised by named tests that passed in the full run (`RepoUnchangedMatrixTests`, `SidecarBoundsTests`), so they are not left as presence-only.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `extractors/cpp-ts/src/{main,protocol,blank,extract}.rs`, `Cargo.toml`, `Cargo.lock` | Rust sidecar from ADR-001 | VERIFIED | Present, substantive (about 2,200 lines), `cargo build --release --locked` succeeds, 105 cargo tests pass |
| `src/Vdiff.Core/Git/GitObjectReader.cs`, `Process/BoundedProcess.cs` | Read-only git object reader, bounded runner | VERIFIED | Substantive; used by `TracerPipeline` and `CliApp` |
| `src/Vdiff.Core/Extraction/CppSidecar.cs` | Sidecar client | VERIFIED (with WR-02 note) | Wired from `TracerPipeline`; response is not cross-checked against the request (advisory) |
| `src/Vdiff.Core/{Model,Diff,Scope,Pipeline,Output,Cli}` | Model, differ, scope, pipeline, writers, CLI | VERIFIED except the two gaps | Wired end to end, proven by the manual run |
| `src/Vdiff.Cli/Program.cs` | Entry point | VERIFIED | Calls `CliApp.Run`; the built `vdiff.dll` ran in this verification |
| `tools/mermaid-check/{check.mjs,package.json,package-lock.json}` | Local syntax check | VERIFIED | Ran in this verification and rejected the keyword case with a parse error |
| `scripts/trace-run.ps1` | Trial wrapper | VERIFIED | Covered by `TraceRunScriptTests`; six-line fixed vocabulary |
| `docs/trials/phase-1-offline-tracer.md` | Public trial record | VERIFIED | Six-line block, SPK-02 evidence, no identifiers |
| `docs/decisions/ADR-001-cpp-tree-sitter-packaging.md` | Accepted ADR | VERIFIED | Status Accepted |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `CliApp` | `TracerPipeline`, `OutputWriter.WriteAll` | WIRED | Run after id resolution and the `--out` check; writes only after every model and diff is built in memory |
| `TracerPipeline` | `GitObjectReader.ListChanged` / `ReadBlobs` | WIRED | Per-side blob reads |
| `TracerPipeline` | `CppSidecar.Extract` | WIRED | One call per commit with `--blank` names |
| `MermaidWriter` | `MermaidLint` / `check.mjs` in tests | WIRED but incomplete | Tests run the check, but only on non-keyword names (gap 1) |
| `HtmlWriter` | `MermaidWriter` output | WIRED | Mermaid text embedded, encoded |
| `trace-run.ps1` | `vdiff.dll` and `check.mjs` | WIRED | Per plan 10 and its tests |

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Real data | Status |
|----------|---------------|--------|-----------|--------|
| `diff.json`, `summary.mmd`, `report.html` | `DiffFile` | git diff-tree + cat-file blobs, tree-sitter extraction, `ModelDiffer` | Yes (manual run showed content derived from the two commits) | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| End-to-end run on a synthetic two-commit repository, head not checked out | built `vdiff.dll` with `--repo --base --head --out` | exit 0, five files | PASS |
| Repository unchanged after that run | hash of every file under the repository directory before and after | identical | PASS |
| Mermaid summary of that run passes the local syntax check | `check.mjs summary.mmd` | exit 1, parse error at `class link[...]` | FAIL (gap 1) |
| `--out` through a junction into the repository is refused | built `vdiff.dll`, `--out <junction>/outx` | exit 0, files written into the repository | FAIL (gap 2) |
| Full .NET suite | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` | 421 passed, 0 failed | PASS |
| Full cargo suite | `cargo test --locked` on the sidecar crate | 105 passed, 0 failed | PASS |

All manual runs used synthetic repositories made in a scratch directory. The temporary junction was removed afterwards.

### Probe Execution

Step 7c: SKIPPED. No `probe-*.sh` is declared by the plans or present under `scripts/`.

### Requirements Coverage

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| TRC-01 | 01-02, 01-03, 01-04, 01-05 | Offline run with explicit commits; reads git objects; repository unchanged | SATISFIED with a caveat | Normal-use behavior proven by manual run and the matrix tests; an aliased `--out` can write into the repository (gap 2) |
| TRC-02 | 01-01, 01-02, 01-03, 01-05 to 01-09 | C++ type and member diff as model JSON, diff JSON, Mermaid classDiagram and HTML | SATISFIED with a caveat | Model JSON, diff JSON and HTML correct in every run; the Mermaid output is unparseable for keyword-named global types (gap 1) |
| TRC-03 | 01-10 | Run on a real private PR, outputs outside this repository, no identifiers recorded | SATISFIED (orchestrator-attested) | Trial record present with result pass and no identifiers; not independently re-run |
| SPK-02 | 01-01, 01-03, 01-06, 01-10 | ADR-001 recorded, tracer built on it | SATISFIED | ADR Accepted; sidecar in use with pinned versions |

All four phase requirement IDs appear in plan frontmatter and in `REQUIREMENTS.md`. No orphaned requirements: the Phase 1 rows in the traceability table are exactly TRC-01, TRC-02, TRC-03 and SPK-02. The requirement checkboxes and traceability rows already show them as complete; that should be revisited if the gaps are treated as blocking TRC-01 and TRC-02.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (phase files) | - | Debt markers (TBD, FIXME, XXX) | none found | Debt-marker gate clear |
| (phase files) | - | TODO, HACK, PLACEHOLDER | none found | None |
| `src/Vdiff.Core/Output/MermaidWriter.cs` | 52, 91-116 | Bare keyword-prone class id | Blocker | Gap 1 |
| `src/Vdiff.Core/Cli/CliApp.cs` | 77-102 | Lexical-only containment guard | Warning | Gap 2 |

### Code review findings weighed against the must-haves

The code review (`01-REVIEW.md`) recorded 1 critical, 4 warnings and 6 info items, all `open`.

- **CR-01** (critical): reproduced; falsifies the 01-08 and 01-09 Mermaid must-haves. Gap 1.
- **WR-01** (warning): reproduced; falsifies the 01-03 "an --out inside the analysed repository is refused" must-have on aliased paths. Gap 2.
- **WR-02** (sidecar answer not cross-checked against the request): confirmed by reading `CppSidecar.ParseResponse`. Does not defeat a must-have today because the Rust side returns every file in request order and that is tested. Recommended hardening, not a gap.
- **WR-03** (trial wrapper snapshot omits the object database and config): does not defeat criterion 2 as worded, which lists status, HEAD, branch, index, refs and worktree list, all of which the wrapper compares. It does weaken how much the trial's `repo_unchanged: yes` proves. Recommended hardening, not a gap.
- **WR-04** (browser launch failure reported as a syntax failure): affects only the reliability of the failure message; the trial recorded `mermaid_syntax: ok`. Not a gap.
- **IN-01 to IN-06**: none defeats a must-have. IN-02 (edges do not distinguish added from removed bases) is a known limitation of a tracer.

### Human Verification Required

None added by this verification. TRC-03 rests on the orchestrator's attested run; if the product owner wants independent confirmation, ask them to confirm that the six-line block in the trial record matches what they saw at the time. No identifiers are needed for that.

### Gaps Summary

Two gaps, both small and independent, both confirmed against running code rather than only by the review:

1. **Mermaid keyword class ids (blocker).** Prefix every emitted class id so it cannot equal a keyword, and add the test the review describes. One line in `ClassId` plus the test cases.
2. **Aliased `--out` bypasses the containment guard (partial).** Resolve links on the deepest existing ancestor of `--out` and on the git roots before comparing, in `CliApp` and in `trace-run.ps1`, and add a test.

The other review warnings and info items are hardening that can be triaged separately. Deferred items: none; no later phase in the roadmap covers either gap (Phase 7 hardens the renderers but does not name keyword-safe ids, and the guard belongs to the tracer's read-only promise).

---

_Verified: 2026-11-17_
_Verifier: Claude (gsd-verifier)_
