#!/usr/bin/env bash
# Phase 01 UAT test 10: run the off-Windows test filters and write one results file.
# Usage (from the repository root):  bash .planning/phases/01-offline-tracer/uat-runs/run-uat-10.sh
# Writes .planning/phases/01-offline-tracer/uat-runs/01-UAT-10-<os>.md and prints its path.
# It never commits or pushes; RUNBOOK-test-10.md says how to do that.
set -u

root="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "run from inside the visualise-diffs clone" >&2; exit 2; }
cd "$root" || exit 2
here=".planning/phases/01-offline-tracer/uat-runs"
project="tests/Vdiff.Tests/Vdiff.Tests.csproj"

case "$(uname -s)" in
  Linux)  os=linux; grep -qi microsoft /proc/version 2>/dev/null && os_detail="WSL" || os_detail="native" ;;
  Darwin) os=macos; os_detail="$(sw_vers -productVersion 2>/dev/null || echo unknown)" ;;
  *)      echo "this runner is for Linux (incl. WSL) and macOS only" >&2; exit 2 ;;
esac

for tool in git dotnet pwsh python3; do
  command -v "$tool" >/dev/null 2>&1 || { echo "missing prerequisite: $tool" >&2; exit 2; }
done

# Refuse to run from a drive-root-like cwd: the drive_root tests assume cwd is not the root (review IN-03).
[ "$PWD" != "/" ] || { echo "cwd is /; run from the clone" >&2; exit 2; }

work="$(mktemp -d "${TMPDIR:-/tmp}/vdiff-uat10.XXXXXX")" || exit 2
trap 'rm -rf "$work"' EXIT

# name|filter|truth
runs=(
  "held_directory|FullyQualifiedName~held_directory|FullyQualifiedName~HeldDirectoryTests|34"
  "trace_run_script|FullyQualifiedName~TraceRunScriptTests|45"
  "drive_root|FullyQualifiedName~drive_root|55"
  "full_suite||all"
)

echo "building once..." >&2
dotnet build "$project" -c Debug -nologo -v quiet >"$work/build.log" 2>&1
build_exit=$?

declare -a exits
for entry in "${runs[@]}"; do
  name="${entry%%|*}"; rest="${entry#*|}"; truth="${rest##*|}"; filter="${rest%|*}"
  echo "running $name..." >&2
  args=(test "$project" --no-build -nologo --results-directory "$work/$name" --logger "trx;LogFileName=$name.trx")
  [ -n "$filter" ] && args+=(--filter "$filter")
  if [ "$build_exit" -eq 0 ]; then
    dotnet "${args[@]}" >"$work/$name.log" 2>&1; exits+=("$name=$?")
  else
    exits+=("$name=build-failed")
  fi
done

out="$here/01-UAT-10-$os.md"
python3 - "$work" "$out" "$os" "$os_detail" "$build_exit" "${exits[@]}" <<'PY'
import glob, os, platform, re, subprocess, sys, datetime
import xml.etree.ElementTree as ET

work, out, osname, detail, build_exit = sys.argv[1:6]
exits = dict(e.split("=", 1) for e in sys.argv[6:])
home = os.path.expanduser("~")
user = os.environ.get("USER") or os.environ.get("LOGNAME") or ""
host = platform.node()

def scrub(text):
    # Keep personal and machine details out of a public repository.
    for needle, repl in ((work, "<work>"), (home, "~"), (host, "<host>")):
        if needle:
            text = text.replace(needle, repl)
    text = re.sub(r"/(?:home|Users)/[^/\s\"']+", "~", text)
    if user:
        text = re.sub(r"\b%s\b" % re.escape(user), "<user>", text)
    return text

def cmd(*a):
    try:
        return subprocess.run(a, capture_output=True, text=True, timeout=60).stdout.strip().splitlines()[0]
    except Exception as ex:  # noqa: BLE001
        return "unavailable (%s)" % type(ex).__name__

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
truths = {"held_directory": "34", "trace_run_script": "45", "drive_root": "55", "full_suite": "all"}
filters = {
    "held_directory": "FullyQualifiedName~held_directory|FullyQualifiedName~HeldDirectoryTests",
    "trace_run_script": "FullyQualifiedName~TraceRunScriptTests",
    "drive_root": "FullyQualifiedName~drive_root",
    "full_suite": "(none)",
}

lines = []
summary = []
for name in ("held_directory", "trace_run_script", "drive_root", "full_suite"):
    trx = glob.glob(os.path.join(work, name, "*.trx"))
    results = []
    if trx:
        root = ET.parse(trx[0]).getroot()
        for r in root.iter("{%s}UnitTestResult" % ns["t"]):
            msg = r.find("t:Output/t:ErrorInfo/t:Message", ns)
            results.append((r.get("testName", "?"), r.get("outcome", "?"),
                            scrub((msg.text or "").strip())[:600] if msg is not None else ""))
    counts = {}
    for _, outcome, _ in results:
        counts[outcome] = counts.get(outcome, 0) + 1
    summary.append((name, exits.get(name, "?"), counts, len(results)))
    lines.append("\n## %s (truth %s)\n" % (name, truths[name]))
    lines.append("filter: `%s`  \nexit: %s  \ncounts: %s\n" % (
        filters[name], exits.get(name, "?"),
        ", ".join("%s %d" % kv for kv in sorted(counts.items())) or "no results"))
    if name == "full_suite":
        failed = [r for r in results if r[1] != "Passed"]
        lines.append("\nNon-passing tests only (%d of %d):\n" % (len(failed), len(results)))
        shown = failed
    else:
        lines.append("\n| Test | Outcome |\n|------|---------|\n")
        for t, o, _ in sorted(results):
            lines.append("| `%s` | %s |\n" % (scrub(t).replace("|", "\\|"), o))
        shown = [r for r in results if r[1] != "Passed"]
    for t, o, m in sorted(shown):
        lines.append("\n- `%s`: %s\n" % (scrub(t), o))
        if m:
            lines.append("\n  ```text\n  %s\n  ```\n" % m.replace("\n", "\n  "))
    log = os.path.join(work, name + ".log")
    if not results and os.path.exists(log):
        tail = scrub(open(log, errors="replace").read())[-1500:]
        lines.append("\nNo TRX results. Log tail:\n\n```text\n%s\n```\n" % tail)

if build_exit != "0":
    blog = os.path.join(work, "build.log")
    tail = scrub(open(blog, errors="replace").read())[-2000:] if os.path.exists(blog) else ""
    lines.append("\n## build failed (exit %s)\n\n```text\n%s\n```\n" % (build_exit, tail))

three_ok = all(e == "0" and n > 0 and c.get("Failed", 0) == 0
               for name, e, c, n in summary if name != "full_suite")
head = [
    "---\n",
    "uat_test: 10\n",
    "phase: 01-offline-tracer\n",
    "os: %s\n" % osname,
    "os_detail: \"%s\"\n" % detail,
    "kernel: \"%s\"\n" % scrub(cmd("uname", "-sr")),
    "arch: %s\n" % platform.machine(),
    "dotnet_sdk: \"%s\"\n" % cmd("dotnet", "--version"),
    "pwsh: \"%s\"\n" % cmd("pwsh", "-NoLogo", "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()"),
    "commit_tested: %s\n" % cmd("git", "rev-parse", "HEAD"),
    "worktree_clean_before_run: %s\n" % ("yes" if not subprocess.run(
        ["git", "status", "--porcelain", "--untracked-files=no"], capture_output=True, text=True).stdout.strip() else "no"),
    "run_at: %s\n" % datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "build_exit: %s\n" % build_exit,
    "three_filters_passed: %s\n" % ("yes" if three_ok else "no"),
    "---\n\n",
    "# Phase 01 UAT test 10 on %s (%s)\n\n" % (osname, detail),
    "Generated by run-uat-10.sh. Do not edit the tables by hand; add notes only under Agent notes.\n",
    "A test that returns early on this OS reports Passed; the reviewer judges that from the test source.\n",
]
with open(out, "w", newline="\n") as f:
    f.writelines(head + lines + ["\n## Agent notes\n\n(none)\n"])
print(out)
PY
