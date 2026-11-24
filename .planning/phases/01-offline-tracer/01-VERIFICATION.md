---
phase: 01-offline-tracer
verified: 2026-11-18T23:00:00Z
status: passed
score: 51/55 must-haves verified
covered_files:
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
  - ".planning/phases/01-offline-tracer/01-13-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-13-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-14-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-14-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-15-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-15-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-16-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-16-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-17-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-17-SUMMARY.md"
  - ".planning/phases/01-offline-tracer/01-18-PLAN.md"
  - ".planning/phases/01-offline-tracer/01-18-SUMMARY.md"
  - "scripts/trace-run.ps1"
  - "src/Vdiff.Core/Output/HeldDirectory.cs"
  - "tests/Vdiff.Tests/Safety/AliasedOutTests.cs"
  - "tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs"
  - "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs"
covered_digest: "v4:sha256:ed72ab88d277ef3e5617e3273fb071c07feab9a78d5c9d4738fbed8e48a0fde6"
behavior_unverified: 4
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 43/47
  gaps_closed:
    - "SC2 literal clause 'everything the run writes lands in the given output directory' for --out given as a drive root (review finding CR-01, plan 01-18). Observed here: the CLI with --out C:\\ and a scratch current directory now holds C:\\ (the refusal text names C:\\base.model.json.tmp-..., exit 1 access denied for this unprivileged account), writes nothing into the current directory, and the repository status stays clean; a mutation that restores the unconditional trim turns 7 of the 10 drive_root tests red."
  gaps_remaining: []
  regressions: []
gaps: []
behavior_unverified_items:
  - truth: "01-17: a protected verdict by handle ends the run with reason bad-arguments before vdiff starts; any other hold failure skips the log silently (truth 40)"
    test: "Extract Open-HeldDirectory and its helpers with the PowerShell parser into a child host (the plan 01-18 drive_root driver in TraceRunScriptTests is a working pattern; review finding WR-03 asks for the test) and call it with a protected list that names the log directory; separately make the type fail to compile and run the script with -LogPath"
    expected: "The first ends the script run with bad-arguments and six fixed lines before vdiff starts; the second runs vdiff normally, writes no log and reports pass"
    why_human: "No repository test reaches Get-JudgedFinalPath, the held-directory-protected verdict or the Invoke-Trace branch that maps it. Plan 01-18's parser-extraction driver covers only the drive-root input, not the protected verdict."
  - truth: "01-16 (verification: backstop): on Linux and macOS no handle is held, the resolved path is judged before and after the directories are created, the bound is stated, and the swap-before-the-hold test runs there too (truth 34)"
    test: "Run dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter \"FullyQualifiedName~held_directory|FullyQualifiedName~HeldDirectoryTests\" on a Linux and on a macOS host (CI leg or local)"
    expected: "All pass; A_swap_before_the_held_directory_is_opened_is_refused_by_its_final_path and the retarget test execute (do not return early)"
    why_human: "The development host is Windows. CreateNarrowed was read and does what the truth states; the behavior on those hosts is not observed here."
  - truth: "01-17 (verification: backstop): on Linux and macOS no handle is held and no type is compiled, the log directory is resolved and judged before and after it is created, the post-run containment re-check stays, and the bound is stated (truth 45)"
    test: "Run dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter \"FullyQualifiedName~TraceRunScriptTests\" on a Linux and on a macOS host (pwsh only)"
    expected: "All pass; A_log_path_below_missing_directories_is_created_under_the_log_hold_and_leaves_no_temp_file runs on that host; the staged log_hold test returns early by design"
    why_human: "Windows-only development host. The off-Windows branch of the script was read, not run."
  - truth: "01-18 (verification: backstop): on Linux and macOS the same unit and CLI drive_root tests run with the slash as the root (no early return) and pass (truth 55)"
    test: "Run dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter \"FullyQualifiedName~drive_root\" on a Linux and on a macOS host"
    expected: "All pass; the slash root is held as the slash (the pre-fix trim reduced it to the empty string and failed with the fixed unresolvable text)"
    why_human: "Windows-only development host. Code reading (Path.GetPathRoot of the slash is the slash) supports it; the run is not observed."
