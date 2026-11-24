---
uat_test: 12
phase: 01-offline-tracer
option: 1 (subst drive)
os: windows
commit_tested: e65ee44
hosts: ["pwsh 7.7.0"]
run_at: 2026-11-19
driver: run-uat-12.ps1
overall: pass
---

# Phase 01 UAT test 12, option 1: a real drive-root write on a subst drive

The steps are described in the header of `run-uat-12.ps1`. The script exited 0, and the `subst` mapping was removed afterwards.

```text
PASS setup discriminates : from current directory T:\sub the trimmed spelling 'T:' resolves to 'T:\sub' (where the CR-01 bug would write)
PASS vdiff exit 0 : exit=0
PASS five files in the root : 5 file(s) in T:\: base.model.json, diff.json, head.model.json, report.html, summary.mmd
PASS nothing in the current directory : 0 entr(y/ies) in T:\sub
PASS no temp files : 0 *.tmp-* file(s)
PASS success line names the typed root : stdout: vdiff: wrote 5 files to T:\
OVERALL PASS on pwsh 7.7.0 at e65ee44 (0 failing check(s))
```

Caveat: `T:\` is a subst root backed by a folder on `C:`. The run shows that a typed drive root is written as that root and never as the current directory. It does not show a write to a separate physical volume.
