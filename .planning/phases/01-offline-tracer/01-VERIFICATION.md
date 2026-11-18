---
phase: 01-offline-tracer
verified: 2026-11-18T14:10:00Z
status: human_needed
score: 25/28 must-haves verified
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
covered_digest: "v4:sha256:2168249b4dc2b80047240b567cdbfd50472354859d084cdb9d0754d5cdbdfd7b"
behavior_unverified: 3
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 13/18
  gaps_closed:
    - "Gap 1 (truths 2 and 7): a junction whose substitute name is the GLOBALROOT form of the repository, or the bare device form, no longer carries --out, -Out or -LogPath into the repository. Reproduced independently in this verification: CLI exit 1 with the fixed line, script script-error on pwsh and Windows PowerShell 5.1, repository snapshot equal, nothing written inside it. Plain drive-letter junctions are still followed (no over-refusal)."
    - "Gap 2 (truth 16): the bound in the doc comments of CliApp.RefuseUnsafeOut, FinalPath.Resolve, the trace-run.ps1 header and the comment above Resolve-FinalPath now matches behavior (every non-drive reported link target refused; subst, mapped drives, 8.3 names and IN-04 named as unresolved)."
    - "Re-review WR-02 (necessity) and IN-01 (stale handoff): the redundant namespace re-test is gone; the handoff is no longer tracked."
  gaps_remaining: []
  regressions: []
behavior_unverified_items:
  - truth: "A file symbolic link planted at an output name is replaced by a regular file and its target keeps its bytes (01-13 truth 2, second half; T-01-42)"
    test: "On a host that can create file symbolic links (Linux or macOS, or Windows with Developer Mode or an elevated shell) run: dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter FullyQualifiedName~A_file_symbolic_link_planted"
    expected: "The test creates the link (does not return early), the run exits 0, the victim file is unchanged and new FileInfo(output).LinkTarget is null afterwards"
    why_human: "Symlink creation is refused on the development host (confirmed again here: 'Administrator privilege required'), so the test returns early and xunit reports it as passed. The hard-link half passes and exercises the same File.Move replace path, so the behavior is very likely right, but it is not shown for the symlink form."
  - truth: "trace-run.ps1 re-checks the -LogPath containment after the vdiff run and before the log write, so a link swapped in during the run is not written through (01-14 truth 5, verification: backstop, T-01-46). Abstained: insufficient_spec"
    test: "Run trace-run.ps1 with -LogPath in a real directory and a -VdiffDll that sleeps about 20 seconds (a small shim assembly); during the sleep replace the log's parent directory by a junction into the analysed repository; let the run finish"
    expected: "No log file appears in the repository, the six output lines keep their shape, and the repository snapshot is unchanged"
    why_human: "The only evidence is a source-order text test, which shows the call is written before the write, not that it runs or stops a swapped link. The plan states no deterministic swap exists. Under the backstop rule presence and wiring never qualify. Lines 395-408 of the script read correctly."
  - truth: "A Windows symbolic link (not a junction) whose target is relative or NT-absolute is refused by the same rule (01-15, verification: backstop; T-01-47). Abstained: insufficient_spec"
    test: "On a capable host run mklink (no /J) with a relative target, and again with an NT-absolute target, into a sandbox repository, then run vdiff --out below the link and trace-run.ps1 -Out / -LogPath below it"
    expected: "CLI exit 1 with the fixed unsupported-root line and nothing created; script script-error with six fixed lines; repository snapshot unchanged"
    why_human: "Creating such a link needs a privilege the dev host lacks (re-confirmed). The rule is a text test on the reported target, pinned at text level by the 18-row table and shown end to end for junctions; the symlink spelling itself has not been run. Review finding IN-07 makes the same point."
