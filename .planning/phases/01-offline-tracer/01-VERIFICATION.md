---
phase: 01-offline-tracer
verified: 2026-11-17T15:20:00Z
status: human_needed
score: 8/8 must-haves verified
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
  - ".planning/phases/01-offline-tracer/01-11-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-11-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-12-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-12-SUMMARY.md"
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
  - "src/Vdiff.Core/Cli/FinalPath.cs"
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
  - "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs"
  - "tests/Vdiff.Tests/Output/MermaidWriterTests.cs"
  - "tests/Vdiff.Tests/Safety/AliasedOutTests.cs"
  - "tests/Vdiff.Tests/Safety/FinalPathTests.cs"
  - "tests/Vdiff.Tests/Support/LinkScratch.cs"
  - "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs"
  - "tools/mermaid-check/check.mjs"
  - "tools/mermaid-check/package-lock.json"
  - "tools/mermaid-check/package.json"
  - "vdiff.sln"
covered_digest: "v4:sha256:c8aeb3f8bed923727e1459f85e2a44d616cddc7b4d6550c3084742cf88885c26"
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 5/7
  gaps_closed:
    - "Gap 1: Mermaid keyword class ids (plan 01-11)"
    - "Gap 2: --out reaching the repository through a symbolic link or directory junction (plan 01-12)"
  gaps_remaining: []
  regressions: []
advisory:
  - finding: "CR-01 (re-review): --out spelled as an extended-length path (\\\\?\\C:\\...) or a loopback UNC path (\\\\localhost\\C$\\...) passes the containment guard and writes five files inside the analysed repository, exit 0. Reproduced in this verification."
    category: other
    reason: "Not a symbolic link or junction, so outside plan 01-12's must-have; outside the T-01-39 text as written (subst, 8.3 only), so the stated residual under-states it. Judged an acceptable residual for the phase goal, pending the human decision below."
    evidence_status: "reproduced (exit 0, git status shows ?? out-ext/ and ?? out-unc/)"
  - finding: "WR-01 (re-review): a hard link or file symbolic link already present at one of the five output names inside a real --out directory redirects the write into the repository. Reproduced in this verification (README.txt overwritten, git status shows M)."
    category: other
    reason: "Needs the operator to plant a link to a repository file inside --out beforehand; the plan listed this as out of scope. Acceptable residual, pending the human decision below."
    evidence_status: "reproduced"
  - finding: "WR-02 (re-review): containment is checked once, before the pipeline runs, and not again before the write."
    category: other
    reason: "This is threat T-01-40 in plan 01-12 (link swapped between check and write), accepted there as a safety-net limit. Not reproduced; reading CliApp.Run confirms the single check."
    evidence_status: "code reading"
human_verification:
  - test: "Decide the disposition of re-review findings CR-01, WR-01 and WR-02: accept them as residuals of the --out guard, or schedule a small hardening plan."
    expected: "Either (a) the product owner accepts that the guard stops accidental and link-based --out choices but not a deliberately obscure spelling (\\\\?\\, loopback UNC, a pre-planted hard link in --out, a swap after the check), and T-01-39 is widened to name those forms (the doc comments of CliApp.RefuseUnsafeOut and FinalPath.Resolve and the trace-run.ps1 header currently name only subst and 8.3 short names); or (b) a gap-closure plan is raised that canonicalises both sides through a final-path call or refuses a root starting with two backslashes on Windows, writes through a temp name plus move, and re-checks just before the write. The verifier recommends (a) for this phase and (b) as hardening before the tool is placed in a pipeline."
    why_human: "Only the product owner can accept a residual risk; no acceptance covers these forms (T-01-39 names subst and 8.3 only, and the review disposition for all of them is still open). The verifier's judgment is that they do not defeat the phase goal, but it cannot record acceptance on the owner's behalf."
---

# Phase 1: Offline Tracer Verification Report

**Phase Goal:** A user can run `vdiff --repo <path> --base <sha> --head <sha> --out <dir>` offline against a local git repository and get a C++ type- and member-level diff in the output directory, with no Azure DevOps contact and no change to the repository that was read.
**Verified:** 2026-11-17
**Status:** human_needed
**Re-verification:** Yes, after gap closure (plans 01-11 and 01-12). The previous report (`gaps_found`, 5/7) stays in git.

