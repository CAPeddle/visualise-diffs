---
phase: 01-offline-tracer
verified: 2026-11-18T09:30:00Z
status: gaps_found
score: 13/18 must-haves verified
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
  - "src/Vdiff.Core/Vdiff.Core.csproj"
  - "src/Vdiff.Core/VdiffException.cs"
  - "tests/Vdiff.FakeSidecar/Program.cs"
  - "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs"
  - "tests/Vdiff.Tests/Output/MermaidWriterTests.cs"
  - "tests/Vdiff.Tests/Safety/AliasedOutTests.cs"
  - "tests/Vdiff.Tests/Safety/FinalPathTests.cs"
  - "tests/Vdiff.Tests/Support/LinkScratch.cs"
  - "tests/Vdiff.Tests/Support/PathSpellings.cs"
  - "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs"
  - "tools/mermaid-check/check.mjs"
  - "tools/mermaid-check/package-lock.json"
  - "tools/mermaid-check/package.json"
  - "vdiff.sln"
covered_digest: "v4:sha256:a19c02ee9644f00856c59dbc2ebc93726d43f4fabb67ec74f49a143163825d46"
behavior_unverified: 2
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 8/8
  gaps_closed:
    - "Previous CR-01 (--out as an extended-length or loopback UNC spelling): refused by the CLI and by trace-run.ps1 (plans 01-13, 01-14); named tests pass"
    - "Previous WR-01 (hard link planted at an output name or at -LogPath): replaced, not written through, in the CLI (temp name plus move) and in the script (delete entry, then create-new); named tests pass"
    - "Previous WR-02 (check once, long before the write): the CLI re-checks immediately before the write; the swap test passes. The script re-checks the log path after the vdiff run (source-order evidence only, see truth 15)"
    - "SPK-02 bookkeeping in REQUIREMENTS.md (checked, Complete)"
  gaps_remaining:
    - "New CR-01 of the re-review: a link whose NT-namespace target is not a Volume{ path still bypasses both guards"
  regressions: []
gaps:
  - truth: "SC2 / TRC-01 / 01-12 truth, re-asserted by 01-13 (K-6, T-01-41 'accept-list applied to every link target') and 01-14 (T-01-44): an --out, -Out or -LogPath reached through a symbolic link or directory junction into the analysed repository is refused and the repository is never changed"
    status: failed
    reason: "Reproduced in this verification (unprivileged, scratch directory outside the project). A directory junction whose substitute name is \\??\\GLOBALROOT\\Device\\HarddiskVolumeN\\<repo path> is reported by .NET (DirectoryInfo.LinkTarget) and by PowerShell (Get-Item .Target) with the \\??\\ stripped, i.e. as the relative-looking text GLOBALROOT\\Device\\.... FinalPath.Step and Resolve-FinalPathStep deny exactly one spelling (Volume{), so this text is combined with the link's parent directory, yields a drive-letter path that matches nothing, passes StartsWithDriveLetter / Test-NonDriveRoot, and the containment comparison finds no overlap. Result: CLI --out <junction>\\outx exits 0, 'wrote 5 files', git status shows '?? outx/' inside the repository; trace-run.ps1 -Out <junction>\\out2 and -LogPath <junction>\\vdiff.log (pwsh and Windows PowerShell 5.1) write into the repository and end 'result: fail, reason: repo-changed' (detected afterwards, not prevented). The control case, a plain mklink /J junction to the same repository, is refused as designed (exit 1, fixed message). No test in the 92-test filtered run or in the suite builds such a link (grep for GLOBALROOT in src, tests, scripts: no match)."
    artifacts:
      - path: "src/Vdiff.Core/Cli/FinalPath.cs"
        issue: "Step lines 82-97: IsVolumeGuidTarget is a one-entry denylist on the raw link target; ResolveLinkTarget then treats the prefix-stripped NT-namespace text as relative"
      - path: "scripts/trace-run.ps1"
        issue: "Resolve-FinalPathStep lines 210-220: same one-entry denylist ('Volume{'); IsPathRooted is false for the stripped text, so it is combined with the link's parent"
    missing:
      - "Replace the denylist with an allow-list on the raw link target in both resolvers: a junction's reported target must be drive-rooted (^[A-Za-z]:\\); refuse any target (junction or symbolic link) whose first segment is an NT namespace name (GLOBALROOT, UNC, Device, Volume{) with the existing fixed UnsupportedRootMessage / script-error"
      - "Regression tests, built unprivileged with FSCTL_SET_REPARSE_POINT and a GLOBALROOT substitute name: FinalPathTests, AliasedOutTests (--out below the junction, repository snapshot unchanged, fixed message), TraceRunScriptTests (-Out gives script-error, -LogPath gives script-error or bad-arguments, nothing written), on both PowerShell hosts"
      - "Cover the symbolic-link form with a \\??\\UNC\\host\\share target (reported without the prefix as UNC\\host\\share, so treated as relative); reviewer's reading only, not reproduced here"
  - truth: "01-13 truth 9 and 01-14 truth 7: the doc comments of CliApp.RefuseUnsafeOut, FinalPath.Resolve and the trace-run.ps1 header name exactly what remains unresolved after the plans"
    status: failed
    reason: "The comments list subst and mapped drive letters, DOS 8.3 names, IN-03 and IN-04 as the only unresolved forms and say a link whose target is a volume GUID path, and 'a path in another Windows namespace', are 'refused, not resolved (T-01-41)'. A junction with a GLOBALROOT target is not refused and is not named. IN-03 does not cover it: IN-03 is rooted-without-drive and drive-relative symbolic-link targets (\\foo, C:foo), a different shape. Same root cause as the gap above; closed by the same change."
    artifacts:
      - path: "src/Vdiff.Core/Cli/CliApp.cs"
        issue: "RefuseUnsafeOut comment lines 86-104"
      - path: "src/Vdiff.Core/Cli/FinalPath.cs"
        issue: "Resolve comment lines 25-50"
      - path: "scripts/trace-run.ps1"
        issue: "header lines 22-30 and comment above Resolve-FinalPath lines 176-187"
    missing:
      - "Re-word the T-01-39/T-01-41 bound after the allow-list lands, so it states only what is then really unresolved"
