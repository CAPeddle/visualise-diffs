---
phase: 01-offline-tracer
verified: 2026-11-18T19:40:00Z
status: gaps_found
score: 43/47 must-haves verified
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
  - "src/Vdiff.Core/Output/HeldDirectory.cs"
  - "src/Vdiff.Core/Output/HtmlWriter.cs"
  - "src/Vdiff.Core/Output/MermaidWriter.cs"
  - "src/Vdiff.Core/Output/OutputWriter.cs"
  - "src/Vdiff.Core/Pipeline/TracerPipeline.cs"
  - "src/Vdiff.Core/Process/BoundedProcess.cs"
  - "src/Vdiff.Core/Scope/ScopeFilter.cs"
  - "src/Vdiff.Core/Vdiff.Core.csproj"
  - "src/Vdiff.Core/VdiffException.cs"
  - "tests/Vdiff.FakeSidecar/Program.cs"
  - "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs"
  - "tests/Vdiff.Tests/Output/MermaidWriterTests.cs"
  - "tests/Vdiff.Tests/Safety/AliasedOutTests.cs"
  - "tests/Vdiff.Tests/Safety/FinalPathTests.cs"
  - "tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs"
  - "tests/Vdiff.Tests/Support/EnvironmentScope.cs"
  - "tests/Vdiff.Tests/Support/LinkScratch.cs"
  - "tests/Vdiff.Tests/Support/PathSpellings.cs"
  - "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs"
  - "tools/mermaid-check/check.mjs"
  - "tools/mermaid-check/package-lock.json"
  - "tools/mermaid-check/package.json"
  - "vdiff.sln"
covered_digest: "v4:sha256:cacc7b382cd43b1b48bc7053ee80006e2ced929fc0d76b4d40e9b2623910a759"
behavior_unverified: 3
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 25/28
  gaps_closed:
    - "G-01-1 (01-UAT.md test 1), within the product owner's scope of 2026-11-18 (the check-to-write swap window only): the --out directory is held without delete sharing and every file is written by the held handle's final path (plan 01-16); the -LogPath directory is held through the vdiff run and the log is written by temp name plus move through the final path (plan 01-17); the vdiff root protects -Out through VDIFF_PROTECTED_DIRS. Observed here: the staged swap tests pass on both PowerShell hosts and a direct probe of Open-HeldDirectory shows the lock, the verdict and the cleanup."
    - "Previous behavior-unverified truths 10 and 24 (symbolic-link forms) and 15 (backstop swap during a script run): resolved by the human results in 01-UAT.md tests 2, 4 and 3 (all result: pass, 2026-11-18); truth 15's mechanism was replaced by plan 01-17 and is now proven by a deterministic staged test."
  gaps_remaining:
    - "SC2 literal clause 'everything the run writes lands in the given output directory' for --out given as a drive root (new in this round: review finding CR-01, introduced by plan 01-16, reproduced independently here)"
  regressions:
    - "Truth 2 (SC2) moved from VERIFIED to FAILED (partial): HeldDirectory.SplitAtDeepestExisting turns 'C:\\' into 'C:' (the current directory on drive C). Before plan 01-16 the same input wrote to the drive root or failed with access denied."
gaps:
  - truth: "SC2: a run never changes the repository it reads, and everything the run writes lands in the given output directory and nowhere else"
    status: partial
    reason: "BLOCKER (a must-have truth, a roadmap success criterion, is falsified for one input). CR-01 reproduced in this verification. With --out C:\\ (a drive root) HeldDirectory trims the trailing separator, 'C:' means the process's current directory on that drive, and the five files land in the current directory while stdout says 'vdiff: wrote 5 files to C:\\' and exit is 0. The first half of SC2 holds: with the current directory inside the analysed repository the same command is refused by the held directory's final-path judgement (exit 1, repository status clean), and nothing was written to C:\\. The defect is output in a directory the operator did not name plus a success line that names another, for one input class. It is a regression of plan 01-16 and it also touches TRC-02's wording (outputs written in the output directory)."
    artifacts:
      - path: "src/Vdiff.Core/Output/HeldDirectory.cs"
        issue: "SplitAtDeepestExisting (line 186) calls TrimEnd on the separators before testing the path, so a root becomes its drive-relative form"
    missing:
      - "Do not trim a root: trim only when the path is longer than Path.GetPathRoot(directory), as the review's suggested fix shows"
      - "A HeldDirectoryTests case for a root input and a CliApp.Run case for --out given as the drive root with a scratch current directory (assert the files are not written to the current directory)"
      - "Mirror check in scripts/trace-run.ps1 Open-HeldDirectory (it does not trim, so the two implementations currently differ here)"
behavior_unverified_items:
  - truth: "01-17: a protected verdict by handle ends the run with reason bad-arguments before vdiff starts; any other hold failure skips the log silently (truth 40)"
    test: "Extract Open-HeldDirectory and its helpers with the PowerShell parser into a child host (or add the test of review finding WR-03) and call it with a protected list that names the log directory; separately make the type fail to compile and run the script with -LogPath"
    expected: "The first ends the script run with bad-arguments and six fixed lines before vdiff starts; the second runs vdiff normally, writes no log and reports pass"
    why_human: "No repository test reaches Get-JudgedFinalPath, the held-directory-protected verdict or the Invoke-Trace branch that maps it (grep of tests finds only a source-order pin). I observed the verdict function itself with a private probe on both hosts (see Behavioral Spot-Checks), but not the end-to-end bad-arguments result nor the skip-on-failure path."
  - truth: "01-16 (verification: backstop): on Linux and macOS no handle is held, the resolved path is judged before and after the directories are created, the bound is stated, and the swap-before-the-hold test runs there too (truth 34)"
    test: "Run dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter \"FullyQualifiedName~held_directory|FullyQualifiedName~HeldDirectoryTests\" on a Linux and on a macOS host (CI leg or local)"
    expected: "All pass; A_swap_before_the_held_directory_is_opened_is_refused_by_its_final_path and the retarget test execute (do not return early)"
    why_human: "The development host is Windows. CreateNarrowed was read and does what the truth states, and the bound paragraph is present at three CLI sites; the behavior on those hosts is not observed here."
  - truth: "01-17 (verification: backstop): on Linux and macOS no handle is held and no type is compiled, the log directory is resolved and judged before and after it is created, the post-run containment re-check stays, and the bound is stated in the script, the register and the validation map (truth 45)"
    test: "Run dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter \"FullyQualifiedName~TraceRunScriptTests\" on a Linux and on a macOS host (pwsh only)"
    expected: "All pass; A_log_path_below_missing_directories_is_created_under_the_log_hold_and_leaves_no_temp_file runs on that host; the staged log_hold test returns early by design"
    why_human: "Windows-only development host. The off-Windows branch (script lines 424-433 and 609-613) was read, not run; the staged hold test is Windows-only, so the interval is open there by the stated bound."