coincidental_reliance_items: []
human_verification:
  - test: "Re-verify 01-UAT.md test 1 (G-01-1) against plans 01-16, 01-17 and now 01-18, and optionally repeat test 3 by hand"
    expected: "Product owner confirms the swap-window scope is closed on Windows as described in 01-17-SUMMARY.md (held directory, final-path writes, VDIFF_PROTECTED_DIRS) and that the stated Linux and macOS bound is acceptable. 01-UAT.md still records test 1 as result: issue; this verification does not edit it."
    why_human: "Closure of a gap the product owner raised and scoped is the owner's decision"
  - test: "Re-confirm the AR-04 sign-off (UAT test 6) on its actual basis"
    expected: "Recorded acknowledgement that AR-04 no longer rests on 'a 5.1 script cannot call a final-path function without compiling code' (plan 01-17 compiles two declarations with Add-Type for the swap window) and that subst, mapped drive letters, DOS 8.3 names and IN-04 are still accepted on the owner's sign-off alone"
    why_human: "Acceptance of risk is the owner's decision; the 01-17 executor flagged the stale rationale and left the row unedited"
  - test: "Run the suite on a Linux host and a macOS host"
    expected: "See behavior_unverified_items[1], [2] and [3]"
    why_human: "Windows-only development host"
  - test: "Protected verdict by handle, end to end, and skip-on-failure of the hold"
    expected: "See behavior_unverified_items[0]; or accept the gap and take WR-03 as a follow-up"
    why_human: "No repository test exercises it"
  - test: "A real drive-root run on a scratch volume (manual-only row of 01-VALIDATION.md, TRC-02 / CR-01)"
    expected: "On a volume the owner controls, run vdiff with --out set to that volume's root from an unrelated current directory: five files appear in the root and none in the current directory"
    why_human: "No test writes into a real drive root by design. Observed here only up to the write: the held directory is C:\\ and the unprivileged write was refused (exit 1, existing could-not-write line); the success path into a writable root is not observed."
  - test: "Re-run the trial wrapper once on the private clone with the current scripts/trace-run.ps1 (recommended)"
    expected: "Six-line block: pass, none, under budget, repo_unchanged yes, outputs_present yes, mermaid_syntax ok; only that block recorded"
    why_human: "UAT test 5 (pass) and the trial record predate plans 01-16 to 01-18; private target content must not enter this repository"
  - test: "Eleven judgment-tier prohibitions (plans 01-16, 01-17, 01-18) flagged unverified-prohibition"
    expected: "Reviewer confirms or overrules each non-authoritative verdict in the Prohibitions section (all read as honoured; note WR-02 under the 01-16 message prohibition and under the 01-18 path prohibition)"
    why_human: "Judgment tier: an LLM-judge verdict is non-authoritative. The seven earlier prohibitions were confirmed in UAT test 7."
advisory:
  - finding: "WR-01 (review of 01-16/01-17, still open): the held handle is opened without the extended-length prefix (HeldDirectory.Open and the script's Open-DirectoryHandle), so an --out or -LogPath directory longer than the legacy limit can fail with the fixed unresolvable message on a host with LongPathsEnabled=0"
    category: other
    reason: "Fail closed, not a bypass; not reproduced for the CLI here. Plan 01-18 did not touch it."
    evidence_status: "review text; code reading of HeldDirectory.cs Open"
  - finding: "WR-02 (still open): an IOException or UnauthorizedAccessException during the write carries the held directory's physical path in the stderr text 'could not write the output: ...'. Observed again here: the drive-root run printed the path 'C:\\base.model.json.tmp-<guid>'"
    category: other
    reason: "Letter of the 01-16 message prohibition is kept (no new refusal prints a path); the path-hygiene intent of T-01-38 is weakened for this one branch (CliApp.cs lines 117-120). For a drive root the printed path is the root plus a temp name, nothing sensitive."
    evidence_status: "reproduced in this verification (drive-root run)"
  - finding: "WR-03 to WR-06 and IN-04 to IN-07 (and the stale IN-08 row) of the plan 01-16/01-17 review remain open in 01-REVIEW-DISPOSITION.md; the 01-18-scope review did not re-assess them"
    category: other
    reason: "Each fails closed or is a coverage or wording gap; none changes a verdict above except WR-03, which is behavior_unverified_items[0]"
    evidence_status: "01-REVIEW-DISPOSITION.md (15 rows: CR-01 fixed, 14 open)"
  - finding: "IN-01 of the 01-18-scope review: SplitAtDeepestExisting still accepts a non-fully-qualified path (C: or C:foo anchors on the drive's current directory if called directly); defended only upstream because CliApp.Run passes FinalPath.Resolve(Path.GetFullPath(...))"
    category: other
    reason: "Not reachable today (class is internal, one caller). A future second caller could reintroduce the CR-01 symptom for a drive-relative input; the review's suggested IsPathFullyQualified guard is not applied."
    evidence_status: "code reading of HeldDirectory.cs lines 188-207 and CliApp.Run; 01-REVIEW.md IN-01"
  - finding: "IN-02 (the keep-the-root trim is copied into three test classes) and IN-03 (the root tests fail rather than skip when the host runs from a drive root) of the 01-18-scope review"
    category: other
    reason: "Test maintainability and a loud failure on an unusual host; no effect on any verdict"
    evidence_status: "01-REVIEW.md"
  - finding: "SC4 trial record (docs/trials/phase-1-offline-tracer.md) predates plans 01-13 to 01-18"
    category: other
    reason: "Normal path re-run in the previous verification on both PowerShell hosts and again here for the CLI; plan 01-18 does not change scripts/trace-run.ps1 (git diff empty); human re-run recommended"
    evidence_status: "fresh CLI run here; git diff e9ccf3e..HEAD -- scripts is empty"
---

# Phase 1: Offline Tracer Verification Report