advisory:
  - finding: "WR-05 (re-review of 01-15): the new rule also refuses harmless Windows links (a relative symbolic link, a mounted-folder volume reported as Volume{...}\\) and prints the unsupported-root text, which says the path must begin with a drive letter when it does."
    category: other
    reason: "Fail-closed is the documented design (R-2, T-01-47, AR-13); the defect is the misleading message and an unexercised refused-but-harmless case. Not a goal blocker. Suggested fix is a separate fixed message for a link target."
    evidence_status: "code reading of FinalPath.cs lines 95-101 and the 18-row table; the relative-symlink case not run (no privilege)"
  - finding: "IN-07: no test builds a symbolic link, or a unc kind, through the CLI or the script; IN-08: the same ten-line T-01-39/T-01-41 paragraph exists in four places and can drift"
    category: other
    reason: "Coverage is stated, not wrong; the four copies currently agree (read in this verification)."
    evidence_status: "grep and reading of the four comment sites"
  - finding: "Carried, unchanged and out of this plan's scope: WR-01 (symlink half of the output-name test passes vacuously, same item as behavior-unverified truth 10), IN-02, IN-03 (TooManyLinksException name), IN-04, WR-03, WR-04, IN-05, IN-06"
    category: other
    reason: "Recorded in 01-REVIEW-DISPOSITION.md; none blocks the phase goal."
    evidence_status: "01-REVIEW-DISPOSITION.md"
  - finding: "SC4 trial record (docs/trials/phase-1-offline-tracer.md, run date 2026-11-17) predates plans 01-13, 01-14 and 01-15, which changed trace-run.ps1 and the CLI guards"
    category: other
    reason: "The script's normal path is covered by the 36+ script tests on both hosts and by a fresh normal CLI run here, so the trial stays valid in substance, but it was not re-run with the hardened wrapper."
    evidence_status: "git log of scripts/trace-run.ps1 versus the trial record date"
human_verification:
  - test: "File symbolic link planted at an output name is replaced (privileged host)"
    expected: "See behavior_unverified_items[0]"
    why_human: "No symlink privilege on the dev host"
  - test: "Staged link swap during a trace-run.ps1 run"
    expected: "See behavior_unverified_items[1]"
    why_human: "Backstop truth; no deterministic automated swap exists"
  - test: "Windows symbolic link (not junction) with a relative or NT-absolute target, CLI and script"
    expected: "See behavior_unverified_items[2]"
    why_human: "No symlink privilege on the dev host"
  - test: "Re-run the trial wrapper once on the private local clone with the hardened scripts/trace-run.ps1 (recommended, not required)"
    expected: "Six-line block: result pass, reason none, under budget, repo_unchanged yes, outputs_present yes, mermaid_syntax ok; only that block recorded"
    why_human: "Private target content must not enter this repository or CI; the existing record predates plans 01-13 to 01-15"
  - test: "Product-owner sign-off on the accepted residuals: T-01-39 (subst and mapped drive letters, DOS 8.3 short names, IN-04) and T-01-47 / AR-13 (every non-drive-letter Windows link target is refused, including a relative symbolic link)"
    expected: "Explicit acceptance recorded against AR-04 and AR-13, which carry plan-level acceptance only; the comment wording being accepted is now accurate (truth 16)"
    why_human: "An acceptance of risk is the owner's decision, not a verifiable fact"
  - test: "Seven judgment-tier prohibitions (plans 01-13, 01-14, 01-15), listed in the Prohibitions section, flagged unverified-prohibition"
    expected: "Reviewer confirms or overrules each non-authoritative verdict (all seven read as honoured)"
    why_human: "Judgment tier: an LLM-judge verdict is non-authoritative"
---

# Phase 1: Offline Tracer Verification Report

**Phase Goal:** A user can run `vdiff --repo <path> --base <sha> --head <sha> --out <dir>` offline against a local git repository and get a C++ type- and member-level diff in the output directory, with no Azure DevOps contact and no change to the repository that was read.
**Verified:** 2026-11-18 (HEAD `41a8efd`)
**Status:** human_needed
**Re-verification:** Yes, after gap-closure plan 01-15 (commits `8d6e84a`..`204030c`). The previous report (`gaps_found`, 13/18) stays in git history.

## Summary of the verdict

The three failed truths of the previous report (2, 7 and 16, one root cause) are closed, and I confirmed it by running the exploit again, not by reading the SUMMARY. Using a private script outside the project, I built three junctions with a raw reparse-point write (no privilege): substitute name `\??\GLOBALROOT\Device\HarddiskVolumeN\<repo>`, the bare device form `\Device\HarddiskVolumeN\<repo>`, and a drive-letter control `\??\X:\<repo>`. All three were live (a tracked file was visible through each), and the first two reported a non-drive-letter target. The junctions carried a drive-letter decoy print name.

