#!/usr/bin/env node
//
// report-goldens.js — Node's answers for the C# `kit report` and `kit sheet` (kit#119, cut 2)
// ─────────────────────────────────────────────────────────────────────────────
// Runs kit.js as a real process over a FROZEN copy of every corpus in
// behaviours/ (and its bindings), plus the refusals, and records stdout, stderr
// and exit code into backend/Balenthiran.Kit.Tests/Fixtures/Check/report-goldens.json.
//
// Frozen, because a golden over the live corpora goes red on every corpus edit,
// including James's first hosted one (kit#182). The corpora are copied INTO the
// golden as strings and written to a temp directory per run.
//
// The temp directory's path is printed by kit.js (`read from <dir>`), so it is
// recorded as `<root>`; the C# test writes its own temp root the same way.
//
//   node prototypes/behaviour-ast/report-goldens.js            # rewrite the golden
//   node prototypes/behaviour-ast/report-goldens.js --check    # exit 1 if it would change

'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const OUT = path.join(__dirname, '..', '..', 'backend', 'Balenthiran.Kit.Tests', 'Fixtures', 'Check', 'report-goldens.json');
const KIT = path.join(__dirname, 'kit.js');
const BEH = path.join(__dirname, 'behaviours');

// The real corpora and bindings, as they are when this is recorded. Not the test
// mappings: report and sheet never read them.
function corpora() {
  const files = {};
  for (const f of fs.readdirSync(BEH)) {
    if (f.endsWith('.beh') || f.endsWith('.bindings.json')) files[`behaviours/${f}`] = fs.readFileSync(path.join(BEH, f), 'utf8');
  }
  return files;
}

const REAL = corpora();
const NAMES = Object.keys(REAL).filter((f) => f.endsWith('.beh')).map((f) => path.basename(f, '.beh'));

// Small corpora for the refusals the real ones never reach.
const EDGE = {
  'edge/broken.beh': 'behaviour BEH-1 "inferred, serving nothing real"\n  source inferred Api.cs:1\n  serves BEH-404\n  when opens page:Home\n',
  'edge/half.beh': 'behaviour BEH-1 "an inference nothing displays"\n  source inferred Api.cs:1\n  when opens page:Home\n  then sees region:Main\n',
  'edge/blank.beh': '# only a header so far\n',
  'edge/twin-a.beh': 'behaviour BEH-1 "a"\n  when opens page:Home\n',
  'edge/twin-b.beh': 'behaviour BEH-1 "b"\n  when opens page:Home\n',
};

const CASES = [];
for (const name of NAMES) {
  CASES.push({ name: `report-${name}`, files: REAL, args: [name, '--dir', 'behaviours'] });
  CASES.push({ name: `sheet-${name}`, files: REAL, args: ['sheet', name, '--dir', 'behaviours'] });
}
CASES.push(
  { name: 'report-all', files: REAL, args: ['--dir', 'behaviours'] },
  { name: 'sheet-rev', files: REAL, args: ['sheet', 'language-vocab', '--rev', 'language-vocab@8228db7', '--dir', 'behaviours'] },
  { name: 'report-unique-substring', files: REAL, args: ['james-habits', '--dir', 'behaviours'] },
  { name: 'report-ambiguous', files: REAL, args: ['trial', '--dir', 'behaviours'] },
  { name: 'report-no-match', files: REAL, args: ['nothing-like-this', '--dir', 'behaviours'] },
  { name: 'report-empty-name', files: REAL, args: ['', '--dir', 'behaviours'] },
  { name: 'report-no-dir', files: {}, args: ['kit', '--dir', 'missing'] },
  { name: 'report-dir-is-file', files: { 'afile': 'x' }, args: ['kit', '--dir', 'afile'] },
  { name: 'report-empty-dir', files: { 'empty/readme.md': 'x' }, args: ['--dir', 'empty'] },
  { name: 'report-blank-corpus', files: EDGE, args: ['blank', '--dir', 'edge'] },
  { name: 'report-broken-link', files: EDGE, args: ['broken', '--dir', 'edge'] },
  { name: 'sheet-broken-link', files: EDGE, args: ['sheet', 'broken', '--dir', 'edge'] },
  { name: 'sheet-incomplete-question', files: EDGE, args: ['sheet', 'half', '--dir', 'edge'] },
  { name: 'report-twins', files: EDGE, args: ['twin', '--dir', 'edge'] },
  { name: 'help', files: {}, args: ['--help'] },
  { name: 'help-short-after-sheet', files: {}, args: ['sheet', '-h'] },
  { name: 'unknown-flag', files: {}, args: ['kit', '--json'] },
  { name: 'rev-needs-value', files: {}, args: ['sheet', 'kit', '--rev', '-h'] },
  { name: 'dir-at-end', files: {}, args: ['kit', '--dir'] },
  { name: 'two-names', files: {}, args: ['kit', 'snip-it', '--dir', 'behaviours'] },
  { name: 'sheet-named-sheet', files: { 'x/sheet.beh': 'behaviour BEH-1 "a"\n  when opens page:Home\n' }, args: ['sheet', 'sheet', '--dir', 'x'] },
);

function materialise(files) {
  const root = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'kit-report-golden-')));
  for (const [rel, text] of Object.entries(files)) {
    fs.mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    fs.writeFileSync(path.join(root, rel), text);
  }
  return root;
}

function record() {
  // Each distinct file set is stored once and named, so the frozen corpora are not
  // repeated for every case that reads them.
  const sets = { real: REAL, edge: EDGE };
  const setOf = (files) => Object.keys(sets).find((k) => sets[k] === files);
  const checks = CASES.map((c) => {
    const root = materialise(c.files);
    try {
      const r = spawnSync(process.execPath, [KIT, ...c.args], { cwd: root, encoding: 'utf8' });
      const strip = (s) => s.split(root).join('<root>');
      const set = setOf(c.files);
      return { name: c.name, ...(set ? { set } : { files: c.files }), args: c.args, exitCode: r.status, stdout: strip(r.stdout), stderr: strip(r.stderr) };
    } finally {
      fs.rmSync(root, { recursive: true, force: true });
    }
  });
  return JSON.stringify({ _generatedBy: 'prototypes/behaviour-ast/report-goldens.js', sets, checks }, null, 2) + '\n';
}

if (require.main === module) {
  const { unknownFlag, refuse } = require('./cli.js');
  const unknown = unknownFlag(process.argv.slice(2), ['--check']);
  if (unknown) process.exit(refuse(unknown, 'usage: node report-goldens.js [--check]'));
  const next = record();
  if (process.argv.includes('--check')) {
    const now = fs.existsSync(OUT) ? fs.readFileSync(OUT, 'utf8') : '';
    if (now !== next) { console.error(`${path.relative(process.cwd(), OUT)} is stale — re-run without --check`); process.exit(1); }
    console.log('report goldens: current');
  } else {
    fs.mkdirSync(path.dirname(OUT), { recursive: true });
    fs.writeFileSync(OUT, next);
    console.log(`wrote ${path.relative(process.cwd(), OUT)}`);
  }
}