**Phase Goal:** A user can run `vdiff --repo <path> --base <sha> --head <sha> --out <dir>` offline against a local git repository and get a C++ type- and member-level diff in the output directory, with no Azure DevOps contact and no change to the repository that was read.
**Verified:** 2026-11-18 (HEAD `8f87fe8`)
**Status:** human_needed
**Re-verification:** Yes, after gap-closure plan 01-18 (review finding CR-01). The previous report (`gaps_found`, 43/47) stays in git history. Truths 1 to 47 are carried from it; truth 2 is re-derived below and the others were re-checked for regression (the only source change since the last verification is `HeldDirectory.cs`; `scripts/` is byte-identical). Truths 48 to 55 are new, from plan 01-18.

## Summary of the verdict

The gap is closed. CR-01 no longer reproduces, the fix is the right shape, and the tests that pin it discriminate. No truth is FAILED. The status is `human_needed`, not `passed`, because four truths are present and wired but not behaviorally observed (three carried: the handle verdict and the two non-Windows backstops; one new: the non-Windows run of the drive-root tests) and because owner decisions and a recommended trial re-run remain.

**CR-01, independently confirmed.**

- Code: `HeldDirectory.SplitAtDeepestExisting` (lines 188-207) now reads `Path.GetPathRoot(directory)` and, after the same trailing-separator trim, starts from the root when the trimmed text is shorter than the root. `C:\`, `C:\\`, `C:/` and `C:\\/` all give root `C:\` and anchor `C:\`; the slash root is kept on Unix. Only that one method changed under `src/` (`git diff e9ccf3e HEAD --stat -- src scripts`: `HeldDirectory.cs`, 12 insertions, 1 deletion). `CliApp`, `OutputWriter`, `FinalPath`, `CliOptions` and `scripts/trace-run.ps1` are unchanged, so no refusal text, exit code, argument or success-line text moved.
- Behavior, CLI: built `src/Vdiff.Cli` at HEAD, ran it against a scratch two-commit C++ repository in the session scratchpad with a scratch current directory and `--out C:\`. Result: `vdiff: could not write the output: Access to the path 'C:\base.model.json.tmp-...' is denied.`, exit 1. The held directory is the root (the failing path is under `C:\`), the scratch current directory is empty, nothing was created in `C:\`, and the repository status is clean. This is the outcome the plan fixed (P-1): write as typed, or exit 1 with the existing could-not-write line. This unprivileged account cannot create files in the drive root, so the exit-0 success path into a real root is not observed (manual-only row, see human items).
- Behavior, tests: `--filter drive_root` passes 10 of 10. I then staged the mutation that disables the root fallback (`if (false && current.Length < root.Length)`), re-ran the filter and got 7 failed, 3 passed: the three root-spelling rows, the judgement fact, the `CliApp.Run` case and both script-parity cases go red; the three below-root control rows stay green. The mutation was reverted with `git checkout` and the tree equals HEAD for `src/`. The tests therefore discriminate, which the executor's red run and the review's reading also state.
- Regression: control run on the unchanged behavior: nested `--out` below a missing directory chain writes the five files and leaves the repository clean; `--out` inside the repository is refused (exit 1, `--out must not lie inside the analysed repository`, nothing written).
- Targeted suites: `AliasedOutTests|FinalPathTests|HeldDirectoryTests|held_directory` 91 passed, 0 failed (matches the plan's projection of 91).
- Full suite, run once by me: see Behavioral Spot-Checks (result recorded there).

**Claims in the brief, checked.** `01-REVIEW.md` (plan 01-18 scope): 0 critical, 0 warning, 3 info, CR-01 reported fixed; consistent with my reading and the mutation run. `01-REVIEW-DISPOSITION.md` row CR-01 is `fixed` (plan 01-18, commit `92ffb55`), the other 14 rows `open`. The commits named in `01-18-SUMMARY.md` (`1567e09`, `92ffb55`, `58c186a`, `15d7100`, `2a24052`) are in the log; the red-then-green order is real (`1567e09` is the test-only commit before `92ffb55`).

## Goal Achievement

### Observable Truths

Roadmap success criteria first (the contract), then the carried plan truths (6 to 47), then the truths of plan 01-18 (48 to 55).

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | SC1: a synthetic two-commit C++ repository run through `vdiff --repo --base --head --out` writes model JSON, diff JSON, a Mermaid summary and an HTML page with the expected added and modified types and members | VERIFIED | Fresh CLI run here (nested `--out`): exit 0, "wrote 5 files", `base.model.json, diff.json, head.model.json, report.html, summary.mmd`; repository status clean. Extraction and writers unchanged by plan 01-18. |
| 2  | SC2: a run never changes the repository it reads, and everything written lands in `--out` | VERIFIED (was FAILED partial) | Repository half: clean status after every run here, and an `--out` inside the repository is refused. Second half for a drive root: the held directory is the root, nothing lands in the current directory (CLI run above); `A_drive_root_is_held_as_the_root_and_never_as_the_current_directory` (3 spellings), the judgement fact and the `CliApp.Run` case pass and go red under the mutation. Bound: the success write into a real root is not observed (manual-only row). |
| 3  | SC3: Mermaid `classDiagram` with `classDef` per state and member tags passes a syntax check; the HTML page is one self-contained file | VERIFIED | Writers unchanged since the previous verification; Mermaid and writer tests are in the full run. |
| 4  | SC4: a run on a small real C++ PR on a private clone finishes within the R26 budget, output outside this repository, recorded only as pass or fail | VERIFIED (orchestrator-attested) | `docs/trials/phase-1-offline-tracer.md`; UAT test 5 (pass). Not re-run (private clone). Human re-run recommended. |
| 5  | SC5: ADR-001 records the packaging choice and the tracer extracts through the chosen sidecar | VERIFIED | ADR-001 unchanged; the sidecar ran in the CLI run here. |
| 6  | Repository text cannot make the Mermaid text unparseable through a class id (01-11) | VERIFIED | `MermaidWriter` unchanged; tests in the full run. |
| 7  | Links and junctions into the repository are refused for `--out`, `-Out`, `-LogPath` (01-12 to 01-15) | VERIFIED | `FinalPathTests`, `AliasedOutTests` pass (91-test run); `FinalPath.cs` unchanged by plan 01-18. |
| 8  | 01-13: `--out` in the extended, device, loopback UNC or extended UNC namespace is refused with the fixed line, nothing created | VERIFIED | Namespace theories in the 91-test run. |
| 9  | 01-13: a hard link planted at an output name is replaced; the pointed-to file keeps its bytes | VERIFIED | `OutputWriter` unchanged; test passes. |
| 10 | 01-13: a file symbolic link planted at an output name is replaced and its target is unchanged | VERIFIED (human UAT) | UAT test 2, result pass. |
| 11 | 01-13: a link swapped in for `--out` after the first check is caught before the write | VERIFIED | Swap tests in the 91-test run; `CliApp` unchanged. |
| 12 | 01-13: refusal text byte-identical; bytes written unchanged; `--out` holds exactly the five names; no temp file after a failed write | VERIFIED | Fresh run lists exactly five files; the drive-root failed write left nothing in `C:\`. |
| 13 | 01-14: script refuses non-drive-namespace `-Out` and `-LogPath`, six fixed lines, reason vocabulary unchanged | VERIFIED | `scripts/trace-run.ps1` unchanged (diff empty); script tests in the full run. |
| 14 | 01-14: a hard link planted at the `-LogPath` entry is replaced by a regular file | VERIFIED | Script unchanged; test passes. |
| 15 | 01-14: log write preceded by a second containment check (backstop, T-01-46) | VERIFIED (superseded) | Replaced on purpose by plan 01-17 (held directory); staged `log_hold` test; UAT test 3 pass. |
| 16 | The doc comments name exactly what is refused and what remains unresolved | VERIFIED | Unchanged files; the one edited comment (`SplitAtDeepestExisting`) makes no claim about subst, mapped drives or 8.3 names. |
| 17 | SPK-02 is evidenced: ADR-001 Accepted and the tracer built on it | VERIFIED | Truth 5. |
| 18 | No Azure DevOps contact and no network use anywhere in the tool | VERIFIED | No network types added; plan 01-18 changed one path-normalisation method. |
| 19 | 01-15: `--out` below a junction whose reported target is GLOBALROOT or bare-device is refused | VERIFIED | `AliasedOutTests` theories pass. |
| 20 | 01-15: `trace-run.ps1` with such a junction reports fail, script-error, six lines, nothing written | VERIFIED | Script unchanged; script theories pass. |
| 21 | 01-15: each such test first proves the junction is live | VERIFIED | Test code unchanged. |
| 22 | 01-15: `FinalPath.Resolve` refuses every Windows link whose reported target is not a drive-letter path | VERIFIED | 37 `FinalPathTests` in the 91-test run. |
| 23 | 01-15: the runtime reports the substitute name, not the print name | VERIFIED | Premise assertion passes. |
| 24 | 01-15 (backstop): a Windows symbolic link whose target is relative or NT-absolute is refused | VERIFIED (human UAT) | UAT test 4, result pass. |
| 25 | 01-15: the post-run log re-check no longer repeats the pure-text namespace test | VERIFIED (superseded) | Superseded by 01-17. |
| 26 | 01-15: reason vocabulary and six-line grammar unchanged | VERIFIED | Script byte-identical. |
| 27 | 01-15: behavior on Linux and macOS unchanged (rule is Windows-only) | VERIFIED (code reading) | `FinalPath.Step` guarded by `OperatingSystem.IsWindows()`; unchanged. |
| 28 | 01-15: stale handoff not tracked; `01-VALIDATION.md` lists the new tests | VERIFIED | `01-VALIDATION.md` now also carries the `drive_root` map row and the manual-only row (grep count of `drive_root`: present). |
| 29 | 01-16: output directory opened without delete sharing before the first file and held until the fifth; rename and delete refused | VERIFIED | `HeldDirectory.cs` share mode 3 (unchanged lines); `A_held_directory_cannot_be_renamed...` in the 91-test run. |
| 30 | 01-16: every temp file and move uses the handle's final path; a retargeted link entry receives nothing | VERIFIED | `OutputWriter` unchanged; retarget test passes. |
| 31 | 01-16: the held directory's final path is judged before the first byte; a swap between the second check and the hold is refused | VERIFIED | `JudgedFinalPath`, `RemoveCreated` unchanged; swap and order tests pass; judgement still applies to a root (`A_drive_root_is_judged_as_the_root...` passes). |
| 32 | 01-16: acquisition happens after the pipeline and the text check, so a failure before the write leaves `--out` absent or untouched | VERIFIED | `CliApp.Run` unchanged. |
| 33 | 01-16: final path accepted in two forms only; an eight-row table pins it | VERIFIED | `Normalize` unchanged; 8-row theory passes. |
| 34 | 01-16 (backstop): on Linux and macOS no handle is held; judged before and after creation; the bound is stated | PRESENT_BEHAVIOR_UNVERIFIED (abstained: insufficient_spec) | `CreateNarrowed` read; not run off Windows. See `behavior_unverified_items[1]`. |
| 35 | 01-16: a run with no swap is unchanged (five files, same bytes, no `*.tmp-*`, success line names the typed path) | VERIFIED | Fresh run here; with the CR-01 fix the success line is now truthful for a root too (the files are in the typed path). |
| 36 | 01-16: one identical swap-interval paragraph at three sites; pointer sentences; T-01-39 wording intact | VERIFIED | `grep -c 'Check-to-write interval (T-01-43, T-01-46)'` is 1 in each of `CliApp.cs`, `HeldDirectory.cs`, `OutputWriter.cs`; plan 01-18 touched only one method's summary. |
| 37 | 01-16: no test, comment or truth claims subst, mapped drives or 8.3 names are refused; AR-04 not edited | VERIFIED | `grep -ci subst` in the changed sources: no new claim; the `01-SECURITY.md` diff from `e9ccf3e` has 0 removed lines (only added rows). |
| 38 | 01-17: on Windows, both hosts, a `-LogPath` run holds the log's directory without delete sharing, judges its final path, rename refused during the run | VERIFIED | Script unchanged; `log_hold` tests in the full run. |
| 39 | 01-17: the log is written by temp name plus move through the held directory's final path | VERIFIED | Script unchanged; tests pass. |
| 40 | 01-17: a protected verdict by handle ends the run with bad-arguments before vdiff starts; any other hold failure skips the log silently | PRESENT_BEHAVIOR_UNVERIFIED | Verdict function observed by a private probe in the previous round; no repository test reaches it (review WR-03). See `behavior_unverified_items[0]`. |
| 41 | 01-17: native calls compiled with `Add-Type` lazily, only with `-LogPath` on Windows, from a fixed two-declaration text | VERIFIED | Script byte-identical. |
| 42 | 01-17: the CLI refuses an `--out` inside any `VDIFF_PROTECTED_DIRS` entry; a malformed value is refused with one fixed message | VERIFIED | `protected_directories` cases pass; `CliApp` unchanged. |
| 43 | 01-17: a staged script test swaps `-Out` for a link into a stand-in vdiff root; the CLI refuses | VERIFIED | Test passes on both hosts. |
| 44 | 01-17: no command-line argument added or changed (D-03) | VERIFIED | `CliOptions.cs` unchanged through HEAD (diff stat of `src` lists only `HeldDirectory.cs`). |
| 45 | 01-17 (backstop): on Linux and macOS no handle is held and no type is compiled; post-run re-check stays | PRESENT_BEHAVIOR_UNVERIFIED (abstained: insufficient_spec) | Off-Windows branch read; not run. See `behavior_unverified_items[2]`. |
| 46 | 01-17: T-01-43 and T-01-46 rows describe the new guarantee and its bound; millisecond claim gone; AR-04 unedited | VERIFIED | Rows unchanged by plan 01-18 (the register diff from `e9ccf3e` removes 0 lines). |
| 47 | 01-17: no test, comment or truth claims subst, mapped drives or 5.1 8.3 names are now refused | VERIFIED | As truth 37. |
| 48 | 01-18: with `--out` given as the root of the drive that holds the current directory, `HeldDirectory.Create` holds that root itself: `held.Path` equals the typed root and is not the process's current directory; the success line names the typed path and the files are in it | VERIFIED | Behavioral. `A_drive_root_is_held_as_the_root_and_never_as_the_current_directory` (3 rows) and `An_out_given_as_a_drive_root_reaches_the_held_directory_as_that_root_and_not_the_current_directory` pass; 7 of 10 red under my mutation; direct CLI run shows the failing write path under `C:\`, cwd empty. |
| 49 | 01-18: the rule is never trim below the root: a root, a root with the alternate separator, and a root with two trailing separators are all held as the root on every platform | VERIFIED (Windows observed; other platforms see truth 55) | Code `HeldDirectory.cs` lines 191-196 read; the three spelling rows pass; the reviewer's arithmetic for `C:\\/` agrees. |
| 50 | 01-18: one step below the root nothing changes: a directory with no, one, or one alternate trailing separator is held as the directory it names | VERIFIED | `A_directory_below_a_drive_root_...` (3 control rows) pass at HEAD and under the mutation (they stay green, as the plan states). |
| 51 | 01-18: the outcome for a drive-root `--out` is decided and fixed (written as typed, or exit 1 with the existing could-not-write line); no refusal text, argument, script reason or exit code is added; the final path of the held root is still judged | VERIFIED | Direct run: exit 1 with the existing line for this account. Source diff is one method; `A_drive_root_is_judged_as_the_root_by_the_final_path_function_and_a_refusal_creates_nothing` passes (and fails under the mutation). |
| 52 | 01-18: no test writes into a real drive root or the process's current directory, and none changes the current directory | VERIFIED | `grep -rl SetCurrentDirectory tests/Vdiff.Tests`: 0 files; the CLI case aborts at `AfterHeld` with a test-local exception before the first byte (stderr `unexpected failure (StopBeforeFirstWrite)`); `C:\` was clean after my runs; the one real write attempt into the root was my own CLI run, which was refused. |
| 53 | 01-18: the script already agrees and is pinned: `Get-FullPath` and `Open-HeldDirectory` hold a drive root as the root on both PowerShell hosts and agree with `HeldDirectory.Create`; `scripts/trace-run.ps1` is not changed | VERIFIED | `git diff e9ccf3e HEAD -- scripts` is empty; `The_script_holds_a_drive_root_as_the_root_and_agrees_with_the_cli_drive_root` passes on `pwsh` and `powershell`. The summary's own script mutation (guard replaced by `$true`) is cited, not repeated; under my source mutation both host cases also go red because they compare with the CLI value. |
| 54 | 01-18: every other contract stays intact (only `HeldDirectory.cs` under `src/`; refusal texts, exit codes, success-line text unchanged; repository-unchanged guarantee unaffected; whole suite passes, 565 expected on Windows) | VERIFIED | Source diff as above; repository status clean after every run here; targeted 91 passed; full-suite result in Behavioral Spot-Checks. |
| 55 | 01-18 (verification: backstop): on Linux and macOS the same unit and CLI `drive_root` tests run with the slash as the root and pass | PRESENT_BEHAVIOR_UNVERIFIED (abstained: insufficient_spec) | Not observable on a Windows host; code reading says `GetPathRoot("/")` is `/`, so the root is kept. See `behavior_unverified_items[3]`. |

**Score:** 51/55 truths verified; 4 present, behavior-unverified (34, 40, 45, 55); 0 failed.

### Deferred Items

None.

### Advisory (New Scope, Unevidenced)

See `advisory` in the frontmatter: WR-01, WR-02 (re-observed), WR-03 to WR-06 and IN-04 to IN-08 (open, not re-assessed by the 01-18-scope review), IN-01 to IN-03 of the 01-18-scope review, and the age of the SC4 trial record.

### Required Artifacts

`verify.artifacts` / `verify.key-links` for plan 01-18 declare six files; checked by reading:

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Vdiff.Core/Output/HeldDirectory.cs` | `SplitAtDeepestExisting` that never trims below the root, containing `GetPathRoot` | VERIFIED | Lines 182-207; `GetPathRoot` count 1; summary states the rule and why |
| `tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs` | two theories and one fact with `drive_root` | VERIFIED | 7 cases pass (3 + 3 + 1); 102 lines added |
| `tests/Vdiff.Tests/Safety/AliasedOutTests.cs` | `CliApp.Run` case for `--out` as the drive root | VERIFIED | Passes; red under the mutation |
| `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs` | child-host parity test on both PowerShell hosts | VERIFIED | 2 cases pass |
| `01-SECURITY.md` | rows T-01-60 to T-01-62, T-01-SC (01-18), AR-18 | VERIFIED | `git diff e9ccf3e HEAD` on the register: 0 removed lines; rows added |
| `01-VALIDATION.md` | map row (`drive_root`), updated count, manual-only row | VERIFIED | `drive_root` present; manual-only row is the human item above |
| Artifacts of plans 01-01 to 01-17 | as before | VERIFIED (carried) | Sources unchanged apart from the file named |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `CliApp.Run` | `HeldDirectory.Create` | WIRED | unchanged; the CLI run here reaches the held root |
| `HeldDirectory` | `FinalPath` | WIRED | unchanged |
| `HeldDirectory.SplitAtDeepestExisting` | `scripts/trace-run.ps1` `Open-HeldDirectory` | WIRED (same rule, two implementations, now pinned) | the CR-01 difference is gone: the script never trimmed a root, the CLI no longer does; parity test passes on both hosts |
| tests | script | WIRED | the parity test parses the script with the PowerShell parser |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `diff.json`, `summary.mmd`, `report.html` | `DiffFile` | git object reads, sidecar extraction, `ModelDiffer` | Yes: the fresh run wrote the five files | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| CR-01 drive-root `--out`, scratch cwd | `dotnet vdiff.dll --repo <scratch> --base .. --head .. --out C:\` | exit 1, `could not write the output ... 'C:\base.model.json.tmp-...' is denied`; cwd empty; `C:\` clean; repo clean | PASS (gap closed) |
| Nested `--out` | same with a missing nested directory | exit 0, five files, repo clean | PASS |
| `--out` inside the repository | same | exit 1, `--out must not lie inside the analysed repository`, repo clean | PASS |
| `drive_root` tests | `dotnet test --filter "FullyQualifiedName~drive_root"` | 10 passed, 0 failed | PASS |
| Mutation (disable the root fallback), then restored | same filter | 7 failed, 3 passed (the three below-root controls stay green) | PASS (tests discriminate) |
| Held-directory and alias suites | `--filter "AliasedOutTests\|FinalPathTests\|HeldDirectoryTests\|held_directory"` | 91 passed, 0 failed | PASS |
| No process-wide current-directory change in tests | `grep -rl SetCurrentDirectory tests/Vdiff.Tests` | 0 | PASS |
| Script unchanged | `git diff e9ccf3e HEAD --stat -- scripts` | empty | PASS |
| Leftovers | `ls $TEMP \| grep -c '^vdiff-links-'`; `git status --short` | 0; only `.planning/config.json` and `.planning/state.json` (pre-existing, not mine) | PASS |
| Full suite, once | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --no-build` | 565 passed, 0 failed, 0 skipped (12 m 38 s); matches the claimed 565 (555 + 10) | PASS |

