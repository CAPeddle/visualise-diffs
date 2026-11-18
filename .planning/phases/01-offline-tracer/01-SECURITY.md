---
phase: "01"
slug: "offline-tracer"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-11-18"
---

# Phase 01 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

Source: `<threat_model>` blocks of `01-01-PLAN.md` to `01-14-PLAN.md` (14 of 14 parse) and `## Threat Flags` of the matching SUMMARY files (all 14 report "None beyond the plan's register"). Audited at grep depth (ASVS L1), `block_on: high`, branch `ccr-c12d6a45-a0eke4`, HEAD `7b7807d`. 46 numbered threat ids (T-01-01 to T-01-46) plus one supply-chain row `T-01-SC` per plan (14 rows; the id is re-used per plan, suffixed here with the plan number to keep rows unique). Later plans supersede earlier dispositions: T-01-40 (accept in 01-12) is mitigated by plan 01-13 and tracked as T-01-43. Plans 01-13 and 01-14 are the gap-closure plans for re-review findings CR-01, WR-01 and WR-02.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| vdiff to sidecar stdin / process (01-01, 01-02, 01-05) | the sidecar must not trust header, lengths or file bytes; the child can hang, flood or crash | request header, file bytes (untrusted source text) |
| source bytes to tree-sitter (01-01, 01-06) | possibly hostile text parsed by native code | repository file contents |
| CLI arguments to git (01-02) | commit ids and paths reach `git` as argument values | user-supplied commit ids, paths |
| analysed repository to vdiff (01-02, 01-04, 01-12, 01-13, 01-14) | tree contents, paths, blobs are untrusted; the repository is read-only for the tool (D-02) | repository objects, config |
| ambient environment / repository git config to child processes (01-02) | GIT_DIR family and `core.fsmonitor` can redirect git or run a command during a read | environment variables, repo config |
| CLI arguments to the tool / vdiff to `--out` (01-03, 01-12, 01-13) | the only place vdiff may write; the operator's path can reach the repository through a link, junction, Windows namespace spelling, planted hard link or swap after the check | output paths |
| repository text to Mermaid and HTML / to summary.mmd / summary.mmd to a renderer (01-03, 01-08, 01-11) | type names and signatures are untrusted text that becomes markup | identifiers, signatures |
| vdiff output to disk (01-08) | files under `--out` must not carry local identity | absolute paths, user and machine names |
| vdiff output to this public repository (01-03, 01-04, 01-10) | private target content must never end up in a committed file | any content from the private clone |
| vdiff output to a renderer (01-09) | the syntax check hands Mermaid text to a headless local browser | generated diagram text |
| npm registry / crates.io to this machine (01-01, 01-03, 01-09) | third-party packages built or installed locally | cargo crates, two flagged npm packages |
| script output to the orchestrator transcript; tracer run to the private clone (01-10) | stdout is shown to the session and product owner; the clone may hold another session's uncommitted work | six-line script output, clone state |
| `-Out`, `-LogPath` and the script's own log write (01-12, 01-14) | operator paths can reach the analysed repository; the log is the one file the script writes itself | log text, output paths |
| user glob patterns to the matcher; repository content to models (01-07) | patterns come from the command line; repeated definitions reach the differ | globs, type and member definitions |
| tests to analysed repository (01-04, 01-05) | tests stand in for a hostile user, repository and child process | synthetic fixtures only |

---

## Threat Register