human_verification:
  - test: "Decide the disposition of CR-01 (--out at a drive root)"
    expected: "Either a small gap-closure plan (do not trim a root; two tests; script mirror check) or an explicit product-owner acceptance recorded as an override against SC2"
    why_human: "The fix is small and the exposure narrow, but whether a reproduced misdirected-output defect ships in Phase 1 is a product-owner call. Not suggested as an override: it is a defect, not an intentional deviation."
  - test: "Re-verify 01-UAT.md test 1 (G-01-1) and optionally test 3 against plans 01-16 and 01-17"
    expected: "Product owner confirms the swap-window scope is closed on Windows as described in 01-17-SUMMARY.md (held directory, final-path writes, VDIFF_PROTECTED_DIRS) and that the stated Linux and macOS bound is acceptable; test 3 (staged swap during a script run) is now automated by the log_hold tests and may be repeated by hand"
    why_human: "Closure of a gap the product owner raised and scoped is the owner's decision"
  - test: "Re-confirm the AR-04 sign-off (UAT test 6) on its actual basis"
    expected: "Recorded acknowledgement that AR-04 no longer rests on 'a 5.1 script cannot call a final-path function without compiling code' (plan 01-17 compiles two declarations with Add-Type for the swap window) and that subst, mapped drive letters, DOS 8.3 names and IN-04 are still accepted on the owner's sign-off alone"
    why_human: "Acceptance of risk is the owner's decision; the 01-17 executor flagged the stale rationale and left the row unedited"
  - test: "Run the suite on a Linux host and a macOS host"
    expected: "See behavior_unverified_items[1] and [2]"
    why_human: "Windows-only development host"
  - test: "Protected verdict by handle, end to end, and skip-on-failure of the hold"
    expected: "See behavior_unverified_items[0]; or accept the gap and take WR-03 as a follow-up"
    why_human: "No repository test exercises it"
  - test: "Re-run the trial wrapper once on the private clone with the current scripts/trace-run.ps1 (recommended)"
    expected: "Six-line block: pass, none, under budget, repo_unchanged yes, outputs_present yes, mermaid_syntax ok; only that block recorded"
    why_human: "UAT test 5 (pass) and the trial record predate plans 01-16 and 01-17, which rewrote the script's log path and added the hold; private target content must not enter this repository"
  - test: "Eight new judgment-tier prohibitions (plans 01-16 and 01-17) flagged unverified-prohibition"
    expected: "Reviewer confirms or overrules each non-authoritative verdict in the Prohibitions section (all read as honoured; note WR-02 under the 01-16 message prohibition)"
    why_human: "Judgment tier: an LLM-judge verdict is non-authoritative. The seven earlier prohibitions were confirmed in UAT test 7."
advisory:
  - finding: "WR-01 (review): the held handle is opened without the extended-length prefix (HeldDirectory.Open and the script's Open-DirectoryHandle), so an --out or -LogPath directory longer than the legacy limit can fail with the fixed unresolvable message on a host with LongPathsEnabled=0; reviewer reproduced the script side under pwsh (log skipped silently)"
    category: other
    reason: "Fail closed, not a bypass; not reproduced for the CLI here (long paths are enabled on this host). Pre-01-16 behavior for such a path was to write."
    evidence_status: "review text; code reading of HeldDirectory.cs line 216 and trace-run.ps1 line 324"
  - finding: "WR-02 (review): an IOException or UnauthorizedAccessException during the write now carries the held directory's physical path (a link's target) in the stderr text 'could not write the output: ...'"
    category: other
    reason: "Letter of the 01-16 message prohibition is kept (no new refusal prints a path), but the path-hygiene intent of T-01-38 is weakened for this one branch; CliApp.cs lines 117-120"
    evidence_status: "code reading; not reproduced"
  - finding: "WR-03 (review): the script's judgement by handle, its protected verdict and ConvertFrom-FinalPathForm have no test; WR-04: a path separator inside the vdiff root breaks the VDIFF_PROTECTED_DIRS handoff (fail closed, wrong cause, tool-failed); WR-05 (carried): relative or drive-less link targets refused with a message naming the wrong problem; WR-06 (carried): the file-symlink test returns early without the privilege"
    category: other
    reason: "Each fails closed or is a coverage gap; none changes a verdict above except WR-03, which is behavior_unverified_items[0]"
    evidence_status: "01-REVIEW.md; WR-03 confirmed by grep of tests"
  - finding: "IN-01 to IN-07 of 01-REVIEW.md (non-Windows creation differences, the limit of 8 written three times, a fixed five-second staging window and EnvironmentScope partial-construction leak, six copies of the swap paragraph, silent early returns, TooManyLinksException reuse, missing symlink and UNC spellings); the disposition file additionally keeps a stale IN-08 row"
    category: other
    reason: "Info level; all 15 rows of 01-REVIEW-DISPOSITION.md remain 'open' (the review reports 14 findings: 1 critical, 6 warning, 7 info)"
    evidence_status: "01-REVIEW.md, 01-REVIEW-DISPOSITION.md"
  - finding: "Artifact pattern miss (info): plan 01-17 lists the pattern 'held across' for 01-SECURITY.md and verify.artifacts reports it missing (6/7 pass); the T-01-43 and T-01-46 rows state the same guarantee in other words (held directory, without delete sharing, across the five writes / across the run)"
    category: other
    reason: "A wording pattern in the plan, not missing content; rows read and compared with the code"
    evidence_status: "verify.artifacts output and reading of 01-SECURITY.md lines 89 and 92"
  - finding: "SC4 trial record (docs/trials/phase-1-offline-tracer.md) predates plans 01-13 to 01-17"
    category: other
    reason: "Normal path re-run here on both PowerShell hosts with the current script (pass, repo unchanged, five outputs, log written under a created directory chain, no temp leftovers); human re-run recommended"
    evidence_status: "fresh runs in this verification"