The scratch repository and outputs live in the session scratchpad outside the project; nothing is committed. The mutation was reverted before the later runs (the 91-test run rebuilt from the restored source).

### Probe Execution

Step 7c: SKIPPED. No `probe-*.sh` is declared by the plans or present under `scripts/`.

### Requirements Coverage

Requirement IDs in plan frontmatter, plans 01-01 to 01-18: SPK-02, TRC-01, TRC-02, TRC-03 (01-18 declares TRC-01 and TRC-02). All four appear in `REQUIREMENTS.md`; no orphaned Phase 1 requirement.

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| TRC-01 | 01-02 to 01-05, 01-12, 01-13, 01-15, 01-16, 01-17, 01-18 | Offline run with explicit commits; reads git objects; repository unchanged | SATISFIED | Truths 1, 2, 7 to 9, 11, 29 to 33, 42, 51; repository clean after every run here |
| TRC-02 | 01-01 to 01-03, 01-05 to 01-09, 01-11, 01-15, 01-18 | C++ type and member diff as model JSON, diff JSON, Mermaid classDiagram, HTML, in the output directory | SATISFIED | Truths 1, 3, 6, 12, 48 to 54. The drive-root exception of the previous round is closed; the exit-0 write into a real root is a manual-only row |
| TRC-03 | 01-10, 01-12, 01-14, 01-15, 01-17 | Real private PR, outputs outside this repository, no identifiers recorded | SATISFIED (orchestrator-attested) | Truths 4, 13, 14, 38 to 43; UAT 5 pass; trial re-run recommended |
| SPK-02 | 01-01, 01-03, 01-06, 01-10, 01-14, 01-15 | ADR-001 recorded, tracer built on it | SATISFIED | Truths 5 and 17 |