behavior_unverified_items:
  - truth: "A file symbolic link planted at an output name is replaced by a regular file and its target keeps its bytes (01-13 truth 2, second half; T-01-42)"
    test: "On a host that can create file symbolic links (Linux or macOS, Windows with Developer Mode or elevated), run: dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter FullyQualifiedName~A_file_symbolic_link_planted"
    expected: "The test creates the link, the run exits 0, the victim file is unchanged and new FileInfo(output).LinkTarget is null afterwards"
    why_human: "On the development host File.CreateSymbolicLink is refused (no privilege), the test returns early and xunit reports it as passed, so it proves nothing here. The hard-link half passes and exercises the same File.Move replace path, so the behavior is very likely right, but it is not shown for the symlink form. Confirm the link could be created before reading the pass as evidence."
  - truth: "trace-run.ps1 re-checks the -LogPath containment after the vdiff run and before the log write, so a link swapped in during the run is not written through (01-14 truth 5, verification: backstop, T-01-46). Abstained: insufficient_spec"
    test: "Run trace-run.ps1 with -LogPath in a real directory and a -VdiffDll that sleeps about 20 seconds (a small shim assembly); during the sleep replace the log's parent directory by a junction into the analysed repository; let the run finish"
    expected: "No log file appears in the repository, the six output lines are unchanged in shape, and the repository snapshot is unchanged"
    why_human: "The only evidence is a source-order text test (TraceRunScriptTests...second_containment...), which proves the call is written before the write, not that it runs or stops a swapped link. The plan itself says no deterministic swap exists. Under the backstop rule, presence and wiring never qualify as verification. Note review finding WR-02 (new): the second Test-NonDriveRoot is redundant (the string cannot change); the second Test-SameOrUnder is the part that carries the value."
