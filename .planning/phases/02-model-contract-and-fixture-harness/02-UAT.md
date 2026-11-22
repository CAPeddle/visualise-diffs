---
status: testing
phase: 02-model-contract-and-fixture-harness
source: [02-VERIFICATION.md]
started: 2026-11-22T12:30:00Z
updated: 2026-11-22T12:30:00Z
---

## Current Test

number: 1
name: Connect azure-pipelines.yml to an Azure DevOps organisation and run it once on both legs
expected: |
  Both the windows-2025 and ubuntu-24.04 legs build, run dotnet test with Category!=MermaidCli, publish the trx results and a fixture-report artifact showing the ten RED rows; the advisory Mermaid step may fail without turning the leg red
awaiting: user response

## Tests

### 1. Connect azure-pipelines.yml to an Azure DevOps organisation and run it once on both legs
expected: Both the windows-2025 and ubuntu-24.04 legs build, run dotnet test with Category!=MermaidCli, publish the trx results and a fixture-report artifact showing the ten RED rows; the advisory Mermaid step may fail without turning the leg red
result: [pending]

### 2. Run the private-fixture recipe (docs/how-to/private-fixtures.md) once on a real clone inside the target organisation
expected: One '<shape>: PASS|FAIL|PENDING <date>' line per shape, details only under VDIFF_PRIVATE_OUT, then a row added to docs/trials/private-fixtures.md
result: [pending]

### 3. Read the C# and cpp-cli golden conventions in tests/fixtures/README.md and the csharp and cross-stack goldens
expected: Owner confirms the proposed signature text, the managed-wrapper stereotype, the package-node label and 'a gcroot field to an unmodelled type makes no edge'
result: [pending]

### 4. Judge that the fixtures, goldens, project files and docs hold no target-organisation or target-product name
expected: No target identifier anywhere in tests/fixtures, docs/how-to, docs/trials
result: [pending]

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
