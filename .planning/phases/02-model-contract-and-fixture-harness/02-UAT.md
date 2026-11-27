---
status: complete
phase: 02-model-contract-and-fixture-harness
source: [02-VERIFICATION.md]
started: 2026-11-22T12:30:00Z
updated: 2026-11-23T14:33:11.999Z
---

## Current Test

[testing complete]

## Tests

### 1. Connect azure-pipelines.yml to an Azure DevOps organisation and run it once on both legs
expected: Both the windows-2025 and ubuntu-24.04 legs build, run dotnet test with Category!=MermaidCli, publish the trx results and a fixture-report artifact showing the ten RED rows; the advisory Mermaid step may fail without turning the leg red
result: pass
note: "Owner-accepted 2026-11-23 without a connected Azure DevOps run: accepted on the local test suite, PipelineYamlTests and the offline demo run. The first connected run is still to happen."

### 2. Run the private-fixture recipe (docs/how-to/private-fixtures.md) once on a real clone inside the target organisation
expected: One '<shape>: PASS|FAIL|PENDING <date>' line per shape, details only under VDIFF_PRIVATE_OUT, then a row added to docs/trials/private-fixtures.md
result: pass
note: "Provisional (owner decision 2026-11-23): recipe run once on a real clone, five PENDING lines, details only under VDIFF_PRIVATE_OUT, rows recorded. Fixtures were picked by an agent with placeholder expected files, not owner-reviewed; the extract-class pick should be replaced."

### 3. Read the C# and cpp-cli golden conventions in tests/fixtures/README.md and the csharp and cross-stack goldens
expected: Owner confirms the proposed signature text, the managed-wrapper stereotype, the package-node label and 'a gcroot field to an unmodelled type makes no edge'
result: pass

### 4. Judge that the fixtures, goldens, project files and docs hold no target-organisation or target-product name
expected: No target identifier anywhere in tests/fixtures, docs/how-to, docs/trials
result: pass
evidence: "All target-name patterns scanned case-insensitively over every tracked file and file name; only hits are one third-party npm package name in the mermaid-check lock file (known false positive). Fixture names synthetic, no internal hosts, keys or emails in content."

## Summary

total: 4
passed: 4
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

## Deferred Follow-Ups

<!-- Found in an offline run of the Phase 1 tracer on three merged PRs of a private target clone, done before test 1 at the product owner's request. Test 1 was later owner-accepted without a connected Azure DevOps run (see its note). -->
- test: 1
  idea: "Partial clone: base files unreadable from git objects show their types as added, not modified. Owner: Phase 3 (two checked-out trees, git only decides what to compare)."
  target_phase: 3
  deferred_at: 2026-11-23
- test: 1
  idea: "A data member typed std::function<void()> makes summary.mmd unparseable; [modified] tags do not show what changed. Owner: Phase 7."
  target_phase: 7
  deferred_at: 2026-11-23