advisory:
  - finding: "IN-01 (re-review): docs/handoffs/phase-01-gap-closure-interrupted-by-shutdown.md is a stale resume note committed to a public repository: it says plan 01-13 is in progress and 01-14 not started, and names a machine-local path and a GitHub account name"
    category: other
    reason: "Documentation hygiene, not code. Delete the file (it stays in git history) before the repository is published further."
    evidence_status: "file read: present, content stale against HEAD"
  - finding: "WR-02 (re-review, necessity): trace-run.ps1 line 391 re-tests Test-NonDriveRoot on $logFull, a string that cannot have changed since line 361, and TraceRunScriptTests pins the redundant call"
    category: other
    reason: "Confirmed by reading: $logFull is assigned once and Test-NonDriveRoot is a pure text test. Drop it and the matching assertion; keep the second Test-SameOrUnder."
    evidence_status: "code reading"
  - finding: "IN-02 (re-review): five script tests and four CLI tests return silently on non-Windows or without a privilege and report as passed; the source-order test matches comment text"
    category: other
    reason: "Coverage is overstated, not wrong. Use a visible skip. Related to the behavior-unverified item above."
    evidence_status: "grep of early returns in AliasedOutTests (lines 169, 187, 205, 283, 347) and the review"
  - finding: "IN-03 (re-review): TooManyLinksException is thrown for a null root and a null link target"
    category: other
    reason: "Misleading name only; behavior is right (one fixed message)."
    evidence_status: "code reading"
human_verification: []
---

# Phase 1: Offline Tracer Verification Report

**Phase Goal:** A user can run `vdiff --repo <path> --base <sha> --head <sha> --out <dir>` offline against a local git repository and get a C++ type- and member-level diff in the output directory, with no Azure DevOps contact and no change to the repository that was read.
**Verified:** 2026-11-18 (HEAD `b5ba14f`)
**Status:** gaps_found
**Re-verification:** Yes, after gap closure plans 01-13 and 01-14. The previous report (`human_needed`, 8/8) stays in git history.

## Summary of the verdict

The two hardening plans did what they promised for every spelling they enumerated, and the earlier gaps stay closed. But the fresh re-review's critical finding is real: I reproduced it independently, on the CLI and on `trace-run.ps1`, under both PowerShell hosts. A directory junction whose target is `\??\GLOBALROOT\Device\HarddiskVolumeN\<repo>` defeats both guards and the tool writes inside the analysed repository.

That falsifies the phase goal's last clause ("no change to the repository that was read") for this form, falsifies the roadmap guarantee that the guard exists to uphold (decision D-03, threat T-01-28), and makes plan 01-13's claim that the accept-list is applied "to every link target" untrue. It is recorded as a gap, not a residual. Reasons:

1. The previous verdict took the residual route (`human_needed`) because the owner had not decided. The owner has since decided: plan 01-13's objective records option (b), a hardening plan, for this very class. Accepting a bypass of the guard they chose to harden would reverse that.
2. It is reproduced, so the evidence gate for re-verification does not demote it; `FinalPath.cs` and `trace-run.ps1` were also modified after the previous verification stamp.
3. The threat register rates T-01-41 high with disposition mitigate, and `01-SECURITY.md` closed it at grep depth (the mitigation text exists). The mitigation does not hold against this form.
4. The fix is small and already specified by the reviewer: an allow-list on the raw link target in both resolvers plus three regression tests.

What narrows it, and why it is still blocking: creating such a junction needs raw `FSCTL_SET_REPARSE_POINT` access (`mklink /J` cannot spell it) but no privilege, and in the target product `--out` comes from pipeline code, so accidental occurrence is very unlikely. It is a deliberate-construction hole, not an accident path. If the owner wants to accept it anyway, the override block at the end of this report records that decision; I have not applied one.

