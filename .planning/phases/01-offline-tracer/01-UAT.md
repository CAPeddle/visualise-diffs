---
status: testing
phase: 01-offline-tracer
source: [01-VERIFICATION.md]
started: 2026-11-17T00:00:00Z
updated: 2026-11-18T12:26:05Z
---

## Current Test

number: 1
name: Decide the disposition of re-review findings CR-01, WR-01 and WR-02
expected: |
  Either (a) the product owner accepts that the --out guard stops accidental and link-based --out choices but not a deliberately obscure spelling (\\?\, loopback UNC, a pre-planted hard link in --out, a swap after the check), and T-01-39 is widened to name those forms in the doc comments of CliApp.RefuseUnsafeOut and FinalPath.Resolve and the trace-run.ps1 header; or (b) a gap-closure plan is raised that canonicalises both sides through a final-path call (or refuses a root starting with two backslashes on Windows), writes through a temp name plus move, and re-checks just before the write.
awaiting: user response

## Tests

### 1. Decide the disposition of re-review findings CR-01, WR-01 and WR-02
expected: Either (a) accept as residuals and widen T-01-39 to name \\?\, loopback UNC, pre-planted hard links and post-check swaps; or (b) raise a hardening gap-closure plan (final-path canonicalisation or refuse \\ roots, temp-name-plus-move writes, re-check before write) in both the CLI and trace-run.ps1.
result: [pending]

### 2. File symbolic link planted at an output name is replaced (privileged host)
expected: On a host that can create file symbolic links (Linux, macOS, or Windows with Developer Mode or an elevated shell), `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter FullyQualifiedName~A_file_symbolic_link_planted` creates the link (does not return early), the run exits 0, the victim file is unchanged and the output is a regular file afterwards
result: [pending]

### 3. Staged link swap during a trace-run.ps1 run
expected: With -LogPath in a real directory and a -VdiffDll that sleeps about 20 s, replacing the log's parent by a junction into the analysed repository during the sleep leaves no log file in the repository, the six output lines keep their shape, and the repository snapshot is unchanged
result: [pending]

### 4. Windows symbolic link (not junction) with a relative or NT-absolute target, CLI and script
expected: On a capable host, `mklink` (no /J) with a relative target, and again with an NT-absolute target, into a sandbox repository: `vdiff --out` below the link exits 1 with the fixed unsupported-root line and creates nothing; `trace-run.ps1 -Out` / `-LogPath` below it end with script-error and six fixed lines; repository snapshot unchanged
result: [pending]

### 5. Re-run the trial wrapper once on the private local clone with the hardened scripts/trace-run.ps1 (recommended, not required)
expected: Six-line block: result pass, reason none, under budget, repo_unchanged yes, outputs_present yes, mermaid_syntax ok; only that block recorded
result: [pending]

### 6. Product-owner sign-off on the accepted residuals AR-04 (T-01-39) and AR-13 (T-01-47)
expected: Explicit acceptance recorded against AR-04 (subst and mapped drive letters, DOS 8.3 short names, IN-04) and AR-13 (every non-drive-letter Windows link target is refused, including a relative symbolic link), which today carry plan-level acceptance only
result: [pending]

### 7. Seven judgment-tier prohibitions (plans 01-13, 01-14, 01-15) flagged unverified-prohibition
expected: Reviewer confirms or overrules each non-authoritative verdict listed in the Prohibitions section of 01-VERIFICATION.md (all seven read as honoured)
result: [pending]

## Summary

total: 7
passed: 0
issues: 0
pending: 7
skipped: 0
blocked: 0

## Gaps