**REQUIREMENTS.md bookkeeping (not a code gap).** At HEAD all four rows (checkbox and traceability table) read `[ ]` / `Gaps Found`. Against the evidence above all four are supported, so the orchestrator may mark them Complete once the owner's human items are settled; the project convention (`aa94c91`, `625f538`) is to hold `Complete` marks while the verification is not `passed`. I make no edit.

### Test Quality Audit

| Test file | Linked req | Active | Skipped | Circular | Assertion level | Verdict |
|-----------|-----------|--------|---------|----------|-----------------|---------|
| `Safety/HeldDirectoryTests.cs` | TRC-01, TRC-02 | 19 | 0 | none | behavioral (hold, create, cleanup, judgement receives the root); value-level for the table | Adequate; root case now present; red under mutation |
| `Safety/AliasedOutTests.cs` | TRC-01, TRC-02 | 35 | 0 (silent early returns off Windows and without the symlink privilege) | none | behavioral through the `AfterHeld` seam | Adequate; the drive-root CLI case aborts before the first byte, so it checks which directory would be written, not a real write |
| `Trial/TraceRunScriptTests.cs` | TRC-03 | 59 on Windows | 0 | none | behavioral; parity test via parser extraction | Adequate; no test of the handle verdict (WR-03) |

The root-derived tests compare against `Path.GetPathRoot(Directory.GetCurrentDirectory())`, so they exercise the drive the host runs from (review IN-03: they fail, not skip, if the host runs from a root). Disabled tests on requirements: 0. Circular patterns: 0.