---

# Phase 1: Offline Tracer Verification Report

**Phase Goal:** A user can run `vdiff --repo <path> --base <sha> --head <sha> --out <dir>` offline against a local git repository and get a C++ type- and member-level diff in the output directory, with no Azure DevOps contact and no change to the repository that was read.
**Verified:** 2026-11-18 (HEAD `2fdb3f5`)
**Status:** gaps_found
**Re-verification:** Yes, after gap-closure plans 01-16 and 01-17 (UAT gap G-01-1). The previous report (`human_needed`, 25/28) stays in git history. Truths 1 to 28 are carried from that report and re-checked for regression (full test run plus the spot-checks below); I did not re-derive them from plans and summaries 01-01 to 01-15.

## Summary of the verdict

The phase goal is achieved for every normal use and for the whole swap-window gap the product owner scoped. One reproduced defect keeps the status at `gaps_found`: review finding CR-01. It is a BLOCKER by the classification rules (a roadmap success criterion is falsified for one input), narrow in scope, and it leaves the repository-unchanged guarantee intact.

**CR-01, judged.** I ran the existing Debug build of HEAD (built after the last source commit) against a scratch repository in the session scratchpad, outside the project. `--out C:\` with a scratch current directory: stdout `vdiff: wrote 5 files to C:\`, exit 0, the five files in the scratch current directory, nothing in `C:\`. Cause confirmed in `HeldDirectory.cs` line 186 (`TrimEnd` turns `C:\` into `C:`, the drive-relative path). Repository safety holds: with the current directory inside the analysed repository the same command exits 1 with `--out must not lie inside the analysed repository` (the held directory's final path is judged), the repository status is clean and nothing reached `C:\`. So the goal's "no change to the repository that was read" is not defeated. The literal SC2 clause "everything the run writes lands in the given output directory and nowhere else" is false for this input, TRC-02's "in the output directory" is likewise not met for it, and the success line names a directory the files are not in. Plan 01-16 introduced it: before, the same input wrote to the root or failed. It needs either the small fix (do not trim a root, two tests) or an explicit owner acceptance.

**G-01-1 closure, observed.** The swap window is closed on Windows for the CLI's output directory, for the script's log directory, and for `-Out` against the vdiff root:

- 91 of the relevant tests pass in one filtered run (`AliasedOutTests`, `FinalPathTests`, `HeldDirectoryTests`, `log_hold`, `protected_directories`), including the staged tests that attack from outside while a sleeping fake sidecar keeps a real run busy, on both PowerShell hosts. The single full run then passed 555 of 555.
- A private probe (scratchpad) that loads the script's own functions: `Open-HeldDirectory` with a protected anchor throws `held-directory-protected` and creates nothing; with a protected leaf it throws the same and removes the directories it created; on success it returns a path without the extended prefix, and while held a rename of the leaf and of its parent are both refused by the operating system and allowed again after close. Identical on PowerShell 7 and Windows PowerShell 5.1.
- A normal run through `trace-run.ps1` on both hosts with a nested `-Out` and a nested `-LogPath` chain that does not exist: `pass`, `none`, repository unchanged, five outputs, the log written with the vdiff output, no `*.tmp-*` leftovers; `summary.mmd` shows `A` modified (`+int y [added]`) and `B` added.
- `-LogPath` spelled with the repository's 8.3 short path (`-Out` was an ordinary path) on both hosts: `bad-arguments`, no directory created in the repository. Which layer caught it, the first text check or the handle judgement, was not separated; nothing is claimed about AR-04.
- Not attempted successfully: `subst` (the command refused its parameter on this host). AR-04 is out of scope by the owner's decision.

**Claims in the brief, checked.** `dotnet test --list-tests` enumerates 555 tests; the full run passed 555, failed 0, skipped 0 (12 m 26 s). The build is part of that run (0 errors; `dotnet test` built the solution's test project and its dependencies). The security claim (76 threats, 76 closed, `threats_open: 0`, `status: verified`) matches the register: 76 `T-01-` rows, 76 with disposition `closed`; I did not audit the controls behind each row (the register states grep depth, ASVS L1). The Nyquist claim matches `01-VALIDATION.md` (`status: validated`, `nyquist_compliant: true`, sign-off 555 passed); the validation audit itself was not repeated.

## Goal Achievement

### Observable Truths

Roadmap success criteria first (the contract), then the carried plan truths of plans 01-01 to 01-15 (truths 6 to 28, carried and regression-checked), then the truths of plans 01-16 and 01-17.

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | SC1: a synthetic two-commit C++ repository run through `vdiff --repo --base --head --out` writes model JSON, diff JSON, a Mermaid summary and an HTML page with the expected added and modified types and members | VERIFIED | Fresh runs here (CLI and both script hosts): exit 0, `base.model.json, diff.json, head.model.json, report.html, summary.mmd`; `summary.mmd` has `A` modified, `+int y [added]`, `B` added. |
| 2  | SC2: a run never changes the repository it reads, and everything written lands in `--out` | FAILED (partial, BLOCKER) | Repository half holds (repository status clean after every run here, including the CR-01 repros). The second sentence fails for `--out` given as a drive root: files land in the current directory, success line names `C:\` (reproduced). See gaps. |
| 3  | SC3: Mermaid `classDiagram` with `classDef` per state and member tags passes a syntax check; the HTML page is one self-contained file | VERIFIED | `summary.mmd` seen above; writers and extraction/diff sources unchanged by plans 01-16 and 01-17; Mermaid and writer tests are in the passing full run. |
| 4  | SC4: a run on a small real C++ PR on a private clone finishes within the R26 budget, output outside this repository, recorded only as pass or fail | VERIFIED (orchestrator-attested) | `docs/trials/phase-1-offline-tracer.md`; UAT test 5 (pass, recorded before plans 01-16/01-17). Not re-run (private clone). Human re-run recommended. |
| 5  | SC5: ADR-001 records the packaging choice and the tracer extracts through the chosen sidecar | VERIFIED | ADR-001 unchanged (`Accepted (PO, 2026-11-16)`); the sidecar executable ran in every run here. |
| 6  | Repository text cannot make the Mermaid text unparseable through a class id (01-11) | VERIFIED | `MermaidWriter` unchanged; its tests are in the passing full run. |
| 7  | Links and junctions into the repository are refused for `--out`, `-Out`, `-LogPath` (01-12 to 01-15) | VERIFIED | `FinalPathTests`, `AliasedOutTests` and `TraceRunScriptTests` (live junction tests) pass in the full run; `FinalPath.cs` changed only by a comment since the previous verification. |
| 8  | 01-13: `--out` in the extended, device, loopback UNC or extended UNC namespace is refused with the fixed line, nothing created | VERIFIED | Namespace theories pass in the 91-test run. |
| 9  | 01-13: a hard link planted at an output name is replaced; the pointed-to file keeps its bytes | VERIFIED | `OutputWriter.WriteText` is temp name plus `File.Move` (read); hard-link test passes. |
| 10 | 01-13: a file symbolic link planted at an output name is replaced and its target is unchanged | VERIFIED (human UAT) | UAT test 2, result pass, on a privileged host. Not reproducible here (no privilege); same replace path as truth 9. |
| 11 | 01-13: a link swapped in for `--out` after the first check is caught before the write | VERIFIED | `CliApp.Run` lines 103-106 (second check, seam, hold judged by final path); swap tests pass. |
| 12 | 01-13: refusal text byte-identical; bytes written unchanged; `--out` holds exactly the five names; no temp file after a failed write | VERIFIED | `RepositoryRefusal` text unchanged; `DeterminismTests`, `TracerEndToEndTests` and the failed-write test pass in the full run; fresh runs list exactly five files, no `*.tmp-*`. |
| 13 | 01-14: script refuses non-drive-namespace `-Out` and `-LogPath`, six fixed lines, reason vocabulary unchanged, both hosts | VERIFIED | Vocabulary line (line 9) present once and unchanged; six lines in every script run here; 0 non-ASCII characters; `TraceRunScriptTests` pass in the full run. |
| 14 | 01-14: a hard link planted at the `-LogPath` entry is replaced by a regular file | VERIFIED | `Write-LogFile` lines 462-488 (delete entry, create new, move); log hard-link test passes in the full run. |
| 15 | 01-14: the log write is preceded after the run by a second containment check (backstop, T-01-46) | VERIFIED (superseded) | Replaced on purpose by plan 01-17 (L-3): on Windows the directory is held and judged before the run, the re-check remains only where no handle is held (script lines 606-613). The intent (no log through a swapped link) is proven by the staged `log_hold` test, which passes on both hosts. UAT test 3 earlier: pass. |
| 16 | The doc comments name exactly what is refused and what remains unresolved | VERIFIED | `FinalPath.cs`, `CliApp.cs`, `trace-run.ps1` still state "Still unresolved: subst and mapped drive letters, DOS 8.3 short names, and IN-04"; each gained one pointer sentence only. |
| 17 | SPK-02 is evidenced: ADR-001 Accepted and the tracer built on it | VERIFIED | Truth 5. |
| 18 | No Azure DevOps contact and no network use anywhere in the tool | VERIFIED | No network types in the new files (`HeldDirectory.cs`, `OutputWriter.cs`, `CliApp.cs`, script): only kernel32 P/Invoke; all runs here were local. |
| 19 | 01-15: `--out` below a junction whose reported target is GLOBALROOT or bare-device is refused | VERIFIED | `AliasedOutTests` NT-namespace theories pass; `FinalPath.Step` unchanged. |
| 20 | 01-15: `trace-run.ps1` with such a junction reports fail, script-error, six lines, nothing written | VERIFIED | Script theories pass on both hosts in the full run. |
| 21 | 01-15: each such test first proves the junction is live | VERIFIED | Unchanged test code, passing. |
| 22 | 01-15: `FinalPath.Resolve` refuses every Windows link whose reported target is not a drive-letter path | VERIFIED | 37 `FinalPathTests` pass. |
| 23 | 01-15: the runtime reports the substitute name, not the print name (premise asserted) | VERIFIED | `FinalPathTests` premise assertion passes. |
| 24 | 01-15 (backstop): a Windows symbolic link whose target is relative or NT-absolute is refused | VERIFIED (human UAT) | UAT test 4, result pass, on a capable host. Not reproducible here. |
| 25 | 01-15: the post-run log re-check no longer repeats the pure-text namespace test | VERIFIED (superseded) | Superseded by 01-17 (see truth 15); the namespace test is still once, pre-run (script line 541). |
| 26 | 01-15: reason vocabulary and six-line grammar unchanged | VERIFIED | Line 9 present once; six lines in every run. |
| 27 | 01-15: behavior on Linux and macOS unchanged (rule is Windows-only) | VERIFIED (code reading) | `FinalPath.Step` guarded by `OperatingSystem.IsWindows()`; not run off Windows (see truths 34 and 45). |
| 28 | 01-15: stale handoff not tracked; `01-VALIDATION.md` lists the new tests | VERIFIED | Unchanged; `01-VALIDATION.md` carries the 01-16 and 01-17 rows (grep `held_directory`, `log_hold`). |
| 29 | 01-16: on Windows the output directory is opened without delete sharing before the first file is created and held until the fifth is in place; rename and delete of it and its ancestors are refused, proven from inside the write | VERIFIED | `HeldDirectory.cs` lines 28-31 (share mode 3, no delete share), `CreateHeld`; `A_held_directory_cannot_be_renamed_or_deleted_by_another_process_during_the_write` passes (behavioral: attacks inside the write via the `AfterHeld` seam). Summary's mutation (c) cited, not repeated. |
| 30 | 01-16: every temp file and move uses the handle's final path; a link entry re-created after the hold receives nothing | VERIFIED | `OutputWriter.WriteAll` passes `held.Path` to every `WriteText` (read, lines 56-60); `A_link_entry_retargeted_while_the_held_directory_is_open_does_not_redirect_the_write` passes. |
| 31 | 01-16: the held directory's final path is judged before the first byte; a swap between the second check and the hold is refused (exit 1, fixed text, nothing written, created directories removed) | VERIFIED | `JudgedFinalPath` and `RemoveCreated` (read); `A_swap_before_the_held_directory_is_opened_is_refused_by_its_final_path` and the `HeldDirectoryTests` order tests pass. Also observed live: cwd inside the repository with `--out C:\` is refused by this judgement. |
| 32 | 01-16: acquisition happens after the pipeline and after the text check, so a failure before the write leaves `--out` absent or untouched | VERIFIED | `CliApp.Run` order (lines 89-106 read); nested `--out` created only under the hold in the runs here. |
| 33 | 01-16: the final path is accepted in two forms only (drive letter judged, UNC accepted unjudged); every other form refused with the fixed text; an eight-row table pins it | VERIFIED | `HeldDirectory.Normalize` lines 73-91 (read); the 8-row theory passes. |
| 34 | 01-16 (backstop): on Linux and macOS no handle is held; the resolved path is judged before and after directory creation; the bound is stated; the swap-before-hold test runs there too | PRESENT_BEHAVIOR_UNVERIFIED (abstained: insufficient_spec) | `CreateNarrowed` (lines 137-160) does what the truth states and the paragraph is present at three CLI sites (`grep` count 3); the non-Windows run is not observed on this Windows host. See `behavior_unverified_items`. |
| 35 | 01-16: a run with no swap is unchanged (five files, same bytes, no `*.tmp-*`, the success line names the typed path) | VERIFIED | Normal runs here; `CliApp.Run` line 110 prints `outPath`, the typed full path. (This is also why CR-01 misreports.) |
| 36 | 01-16: one identical swap-interval paragraph at `RefuseUnsafeOut`, `HeldDirectory`, `OutputWriter.WriteAll`; one pointer sentence in `CliApp` and `FinalPath`; T-01-39 wording intact | VERIFIED | `grep -c 'Check-to-write interval (T-01-43, T-01-46)'` over the three files: 3; `grep -c 'separate bound (T-01-43, T-01-46)'` over CliApp and FinalPath: 2; "Still unresolved" sentence intact. |
| 37 | 01-16: no test, comment or truth claims subst, mapped drives or 8.3 names are refused; AR-04 not edited | VERIFIED | `grep -ci subst` on `HeldDirectory.cs` and `HeldDirectoryTests.cs`: 0 and 0; the AR-04 row is identical from plan 01-17's base (`5a1a4e2`) to HEAD (diff of `^[-+]\| AR-04` is empty). |
| 38 | 01-17: on Windows, both hosts, a `-LogPath` run opens the log's directory without delete sharing before vdiff starts, holds it until the log is written, judges its final path against the vdiff root, work tree and git directories; rename of it and its parent is refused during the run, proven by a staged test | VERIFIED | `Open-HeldDirectory` (script lines 387-444, read), hold taken before the snapshot (line 571) and closed in `finally` (line 660); `The_log_hold_blocks_a_rename_and_a_retargeted_link_entry_receives_no_log` passes on pwsh and powershell; my own probe shows the lock on both hosts. |
| 39 | 01-17: the log is written by temp name plus move through the held directory's final path; a retargeted link entry receives nothing; a missing chain is created under the judged anchor | VERIFIED | `Write-LogFile` (lines 462-488, read) called only as `Write-LogFile $logHold.Path` (grep `Write-LogFile $logFull` outside comments: 0); the staged test and `A_log_path_below_missing_directories...` pass on both hosts; my normal run created `logs\deep` and wrote the log. |
| 40 | 01-17: a protected verdict by handle ends the run with bad-arguments before vdiff starts; any other hold failure skips the log silently | PRESENT_BEHAVIOR_UNVERIFIED | Verdict function observed by my probe (both hosts); mapping at line 574 read. No repository test reaches it (review WR-03, confirmed by grep). See `behavior_unverified_items`. |
| 41 | 01-17: native calls are compiled with `Add-Type` lazily, only with `-LogPath` on Windows, from a fixed single-quoted two-declaration text | VERIFIED | `Initialize-HoldType` lines 305-316 (fixed single-quoted here-string, two `DllImport` members); called only inside the Windows branch of `Open-HeldDirectory`, which is called only when `$logFull` is set; `Add-Type` appears once outside comments. |
| 42 | 01-17: the CLI refuses an `--out` inside any `VDIFF_PROTECTED_DIRS` entry at both checks and in the held directory; a malformed value is refused with one fixed message naming no value | VERIFIED | `CliApp` lines 74-75, 172-175, 208-212, 223-239 (read); the eight CLI `protected_directories` cases pass (below, itself, through-link, relative, empty entry, nine entries, post-check swap, control). |
| 43 | 01-17: a staged script test swaps `-Out` for a link into a stand-in vdiff root during the run; the CLI refuses, nothing is written, six fixed lines | VERIFIED | `An_out_retargeted_into_the_vdiff_root_during_the_run_receives_nothing_protected_directories` passes on both hosts; the script passes the root only through the environment (`$protectedForChild`, lines 591 and 594; `$vdiffArgs` lines naming `VdiffRoot`: 0). The summary's mutation check is cited, not repeated. |
| 44 | 01-17: no command-line argument added or changed (D-03) | VERIFIED | `git diff --stat 65261d3 HEAD -- src/Vdiff.Core/Cli/CliOptions.cs` is empty; `grep -ci protect CliOptions.cs`: 0. |
| 45 | 01-17 (backstop): on Linux and macOS no handle is held and no type is compiled; the post-run re-check stays; the bound is stated in the script, the register and the validation map | PRESENT_BEHAVIOR_UNVERIFIED (abstained: insufficient_spec) | Off-Windows branch read (script lines 424-433, 609-613); statement present (script paragraph count 1; register rows). Not run off Windows. See `behavior_unverified_items`. |
| 46 | 01-17: the T-01-43 and T-01-46 rows and the residual line describe the new guarantee and its bound, the millisecond claim is gone, AR-04 unedited, the script carries the same paragraph and pointer sentences | VERIFIED | Rows at `01-SECURITY.md` lines 89 and 92 read; `grep -ci millisecond`: 0; `grep -c 'is not closed'`: 0; script paragraph 1, pointer 2. The plan's literal pattern "held across" is not in the file (advisory; wording only). |
| 47 | 01-17: no test, comment or truth claims subst, mapped drives or 5.1 8.3 names are now refused; AR-04's changed rationale is recorded, not edited | VERIFIED | Not-edited gates for 01-16 (`65261d3` to `a817266`) and 01-17 (`5a1a4e2` to `f4f1a05`) are empty for the files the plans forbid; the AR-04 row is unchanged through HEAD; the stale rationale is in `01-17-SUMMARY.md` "Flagged to the orchestrator" and in the human items above. |

**Score:** 43/47 truths verified; 3 present, behavior-unverified (34, 40, 45); 1 failed, partial (2).

### Deferred Items

None. No later roadmap phase covers the drive-root `--out` defect or the guard.

### Advisory (New Scope, Unevidenced)

See `advisory` in the frontmatter: WR-01 (long-path regression, unreproduced for the CLI), WR-02 (physical path in an I/O exception text), WR-03 to WR-06, IN-01 to IN-07 (plus the stale IN-08 row), the "held across" pattern miss, and the age of the SC4 trial record.

### Required Artifacts

`verify.artifacts` and `verify.key-links` were run for plans 01-16 and 01-17.

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| Plan 01-16 artifacts | 6 files | VERIFIED | `verify.artifacts` 6/6; `verify.key-links` 4/4 |
| `src/Vdiff.Core/Output/HeldDirectory.cs` | `Create`, `Normalize`, `GetFinalPathNameByHandleW`, acquisition order, non-Windows narrowing | VERIFIED (with gap) | 254 lines, two `DllImport`, handle share mode 3; the CR-01 defect is in `SplitAtDeepestExisting` |
| `src/Vdiff.Core/Output/OutputWriter.cs` | `WriteAll` writes only through a `HeldDirectory` path, creates no directory | VERIFIED | `Directory.CreateDirectory` outside comments: 0 |
| Plan 01-17 artifacts | 7 files | VERIFIED (6/7 by pattern) | `verify.artifacts` 6/7; the miss is the pattern "held across" in `01-SECURITY.md` (content present in other words, see advisory); `verify.key-links` 3/3 |
| `scripts/trace-run.ps1` | `Initialize-HoldType`, `Open-HeldDirectory`, `Close-HeldDirectory`, rewritten `Write-LogFile`, hold in `Invoke-Trace`, env pass-through | VERIFIED | read lines 295-488 and 530-662; ASCII only |
| Artifacts of plans 01-01 to 01-15 | as before | VERIFIED (carried) | sources unchanged apart from the files named; full run |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `CliApp.Run` | `HeldDirectory.Create` | WIRED | line 106, with `FindRefusal` as the judgement |
| `HeldDirectory` | `FinalPath` | WIRED | `StartsWithDriveLetter`, `UnsupportedRootMessage`, `UnresolvableMessage`, `Resolve` (via `FindRefusal`) |
| `OutputWriter.WriteAll` | `HeldDirectory` | WIRED | line 108 passes `held`; every `WriteText` uses `held.Path` |
| `trace-run.ps1` | `CliApp` (`VDIFF_PROTECTED_DIRS`) | WIRED | script line 594 sets it, `CliApp` line 75 reads it |
| `trace-run.ps1` `Open-HeldDirectory` | `HeldDirectory.Create` | WIRED (same rule, two implementations) | same order, same forms; one difference remains (CR-01: the script does not trim a root) |
| tests | fake sidecar delay mode | WIRED | staged tests wait on the pid file |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `diff.json`, `summary.mmd`, `report.html` | `DiffFile` | git object reads, sidecar extraction, `ModelDiffer` | Yes: runs here name modified `A` and added `B` | FLOWING |
| `vdiff.log` | `run.Stdout`/`run.Stderr` | the vdiff child process | Yes: file contains `vdiff: wrote 5 files to ...` | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| CR-01 drive-root `--out` | `dotnet vdiff.dll ... --out C:\` with a scratch current directory | exit 0, "wrote 5 files to C:\", five files in the current directory, none in `C:\` | FAIL (defect reproduced) |
| CR-01 with current directory inside the repository | same | exit 1, `--out must not lie inside the analysed repository`; repository status clean | PASS (guard holds) |
| Relevant tests | `dotnet test --filter "held_directory\|HeldDirectoryTests\|protected_directories\|log_hold\|AliasedOutTests\|FinalPathTests"` | 91 passed, 0 failed, 0 skipped (2 m 55 s) | PASS |
| Test count | `dotnet test --list-tests --no-build` | 555 | PASS (matches the claim) |
| Full suite, run once | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --no-build` | 555 passed, 0 failed, 0 skipped (12 m 26 s) | PASS |
| Script hold functions, both hosts (private probe of `Open-HeldDirectory`) | protected anchor, protected leaf, success | verdict thrown, nothing left behind, prefix stripped, rename of leaf and parent refused while held and allowed after close | PASS |
| Normal script run, nested `-Out` and `-LogPath` chains, both hosts | `trace-run.ps1 ... -LogPath <missing chain>` | pass, none, repo unchanged, five outputs, log present, no `*.tmp-*` | PASS |
| 8.3 short-path `-LogPath` into the repository, both hosts | `trace-run.ps1 -LogPath <short repo path>\include\shortlog\vdiff.log` | `bad-arguments`, no directory created in the repository | PASS (layer not separated) |
| Plan acceptance greps | see truths 36, 37, 41, 44, 46 | all as planned | PASS |
| Leftovers | `ls $TEMP \| grep -c '^vdiff-links-'`; `git status --short` | 0; only `.planning/config.json` (pre-existing) and this report | PASS |