| Case | Before (previous report) | Now |
|------|--------------------------|-----|
| CLI `--out <globalroot junction>\outx` | exit 0, five files written into the repository | exit 1, fixed unsupported-root line, nothing created, snapshot equal |
| CLI via the bare-device junction | not tested then | exit 1, same line, nothing created |
| CLI via the drive-letter control junction | exit 1, containment line | exit 1, containment line (unchanged) |
| `trace-run.ps1 -Out` via GLOBALROOT and bare, pwsh and Windows PowerShell 5.1 | `repo-changed`, `?? out2/` in the repository | `fail`, `script-error`, `unknown`, `no`, `skipped`, exactly six lines, no `out2` in the repository |
| `trace-run.ps1 -LogPath` via GLOBALROOT and bare, both hosts | `repo-changed`, log inside the repository | `script-error`, no log in the repository |
| Same two script calls via the control junction | `out-not-outside` / `bad-arguments` | unchanged |

No regression: a normal two-commit run exits 0 and writes the five files, with `summary.mmd` showing `A` modified (`+int y [added]`) and `B` added; a `mklink /J` junction to a directory outside the repository is still followed (exit 0, five files in the target); the repository's status, HEAD and refs are identical before and after. All scratch junctions were removed as links, no `vdiff-links-*` directory remains in the temp area, and `git status` shows only the pre-existing `.planning/config.json` modification.