Everything else verified: the CLI and script namespace refusals, the temp-name write, the pre-write re-check, byte identity, no over-refusal, the sibling case, the six-line script output and the reason vocabulary. A filtered run of the changed areas passed 92 of 92 here; the full suite was last run at 496 passed, 0 failed (validation record) and was not repeated.

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | SC1: a synthetic two-commit C++ repository run through `vdiff --repo --base --head --out` writes model JSON, diff JSON, a Mermaid summary and an HTML page with the expected added, removed and modified types and members | VERIFIED | Fresh run in this verification on a two-commit scratch repository (`struct A` gains a member, `struct B` added): exit 0, five files. `TracerEndToEndTests` in the 92-test run pass. |
| 2  | SC2: a run never changes the repository it reads, and everything written lands in `--out` and nowhere else | FAILED (partial) | Holds for every run the guard is meant to admit: `RepoUnchangedMatrixTests` pass, and the manual before/after check of the earlier verification still stands. Defeated by the GLOBALROOT junction (gap 1): exit 0, `?? outx/` in the repository. |
| 3  | SC3: Mermaid `classDiagram` with `classDef` per state and member tags passes a syntax check; the HTML page is one self-contained file | VERIFIED | `MermaidWriter`, `HtmlWriter` and the extraction/diff sources are unchanged since the previous verification (`git log --since` over them is empty); the earlier real syntax check and keyword-id test still stand. |
| 4  | SC4: a run on a small real C++ PR on a private clone finishes within the R26 budget, output outside this repository, recorded only as pass or fail | VERIFIED (orchestrator-attested) | `docs/trials/phase-1-offline-tracer.md` carries the six-line block. Not re-run, by instruction. The trial predates plans 01-13/01-14, which changed `trace-run.ps1`; the 36-test script suite passes on both hosts, so the wrapper's normal path is unchanged. |
| 5  | SC5: ADR-001 records the packaging choice and the tracer extracts through the chosen sidecar | VERIFIED | Unchanged by the gap plans; the sidecar ran in every manual run here. |
| 6  | Earlier gap closed: repository text cannot make the Mermaid text unparseable through a class id (plan 01-11) | VERIFIED | `MermaidWriter` unchanged since; keyword tests in the suite. |
| 7  | Links and junctions: an `--out`, `-Out` or `-LogPath` reached through a symbolic link or directory junction into the repository is refused, nothing written, repository unchanged (plan 01-12; K-6 and T-01-41 of 01-13; T-01-44 of 01-14) | FAILED | Refused for ordinary junctions and links, for `Volume{...}` targets, chains and loops (tests pass; control run exit 1). Not refused for a junction with an NT-namespace target other than `Volume{` (gap 1). |
| 8  | 01-13: an `--out` spelled in the extended, device, loopback UNC or extended UNC namespace is refused by the CLI: exit 1, the fixed unsupported-root line, nothing created; a protected root in such a namespace is refused; a Volume GUID junction is refused; no over-refusal | VERIFIED | `FinalPath.Step` lines 67-71 accept-list on the full path text; `AliasedOutTests` namespace theory (four rows), mirror, Volume-GUID-junction and sibling facts and `FinalPathTests` pass. Judged on the four spellings as the plan words it; its "closes CR-01" claim fails only for link targets (gap 1). |
| 9  | 01-13: a hard link planted at any of the five output names is replaced by a regular file; the file it pointed to keeps its bytes; the repository snapshot is unchanged | VERIFIED | `OutputWriter.WriteText` lines 63-90: random `*.tmp-*` name, `FileMode.CreateNew`, `File.Move(..., overwrite: true)`; `A_hard_link_planted_at_every_output_name...` passes; recorded RED before the change (victim overwritten). |
| 10 | 01-13: a file symbolic link planted at an output name is replaced and its target is unchanged | PRESENT_BEHAVIOR_UNVERIFIED | Code is the same replace path as truth 9, but the test returns early where the link cannot be created, as on this host (see `behavior_unverified_items`). |
| 11 | 01-13: a link swapped in for `--out` after the first check and before the write is caught by a second check immediately before `WriteAll`: exit 1, existing text, nothing created | VERIFIED | `CliApp.Run` lines 56-61 (hook, second `RefuseUnsafeOut`, `WriteAll`); `An_out_swapped_for_a_link_into_the_repository_after_the_first_check...` asserts the hook ran and the refusal; RED recorded in the SUMMARY (five files written through the junction). The interval between the second check and the first write remains, stated at the guard (T-01-43). |
| 12 | 01-13: refusal text for real containment byte-identical; bytes written unchanged (UTF-8 no BOM, LF); after success `--out` holds exactly the five names; no temp file after a failed write | VERIFIED | `RefuseUnsafeOut` lines 107-118 unchanged text; `A_failed_write_leaves_no_temporary_file_behind`, `DeterminismTests` and `TracerEndToEndTests` pass in the run. |
| 13 | 01-14: `trace-run.ps1` refuses a non-drive-namespace `-Out` (out-not-outside) and `-LogPath` (bad-arguments), fails closed on a Volume GUID link target (script-error), still passes a sibling directory, gives bad-arguments for an omitted `-Out`, prints exactly six fixed lines, reason vocabulary unchanged, both hosts | VERIFIED | `Test-NonDriveRoot` lines 168-171 and its uses at 195, 351, 354, 361, 391; the `Aliased_` theories pass on pwsh and Windows PowerShell 5.1 (`TraceRunScriptTests` pass in the 92); header vocabulary line unchanged; ASCII check 0 non-ASCII characters. |
| 14 | 01-14: a hard link planted at the `-LogPath` entry is replaced by a regular file; the repository file keeps its bytes | VERIFIED | `Write-LogFile` lines 292-308 (reads attributes without following, deletes the entry, `CreateNew`); `Aliased_log_path_hard_linked...` passes on both hosts. |
| 15 | 01-14: the log write is preceded, after the vdiff run, by a second containment check (verification: backstop, T-01-46) | PRESENT_BEHAVIOR_UNVERIFIED (abstained: insufficient_spec) | Lines 387-400 place the check before the write inside the swallow-all try/catch. Evidence is source order only; see `behavior_unverified_items`. |
| 16 | 01-13 truth 9 / 01-14 truth 7: the doc comments name exactly what remains unresolved | FAILED | Comments are present, cite T-01-39 and T-01-41 and name subst, 8.3, IN-03, IN-04, but call other namespaces and link targets "refused" while the GLOBALROOT junction is neither refused nor named (gap 2). |
| 17 | SPK-02 shown as checked and Complete in `REQUIREMENTS.md` (plan 01-14 task 3) | VERIFIED | `REQUIREMENTS.md` line 23 `[x] **SPK-02**`, line 106 `| SPK-02 | Phase 1 | Complete |`; no `Gaps Found` left. |
| 18 | No Azure DevOps contact and no network use anywhere in the tool | VERIFIED | No network types in the changed files; the repro used local scratch directories only. |