The scratch repository, scratch outputs and probe scripts live in the session scratchpad outside the project and are not committed. The CR-01 reproduction does not change any tracked file.

### Probe Execution

Step 7c: SKIPPED. No `probe-*.sh` is declared by the plans or present under `scripts/`.

### Requirements Coverage

Requirement IDs declared in plan frontmatter, plans 01-01 to 01-17: SPK-02, TRC-01, TRC-02, TRC-03 (01-16 declares TRC-01; 01-17 declares TRC-01 and TRC-03). All four appear in `REQUIREMENTS.md` and in the roadmap's Phase 1 line. No orphaned Phase 1 requirement.

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| TRC-01 | 01-02 to 01-05, 01-12, 01-13, 01-15, 01-16, 01-17 | Offline run with explicit commits; reads git objects; repository unchanged | SATISFIED | Truths 1, 7 to 9, 11, 29 to 33, 42; repository status clean after every run here. CR-01 concerns where output lands, not this text. |
| TRC-02 | 01-01 to 01-03, 01-05 to 01-09, 01-11, 01-15 | C++ type and member diff as model JSON, diff JSON, Mermaid classDiagram, HTML, in the output directory | PARTIAL (one input) | Truths 1, 3, 6, 12 hold for every ordinary `--out`; CR-01 means the outputs are not written in the output directory for `--out` given as a drive root (gap on truth 2) |
| TRC-03 | 01-10, 01-12, 01-14, 01-15, 01-17 | Real private PR, outputs outside this repository, no identifiers recorded | SATISFIED (orchestrator-attested) | Truths 4, 13, 14, 38 to 43; UAT 5 pass; trial re-run recommended |
| SPK-02 | 01-01, 01-03, 01-06, 01-10, 01-14, 01-15 | ADR-001 recorded, tracer built on it | SATISFIED | Truths 5 and 17 |

