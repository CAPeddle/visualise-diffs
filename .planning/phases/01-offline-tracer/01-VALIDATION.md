---
phase: "1"
slug: "offline-tracer"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-16"
validated: "2026-11-18"
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from
> `01-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 on Microsoft.NET.Test.Sdk 17.14.1 (.NET); `cargo test` (Rust sidecar) |
| **Config file** | `tests/Vdiff.Tests/Vdiff.Tests.csproj` (plan 01-02); `extractors/cpp-ts/Cargo.toml` (plan 01-01) |
| **Quick run command** | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo --filter "FullyQualifiedName~<Class>"` or `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml <module>` |
| **Full suite command** | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` then `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` |
| **Estimated runtime** | ~30 s per filtered run after a one-time ~76 s cold sidecar build (collection fixture) |

A filter that matches nothing exits 0 in both runners, so every verify greps for a non-zero
passed count (`Passed! +- Failed: +0, Passed: +[1-9]`, `test result: ok\. [1-9][0-9]* passed; 0 failed`).

---

## Sampling Rate

- **After every task commit:** the task's own filtered `dotnet test` or `cargo test <module>`
- **After every plan wave:** full `dotnet test` plus full `cargo test --locked`
- **Before `/gsd-verify-work`:** both full suites green; `git ls-files` shows no output or build paths
- **Max feedback latency:** ~30 s (after the cold sidecar build)

---

## Per-Requirement Verification Map

| Requirement / criterion | Behavior | Test Type | Automated Command | Plan |
|---|---|---|---|---|
| SPK-02 / SC5 | ADR-001 Accepted; sidecar pinned, versions reported | unit + grep | `cargo test --locked ...` version guard; `grep -c "^- \*\*Status:\*\* Accepted (PO, 2026-11-16)" docs/decisions/ADR-001-cpp-tree-sitter-packaging.md` | 01-01, 01-10 |
| SPK-02 / SC5 | extraction runs through the sidecar; metadata in every model | integration | `--filter "FullyQualifiedName~TracerEndToEndTests"` | 01-03 |
| TRC-01 / SC2 | repo byte-identical before/after in every state, incl. an fsmonitor-hook repo | integration | `--filter "FullyQualifiedName~RepoUnchangedMatrixTests"` | 01-04 |
| TRC-01 | hostile args never reach git as options; abbreviated-id shadowing rejected | integration | `--filter "FullyQualifiedName~HostileInputTests"` | 01-04 |
| TRC-01 | read-only git env and `-c` overrides | unit | `--filter "FullyQualifiedName~GitObjectReaderTests"` | 01-02 |
| TRC-02 / SC1 | diff.json lists exactly the expected types and members | integration | `--filter "FullyQualifiedName~TracerEndToEndTests"` | 01-03 |
| TRC-02 | member-level states, duplicates, canonical order | unit | `--filter "FullyQualifiedName~ModelDifferTests\|FullyQualifiedName~ModelBuilderTests"` | 01-07 |
| TRC-02 | extraction shapes (enums, operators, templates, preproc, error nodes, deadline + reset) | cargo | `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` | 01-01, 01-06 |
| TRC-02 / SC3 | Mermaid structure and injection safety | unit | `--filter "FullyQualifiedName~MermaidWriterTests"` | 01-08 |
| TRC-02 / SC3 | real Mermaid syntax check incl. a >50,000-char diagram (maxTextSize raised) | integration | `--filter "Category=MermaidCli"` | 01-09 |
| TRC-02 | HTML self-contained; byte-identical deterministic outputs, LF, no local path | unit + integration | `--filter "FullyQualifiedName~HtmlWriterTests\|FullyQualifiedName~DeterminismTests"` | 01-08 |
| TRC-01/02 | sidecar hang, flood, exit, garbage → exit 1, nothing written | integration | `--filter "FullyQualifiedName~SidecarBoundsTests"` | 01-05 |
| TRC-03 / SC4 | trial script prints six fixed lines, no leaks | integration | `--filter "FullyQualifiedName~TraceRunScriptTests"` | 01-10 |
| D-09 hygiene | no output/build dirs tracked; no remote use in tests | integration | `--filter "FullyQualifiedName~RepoHygieneTests"` | 01-04 |
| TRC-02 | keyword-named global types pass the real Mermaid syntax check | integration | `--filter "FullyQualifiedName~Writer_output_for_keyword_named_global_types"` | 01-11 |
| TRC-01 | `--out` through a directory link, link chain or loop is refused; loops fail closed | integration + unit | `--filter "FullyQualifiedName~AliasedOutTests\|FullyQualifiedName~FinalPathTests"` | 01-12 |
| TRC-03 | trial script refuses an aliased `-Out` (link, chain, loop) | integration | `--filter "FullyQualifiedName~TraceRunScriptTests.Aliased"` | 01-12 |
| TRC-01 (CR-01) | `--out` in a non-drive namespace or through a volume-GUID junction refused with the fixed line; protected root in such a namespace refused | integration + unit | `--filter "FullyQualifiedName~AliasedOutTests\|FullyQualifiedName~FinalPathTests"` | 01-13 |
| TRC-01 (WR-01) | hard link planted at an output name is replaced, target unchanged; no temp file after a failed write | integration | `--filter "FullyQualifiedName~AliasedOutTests"` | 01-13 |
| TRC-01 (WR-02) | link swapped in after the first check is refused before the write | integration | `--filter "FullyQualifiedName~An_out_swapped_for_a_link"` | 01-13 |
| TRC-01 | no over-refusal: sibling dir, lower-case drive with forward slashes, link outside the repo | integration + unit | `--filter "FullyQualifiedName~AliasedOutTests\|FullyQualifiedName~FinalPathTests"` | 01-13 |
| TRC-03 (CR-01) | script: namespace `-Out` → `out-not-outside`, namespace `-LogPath` → `bad-arguments`, volume-GUID junction → `script-error` (pwsh and Windows PowerShell 5.1) | integration | `--filter "FullyQualifiedName~TraceRunScriptTests.Aliased"` | 01-14 |
| TRC-03 (WR-01) | script: hard-linked `-LogPath` entry replaced, repository file unchanged | integration | `--filter "FullyQualifiedName~Aliased_log_path_hard_linked"` | 01-14 |
| TRC-03 (WR-02) | script: the by-name containment re-check sits between the vdiff run and the log write through the held directory (`Write-LogFile $logHold.Path`, one write call only), and the first namespace and containment checks stay before the run; the re-check is the one that stays where no handle is held (Linux and macOS); source-order pin, re-pinned by 01-17 (the runtime proof on Windows is the staged `log_hold` test) | unit (text) | `--filter "FullyQualifiedName~second_containment"` | 01-14, 01-17 |
| TRC-03 | script: several bad inputs at once report the highest-precedence reason (bad-arguments > out-not-outside > bad `-LogPath`) | integration | `--filter "FullyQualifiedName~Several_bad_inputs"` | 01-14 |
| TRC-01 (gap 1) | `--out` through a junction whose substitute name is the GLOBALROOT form or the bare device form of the repository is refused with the fixed line, nothing created, repository unchanged; resolver refuses globalroot, device-no-prefix and unc, follows a raw drive-letter junction, accepts a reported link target only when it is a drive-letter path (18-row table) | integration + unit | `--filter "FullyQualifiedName~AliasedOutTests\|FullyQualifiedName~FinalPathTests"`; token `nt_namespace_target` | 01-15 |
| TRC-03 (gap 1) | script: `-Out` and `-LogPath` below such a junction end with `script-error`, six fixed lines, nothing written into the repository (pwsh and Windows PowerShell 5.1) | integration | `--filter "FullyQualifiedName~TraceRunScriptTests.Aliased"`; token `nt_namespace_target` | 01-15 |
| TRC-01 / TRC-03 (gap 1) | all of the above in one run (28 tests on Windows) | integration + unit | `--filter "FullyQualifiedName~nt_namespace_target\|FullyQualifiedName~raw_junction\|FullyQualifiedName~reported_link_target"` | 01-15 |
| TRC-01 (G-01-1) | the held output directory refuses rename and delete of itself and of its parent and grandparent during the write on Windows; a link entry retargeted while it is held receives nothing; a swap between the second check and the hold is refused by the final path with the repository untouched; the final-path forms (drive letter judged, UNC accepted unjudged, every other shape refused) are pinned by an eight-row table; directories created before a refusal are removed; the hold is released on Dispose; off Windows the swap-before-the-hold and retarget tests still run | integration + unit | `--filter "FullyQualifiedName~held_directory\|FullyQualifiedName~HeldDirectoryTests"` (24 tests on Windows after plan 01-18); token `held_directory` | 01-16 |
| TRC-03 (G-01-1) | the script holds the log's directory without delete sharing through a real vdiff run (a sleeping fake sidecar keeps vdiff busy): rename of it and of its parent is refused, a link entry in the typed `-LogPath` that is deleted and re-created pointing at the analysed repository receives nothing, the log appears at the requested name in the held directory by temporary name plus move, no `*.tmp-*` remains and the repository snapshot is unchanged; a missing directory chain is created under the judged anchor; the order is pinned (hold before the run, one write by `$logHold.Path`, close in a `finally`, no write by the typed path); both PowerShell hosts on Windows | integration + unit (text) | `--filter "FullyQualifiedName~log_hold"` (5 tests on Windows); token `log_hold` | 01-17 |
| TRC-01 / TRC-03 (G-01-1) | the vdiff root as a protected directory: the CLI refuses an `--out` below, equal to or through a link to any `VDIFF_PROTECTED_DIRS` entry at both checks and in the held directory with the fixed text, refuses a malformed value (relative, empty entry, more than 8) with one fixed message that names the variable and not the value, and still writes an `--out` outside every entry; a staged script run swaps `-Out` for a link into a stand-in vdiff root during the run and nothing is written there (a mutation that stops the script passing the variable turns it red); the root travels in the environment and never as an argument | integration + unit (text) | `--filter "FullyQualifiedName~protected_directories"` (11 tests on Windows: 8 CLI, 3 script); token `protected_directories` | 01-17 |
| TRC-02 / SC2 (CR-01) | `--out` given as a drive root is held as that root and never as the process's current directory; the CLI case records the directory every file would go to through the `AfterHeld` seam and stops before the first byte, so nothing is written into a real root; the unit rows pin a root, a root with the alternate separator and a root with two trailing separators, three below-root spellings (none, separator, alternate separator) and a fact that the final-path judgement receives the root and a refusal creates nothing; the script's own functions (`Open-HeldDirectory`, `Get-FullPath`) agree with the CLI side on both PowerShell hosts and a mutation that trims unconditionally turns the script test red | unit + integration | `--filter "FullyQualifiedName~drive_root"` (10 tests on Windows: 7 unit, 1 CLI, 2 script); token `drive_root` | 01-18 |

*Status per task is tracked in the plan SUMMARY files.*

---

## Wave 0 Requirements

Existing infrastructure: none (greenfield). Plans 01-01 and 01-02 create all of it
(solution, test project, `SyntheticRepo`, `RepoSnapshot`, `SidecarFixture`, fake sidecar), so no
separate Wave 0 is needed. Complete: `SupportTests` pass, and the 01-12..01-14 helpers
(`PathSpellings`, `LinkScratch`) are in place.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real run on the PO's private local Windows clone inside the R26 budget, repo unchanged | TRC-03 / SC4 | Private target content must not enter the public repo or CI | Orchestrator runs `pwsh -NoProfile -File scripts/trace-run.ps1` with inputs supplied at run time; only the six-line pass/fail block is recorded (plan 01-10 T2) |
| npm package approval (mermaid-cli, puppeteer) | TRC-02 | Package-legitimacy gate flagged "too-new" | PO answers the 01-03 checkpoint; answer recorded as a `package-checkpoint:` line |
| File symbolic link planted at an output name is replaced, target unchanged | TRC-01 (WR-01) | `AliasedOutTests::A_file_symbolic_link_planted_at_an_output_name_is_replaced_and_its_target_is_unchanged` returns early without SeCreateSymbolicLink, which the dev host lacks; the hard-link test covers the same replace path | Run the filter `FullyQualifiedName~A_file_symbolic_link` in an elevated shell, with Developer Mode on, or on Linux/macOS, and confirm the test asserted (not returned early) |
| Doc comments name what is refused (every non-drive reported link target on Windows: GLOBALROOT, bare device, UNC, volume GUID, relative, drive-relative) and what stays unresolved (subst and mapped drive letters, DOS 8.3 short names, IN-04); cites T-01-39 and T-01-41 | TRC-01, TRC-03 | Wording is a human judgement; greps only check that the IDs are cited | Read the same bound in four places: the doc comments of `CliApp.RefuseUnsafeOut` and `FinalPath.Resolve` (`src/Vdiff.Core/Cli/`), the header of `scripts/trace-run.ps1` and the comment above `Resolve-FinalPath` (01-15); in `CliApp.RefuseUnsafeOut` and `FinalPath.Resolve` also confirm the last sentence of the T-01-39 paragraph, "The check-to-write interval is a separate bound (T-01-43, T-01-46), described where the output is written." (01-16) |
| Linux and macOS: the swap interval is bounded by statement, not closed | TRC-01 (G-01-1) | No handle is held off Windows, so no test can prove a closure; the bound is a written statement, and the tests cannot show that three comments say the same thing | Read the paragraph that begins "Check-to-write interval (T-01-43, T-01-46)" in three places: the doc comments of `CliApp.RefuseUnsafeOut` (`src/Vdiff.Core/Cli/`), the `HeldDirectory` class (`src/Vdiff.Core/Output/`) and `OutputWriter.WriteAll`, and confirm the three are word for word equal and state that the interval stays open off Windows (01-16); the same bound is stated a fourth time in the script, in the comment that begins "Check-to-write interval (T-01-43, T-01-46)" above the log hold in `Invoke-Trace` in `scripts/trace-run.ps1` (in the script's own terms and in ASCII), and in the T-01-43 and T-01-46 rows of `01-SECURITY.md` (01-17) |
| Staged swap during a script run | TRC-01, TRC-03 (G-01-1) | The automated `log_hold` and `protected_directories` tests replace the manual check of `01-UAT.md` test 3 for Windows; off Windows no handle is held, so no test can prove a closure | A human may repeat the swap on Linux or macOS: start `trace-run.ps1` with a `-LogPath` below a directory link and, while vdiff runs, delete and re-create the link pointing at the analysed repository, to see the open bound described in the script, in `01-SECURITY.md` and here (01-17) |
| Symbolic link (not a junction) whose target is relative or NT-absolute is refused on Windows | TRC-01, TRC-03 (gap 1) | Creating such a link needs a privilege the dev host lacks (Developer Mode, an elevated shell); the rule is pinned at text level by `A_reported_link_target_is_accepted_only_when_it_is_a_drive_letter_path` | On a capable host make `mklink` (no `/J`) with a relative target, or with an NT-absolute target, into a sandbox repository and run `vdiff --out` below it: expect exit 1 and the fixed unsupported-root line, nothing created. The relative form also runs as `A_relative_link_target_is_resolved_from_the_links_own_directory` on Linux and macOS, where it stays accepted (01-15) |
| A real run with `--out` at a drive root writes the five files into that root and none into the current directory | TRC-02 (CR-01) | No test writes into a real drive root, so the development machine's root stays clean; the automated rows prove that the held directory is the root and that the run stops before the first byte, and the existing tests prove every write goes through the held path | On a scratch volume the owner controls, run the CLI with `--out` set to that volume's root from an unrelated current directory and confirm five files appear in the root and none in the current directory (01-18) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (except the two deliberate checkpoints, 01-03 T3 and 01-10 T2)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [ ] Feedback latency < 60 s (filtered runs ~30 s; the full suite takes ~8 min)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-18 by validate-phase. Full suite: 565 passed, 0 failed (xunit, re-measured after plan 01-18), 105 passed (cargo).

## Validation Audit 2026-11-18

| Metric | Count |
|---|---|
| Gaps found | 4 |
| Resolved | 2 |
| Escalated | 2 |

## Validation Audit 2026-11-18

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |
