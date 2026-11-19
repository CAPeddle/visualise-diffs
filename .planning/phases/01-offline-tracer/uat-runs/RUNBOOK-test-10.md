# Runbook: phase 01 UAT test 10 on Linux (WSL) and macOS

UAT test 10 in `../01-UAT.md` asks for the phase 01 safety tests to be run off Windows. The
development host is Windows, so three truths in `../01-VERIFICATION.md` are read in code but not
observed running:

| Truth | What must hold on Linux and macOS |
|-------|-----------------------------------|
| 34 | `HeldDirectory` holds no handle; the path is judged before and after the directories are created; the swap-before-the-hold test and the retarget test run (no early return) and pass |
| 45 | `scripts/trace-run.ps1` holds no handle and compiles no type; the log-below-missing-directories test runs and passes; the staged `log_hold` test returns early by design |
| 55 | The `drive_root` unit and CLI tests run with `/` as the root and pass |

One agent runs this on a macOS host and one on a Linux host; WSL counts as Linux. Each agent
produces one results file and pushes it. The reviewer on the Windows host judges the results
and records the verdict in `01-UAT.md`. **Agents do not edit `01-UAT.md` or any source or test
file, and do not fix failing tests.** A failure is a result to report, not a task.

## 1. Prerequisites

- git, python3, .NET SDK 10.0.100 or later (`dotnet --version`), PowerShell 7 (`pwsh`).
- If one is missing, stop and ask your operator before installing anything.

## 2. Get the code

The repository is public, so an anonymous HTTPS fetch works.

```bash
git clone https://github.com/CAPeddle/visualise-diffs.git ~/src/visualise-diffs   # or fetch in an existing clone
cd ~/src/visualise-diffs
git fetch origin
git switch ccr-c12d6a45-a0eke4 && git pull --ff-only
git rev-parse HEAD
```

**On WSL, the clone must be on the Linux file system (for example under `~`), never under
`/mnt/c`.** `/mnt/c` is NTFS mounted through 9P: links, case and permissions behave differently
there, and the run would test neither Linux nor Windows.

The handoff that sent you here names the commit to test. If `git rev-parse HEAD` is older
than that commit, fetch again. If it is newer, test it anyway; the results file records the
SHA.

## 3. Run

```bash
bash .planning/phases/01-offline-tracer/uat-runs/run-uat-10.sh
```

The script builds once and runs four passes: the three filters from the table above, then the
full suite for context. It writes `.planning/phases/01-offline-tracer/uat-runs/01-UAT-10-<os>.md`
(`linux` or `macos`) and prints the path. On a cold machine it takes several minutes. Exit
codes: 0 means the file was written (whether or not the tests passed); 2 means a prerequisite
or location check failed and nothing was written.

## 4. Check the file before committing

- The tables are generated. Do not edit them. You may replace `(none)` under `## Agent notes`
  with facts only: anything you installed, anything odd in the run, or a rerun and why.
- The script replaces home paths, the user name and the host name. Check by hand:
  `grep -nE "/home/|/Users/|$(whoami)|$(hostname)" <file>` must print nothing. Edit by hand
  only to remove a personal detail the script missed, and say so under Agent notes.
- This repository is public. If the machine has a `shared-copilot` clone, stage the file and run
  `bash <shared-copilot>/docs/investigations/visualise-diffs/scripts/check-public-diff.sh`. It
  must print `clean`. If there is no clone, say so under Agent notes.

## 5. Commit and push

Stage only the results file:

```bash
f=.planning/phases/01-offline-tracer/uat-runs/01-UAT-10-<os>.md
git add "$f"
git commit -m "test(01): UAT test 10 results on <os>" -- "$f"
git pull --rebase origin ccr-c12d6a45-a0eke4
git push origin HEAD:ccr-c12d6a45-a0eke4
```

The two agents write different files, so the rebase is clean. Never force-push. If the push is
refused for access (the repository belongs to the `CAPeddle` GitHub account), do not try other
credentials. Copy the file instead to
`<shared-copilot>/docs/investigations/visualise-diffs/uat-runs/01-UAT-10-<os>.md`, commit it
there on `main`, and push. Then say which route you used.

## 6. Report

Reply to your operator with:

- the pushed commit SHA and the route (visualise-diffs branch, or shared-copilot);
- `commit_tested` and `three_filters_passed` from the frontmatter;
- any non-passing test names.
