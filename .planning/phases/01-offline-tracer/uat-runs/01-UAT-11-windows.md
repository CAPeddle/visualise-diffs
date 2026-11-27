---
uat_test: 11
phase: 01-offline-tracer
option: a
os: windows
commit_tested: 914b31a
hosts: ["pwsh 7.7.0", "powershell 5.1.26100.9711"]
run_at: 2026-11-19
driver: run-uat-11.ps1
overall: pass
---

# Phase 01 UAT test 11, option (a): truth 40 end to end on Windows

The checks are described in the header of `run-uat-11.ps1`. Both hosts exited 0. The `subst` mapping was removed afterwards.

## pwsh 7.7.0

```text
PASS C1 verdict-by-handle : text-under-protected=False; Open-HeldDirectory threw 'held-directory-protected'; created-inside-protected=False
PASS C1 control : held '<scratch>\free\x'
PASS C2 protected-verdict-maps-to-bad-arguments : text-check-refuses-subst-log=False; result=fail reason=bad-arguments repo_unchanged=unknown outputs_present=no; exit=1; out-entries=0 (vdiff started if > 0); logs-dir-in-repo=False; repo-status-unchanged=True
PASS C3 control (log written when the hold works) : result=pass reason=none repo_unchanged=yes outputs_present=yes; log-written=True
PASS C3 failed-hold-skips-log : result=pass reason=none repo_unchanged=yes outputs_present=yes; exit=0; output-files=5; log-written=False; log-dir-exists=False
OVERALL PASS on pwsh 7.7.0 at 914b31a (0 failing check(s))
```

## Windows PowerShell 5.1

```text
PASS C1 verdict-by-handle : text-under-protected=False; Open-HeldDirectory threw 'held-directory-protected'; created-inside-protected=False
PASS C1 control : held '<scratch>\free\x'
PASS C2 protected-verdict-maps-to-bad-arguments : text-check-refuses-subst-log=False; result=fail reason=bad-arguments repo_unchanged=unknown outputs_present=no; exit=1; out-entries=0 (vdiff started if > 0); logs-dir-in-repo=False; repo-status-unchanged=True
PASS C3 control (log written when the hold works) : result=pass reason=none repo_unchanged=yes outputs_present=yes; log-written=True
PASS C3 failed-hold-skips-log : result=pass reason=none repo_unchanged=yes outputs_present=yes; exit=0; output-files=5; log-written=False; log-dir-exists=False
OVERALL PASS on powershell 5.1.26100.9711 at 914b31a (0 failing check(s))
```

## Reading

- C2 is the case WR-03 found untested. The script's text checks let a `subst` path through (accepted residual AR-04), and only the judgement by handle refused it. The run ended with `bad-arguments`, and an empty `-Out` shows vdiff never started.
- C3 is the skip-on-failure branch. The only difference from the control run is the pre-defined type that makes the script's `Add-Type` fail. With that type in place the log is skipped while the run still passes.
- This is a driver kept with the UAT runs, not a repository test. WR-03, which asks for a test in the repository, stays open.