**Score:** 13/18 truths verified; 2 present, behavior-unverified (truths 10, 15); 3 failed (2, 7, 16, one root cause).

### Root cause of the failed truths

One defect in two implementations: a one-entry denylist (`Volume{`) on the raw link target. The runtime strips `\??\` from a junction's substitute name before reporting it, so every other NT-namespace absolute target (`GLOBALROOT\...`, and for symbolic links `UNC\host\share`) arrives as relative-looking text and is resolved against the link's parent. The `StartsWithDriveLetter` / `Test-NonDriveRoot` accept-list is then applied to the combined path, which always starts with a drive letter, so it cannot catch it. The accept-list needs to run on the raw target, and a junction's target (always absolute) must be drive-rooted.

### Reproduction (this verification)

All runs used a scratch directory outside the project and a private copy of the built tool (Debug build, newer than every source file). Junction created with a raw reparse-point write (no privilege).

| Step | Command (abridged) | Observed |
|------|--------------------|----------|
| Junction reported as | `Get-Item <scratch>\gr` | `LinkType: Junction`, `Target: GLOBALROOT\Device\HarddiskVolumeN\<scratch>\repo` (prefix stripped); `DirectoryInfo.LinkTarget` identical |
| CLI | `dotnet vdiff.dll --repo <repo> --base c1 --head c2 --out <scratch>\gr\outx` | exit 0, `wrote 5 files`; `git status --short` shows `?? outx/` |
| Control | same with a plain `mklink /J` junction to the repository | exit 1, `vdiff: --out must not lie inside the analysed repository`, nothing new in the repository |
| Script `-Out` | `trace-run.ps1 -Out <scratch>\gr\out2 ...` (pwsh) | `result: fail`, `reason: repo-changed`, `repo_unchanged: no`; `?? out2/` |
| Script `-LogPath` | `-Out <elsewhere> -LogPath <scratch>\gr\vdiff.log` (pwsh, then Windows PowerShell 5.1) | both `reason: repo-changed`; `?? vdiff.log`, `?? vdiff5.log` |

Cleanup: the two junctions were removed as links, the scratch tree was deleted, `git status` shows only the pre-existing `.planning/config.json` modification, and no `vdiff-links-*` directory is left in the temp area.

The script side detects the damage afterwards (the run is reported as failed) but the write has already happened, so for `-LogPath` and `-Out` the guard fails to prevent it. The symbolic-link form with a `\??\UNC\host\share\...` target is the reviewer's code-reading claim; I did not reproduce it (it needs the symlink privilege).

### Behavior-dependent truths

"Repository unchanged", the guard refusals, the hard-link replacement and the swap re-check are behavior-dependent. Truths 9, 11, 12, 13 and 14 each have a named test that ran and passed here (92 of 92 in the filtered run over `AliasedOutTests`, `FinalPathTests`, `TraceRunScriptTests`, `RepoUnchangedMatrixTests`, `TracerEndToEndTests`) and, for 9 and 11, a recorded RED step. Truths 10 and 15 have no behavior-exercising evidence on this host and are listed above rather than counted.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Vdiff.Core/Cli/FinalPath.cs` | accept-list on the full path text; Volume GUID refusal; fixed messages | VERIFIED (exists, substantive, wired) but incomplete for link targets | `verify.artifacts` 8/8 for plan 01-13; the link-target check is the denylist described above |
| `src/Vdiff.Core/Cli/CliApp.cs` | two `RefuseUnsafeOut` calls, internal `Run` seam | VERIFIED | Lines 20-24, 47, 56-61 |
| `src/Vdiff.Core/Output/OutputWriter.cs` | temp name plus move | VERIFIED | No `WriteAllBytes` outside comments |
| `src/Vdiff.Core/Vdiff.Core.csproj` | `InternalsVisibleTo` | VERIFIED | one item |
| `scripts/trace-run.ps1` | `Test-NonDriveRoot`, `Write-LogFile`, second check | VERIFIED but incomplete for link targets | `verify.artifacts` 3/3 for plan 01-14 |
| Tests: `PathSpellings`, `LinkScratch`, `AliasedOutTests`, `FinalPathTests`, `TraceRunScriptTests` | regression nets | VERIFIED, with no case for a non-`Volume{` NT target | no `GLOBALROOT` anywhere in src, tests or scripts |
| `REQUIREMENTS.md` | SPK-02 checked, Complete | VERIFIED | |
| Artifacts of plans 01-01 to 01-12 | as before | VERIFIED | `verify.artifacts` and `verify.key-links` pass for every plan 01-01 to 01-12 (all counts equal totals) as a regression check |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `CliApp.RefuseUnsafeOut` | `FinalPath.Resolve` | WIRED | `verify.key-links` 4/4 plan 01-13 |
| `CliApp.Run` | `RefuseUnsafeOut` before `OutputWriter.WriteAll` | WIRED | lines 59-61 |
| `Resolve-FinalPathStep` | `Test-NonDriveRoot` | WIRED | `verify.key-links` 4/4 plan 01-14 |
| `Invoke-Trace` | `Write-LogFile` after a second check | WIRED | lines 389-397 |

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Real data | Status |
|----------|---------------|--------|-----------|--------|
| `diff.json`, `summary.mmd`, `report.html` | `DiffFile` | git object reads, sidecar extraction, `ModelDiffer` | Yes: the scratch run's output names the added and modified types | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Normal run, two-commit scratch repository | built `vdiff.dll`, `--out` outside the repository | exit 0, five files | PASS |
| Plain `mklink /J` junction into the repository | `--out <junction>\outc` | exit 1, refusal, repository unchanged | PASS |
| Junction with a GLOBALROOT target into the repository | `--out <junction>\outx` | exit 0, `?? outx/` in the repository | FAIL (gap 1) |
| Same junction, `trace-run.ps1 -Out`, pwsh | six lines | `fail`, `repo-changed`; `?? out2/` | FAIL (gap 1) |
| Same junction, `trace-run.ps1 -LogPath`, pwsh and 5.1 | six lines | `fail`, `repo-changed`; log inside the repository | FAIL (gap 1) |
| Filtered suite: `AliasedOutTests`, `FinalPathTests`, `TraceRunScriptTests`, `RepoUnchangedMatrixTests`, `TracerEndToEndTests` | `dotnet test ... --filter` | 92 passed, 0 failed, 0 skipped (4 m 28 s) | PASS |
| Leftover links | `ls $TEMP | grep -c '^vdiff-links-'` | 0 | PASS |