### Decision Coverage

D-02 (read-only repository), D-03 (no argument, text or code added) and D-09 (synthetic fixtures, nothing machine-specific) are honored by plan 01-18: one method changed, fixtures are the existing synthetic ones, debt-marker grep over `HeldDirectory.cs` and `HeldDirectoryTests.cs` finds nothing.

### Prohibitions (judgment tier; LLM-judge verdicts are non-authoritative)

The eight of plans 01-16 and 01-17 stay flagged as in the previous report (all honoured; see human item). Plan 01-18 adds three:

| Prohibition | Plan | Verdict | Flag |
|-------------|------|---------|------|
| MUST NOT write any file into a drive root, the current directory or any real directory outside a scratch directory from a test, and MUST NOT change the current directory from a test | 01-18 | Honoured: 0 `SetCurrentDirectory` in tests; the CLI case throws at `AfterHeld`; unit rows open the root read-only; `C:\` and the cwd were clean afterwards | unverified-prohibition: human review recommended |
| MUST NOT add an argument, a refusal text, a script reason or an exit code, change the success-line text, or exempt a root from the final-path judgement | 01-18 | Honoured: only `HeldDirectory.cs` changed under `src/`; the judgement fact shows the function receives the root | unverified-prohibition: human review recommended |
| MUST NOT print or record a path, commit id, device name, user or machine name in any refusal, fixture, committed file or the six script lines | 01-18 | Honoured for what the plan adds (no new text). Not covered: the pre-existing I/O error branch prints the held path (WR-02, re-observed in my drive-root run: `C:\base.model.json.tmp-...`) | unverified-prohibition: human review recommended |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/Vdiff.Core/Output/HeldDirectory.cs` | 188-207 | accepts a non-fully-qualified path if called directly (review IN-01) | Info | not reachable today; defended upstream |
| `src/Vdiff.Core/Cli/CliApp.cs` | 117-120 | prints `e.Message` of I/O exceptions, carrying the held physical path | Warning | advisory WR-02 |
| `HeldDirectory.cs`, `trace-run.ps1` | handle open | no extended-length prefix | Warning | advisory WR-01 |
| files modified by plan 01-18 | - | TBD, FIXME, XXX | none found | debt-marker gate clear |

