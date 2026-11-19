#!/usr/bin/env node
// Local Mermaid syntax check for vdiff output.
//
// Usage: node check.mjs <file.mmd>
//
// Runs @mermaid-js/mermaid-cli (pinned in package-lock.json) in a headless browser on this
// machine. Nothing is sent to any service; the script makes no network request of its own.
//
// Exit codes:
//   0   the diagram parsed and rendered
//   1   mermaid-cli rejected the diagram (a syntax error)
//   2   usage error: no file argument, or the file cannot be read
//   3   a prerequisite is missing: installed packages, or a Chrome/Chromium/Edge browser
//   4   the check itself broke: timeout, or mermaid-cli could not be started
//
// Environment:
//   VDIFF_MERMAID_BROWSER     path of the browser executable (overrides detection)
//   VDIFF_MERMAID_NO_SANDBOX  set to any non-empty value to launch the browser with --no-sandbox
//                             (needed only in a root shell on Linux; the default keeps the sandbox)

import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { delimiter, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const EXIT_SYNTAX = 1;
const EXIT_USAGE = 2;
const EXIT_PREREQUISITE = 3;
const EXIT_BROKEN = 4;

const TIMEOUT_MS = 120_000;

// mermaid-cli exits 0 on any diagram longer than the default 50,000 characters without parsing
// it, so the text-size limit is raised well above anything vdiff writes (T-01-33).
const MERMAID_CONFIG = { maxTextSize: 2_000_000, maxEdges: 100_000 };

const here = dirname(fileURLToPath(import.meta.url));
const cli = join(here, 'node_modules', '@mermaid-js', 'mermaid-cli', 'src', 'cli.js');

function fail(code, message) {
  process.stderr.write(`mermaid-check: ${message}\n`);
  process.exit(code);
}

function onPath(names) {
  const dirs = (process.env.PATH ?? '').split(delimiter).filter(Boolean);
  for (const dir of dirs) {
    for (const name of names) {
      const candidate = join(dir, name);
      if (existsSync(candidate)) {
        return candidate;
      }
    }
  }
  return undefined;
}

function candidates() {
  if (process.platform === 'win32') {
    const roots = [process.env.ProgramFiles, process.env['ProgramFiles(x86)']].filter(Boolean);
    return roots.flatMap((root) => [
      join(root, 'Google', 'Chrome', 'Application', 'chrome.exe'),
      join(root, 'Microsoft', 'Edge', 'Application', 'msedge.exe'),
    ]);
  }
  if (process.platform === 'darwin') {
    return ['/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'];
  }
  return ['google-chrome', 'chromium', 'chromium-browser'].map((name) => onPath([name])).filter(Boolean);
}

function findBrowser() {
  const fromEnv = process.env.VDIFF_MERMAID_BROWSER;
  if (fromEnv) {
    return existsSync(fromEnv) ? fromEnv : undefined;
  }
  return candidates().find((path) => existsSync(path));
}

const input = process.argv[2];
if (!input || process.argv.length > 3) {
  fail(EXIT_USAGE, 'usage: node check.mjs <file.mmd>');
}
const inputPath = resolve(input);
try {
  readFileSync(inputPath);
} catch (e) {
  fail(EXIT_USAGE, `cannot read ${inputPath}: ${e.message}`);
}

if (!existsSync(cli)) {
  fail(EXIT_PREREQUISITE, 'mermaid-cli is not installed; run: npm --prefix tools/mermaid-check ci --ignore-scripts');
}

const browser = findBrowser();
if (!browser) {
  fail(
    EXIT_PREREQUISITE,
    process.env.VDIFF_MERMAID_BROWSER
      ? `VDIFF_MERMAID_BROWSER does not exist: ${process.env.VDIFF_MERMAID_BROWSER}`
      : 'no Chrome, Chromium or Edge found; install one or set VDIFF_MERMAID_BROWSER to its executable',
  );
}

const work = mkdtempSync(join(tmpdir(), 'vdiff-mermaid-'));
let status;
let brokenMessage;
let stderrText = '';
try {
  const slash = (p) => p.replaceAll('\\', '/');
  const args = ['--disable-gpu'];
  if (process.env.VDIFF_MERMAID_NO_SANDBOX) {
    args.push('--no-sandbox');
  }
  const puppeteerConfig = join(work, 'puppeteer.json');
  const mermaidConfig = join(work, 'mermaid.json');
  const output = join(work, 'out.svg');
  writeFileSync(puppeteerConfig, JSON.stringify({ executablePath: slash(browser), args }));
  writeFileSync(mermaidConfig, JSON.stringify(MERMAID_CONFIG));

  const run = spawnSync(
    process.execPath,
    [cli, '-p', puppeteerConfig, '-c', mermaidConfig, '-i', inputPath, '-o', output],
    { encoding: 'utf8', timeout: TIMEOUT_MS, windowsHide: true, maxBuffer: 16 * 1024 * 1024 },
  );
  if (run.error) {
    const timedOut = run.error.code === 'ETIMEDOUT';
    brokenMessage = timedOut ? `timed out after ${TIMEOUT_MS / 1000} s` : `could not run mermaid-cli: ${run.error.message}`;
  } else {
    status = run.status ?? EXIT_BROKEN;
    stderrText = run.stderr ?? '';
  }
} finally {
  rmSync(work, { recursive: true, force: true });
}

// The temporary directory is gone before any exit below (process.exit would skip a finally block).
if (brokenMessage) {
  fail(EXIT_BROKEN, brokenMessage);
}
if (status !== 0) {
  const lines = stderrText.split(/\r?\n/).filter((line) => line.trim().length > 0).slice(0, 5);
  process.stderr.write(`mermaid-check: FAILED\n${lines.join('\n')}${lines.length ? '\n' : ''}`);
  process.exit(status === EXIT_SYNTAX ? EXIT_SYNTAX : EXIT_BROKEN);
}
process.stdout.write('mermaid-check: OK\n');