A green suite next to a live reproduction means the regression test for this form is missing; it is listed under the gap's `missing`.

### Probe Execution

Step 7c: SKIPPED. No `probe-*.sh` is declared by the plans or present under `scripts/`.

### Requirements Coverage

Requirement IDs from plan frontmatter across all 14 plans: SPK-02 (01-01, 01-03, 01-06, 01-10), TRC-01 (01-02 to 01-05, 01-12, 01-13), TRC-02 (01-01 to 01-03, 01-05 to 01-09, 01-11), TRC-03 (01-10, 01-12, 01-14). Plan 01-14 also lists SPK-02. All four appear in `REQUIREMENTS.md` and in the roadmap's Phase 1 line; no orphaned Phase 1 requirement.

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| TRC-01 | 01-02 to 01-05, 01-12, 01-13 | Offline run with explicit commits; reads git objects; repository unchanged | PARTIAL, blocked by gap 1 | Unchanged for every normal run and every enumerated alias form (truths 1, 2 matrix, 8, 9, 11, 12); not for the GLOBALROOT-target junction. |
| TRC-02 | 01-01 to 01-03, 01-05 to 01-09, 01-11 | C++ type and member diff as model JSON, diff JSON, Mermaid classDiagram, HTML | SATISFIED | Truths 1, 3, 6, 12 |
| TRC-03 | 01-10, 01-12, 01-14 | Run on a real private PR, outputs outside this repository, no identifiers recorded | SATISFIED (orchestrator-attested), with the same script-side caveat | Trial record; the script reports the aliased run as failed, after the fact. Truth 13 |
| SPK-02 | 01-01, 01-03, 01-06, 01-10, 01-14 | ADR-001 recorded, tracer built on it | SATISFIED | Truths 5, 17 |