What keeps the status from `passed` is not a code gap. Three truths are present and wired but cannot be behaviorally proven on this host (two need a symlink privilege, one is a backstop race), and Step 8 produced human items (owner sign-off on accepted residuals, flagged prohibitions, an optional trial re-run). Nothing blocks the phase goal.

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | SC1: a synthetic two-commit C++ repository run through `vdiff --repo --base --head --out` writes model JSON, diff JSON, a Mermaid summary and an HTML page with the expected added and modified types and members | VERIFIED | Fresh run here: exit 0, `base.model.json, diff.json, head.model.json, report.html, summary.mmd`; `summary.mmd` shows `A` modified with `+int y [added]` and `B` added. `TracerEndToEndTests` in the 134-test run pass. |
| 2  | SC2: a run never changes the repository it reads, and everything written lands in `--out` | VERIFIED | Status, HEAD and refs equal before and after every run in this verification, including the three junction cases that previously failed. `RepoUnchangedMatrixTests` pass in the 134. Previously failed; now closed. |
| 3  | SC3: Mermaid `classDiagram` with `classDef` per state and member tags passes a syntax check; the HTML page is one self-contained file | VERIFIED | `summary.mmd` seen above (classDef added/removed/modified, trailing `[added]` tags); `MermaidCliSyntaxTests`, `MermaidWriterTests` pass in the 134; writers and extraction/diff sources unchanged since the previous verification (`git diff daae5d0..HEAD` over `src`, `extractors`, `tools` lists only the gap-closure files). |
| 4  | SC4: a run on a small real C++ PR on a private clone finishes within the R26 budget, output outside this repository, recorded only as pass or fail | VERIFIED (orchestrator-attested) | `docs/trials/phase-1-offline-tracer.md` carries the six-line block (pass, none, under-1-min, yes, yes, ok). Not re-run (private clone). The trial predates plans 01-13 to 01-15; see advisory and the human item. |
| 5  | SC5: ADR-001 records the packaging choice and the tracer extracts through the chosen sidecar | VERIFIED | ADR status line reads `Accepted (PO, 2026-11-16)`; the sidecar ran in every manual run here (`extractors/cpp-ts/target/release/vdiff-cpp-ts.exe`). |
| 6  | Repository text cannot make the Mermaid text unparseable through a class id (plan 01-11) | VERIFIED | `MermaidWriter` unchanged; keyword-id tests in the passing run. |
| 7  | Links and junctions: an `--out`, `-Out` or `-LogPath` reached through a symbolic link or directory junction into the repository is refused, nothing written, repository unchanged (01-12, 01-13 K-6/T-01-41, 01-14 T-01-44, 01-15) | VERIFIED | Ordinary, volume-GUID, GLOBALROOT, bare-device and UNC junctions and chains: refused (live repro for the first, GLOBALROOT, bare and control; `AliasedOutTests`, `FinalPathTests`, `TraceRunScriptTests` pass). Previously failed; now closed. |
| 8  | 01-13: `--out` in the extended, device, loopback UNC or extended UNC namespace is refused by the CLI with the fixed line, nothing created; protected roots in such a namespace refused; no over-refusal | VERIFIED | `FinalPath.Step` lines 74-77; the namespace theory and mirror/sibling facts pass in the 134-test run. |
| 9  | 01-13: a hard link planted at any of the five output names is replaced by a regular file; the pointed-to file keeps its bytes | VERIFIED | `OutputWriter.WriteText` temp name plus `File.Move` (no `WriteAllBytes` outside comments); the hard-link test passes. |
| 10 | 01-13: a file symbolic link planted at an output name is replaced and its target is unchanged | PRESENT_BEHAVIOR_UNVERIFIED | Same replace path as truth 9, but the test returns early where the link cannot be created, as on this host. See `behavior_unverified_items`. |
| 11 | 01-13: a link swapped in for `--out` after the first check and before the write is caught by a second check immediately before `WriteAll` | VERIFIED | `CliApp.Run` lines 56-61 (seam, second `RefuseUnsafeOut`, `WriteAll`); the swap test passes in the 134-test run. |
| 12 | 01-13: refusal text for real containment byte-identical; bytes written unchanged; `--out` holds exactly the five names after success; no temp file after a failed write | VERIFIED | `RefuseUnsafeOut` text at lines 113 and 121 unchanged; `DeterminismTests`, `TracerEndToEndTests` and the failed-write test pass; fresh run listed exactly five files. |
| 13 | 01-14: `trace-run.ps1` refuses a non-drive-namespace `-Out` (out-not-outside) and `-LogPath` (bad-arguments), still passes a sibling directory, prints exactly six fixed lines, reason vocabulary unchanged, both hosts | VERIFIED | Script lines 361-373; `TraceRunScriptTests` pass on both hosts in the 134-test run; the reason vocabulary header line (line 9) is unchanged; six lines observed in every script run here; 0 non-ASCII characters. |
| 14 | 01-14: a hard link planted at the `-LogPath` entry is replaced by a regular file; the repository file keeps its bytes | VERIFIED | `Write-LogFile` lines 300-316 (attributes read without following, entry deleted, `CreateNew`); the log hard-link test passes on both hosts. |
| 15 | 01-14: the log write is preceded, after the vdiff run, by a second containment check (verification: backstop, T-01-46) | PRESENT_BEHAVIOR_UNVERIFIED (abstained: insufficient_spec) | Script lines 395-408 put the containment loop before `Write-LogFile` inside the swallow-all try/catch. Evidence is source order only. See `behavior_unverified_items`. |
| 16 | Failed truth 16 of the previous report (01-13 truth 9 / 01-14 truth 7 / 01-15): the doc comments name exactly what is refused and what remains unresolved | VERIFIED | Read in all four places: `FinalPath.cs` lines 44-55, `CliApp.cs` lines 91-102, `trace-run.ps1` lines 22-34 and 186-195. Each states that on Windows a link is followed only when its reported target is a drive-letter path, names GLOBALROOT, bare device, UNC, volume GUID, relative, no-drive and drive-relative targets as refused, and leaves exactly subst and mapped drives, 8.3 names and IN-04 unresolved, citing T-01-39 and T-01-41. This matches the behavior reproduced above. The old IN-03 sense is no longer cited as open. Previously failed; now closed. |
| 17 | SPK-02 is evidenced: ADR-001 Accepted and the tracer built on it | VERIFIED | Truth 5. The `[x]`/Complete mark in `REQUIREMENTS.md` was reverted deliberately by `2eaf361` and is orchestrator-owned bookkeeping (plan 01-15 R-6); see Required actions. |
| 18 | No Azure DevOps contact and no network use anywhere in the tool | VERIFIED | No network types in the sources read; all runs here used local scratch directories only. |
| 19 | 01-15: an `--out` below a junction whose substitute name is the GLOBALROOT or bare-device form of the repository is refused: exit 1, `vdiff: ` plus the fixed message, stdout empty, nothing created, snapshot unchanged | VERIFIED | Live repro above (both forms); `AliasedOutTests.An_out_through_a_junction_with_an_nt_namespace_target_...(globalroot, device-no-prefix)` pass. |
| 20 | 01-15: `trace-run.ps1` given `-Out` or `-LogPath` below such a junction reports fail, script-error, six lines, nothing written into the repository, on pwsh and Windows PowerShell 5.1 | VERIFIED | Live repro above on both hosts for both parameters and both forms (`repo_unchanged: unknown`, `outputs_present: no`, `mermaid_syntax: skipped`, no `out2`, no `vdiff.log` in the repository); the four script theories pass. |
| 21 | 01-15: every one of those tests first proves the junction is live; the junction is a raw reparse-point write needing no privilege | VERIFIED | `AliasedOutTests.cs` lines 219-241 assert `File.Exists(...include/a.h)` through the alias before the run; `LinkScratch.RawJunction` writes `FSCTL_SET_REPARSE_POINT` with no privilege (my own equivalent also worked unprivileged and was live). |
| 22 | 01-15: `FinalPath.Resolve` refuses on Windows every link whose reported target is not a drive-letter path; a drive-letter junction written by the raw helper is followed like a mklink junction | VERIFIED | `FinalPath.Step` lines 98-101 (`StartsWithDriveLetter(info.LinkTarget)` before `ResolveLinkTarget`); my control junction was refused only by containment (correct), a `mklink /J` junction to an outside directory was followed, and the 28 filtered tests (globalroot, device-no-prefix, unc, raw drive control, 18-row table) pass. |
| 23 | 01-15: the premise that the runtime reports the substitute name, not the print name, is asserted; NT tests use a drive-letter decoy print name | VERIFIED | Tests read (`AliasedOutTests.cs` lines 229-232 pass `links.Root` as the print name); my repro used the same decoy and the reported target was non-drive in both cases. |
| 24 | 01-15 (verification: backstop): a Windows symbolic link (not a junction) whose target is relative or NT-absolute is refused by the same rule | PRESENT_BEHAVIOR_UNVERIFIED (abstained: insufficient_spec) | The same text rule applies to both link types (`FinalPath.cs` line 98; `trace-run.ps1` line 226) and is pinned at text level by the reported-target table; no symbolic link could be created here. See `behavior_unverified_items`. |
| 25 | 01-15: the log-path re-check after the vdiff run no longer repeats the pure-text namespace test (WR-02); the file-system containment re-check stays before the log write | VERIFIED | `trace-run.ps1` lines 395-408 repeat only `Test-SameOrUnder`; `Test-NonDriveRoot $logFull` appears once outside comments (line 369). |
| 26 | 01-15: the script's reason vocabulary and six-line grammar are unchanged | VERIFIED | Header line 9 unchanged; six lines in the fixed order in every script run here. |
| 27 | 01-15: behavior on Linux and macOS is unchanged (the rule is Windows-only) | VERIFIED (code reading) | `FinalPath.Step` guards the new condition with `OperatingSystem.IsWindows()`; `Test-NonDriveRoot` returns `$false` unless `$script:IsWin`. Not run on a non-Windows host. |
| 28 | 01-15: the stale handoff is no longer tracked (IN-01); `01-VALIDATION.md` lists the new tests and the manual-only symbolic-link check | VERIFIED | `git ls-files docs/handoffs` empty; `01-VALIDATION.md` lines 95-97 and the `nt_namespace_target` rows. |