Evidence is at grep depth: a file:line for the mitigation in the implementation, and the test that pins it where one exists. Paths are relative to the repository root.

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-01-01 | Tampering | commit-id handling in `GitObjectReader` (01-02) | high | mitigate | `GitObjectReader.cs:125-129` `ValidateCommitInput` (7 to 64 hex only); `:106` `rev-parse --verify --end-of-options`; `GitCommand.Build` takes an argument list, no shell. Pinned by `HostileInputTests.cs:82,96` (hostile base/head refused, never reach git) | closed |
| T-01-02 | Tampering | any git call against the analysed repository (01-02) | high | mitigate | `BoundedProcess.cs:258-266` `GitCommand` prefix `--no-optional-locks`, `RemovedVariables` GIT_DIR family; `RepoUnchangedMatrixTests.cs` before/after `RepoSnapshot` matrix | closed |
| T-01-03 | Information disclosure | fixtures, outputs, committed files (01-03) | high | mitigate | Synthetic-only fixtures (`tests/Vdiff.Tests/Support/SyntheticRepo.cs`); `.gitignore` rules checked by `RepoHygieneTests.cs:64` and `:90`; `--out` inside the repository refused (`CliApp.cs:47`) | closed |
| T-01-04 | Elevation of privilege | repository paths used as file names (01-01) | high | mitigate | Sidecar opens no files: no `File::open` or `std::fs` use in `extractors/cpp-ts/src/{main,protocol,extract}.rs` (negative grep); .NET side writes fixed output names (`OutputWriter.cs:11`) | closed |
| T-01-05 | Denial of service | sidecar subprocess and any child (01-02) | high | mitigate | `BoundedProcess.cs:84-92` timeout, stdout cap, concurrent stderr drain, `KillTree`; `SidecarBoundsTests.cs` (22 tests, 43 cases per 01-05 SUMMARY) | closed |
| T-01-06 | Tampering | repository text rendered into report.html and summary.mmd (01-03) | medium | mitigate | `HtmlWriter.cs:128` `Enc` = `WebUtility.HtmlEncode`, no script element or external URL (`:11-12`); `HtmlWriterTests.cs` (11 tests) | closed |
| T-01-07 | Denial of service | very large blobs (01-02) | medium | mitigate | `GitObjectReader.cs:76` 4 MiB per-blob cap, `:117,251` `cat-file --batch-check` before reading; oversized files unmodelled (`HostileInputTests.cs`) | closed |
| T-01-08 | Spoofing | inherited git environment, lazy fetch in partial clones (01-02) | medium | mitigate | `BoundedProcess.cs:264` env removal, `:281-299` `GIT_NO_LAZY_FETCH=1`; `GitObjectReader.cs:11` minimum git 2.45 | closed |
| T-01-09 | Elevation of privilege | sidecar located through env var VDIFF_CPP_TS (01-02) | low | accept | Accepted: same trust as PATH; named in the locator error text (`CppSidecar.cs:101,128`). See Accepted Risks Log AR-01 | closed |
| T-01-10 | Tampering | `--out` given as a symlink or junction into the analysed repository (01-04) | low | accept | Accepted in 01-04 as a documented limit; in effect superseded by 01-12, which resolves links (see T-01-36). See AR-02 | closed |
| T-01-11 | Information disclosure | generated vdiff or build output committed to the public repository (01-04) | high | mitigate | `RepoHygieneTests.cs:64` (ignore rules committed), `:90` (no tracked generated output), theory cases `:257-276` | closed |
| T-01-12 | Information disclosure | test or script code that clones, fetches or reaches a real repository (01-04) | high | mitigate | `RepoHygieneTests.cs:204` `Test_and_script_sources_cannot_reach_a_remote` over `tests/` and `scripts/`, scanner self-tests `:228-252` | closed |
| T-01-13 | Denial of service | hostile blobs: binary, oversized, missing object (01-04) | medium | mitigate | `HostileInputTests.cs` cases; oversized and missing objects become unmodelled entries | closed |
| T-01-14 | Denial of service | deeply nested or pathological source in the sidecar (01-06) | high | mitigate | `extract.rs:129` `MAX_DEPTH = 128` (checks `:158,245`); `main.rs:24,69` 64 MiB worker stack; process boundary keeps a crash out of vdiff | closed |
| T-01-15 | Tampering | macro names in the request header (01-06) | medium | mitigate | `protocol.rs:70-76,96-97` `is_ascii_identifier` rejection, test `:258`; `CliOptions.cs:20-21` length and count caps | closed |
| T-01-16 | Repudiation | partial extraction shown as complete (01-06) | medium | mitigate | `extract.rs:61` `count_error_nodes`, `:141` `extract_with_skips` counts depth-cap skips; ADR-001 requires labels | closed |
| T-01-17 | Denial of service | repeated type or member definitions (01-07) | medium | mitigate | `ModelBuilder.cs:68,92,96-116` `#n` suffix policy and `duplicates` warnings; `ModelBuilderTests.cs` | closed |
| T-01-18 | Information disclosure | outputs embedding absolute local paths, user or machine names (01-08) | medium | mitigate | `DeterminismTests.cs:191` `No_output_file_contains_the_sandbox_the_temp_directory_the_user_name_or_the_machine_name` | closed |
| T-01-19 | Denial of service | glob pattern abuse (01-07) | low | mitigate | `CliOptions.cs:22-23` `MaxIncludeGlobs = 64`, `MaxGlobLength = 512`; `CliOptionsTests.cs`, `ScopeFilterTests.cs` | closed |
| T-01-20 | Tampering | crafted member text altering Mermaid structure (01-08) | medium | mitigate | `MermaidWriter.cs:52,76` single `Escape`; `MermaidLint.cs` structural lint; `MermaidWriterTests.cs` injection test | closed |
| T-01-21 | Information disclosure | syntax check sending private content to an external renderer (01-09) | high | mitigate | `tools/mermaid-check/check.mjs` runs mermaid-cli through the local browser, no hosted renderer; `MermaidCliSyntaxTests.cs` uses synthetic output | closed |
| T-01-22 | Elevation of privilege | browser launched without its sandbox (01-09) | low | accept | `check.mjs:113-114` adds `--no-sandbox` only when `VDIFF_MERMAID_NO_SANDBOX` is set. See AR-03 | closed |
| T-01-23 | Information disclosure | script printing a path, commit id, name or count from the private run (01-10) | high | mitigate | `scripts/trace-run.ps1:9-11` fixed vocabulary; `TraceRunScriptTests.cs:84,176,215` assert exactly six lines, grammar, no leak | closed |
| T-01-24 | Information disclosure | committed record carrying target identifiers or figures (01-10) | high | mitigate | `docs/trials/phase-1-offline-tracer.md` carries no commit-id-like strings (grep for 12+ hex chars: none); `RepoHygieneTests` public-diff guard | closed |
| T-01-25 | Tampering | trial altering the private clone or another session's work (01-10) | high | mitigate | `trace-run.ps1:136-138` every git call carries `core.fsmonitor=false --no-optional-locks`; `:250,375` before/after `Get-RepoSnapshot`, `repo_unchanged`. Note: review finding WR-03 (open) says the snapshot ignores object database, config, hooks; the guards, not the snapshot, carry the control | closed |
| T-01-26 | Tampering | `-Out` pointing inside the clone or this public repository (01-10) | medium | mitigate | `trace-run.ps1:351-356` `out-not-outside`; `TraceRunScriptTests.cs:384,437,600` | closed |
| T-01-27 | Denial of service | oversized or malformed request header, overflowing lengths (01-01) | high | mitigate | `protocol.rs:15,17,80` 8 MiB header, 512 MiB total caps; `main.rs:107-112` fixed-line panic hook and read limit; cargo tests per 01-01 | closed |
| T-01-28 | Tampering | vdiff writing outside `--out` or inside the analysed repository (01-03) | high | mitigate | `CliApp.cs:47,60,105` `RefuseUnsafeOut`; `OutputWriter.cs:11` fixed file names; `TracerEndToEndTests.cs`, `RepoUnchangedMatrixTests.cs` | closed |
| T-01-29 | Denial of service | sidecar child left running after timeout or cap kill (01-05) | medium | mitigate | `BoundedProcess.cs:92` `KillTree`; `SidecarBoundsTests.cs` fake sidecar pid file asserts the process is gone | closed |
| T-01-30 | Elevation of privilege | repository-controlled `core.fsmonitor` (01-02) | high | mitigate | `BoundedProcess.cs:260` `-c core.fsmonitor=false` on every call via `GitCommand.Prefix`; `RepoUnchangedMatrixTests.cs:215-238` marker-hook scenario | closed |
| T-01-31 | Spoofing | branch or tag named like a commit abbreviation (01-02) | medium | mitigate | `GitObjectReader.cs:153-156` resolved id must start with the input; hostile test in `GitObjectReaderTests.cs` / matrix (01-04 SUMMARY) | closed |
| T-01-32 | Denial of service | pathological file makes one parse run very long (01-01) | high | mitigate | `extract.rs:28-35` progress-callback deadline, status `timeout`; `main.rs:208` timeout-then-next-file test; `SidecarBoundsTests.cs` end to end | closed |
| T-01-33 | Repudiation | syntax check reporting success on text it never parsed (01-09) | medium | mitigate | `check.mjs:36` `maxTextSize: 2_000_000, maxEdges: 100_000`; `MermaidCliSyntaxTests.cs:210,221` over-50,000-character valid and broken cases | closed |
| T-01-34 | Denial of service | `MermaidWriter.ClassId` bare fqn id equal to a Mermaid keyword (01-11) | medium | mitigate | `MermaidWriter.cs:91-101` every id prefixed `C_`; `MermaidWriterTests.cs` keyword theory, end-to-end through the real parser | closed |
| T-01-35 | Repudiation | lint and syntax tests giving false assurance on keyword-prone ids (01-11) | medium | mitigate | `tests/Vdiff.Tests/Output/MermaidLint.cs:12-17` requires the `C_` prefix on class ids and edge endpoints | closed |
| T-01-36 | Tampering | `CliApp.RefuseUnsafeOut` comparing lexical paths; `--out` through a link into the repository (01-12) | high | mitigate | `FinalPath.cs:56-61` `Resolve`, used by `CliApp.cs` `RefuseUnsafeOut`; `AliasedOutTests.cs:58-94`, `FinalPathTests.cs` | closed |
| T-01-37 | Tampering | `Test-SameOrUnder` in `trace-run.ps1`: `-Out` and `-LogPath` through a link (01-12) | high | mitigate | `trace-run.ps1:188-230` `Resolve-FinalPath` inside `Test-SameOrUnder`; `TraceRunScriptTests.cs:413,437,456` on both hosts | closed |
| T-01-38 | Information disclosure | unresolvable link making the CLI print a .NET exception with a full path (01-12) | low | mitigate | `FinalPath.cs:14,61` fixed `UnresolvableMessage`, no inner message; `FinalPathTests.cs` exact-stderr comparison; script prints six fixed lines | closed |
| T-01-39 | Tampering | `--out` spelled through a subst drive letter or a DOS 8.3 short name (01-12) | low | accept | Accepted residual, rationale in `01-12-PLAN.md` register and the `01-12-SUMMARY.md` "Accepted residual (T-01-39)" section; bound stated at the guard (`CliApp.cs:91`, `FinalPath.cs:43`, `trace-run.ps1:23,182`). Still residual after 01-13 and 01-14 (also IN-03 drive-relative and rooted-without-drive link targets, IN-04 case-insensitive file systems outside Windows). See AR-04 | closed |
| T-01-40 | Tampering | link swapped between the check and the write (01-12) | low | accept | Superseded: disposition changed accept to mitigate by plan 01-13 prose record; mitigation and residual tracked as T-01-43. Not a separate open item | closed (superseded by T-01-43) |
| T-01-41 | Tampering | `FinalPath.Resolve` and `RefuseUnsafeOut` with a non-drive Windows namespace (01-13) | high | mitigate | `FinalPath.cs:18-20,36,65-91` accept-list on full path text, fixed `UnsupportedRootMessage`, volume GUID link target refused; `FinalPathTests.cs`, `AliasedOutTests.cs` | closed |
| T-01-42 | Tampering | `OutputWriter.WriteText`: hard link or file symlink planted at an output name (01-13) | high | mitigate | `OutputWriter.cs:55-70` random temp name with `FileMode.CreateNew`, then `File.Move` overwrite; no `WriteAllBytes`; residual (a killed process can leave one `*.tmp-*` in `--out`, never in the repository) is documented | closed |
| T-01-43 | Tampering | `CliApp.Run`: link swapped between check and write (01-13; was T-01-40) | low | mitigate | `CliApp.cs:59-61` second `RefuseUnsafeOut` immediately before `OutputWriter.WriteAll`, doc at `:100-101`; swap test via the internal seam (`b1c7c2c`). Residual (millisecond interval) stated at the code | closed |
| T-01-44 | Tampering | `Test-SameOrUnder` and `Invoke-Trace`: non-drive namespace or volume GUID link target (01-14) | high | mitigate | `trace-run.ps1:164-168` `Test-NonDriveRoot`, `:195` in `Resolve-FinalPathStep`, `:218` `Volume{` target throws, `:351-361` applied to `-Out` and `-LogPath`; `TraceRunScriptTests.cs:480,513,547,600` | closed |
| T-01-45 | Tampering | `-LogPath` write: hard link or file symlink pre-planted at the log path (01-14) | high | mitigate | `trace-run.ps1:287-305` `Write-LogFile` deletes the directory entry with `[IO.File]::Delete` and creates with `FileMode.CreateNew`; `TraceRunScriptTests.cs:631` hard-link test. File symlink form not script-tested (no privilege on dev host); covered by rule and the plan 01-13 C# test | closed |
| T-01-46 | Tampering | log path swapped for a link between first check and log write (01-14) | low | mitigate | `trace-run.ps1:389-397` namespace test and containment loop rerun immediately before `Write-LogFile`; pinned by source-order test `TraceRunScriptTests.cs:309`; residual interval stated | closed |
| T-01-SC (01-01) | Tampering | cargo crates tree-sitter, tree-sitter-cpp, rayon, serde, serde_json | high | mitigate | ADR-001 pins; `extractors/cpp-ts/Cargo.toml:7-8` exact pins, committed `Cargo.lock`, legitimacy check recorded in 01-01 | closed |
| T-01-SC (01-02) | Tampering | NuGet packages (FileSystemGlobbing, xunit stack) | low | accept | Accepted: NuGet is outside the cargo and npm legitimacy gate; Microsoft and xunit.net releases (`Vdiff.Core.csproj:12`, `Vdiff.Tests.csproj:12-14`). See AR-05 | closed |
| T-01-SC (01-03) | Tampering | npm `@mermaid-js/mermaid-cli` and `puppeteer` (both SUS: too-new) | high | mitigate | Blocking human checkpoint answered: `01-03-SUMMARY.md:168` `package-checkpoint: approved mermaid-cli=12.2.0 puppeteer=25.17.0`; installed in 01-09 | closed |
| T-01-SC (01-04) | Tampering | no new packages | low | accept | Accepted: no package install in the plan. See AR-06 | closed |
| T-01-SC (01-05) | Tampering | no new packages | low | accept | Accepted: fake sidecar uses the SDK only. See AR-07 | closed |
| T-01-SC (01-06) | Tampering | no new crates | low | accept | Accepted: `Cargo.lock` unchanged since 01-01 (`git log` shows only `6d37bd5`). See AR-08 | closed |
| T-01-SC (01-07) | Tampering | no new packages | low | accept | Accepted: no new install in the plan. See AR-09 | closed |
| T-01-SC (01-08) | Tampering | no new packages | low | accept | Accepted: no install in the plan. See AR-10 | closed |
| T-01-SC (01-09) | Tampering | npm packages `@mermaid-js/mermaid-cli` and `puppeteer` | high | mitigate | `tools/mermaid-check/package.json:11-12` exact pins equal the checkpoint line; `package-lock.json` has 202 `integrity` entries; `npm ci --ignore-scripts` recorded in `01-09-SUMMARY.md:84,166` | closed |
| T-01-SC (01-10) | Tampering | no new package installs | low | accept | Accepted: uses the lockfile approved at the 01-03 checkpoint. See AR-11 | closed |
| T-01-SC (01-11) | Tampering | npm/pip/cargo installs | high | mitigate | No manifest or lockfile touched: `Cargo.lock` and `package-lock.json` last changed in 01-01 and 01-09 commits | closed |
| T-01-SC (01-12) | Tampering | npm/pip/cargo installs | high | mitigate | No manifest or lockfile touched (same git history check) | closed |
| T-01-SC (01-13) | Tampering | npm/pip/cargo installs | high | mitigate | No manifest or lockfile touched (same git history check; the one csproj change in `b1c7c2c` adds no `PackageReference`) | closed |
| T-01-SC (01-14) | Tampering | npm/pip/cargo installs | high | mitigate | No manifest or lockfile touched (same git history check) | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

"Accepted By" is the plan that recorded the acceptance; none of these has a separate product-owner sign-off in the plan files. `01-VERIFICATION.md` (status `human_needed`) lists a pending product-owner decision on the T-01-39 residual wording (see AR-04).

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-01 | T-01-09 | Sidecar path comes from an env var set by the invoking user on their own machine; same trust as PATH; named in the locator error text. Source: `01-02-PLAN.md` | plan 01-02 | 2026-11-16 |
| AR-02 | T-01-10 | The user chooses `--out` on their own machine; a link target was not resolved; the trial script compared paths textually. Source: `01-04-PLAN.md`, `01-04-SUMMARY.md:169`. Superseded in effect: plan 01-12 resolves links (T-01-36, T-01-37) | plan 01-04 | 2026-11-16 |
| AR-03 | T-01-22 | `--no-sandbox` is added only when the user sets `VDIFF_MERMAID_NO_SANDBOX` for a root shell; default launches with the sandbox. Source: `01-09-PLAN.md` | plan 01-09 | 2026-11-16 |
| AR-04 | T-01-39 | subst and mapped drive letters and DOS 8.3 short names are not resolved: doing so needs a Win32 final-path call that the PowerShell 5.1 script cannot make without compiling code, and short-name matching differs between .NET Framework and .NET Core. Every link form and every non-drive namespace is refused, the common cases are refused, and the limit is stated at `CliApp.cs:91`, `FinalPath.cs:43` and `trace-run.ps1:23,182`. Source: `01-12-PLAN.md`, `01-12-SUMMARY.md` "Accepted residual (T-01-39)". `01-13-SUMMARY.md:80` and `01-14-SUMMARY.md:75` call it "still open" and add review findings IN-03 and IN-04 to the residual; the disposition stays accept, the wording is not a re-opening | plan 01-12 | 2026-11-17 |
| AR-05 | T-01-SC (01-02) | NuGet is outside the cargo and npm legitimacy gate; packages are Microsoft and xunit.net releases at the SDK template versions. Source: `01-02-PLAN.md` | plan 01-02 | 2026-11-16 |
| AR-06 | T-01-SC (01-04) | No package-manager install in the plan. Source: `01-04-PLAN.md` | plan 01-04 | 2026-11-16 |
| AR-07 | T-01-SC (01-05) | No package install; fake sidecar uses the SDK only. Source: `01-05-PLAN.md` | plan 01-05 | 2026-11-16 |
| AR-08 | T-01-SC (01-06) | No new crates; `Cargo.lock` unchanged. Source: `01-06-PLAN.md`, `01-06-SUMMARY.md` | plan 01-06 | 2026-11-16 |
| AR-09 | T-01-SC (01-07) | No new packages; FileSystemGlobbing was added in 01-02. Source: `01-07-PLAN.md` | plan 01-07 | 2026-11-16 |
| AR-10 | T-01-SC (01-08) | No package-manager install in the plan. Source: `01-08-PLAN.md` | plan 01-08 | 2026-11-16 |
| AR-11 | T-01-SC (01-10) | No new installs; optional Mermaid check uses the lockfile approved at the 01-03 checkpoint. Source: `01-10-PLAN.md` | plan 01-10 | 2026-11-16 |
| AR-12 | T-01-40 | Accepted in plan 01-12: the guard is a safety net against an accidental `--out`, not a defence against a concurrent hostile process on the same machine. Superseded: plan 01-13 changed the disposition to mitigate (T-01-43). Listed for history; not a live acceptance | plan 01-12 (superseded by 01-13) | 2026-11-17 |

Residuals recorded inside mitigated threats (not separate acceptances): T-01-42 (a killed process can leave one `*.tmp-*` file in `--out`), T-01-43 and T-01-46 (the interval between the second check and the write is not closed).

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-18 | 60 | 60 | 0 | gsd-secure-phase (grep-depth draft, ASVS L1, block_on high) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-18 by gsd-secure-phase at grep depth (ASVS L1); threats_open 0. Accepted risks carry plan-level acceptance only (see the note above the Accepted Risks Log).