## Summary of the verdict

Both gaps from the previous report are closed and were re-checked against running code, not against the SUMMARY files:

- **Gap 1 (keyword class ids): closed.** A two-commit synthetic repository declaring global `struct link`, `note`, `click`, `callback`, `href`, `cssClass` and `linked : link` now yields `class C_link["link"]:::added` and so on, and the repository's own `tools/mermaid-check/check.mjs` exits 0 (`mermaid-check: OK`). The previous report reproduced exit 1 on the same shape.
- **Gap 2 (junction aliasing): closed.** With a directory junction pointing at the analysed repository, `--out <junction>/outx` now exits 1 with `vdiff: --out must not lie inside the analysed repository`, writes nothing, and the repository hash and `git status` are unchanged. The previous report reproduced exit 0 and `?? outx/`.

The full .NET suite was re-run in this verification: **455 passed, 0 failed, 0 skipped** (7 m 12 s).

The fresh code re-review found a real hole in the new guard (CR-01) and two warnings (WR-01, WR-02). I reproduced CR-01 and WR-01 myself. My judgment is that none of them defeats the phase goal or any roadmap success criterion as worded, so they are recorded as residuals, not gaps. Because nobody has accepted those specific forms (the accepted residual T-01-39 names only subst drives and 8.3 names), the owner's decision is requested, which makes the status `human_needed` rather than `passed`.

## Decision on CR-01, WR-01 and WR-02: gap or acceptable residual

**Decision: acceptable residuals for this phase, not gaps. Owner confirmation requested, and a hardening plan recommended before the tool runs in a pipeline.**

Why they are not gaps against the roadmap:

1. Criterion 2 says a run never changes the repository it reads and that everything the run writes lands in the given output directory. In every run here, including the CR-01 and WR-01 runs, the five files landed in the directory the operator named. The guard is an extra protection (decision D-03, threat T-01-28) against an operator choosing `--out` inside the repository. It is not the criterion itself.
2. The criterion is evidenced by the before/after identity matrix (`RepoUnchangedMatrixTests`, passing) and by my manual hash of every file under the repository, `.git` included, before and after a normal run (identical).
3. Every accidental spelling is refused: relative `--out` from inside the repository, `.`, forward slashes, upper case, trailing separator, `..\` from a subdirectory, a junction (all exit 1, checked here), and a sibling named `repo-out` is correctly accepted. CR-01 needs the operator to type an extended-length (`\\?\`) or loopback UNC (`\\localhost\C$\`) prefix; WR-01 needs a pre-planted hard link inside `--out`; WR-02 needs a link swapped during a run. All are deliberate. The plan already accepted the swap case as T-01-40.
4. Plan 01-12's own must-have is scoped to symbolic links and junctions, and that scope is fully met (see truth 7).

Why a human decision is still needed:

- The accepted-residual text (T-01-39, in the doc comments and the SUMMARY) says the only unresolved aliases are subst drive letters and 8.3 names. CR-01 shows two more alias forms, and WR-01 a write-through form, that the text does not mention. Residual acceptance has to name what is accepted.
- The consequence of an unlisted bypass is the project's central promise being broken silently (exit 0, files in the repository), so it should not be waved through by the verifier alone.

Why it should be fixed before pipeline use: in the target product `--out` is chosen by pipeline code, not typed, so these forms stay unlikely, but a short hardening plan (canonicalise through a final-path call or refuse a `\\` root on Windows; write via temp name plus move; re-check before the write) is cheap and removes the class.

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | SC1: a synthetic two-commit C++ repository run through `vdiff --repo --base --head --out` writes model JSON, diff JSON, a Mermaid summary and an HTML page, and the diff lists the expected added, removed and modified types and members | VERIFIED | Fresh run in this verification (head not the checked-out state of a scratch repository): exit 0, `wrote 5 files`; `summary.mmd` shows `keep` modified with `+int y [added]`, `link` added with `+int a [added]`, and the other added types. `TracerEndToEndTests` and the rich-fixture tests are in the 455-test pass. |
| 2  | SC2: a run never changes the repository it reads, and everything written lands in `--out` and nowhere else | VERIFIED (residuals noted) | SHA-256 of every file under the repository directory, `.git` included, identical before and after a normal run; `git status` empty. `RepoUnchangedMatrixTests` pass (neither commit checked out, dirty tree, stash, linked worktree, detached HEAD, bare repo, fsmonitor hook). The guard residuals (CR-01, WR-01, WR-02) are listed in the decision section above and in the human item. |
| 3  | SC3: the Mermaid summary is a `classDiagram` with `classDef` per state and trailing member tags and passes a Mermaid syntax check; the HTML page is one self-contained file | VERIFIED | `summary.mmd` begins `classDiagram`, three `classDef` lines, `:::state`, `[added]` tags; `check.mjs` (mermaid-cli, headless browser) returned `mermaid-check: OK` on the keyword-named case that failed in the previous report. HTML: carried from the previous verification (inline CSS, no script, repository text HTML-encoded); `HtmlWriter` unchanged by the gap plans (the gap-closure diff does not touch it). |
| 4  | SC4: a run on a small real C++ PR on a private clone finishes within the R26 budget with output outside this repository, recorded only as pass or fail | VERIFIED (orchestrator-attested) | `docs/trials/phase-1-offline-tracer.md` carries the six-line block (pass, under the budget, repository unchanged, outputs present, Mermaid syntax ok). Not re-run, by instruction. Note: plan 01-12 changed `trace-run.ps1`; the eight `Aliased_` script tests and the other `TraceRunScriptTests` pass on both PowerShell hosts in the 455-test run, so the wrapper still behaves as recorded. |
| 5  | SC5: ADR-001 records the packaging choice and the tracer extracts through the chosen sidecar | VERIFIED | ADR-001 status `Accepted (PO, 2026-11-16)`; sidecar located and run in every manual run here (`vdiff-cpp-ts.exe`); crate untouched by the gap plans. |
| 6  | Gap 1 closed: repository text can never make the Mermaid text unparseable through a class id (plans 01-08, 01-09, 01-11) | VERIFIED | `MermaidWriter.ClassId` (lines 91-116) starts every id with the literal `C_` and the same `ids` list feeds `AppendClass` and `Edges`; manual run with six keyword-named global types plus a derived one passes the real check; the new end-to-end test `Writer_output_for_keyword_named_global_types_passes_the_real_syntax_check`, a keyword theory and an all-ids-prefixed property test pass in the full run; `MermaidLint` now rejects an unprefixed id or edge endpoint. |
| 7  | Gap 2 closed: an `--out` reached through a symbolic link or directory junction into the repository (the link, below it, a link to a subdirectory, to `.git`, a chain, and the reverse direction) is refused, nothing created, repository unchanged; a link to an outside directory still works; a loop gives one fixed message (plan 01-12) | VERIFIED | `CliApp.RefuseUnsafeOut` runs the lexical test, then `FinalPath.Resolve` on `--out` and on the three git roots, then compares; resolver failure throws `VdiffException(UnresolvableMessage)`, never falls back to the lexical path. Manual junction run: exit 1, repository hash unchanged, junction removed afterwards as a link. `AliasedOutTests` (9) and `FinalPathTests` (8) pass; the eight `Aliased_` script tests pass on Windows PowerShell 5.1 and PowerShell 7. The link walk was also run on Linux by the executor (WSL), per its SUMMARY; not re-run here. |
| 8  | No Azure DevOps contact and no network use anywhere in the tool (plan 03) | VERIFIED | Unchanged by the gap plans; `FinalPath.cs`, `CliApp.cs` and `MermaidWriter.cs` contain no network types. The syntax check uses a local headless browser. |

**Score:** 8/8 truths verified; 0 behavior-unverified.

### Behavior-dependent truths

"Repository unchanged" and the guard refusals are behavior-dependent. They are covered by named tests that pass (`RepoUnchangedMatrixTests`, `AliasedOutTests`, `FinalPathTests`, `TraceRunScriptTests.Aliased_*`) and by the manual runs above, so none is left as presence only.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Vdiff.Core/Output/MermaidWriter.cs` | prefixed class ids | VERIFIED | `C_` prefix in `ClassId`; used for classes and edges |
| `src/Vdiff.Core/Cli/FinalPath.cs` | link-resolving path walk, fail closed, 40-link budget | VERIFIED | 84 lines, substantive; wired from `CliApp.RefuseUnsafeOut` (four `FinalPath.Resolve` calls) |
| `src/Vdiff.Core/Cli/CliApp.cs` | lexical plus resolved containment | VERIFIED (see residuals) | Both tests run before the pipeline; the resolved path is not reused for the write |
| `scripts/trace-run.ps1` | `Resolve-FinalPath` in `Test-SameOrUnder` | VERIFIED | Present, ASCII, used by `-Out` and `-LogPath` checks; same bound as the CLI |
| `tests/Vdiff.Tests/Support/LinkScratch.cs`, `Safety/AliasedOutTests.cs`, `Safety/FinalPathTests.cs`, `Trial/TraceRunScriptTests.cs`, `Output/*` | regression nets | VERIFIED | Present and passing; no `vdiff-links-*` directory left in the temp area |
| All other phase artifacts (sidecar, git reader, process runner, model, differ, writers, trial record, ADR, mermaid check) | as in the previous report | VERIFIED | Not changed by the gap plans except as listed; regression-checked by the full suite |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `CliApp.RefuseUnsafeOut` | `FinalPath.Resolve` | WIRED | Called on `--out` and on `Root`, `GitDir`, `CommonDir` |
| `MermaidWriter.Render` | `ClassId` (classes and edges) | WIRED | One shared `ids` list |
| `Test-SameOrUnder` | `Resolve-FinalPath` | WIRED | Both `-Out` and `-LogPath` loops in `Invoke-Trace` |
| `CliApp.Run` | `OutputWriter.WriteAll` | WIRED, with the residuals | Check happens before `TracerPipeline.Run`; the write uses the lexical `outPath` |

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Real data | Status |
|----------|---------------|--------|-----------|--------|
| `diff.json`, `summary.mmd`, `report.html` | `DiffFile` | git object reads, sidecar extraction, `ModelDiffer` | Yes: the manual run's output names the types and members of the scratch commits | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Normal run on a synthetic two-commit repository, keyword-named global types | built `vdiff.dll`, `--repo --base --head --out` outside the repository | exit 0, five files | PASS |
| Repository unchanged after that run | SHA-256 of all files incl. `.git`, before and after; `git status` | identical; empty | PASS |
| Mermaid summary of that run passes the real syntax check | `node tools/mermaid-check/check.mjs summary.mmd` | `mermaid-check: OK`, exit 0 | PASS |
| `--out` through a junction into the repository | `--out <junction>/outx` | exit 1, refusal message, repository unchanged | PASS |
| Accidental spellings: relative, `.`, forward slashes, upper case, trailing separator, `..` from `.git` | each as `--out` inside the repository | all exit 1 with the refusal message | PASS |
| Sibling directory `repo-out` | `--out` next to the repository | exit 0 (not over-refused) | PASS |
| `--out \\?\C:\<repo>\out-ext` | extended-length prefix inside the repository | exit 0, files written inside the repository, `?? out-ext/` | FAIL (residual CR-01, not a phase gap) |
| `--out \\localhost\C$\<repo>\out-unc` | loopback UNC inside the repository | exit 0, files written inside the repository, `?? out-unc/` | FAIL (residual CR-01) |
| Hard link at `summary.mmd` inside a real `--out`, pointing at a repository file | `--out` outside, pre-planted link | exit 0, repository file overwritten, ` M README.txt` | FAIL (residual WR-01) |
| Full .NET suite | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` | 455 passed, 0 failed, 0 skipped | PASS |

`trace-run.ps1` has the same extended-length and UNC hole by code reading (`Get-FullPath` keeps the prefixes); I did not run it for those forms. All manual runs used scratch repositories under the session scratch directory; the temporary junction was removed as a link (`rmdir`) and the scratch tree was deleted afterwards. The cargo suite was not re-run: the sidecar crate is unchanged by the gap plans (105 passing in the previous verification).

### Probe Execution

Step 7c: SKIPPED. No `probe-*.sh` is declared by the plans or present under `scripts/`.

### Requirements Coverage

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| TRC-01 | 01-02, 01-03, 01-04, 01-05, 01-12 | Offline run with explicit commits; reads git objects; repository unchanged | SATISFIED | Truths 1, 2, 7; guard residuals pending owner decision |
| TRC-02 | 01-01, 01-02, 01-03, 01-05 to 01-09, 01-11 | C++ type and member diff as model JSON, diff JSON, Mermaid classDiagram and HTML | SATISFIED | Truths 1, 3, 6 |
| TRC-03 | 01-10, 01-12 | Run on a real private PR, outputs outside this repository, no identifiers recorded | SATISFIED (orchestrator-attested) | Trial record; wrapper guard tests pass |
| SPK-02 | 01-01, 01-03, 01-06, 01-10 | ADR-001 recorded, tracer built on it | SATISFIED | Truth 5 |

All four phase requirement IDs appear in plan frontmatter and in `REQUIREMENTS.md`; no orphaned Phase 1 requirement. **Bookkeeping defect:** `REQUIREMENTS.md` still shows `SPK-02` with an unchecked box (line 23) and a traceability status of `Gaps Found` (line 106), while TRC-01 to TRC-03 show Complete. The ADR is Accepted and the tracer uses the sidecar, so SPK-02 is satisfied; the file should be updated to checked and Complete. This is a documentation fix, not a code gap.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (changed files in the gap-closure diff) | - | TBD, FIXME, XXX, TODO, HACK, PLACEHOLDER | none found | Debt-marker gate clear |
| `src/Vdiff.Core/Cli/CliApp.cs` | 84-103 | Containment compares spellings, not identity, for `\\?\` and UNC roots | Warning | Residual CR-01 |
| `src/Vdiff.Core/Output/OutputWriter.cs` | 47-62 | Writes through an existing link or hard link at an output name | Warning | Residual WR-01 |
| `src/Vdiff.Core/Cli/CliApp.cs` | 60-62 | `IOException` text printed with path | Info | Review IN-01; not a phase blocker |

### Review dispositions

`01-REVIEW-DISPOSITION.md` lists all 11 findings as `open`. This verification suggests: CR-01 and WR-01 `deferred` to a hardening plan (or `accepted` by the owner, per the human item), WR-02 `skipped` (it is T-01-40, accepted in plan 01-12), IN-01 to IN-04 and the older WR-03, WR-04, IN-05, IN-06 left as triage items. The orchestrator or owner sets these; the verifier does not edit the file.

### Human Verification Required

#### 1. Disposition of the `--out` guard residuals (CR-01, WR-01, WR-02)

**Test:** Review the decision section above and either accept the residuals or schedule hardening.
**Expected:** Accept (a): note that the guard refuses accidental and link-based `--out` choices, and widen the T-01-39 text (doc comments of `CliApp.RefuseUnsafeOut` and `FinalPath.Resolve`, `trace-run.ps1` header, 01-12 SUMMARY) to name `\\?\`, loopback UNC, pre-planted links in `--out` and the check-to-write gap. Or (b): raise a gap-closure plan (canonicalise through a final-path call, or refuse a `\\` root on Windows; write through a temp name and move; re-check before the write; same in the script).
**Why human:** Accepting residual risk is the owner's call; no acceptance exists for these forms.

### Gaps Summary

No gaps remain against the roadmap success criteria or the must-haves of plans 01-11 and 01-12. Both previously reported gaps are closed and re-verified against running code and a full 455-test pass. The single open item is the owner's decision on the residual hole found by the re-review (CR-01, WR-01, WR-02), plus a documentation fix to `REQUIREMENTS.md` for SPK-02. Deferred items: none; no later roadmap phase covers the guard.

---

_Verified: 2026-11-17_
_Verifier: Claude (gsd-verifier)_