### Human Verification Required

#### 1. UAT test 1 (G-01-1) re-verification, and optionally test 3

**Test:** Review `01-17-SUMMARY.md`, `01-18-SUMMARY.md` and this report against `01-UAT.md` test 1 (still `result: issue`, unedited here); optionally repeat test 3 by hand.
**Expected:** The owner confirms the swap-window scope is closed on Windows and that the stated Linux and macOS bound is acceptable.
**Why human:** Closure of an owner-raised, owner-scoped gap.

#### 2. AR-04 sign-off on its actual basis

**Test:** Re-read AR-04 in `01-SECURITY.md` against `01-17-SUMMARY.md` "Flagged to the orchestrator".
**Expected:** Recorded acknowledgement that the row no longer rests on "a 5.1 script cannot call a final-path function without compiling code" and that subst, mapped drive letters, DOS 8.3 names and IN-04 stay accepted on the owner's sign-off alone.
**Why human:** Acceptance of risk is the owner's decision.

#### 3. Linux and macOS runs (truths 34, 45 and 55)

**Test:** Run the suite, or the filters in `behavior_unverified_items[1]`, `[2]` and `[3]`, on a Linux and on a macOS host.
**Expected:** All pass; the non-Windows tests execute rather than return early; the slash root is held as the slash.
**Why human:** Windows-only development host.