### Test Quality Audit

| Test file | Linked req | Active | Skipped | Circular | Assertion level | Verdict |
|-----------|-----------|--------|---------|----------|-----------------|---------|
| `Safety/AliasedOutTests.cs` | TRC-01 | 21 | 0 (silent early returns: non-Windows guards at 169, 187, 205, 347; no-privilege symlink at 283) | none | behavioral (exit code, stderr text, snapshot equality) | Adequate; the symlink fact is vacuous on this host (truth 10) |
| `Safety/FinalPathTests.cs` | TRC-01 | 15 | 0 | none | value | Adequate; no case for a non-`Volume{` NT target |
| `Trial/TraceRunScriptTests.cs` | TRC-03 | 36 on Windows | 0 | none | behavioral | Adequate; includes one source-text pin (truth 15) |

Disabled tests on requirements: 0. Circular patterns: 0. Insufficient assertions: the silent early returns (warning, see advisory IN-02).

### Decision Coverage

All 11 trackable CONTEXT.md decisions are honored by shipped artifacts (11/11, not blocking).

### Prohibitions (judgment tier; LLM-judge verdicts are non-authoritative)

| Prohibition | Plan | Verdict | Flag |
|-------------|------|---------|------|
| MUST NOT modify any file other than the five output files, whether the repository or another file is reached through an alternate namespace, a link, a planted hard link or a swap | 01-13 | Violated for a junction with a non-`Volume{` NT-namespace target (reproduced); honoured for the other listed forms | unverified-prohibition: human review recommended until gap 1 is closed |
| MUST NOT print a path, commit id or .NET exception text in any refusal the plan adds | 01-13 | Honoured: `UnsupportedRootMessage` and `UnresolvableMessage` are constant text; `Resolve` swallows the inner exception. (The pre-existing `IOException` text path, review IN-01, is outside the refusals this plan adds.) | unverified-prohibition: human review recommended |
| MUST NOT print or record any path, commit id, user or machine name in the six lines for any refusal the plan adds | 01-14 | Honoured: six fixed lines; `AssertNothingLeaks` in the script tests | unverified-prohibition: human review recommended |
| MUST NOT write the log to any file other than the `-LogPath` entry, whether spelled in another namespace, a link, a hard link, or swapped | 01-14 | Violated for the GLOBALROOT junction (log landed in the repository); honoured for namespaces and hard links | unverified-prohibition: human review recommended until gap 1 is closed |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/Vdiff.Core/Cli/FinalPath.cs` | 89-92, 115-116 | One-entry denylist on a raw link target | Blocker | Gap 1 |
| `scripts/trace-run.ps1` | 214-219 | Same denylist | Blocker | Gap 1 |
| `scripts/trace-run.ps1` | 391 | Redundant re-test of an unchanged string | Info | Advisory WR-02 (necessity) |
| `docs/handoffs/phase-01-gap-closure-interrupted-by-shutdown.md` | whole file | Stale resume note in a public repository | Info | Advisory IN-01 |
| files modified by the phase | - | TBD, FIXME, XXX | none found | Debt-marker gate clear |

### Review and security records

- `01-REVIEW.md` (committed `53215fb`): CR-01 critical, WR-01 and WR-02 warnings, IN-01 to IN-03 info. CR-01 confirmed and reproduced above; WR-01 (symlink half vacuous) confirmed from the test source and the host; WR-02 (necessity) confirmed by reading; IN-01 confirmed. `01-REVIEW-DISPOSITION.md` lists all 11 findings as `open`. Suggested: CR-01 `fixed` by the closure plan; new WR-01 `deferred` to a privileged or Linux run; new WR-02 `fixed` with the same plan; IN-01 `fixed` by deleting the handoff. The orchestrator sets these.
- `01-SECURITY.md` (`threats_open: 0`) is a grep-depth audit: it confirms the mitigation text exists at the cited lines, not that it holds. T-01-41 (high, mitigate) and T-01-44 (high, mitigate) are closed there; this verification shows both mitigations are incomplete for one form. Recommend re-opening them at the next `/gsd-secure-phase`. T-01-39 is an accepted residual with plan-level acceptance only and no explicit owner sign-off (AR-04); the comment wording the owner is accepting is the wording that gap 2 says is inaccurate, so sign-off should wait for the corrected text.
- `01-VALIDATION.md` (validated; two manual-only items): the file-symlink item is raised again above as a human check; the doc-wording item is superseded by gap 2.
- `01-UAT.md` test 1 is still `[pending]`, yet plan 01-13 records that the owner chose option (b). Its result should be recorded as resolved by plans 01-13/01-14; this verification supersedes the two items it was holding, so nothing from it carries forward as an open human item.

### Gaps Summary

One root cause, two failed truth groups. The hardening plans added an accept-list on the *path* and a denylist on the *link target*, and the denylist covers one NT-namespace spelling out of several. The runtime's own prefix-stripping converts the others into relative text that the accept-list then blesses. Fix both resolvers with an allow-list on the raw target (a junction target must be drive-rooted; refuse NT namespace first segments), add the three regression tests the reviewer specified (reproducible unprivileged, as shown here), re-word the T-01-39/T-01-41 comments, and re-run. After that, truths 2, 7 and 16 should flip, TRC-01 becomes satisfied, and the two behavior-unverified items remain as human checks (file-symlink replacement on a capable host; a staged swap during a script run).

Deferred items: none. No later roadmap phase covers the `--out` guard.

#### If the owner wishes to accept gap 1 instead

This is not an intentional deviation, so I do not recommend it. If the owner decides to accept it as a known limit of an operator-side safety net (the tool writes where it is told), record it with an override and widen the T-01-39 text to name the form:

```yaml
overrides:
  - must_have: "an --out, -Out or -LogPath reached through a symbolic link or directory junction into the analysed repository is refused"
    reason: "{owner's reason; a junction with an NT-namespace target other than Volume{ is accepted as a residual; T-01-39 widened to name it}"
    accepted_by: "{owner}"
    accepted_at: "{ISO timestamp}"
```

---

_Verified: 2026-11-18_
_Verifier: Claude (gsd-verifier)_