**Score:** 25/28 truths verified; 3 present, behavior-unverified (truths 10, 15, 24); 0 failed.

The plan's mutation (non-vacuity) record (rows and hosts that fail under a weakened rule) is cited from `01-15-SUMMARY.md`; I could not repeat it without editing source. The red commit `8d6e84a` touches only three test files (`git show --stat`).

### Deferred Items

None. No later roadmap phase covers the `--out` guard.

### Advisory (New Scope, Unevidenced)

| # | Finding | Category | Why Advisory |
|---|---------|----------|--------------|
| 1 | WR-05: harmless Windows links (relative symlink, mounted volume folder) are refused with a message that names the wrong problem | other | Fail-closed by design (R-2, T-01-47, AR-13); a message and coverage defect, not a goal defect |
| 2 | IN-07, IN-08, and the carried WR-01, IN-02, IN-03, IN-04, WR-03, WR-04, IN-05, IN-06 | other | Recorded in the review and disposition; none affects a must-have |
| 3 | SC4 trial record predates the hardening plans | other | Normal path covered by tests and a fresh run; re-run recommended as a human item |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Vdiff.Core/Cli/FinalPath.cs` | allow-list on the reported link target; volume GUID helper removed | VERIFIED | `verify.artifacts` 7/7 for plan 01-15; `IsVolumeGuidTarget` count 0; line 98 |
| `scripts/trace-run.ps1` | `Test-NonDriveRoot $target`; redundant re-test removed | VERIFIED | line 226; `Volume{` absent; 0 non-ASCII characters |
| `tests/Vdiff.Tests/Support/LinkScratch.cs` | `RawJunction`, `NtSubstituteName`, cleanup as links | VERIFIED | `Dispose` removes links first with non-recursive `Directory.Delete`, then `Root`; registered before the control call |
| `AliasedOutTests.cs`, `FinalPathTests.cs`, `TraceRunScriptTests.cs` | NT-namespace regression nets | VERIFIED | 28 filtered tests pass |
| `01-VALIDATION.md` | map rows, manual-only symlink row | VERIFIED | |
| Artifacts of plans 01-01 to 01-14 | as before | VERIFIED | `verify.artifacts` and `verify.key-links` pass for every plan 01-01 to 01-14 (all counts equal totals) |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `AliasedOutTests` / `TraceRunScriptTests` | `LinkScratch.RawJunction` | WIRED | `verify.key-links` 5/5 for plan 01-15 |
| `CliApp.RefuseUnsafeOut` | `FinalPath.Resolve` | WIRED | lines 116-119; the only link-reading site in `src/` is `FinalPath.Step` |
| `trace-run.ps1` | `FinalPath.cs` rule | WIRED | same predicate before the combine in both |
| `CliApp.Run` | `RefuseUnsafeOut` before `WriteAll` | WIRED | lines 59-61 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `diff.json`, `summary.mmd`, `report.html` | `DiffFile` | git object reads, sidecar extraction, `ModelDiffer` | Yes: the fresh run's output names modified `A` and added `B` | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Normal run, two-commit scratch repository | `dotnet vdiff.dll --repo --base --head --out <outside>` | exit 0, five files, correct classDiagram | PASS |
| GLOBALROOT junction, CLI | `--out <junction>\outx` | exit 1, fixed line, repository unchanged | PASS |
| Bare-device junction, CLI | same | exit 1, fixed line | PASS |
| GLOBALROOT and bare junctions, `trace-run.ps1 -Out` and `-LogPath`, pwsh and 5.1 | six-line output | `fail`, `script-error`, nothing written | PASS (8 of 8 combinations) |
| Control drive-letter junction into the repository | CLI and script | refused by containment (exit 1; `out-not-outside`, `bad-arguments`) | PASS |
| `mklink /J` junction to an outside directory | `--out <junction>\viajn` | exit 0, five files in the target | PASS (no over-refusal) |
| Filtered, new tests: `nt_namespace_target`, `raw_junction`, `reported_link_target` | `dotnet test --filter` | 28 passed, 0 failed (30 s) | PASS |
| Filtered regression: `AliasedOutTests`, `FinalPathTests`, `TraceRunScriptTests`, `RepoUnchangedMatrixTests`, `TracerEndToEndTests`, `MermaidCliSyntaxTests`, `DeterminismTests` | `dotnet test --no-build --filter` | 134 passed, 0 failed, 0 skipped (7 m 14 s) | PASS |
| Leftover links | `ls $TEMP | grep -c '^vdiff-links-'` | 0 | PASS |

The full xunit suite (last recorded 524 passed, 0 failed, 9 m 57 s, in `01-15-SUMMARY.md` and `01-VALIDATION.md`) was not repeated. The sources changed since the previous verification are confined to the gap-closure files, all of which sit inside the filtered runs. Scratch scripts used for the repro live in the session scratchpad outside the project and are not committed.

### Probe Execution

Step 7c: SKIPPED. No `probe-*.sh` is declared by the plans or present under `scripts/`.

### Requirements Coverage

Requirement IDs from plan frontmatter across all 15 plans: SPK-02 (01-01, 01-03, 01-06, 01-10, 01-14, 01-15), TRC-01 (01-02 to 01-05, 01-12, 01-13, 01-15), TRC-02 (01-01 to 01-03, 01-05 to 01-09, 01-11, 01-15), TRC-03 (01-10, 01-12, 01-14, 01-15). All four appear in `REQUIREMENTS.md` and in the roadmap's Phase 1 line; no orphaned Phase 1 requirement.

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| TRC-01 | 01-02 to 01-05, 01-12, 01-13, 01-15 | Offline run with explicit commits; reads git objects; repository unchanged | SATISFIED | Truths 1, 2, 7, 8, 9, 11, 12, 19, 22. Open only as human checks: the symlink forms (10, 24). |
| TRC-02 | 01-01 to 01-03, 01-05 to 01-09, 01-11, 01-15 | C++ type and member diff as model JSON, diff JSON, Mermaid classDiagram, HTML | SATISFIED | Truths 1, 3, 6, 12 |
| TRC-03 | 01-10, 01-12, 01-14, 01-15 | Run on a real private PR, outputs outside this repository, no identifiers recorded | SATISFIED (orchestrator-attested) | Truth 4, truths 13, 14, 20; trial re-run is a recommended human item |
| SPK-02 | 01-01, 01-03, 01-06, 01-10, 01-14, 01-15 | ADR-001 recorded, tracer built on it | SATISFIED | Truths 5, 17 |

### Test Quality Audit

| Test file | Linked req | Active | Skipped | Circular | Assertion level | Verdict |
|-----------|-----------|--------|---------|----------|-----------------|---------|
| `Safety/AliasedOutTests.cs` | TRC-01 | 23 | 0 (silent early returns: non-Windows guards and the no-privilege symlink fact) | none | behavioral (exit, stderr text, snapshot equality, liveness) | Adequate; the symlink fact is vacuous on this host (truth 10) |
| `Safety/FinalPathTests.cs` | TRC-01 | 37 (15 earlier plus 22 new, 18 of them table rows) | 0 | none | value and behavioral | Adequate; includes the premise assertion |
| `Trial/TraceRunScriptTests.cs` | TRC-03 | 49 on Windows | 0 | none | behavioral | Adequate; one source-text pin (truth 15) |

Disabled tests on requirements: 0. Circular patterns: 0. The silent early returns are advisory (review IN-02).

### Decision Coverage

All trackable `01-CONTEXT.md` decisions D-01 to D-11 are honored by shipped artifacts; the 01-15 plan covers D-02, D-03 and D-09 and its source audit lists the rest as unaffected. Not blocking.

### Prohibitions (judgment tier; LLM-judge verdicts are non-authoritative)

| Prohibition | Plan | Verdict | Flag |
|-------------|------|---------|------|
| MUST NOT modify any file other than the five output files, whether the repository or another file is reached through an alternate namespace, a link, a planted hard link or a swap | 01-13 | Honoured. Previously violated for the GLOBALROOT junction; now refused (repro). Swap interval after the second check is a stated residual (T-01-43). | unverified-prohibition: human review recommended |
| MUST NOT print a path, commit id or .NET exception text in any refusal the plan adds | 01-13 | Honoured: `UnsupportedRootMessage` and `UnresolvableMessage` are constant text; observed stderr in the repro carries no path. | unverified-prohibition: human review recommended |
| MUST NOT print or record any path, commit id, user or machine name in the six lines for any refusal the plan adds | 01-14 | Honoured: six fixed lines observed in every script run here. | unverified-prohibition: human review recommended |
| MUST NOT write the log to any file other than the `-LogPath` entry, whether another namespace, a link, a hard link or a swap | 01-14 | Honoured. Previously violated for the GLOBALROOT junction; now no log in the repository on both hosts (repro). | unverified-prohibition: human review recommended |
| MUST NOT write into the repository, or anywhere other than the five output names and the `-LogPath` entry, through a junction or symbolic link whose reported target is not a drive-letter path | 01-15 | Honoured for junctions (repro, GLOBALROOT, bare device, UNC row at resolver level); the symlink spelling is behavior-unverified (truth 24). | unverified-prohibition: human review recommended |
| MUST NOT print or record a path, commit id, device number, user name or machine name in any refusal this plan adds, in the six script lines, or in a committed file | 01-15 | Honoured: grep for `HarddiskVolume` followed by a digit across `*.cs`, `*.ps1`, `*.md` finds nothing; the test table uses the letter N; device name is read at run time. | unverified-prohibition: human review recommended |
| MUST NOT leave a junction in the system temp area, and no test cleanup may follow a junction into a real directory | 01-15 | Honoured: `LinkScratch.Dispose` deletes links non-recursively before `Root`; `vdiff-links-*` count 0 after both test runs; my own scratch cleanup used the same order. | unverified-prohibition: human review recommended |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `FinalPath.cs`, `trace-run.ps1`, `CliApp.cs` | 95-101, 224-227 | New refusal also refuses harmless relative links with a generic message | Warning | Advisory WR-05; fail-closed by design |
| Four comment blocks | - | Same ten-line paragraph copied | Info | Advisory IN-08 |
| files modified by the phase | - | TBD, FIXME, XXX | none found | Debt-marker gate clear (grep over `src`, `scripts`, `tests/Vdiff.Tests`, `extractors/cpp-ts/src`, `tools/mermaid-check/check.mjs`) |

### Human Verification Required

#### 1. File symbolic link replaced at an output name

**Test:** On a host that can create file symlinks, run `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter FullyQualifiedName~A_file_symbolic_link_planted` and confirm the test asserted rather than returned early.
**Expected:** exit 0, victim file unchanged, output no longer a link.
**Why human:** no symlink privilege on the dev host.

#### 2. Staged swap during a script run (backstop)

**Test:** Run `trace-run.ps1` with a slow `-VdiffDll` and swap the log's parent for a junction into the repository during the run.
**Expected:** no log in the repository; six lines unchanged; snapshot unchanged.
**Why human:** no deterministic automated swap exists.

#### 3. Windows symbolic link (not junction), relative or NT-absolute target

**Test:** On a capable host, `mklink` (no `/J`) into a sandbox repository, then `vdiff --out` and `trace-run.ps1 -Out` / `-LogPath` below it.
**Expected:** CLI exit 1 with the fixed line; script `script-error`; nothing written.
**Why human:** no symlink privilege on the dev host.

#### 4. Trial wrapper re-run on the private clone (recommended)

**Test:** Run `scripts/trace-run.ps1` once against the private clone with the hardened script; record only the six-line block.
**Expected:** pass, none, under budget, repo_unchanged yes, outputs_present yes, mermaid_syntax ok.
**Why human:** private target content stays out of this repository; the existing record predates plans 01-13 to 01-15.

#### 5. Owner sign-off on accepted residuals

**Test:** Review AR-04 (T-01-39: subst and mapped drive letters, DOS 8.3 names, IN-04) and AR-13 (T-01-47: every non-drive-letter Windows link target refused, including a relative symbolic link) in `01-SECURITY.md`.
**Expected:** explicit acceptance recorded; the comment wording is now accurate.
**Why human:** risk acceptance is the owner's decision.

#### 6. Seven flagged judgment-tier prohibitions

**Test:** Confirm or overrule the verdicts in the Prohibitions section.
**Expected:** all seven honoured.
**Why human:** LLM-judge verdicts are non-authoritative.

### Required actions for the orchestrator (bookkeeping, not gaps)

- `REQUIREMENTS.md`: the TRC-01, TRC-02, TRC-03 and SPK-02 checkboxes are unchecked and the traceability rows read `Gaps Found` (reverted on purpose by `2eaf361`). Set them to checked and `Complete` once the human items above are accepted.
- `01-REVIEW-DISPOSITION.md` still lists every finding as `open`. CR-01 (fixed and verified here), WR-02 (necessity) and IN-01 (fixed) can be marked `fixed`; WR-01 and the symlink-related IN-07 can be marked `deferred` to a privileged or Linux run; WR-05 is advisory.
- `01-SECURITY.md`: line 121 still says `01-VERIFICATION.md (status human_needed)` about an older report; update the wording after this report lands. T-01-41 and T-01-44 are re-closed there with the 01-15 mitigation, and this verification now supports that at behavior depth, not only grep depth.
- `01-UAT.md` (not modified here): the copy in the working tree still shows test 1 as `[pending]` with `status: testing`. If it carries recorded human results elsewhere, merge the human items above into it.

### Gaps Summary

No gaps. The one root cause behind the previous three failed truths (a one-entry denylist on a raw link target) is replaced by one allow-list on the reported target in both implementations, and the escape no longer reproduces on the CLI or on either PowerShell host. The phase goal, including its last clause (no change to the repository that was read), holds for every form I could build and run on this host. The remaining items are behavior-unverified truths that need a symlink-capable host or a race, plus owner decisions and a recommended trial re-run.

---

_Verified: 2026-11-18_
_Verifier: Claude (gsd-verifier)_