**REQUIREMENTS.md mismatch (bookkeeping, not a code gap).** At HEAD `REQUIREMENTS.md` shows TRC-01 and TRC-03 as `[x]` and `Complete` (set by the 01-17 closing commit `f16ea0b`), while TRC-02 and SPK-02 still read `[ ]` and `Gaps Found`. Plan 01-14 task 3 expects `[x] **SPK-02**` and a `| SPK-02 | Phase 1 | Complete |` row; commit `cc570a1` made that edit and `2eaf361` reverted it on purpose (premature Complete while gaps were open), so that plan's verify cannot pass at HEAD. Against the evidence above: SPK-02 is satisfied and may be checked now; TRC-01 and TRC-03 are supported; TRC-02 should stay unchecked until CR-01 is fixed or accepted. The project's own convention (`2eaf361`, `b60fd72`) is to hold `Complete` marks while the phase verification is `gaps_found`, so the 01-17 executor's early marks of TRC-01 and TRC-03 are at odds with it; this is the orchestrator's call, I make no edit.

### Test Quality Audit

| Test file | Linked req | Active | Skipped | Circular | Assertion level | Verdict |
|-----------|-----------|--------|---------|----------|-----------------|---------|
| `Safety/HeldDirectoryTests.cs` | TRC-01 | 12 (4 facts, 8 rows) | 0 (one Windows-only early return) | none | behavioral for create, cleanup and dispose; value-level for the table | Adequate; no root-input case (CR-01) |
| `Safety/AliasedOutTests.cs` | TRC-01 | 34 | 0 (silent early returns off Windows and without the symlink privilege) | none | behavioral (exit, stderr text, snapshot, attacks from inside the run via hooks) | Adequate; gap for the drive-root input |
| `Trial/TraceRunScriptTests.cs` | TRC-03 | 57 on Windows | 0 | none | behavioral (staged runs wait on the pid file); source-order pins | Adequate; no test of the handle verdict (WR-03) |

Disabled tests on requirements: 0. Circular patterns: 0.

### Decision Coverage

Decisions D-02, D-03 and D-09 of `01-CONTEXT.md` are honored by plans 01-16 and 01-17: no argument added (truth 44), repository read-only, synthetic fixtures; no machine value found by the debt and privacy greps. Others unaffected. Not blocking.

### Prohibitions (judgment tier; LLM-judge verdicts are non-authoritative)

Plans 01-16 and 01-17 add eight; the seven of plans 01-13 to 01-15 were confirmed in UAT test 7 and are not repeated.

| Prohibition | Plan | Verdict | Flag |
|-------------|------|---------|------|
| MUST NOT write into or replace a file in the repository through a link or directory swap after the last check | 01-16 | Honoured on Windows (staged and unit tests pass; the held directory's judgement refuses even the CR-01 case when the current directory is the repository). Off Windows bounded by statement. | unverified-prohibition: human review recommended |
| MUST NOT add an argument, change the five names, the exit-code contract or the success line | 01-16 | Honoured: `CliOptions.cs` unchanged, names are constants, success line text unchanged. CR-01 shows the success line can name a place the files are not in. | unverified-prohibition: human review recommended |
| MUST NOT print or record a path, commit id, device name, user or machine name in any refusal this plan adds | 01-16 | Honoured for the refusals it adds (fixed texts). WR-02: the pre-existing I/O error branch now prints the held directory's physical path. | unverified-prohibition: human review recommended |
| MUST NOT leave a junction or link in the temp area; cleanup must not follow a link | 01-16 | Honoured: 0 `vdiff-links-*` entries after my runs. | unverified-prohibition: human review recommended |
| MUST NOT write the log or any file into the analysed or vdiff repository through a link or swap during the run | 01-17 | Honoured on Windows (staged test, my probe, normal runs). | unverified-prohibition: human review recommended |
| MUST NOT print or record a path, commit id, device name, user or machine name in the six lines, a refusal, a fixture or a committed file | 01-17 | Honoured: six fixed lines in every run here. | unverified-prohibition: human review recommended |
| MUST NOT add an argument to vdiff or a new reason to the script | 01-17 | Honoured: vocabulary line present once, unchanged; `CliOptions.cs` unchanged. | unverified-prohibition: human review recommended |
| MUST NOT leave a junction in the temp area; no cleanup may follow a link | 01-17 | Honoured: 0 leftovers. | unverified-prohibition: human review recommended |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/Vdiff.Core/Output/HeldDirectory.cs` | 186 | `TrimEnd` of separators before the existence test turns a root into its drive-relative form | Blocker (SC2 second clause; the gap) | CR-01 |
| `src/Vdiff.Core/Cli/CliApp.cs` | 117-120 | prints `e.Message` of I/O exceptions, now carrying the held physical path | Warning | advisory WR-02 |
| `HeldDirectory.cs`, `trace-run.ps1` | 216, 324 | handle opened without the extended-length prefix | Warning | advisory WR-01 |
| files modified by the phase | - | TBD, FIXME, XXX | none found | Debt-marker gate clear (grep over the nine files changed by plans 01-16 and 01-17) |

### Human Verification Required

#### 1. CR-01 disposition

**Test:** Decide whether to close the gap (do not trim a root in `HeldDirectory.SplitAtDeepestExisting`; add a root-input unit test and a `CliApp.Run` test with a scratch current directory; check the script's `Open-HeldDirectory` for the same input) or to accept it.
**Expected:** A gap-closure plan, or an explicit owner acceptance recorded as an override against SC2.
**Why human:** The fix is small and the exposure narrow; shipping a reproduced misdirected-output defect is the owner's call. An override is not suggested: this is a defect, not an intentional deviation.

#### 2. UAT test 1 (G-01-1) re-verification, and optionally test 3

**Test:** Review `01-17-SUMMARY.md` and this report against UAT test 1; optionally repeat test 3 (staged swap during a script run) by hand.
**Expected:** The owner confirms the swap-window scope (held directory, final-path writes, `VDIFF_PROTECTED_DIRS`) is closed on Windows and that the stated Linux and macOS bound is acceptable.
**Why human:** Closure of an owner-raised, owner-scoped gap.

#### 3. AR-04 sign-off on its actual basis

**Test:** Re-read AR-04 in `01-SECURITY.md` against `01-17-SUMMARY.md` "Flagged to the orchestrator".
**Expected:** Recorded acknowledgement that the row no longer rests on "a 5.1 script cannot call a final-path function without compiling code" and that subst, mapped drive letters, DOS 8.3 names and IN-04 stay accepted on the owner's sign-off alone.
**Why human:** Acceptance of risk is the owner's decision.

#### 4. Linux and macOS runs (truths 34 and 45)

**Test:** Run the suite, or the filters in `behavior_unverified_items[1]` and `[2]`, on a Linux and on a macOS host.
**Expected:** All pass; the non-Windows tests execute rather than return early.
**Why human:** Windows-only development host.

#### 5. Protected verdict by handle, end to end (truth 40)

**Test:** See `behavior_unverified_items[0]`.
**Expected:** `bad-arguments` with six fixed lines before vdiff starts; a type that cannot compile skips the log and the run passes.
**Why human:** No repository test reaches it; accept as follow-up (review WR-03) or add the test.

#### 6. Trial wrapper re-run on the private clone (recommended)

**Test:** Run `scripts/trace-run.ps1` once against the private clone with the current script; record only the six-line block.
**Expected:** pass, none, under budget, repo_unchanged yes, outputs_present yes, mermaid_syntax ok.
**Why human:** The existing record and UAT test 5 predate plans 01-16 and 01-17; private target content stays out of this repository.

#### 7. Eight new flagged prohibitions

**Test:** Confirm or overrule the verdicts in the Prohibitions section.
**Expected:** All read as honoured; note WR-02 under the 01-16 message prohibition.
**Why human:** Judgment tier: LLM-judge verdicts are non-authoritative.

### Gaps Summary

One gap, one root cause: `HeldDirectory.SplitAtDeepestExisting` trims the trailing separator of a root path, so `--out C:\` is acted on as `C:`, the current directory on that drive. The five outputs then land in the current directory while the success line names the drive root. This is the review's CR-01, reproduced here, introduced by plan 01-16, and fixed by testing the path length against `Path.GetPathRoot` before trimming, with a root-input unit test and a CLI test; the script's `Open-HeldDirectory` does not trim and so already differs. The repository-unchanged guarantee is not affected: the held directory's final-path judgement refused the same input when the current directory was inside the repository. TRC-02's wording is partly affected for that input; TRC-01's is not.

Everything else the product owner scoped for G-01-1 is achieved and observed on Windows, including the Windows-only swap tests on both PowerShell hosts and a full run of 555 tests with no failure. What remains outside the gap list is a Linux and macOS observation, one script path with no test (the handle verdict), owner decisions on the sign-offs, and a recommended trial re-run. The other 13 findings of the review (6 warnings, 7 info) fail closed or are coverage and wording gaps; `01-REVIEW-DISPOSITION.md` lists 15 rows (it keeps a stale IN-08) and all are still `open`.

---

_Verified: 2026-11-18_
_Verifier: Claude (gsd-verifier)_