#### 4. Protected verdict by handle, end to end (truth 40)

**Test:** See `behavior_unverified_items[0]`.
**Expected:** `bad-arguments` with six fixed lines before vdiff starts; a type that cannot compile skips the log and the run passes.
**Why human:** No repository test reaches it; accept as follow-up (review WR-03) or add the test.

#### 5. A real drive-root run on a scratch volume

**Test:** On a volume the owner controls, run `vdiff` with `--out` set to the volume root from an unrelated current directory.
**Expected:** Five files in the root, none in the current directory, success line names the root.
**Why human:** Deliberately not automated; this account cannot create files in the C: root, so the success path was not observed.

#### 6. Trial wrapper re-run on the private clone (recommended)

**Test:** Run `scripts/trace-run.ps1` once against the private clone with the current script; record only the six-line block.
**Expected:** pass, none, under budget, repo_unchanged yes, outputs_present yes, mermaid_syntax ok.
**Why human:** The existing record and UAT test 5 predate plans 01-16 to 01-18; private content stays out of this repository.

#### 7. Eleven flagged prohibitions

**Test:** Confirm or overrule the verdicts in the Prohibitions section (eight from plans 01-16 and 01-17 in the previous report, three above).
**Expected:** All read as honoured; note WR-02.
**Why human:** Judgment tier: LLM-judge verdicts are non-authoritative.

The previous human item 1 (disposition of CR-01) is resolved: the owner chose a fix plan and plan 01-18 delivered it.

### Gaps Summary

No gaps. CR-01, the single gap of the previous report, is closed and independently confirmed by code reading, by a direct CLI run, and by a mutation run in which the pinning tests turn red. The status is `human_needed` because of the four behavior-unverified truths, the owner sign-offs and the recommended trial re-run; none of these is a defect found in the code. Open advisories (WR-01 to WR-06, IN-01 to IN-08, and IN-01 to IN-03 of the new review) fail closed or concern tests and wording.

---

_Verified: 2026-11-18_
_Verifier: Claude (gsd-verifier)_
