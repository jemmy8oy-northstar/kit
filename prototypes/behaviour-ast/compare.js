#!/usr/bin/env node
'use strict';
/**
 * The check that decides whether any of this is real.
 *
 * "It generates a test" is worthless if the test is not the one a person would
 * have written. So: take every line the generator emitted, normalise whitespace,
 * and look for it in snip-it's ACTUAL hand-written e2e spec on origin/dev.
 *
 * Both outcomes are informative, which is why it prints per-line YES/NO rather
 * than a pass/fail — a summary that says "89%" while hiding which 11% is the
 * kind of check that flatters the thing it measures.
 */
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const { parse, resolve, generate } = require('./kit');

const SPEC = 'frontend/e2e/editor.spec.ts';
// A local checkout of snip-it, read at origin/dev. Defaults to a sibling clone
// next to this repo; override with SNIPIT_REPO or argv[2].
const REPO = process.env.SNIPIT_REPO || process.argv[2] ||
  path.resolve(__dirname, '..', '..', '..', 'snip-it');

const real = execFileSync('git', ['-C', REPO, 'show', `origin/dev:${SPEC}`], { encoding: 'utf8' });

// The first version of this comparator scored 18/28 and SEVEN of the ten misses
// were its own fault, not the generator's: the spec reaches the same URL through
// a constant (EDITOR_PATH / MOCK_TRANSCRIPTION_JOB_ID) and wraps long expects
// over three lines. Comparing line-by-line against raw text measured formatting.
// So: expand the constants (values read from the repo, not assumed) and collapse
// the whole file to one whitespace-normalised blob before searching.
const JOB_ID = '11111111-1111-1111-1111-111111111111'; // fixtures/mockTranscript.ts:85
const expand = (s) => s
  .replace(/EDITOR_PATH/g, `'./editor/${JOB_ID}'`)
  .replace(/\$\{MOCK_TRANSCRIPTION_JOB_ID\}/g, JOB_ID);

const norm = (s) => s
  .replace(/\s+/g, ' ').replace(/['`]/g, '"')
  .replace(/,\s*\)/g, ')')   // trailing comma from a prettier-wrapped argument list
  .replace(/\(\s+/g, '(')    // `expect(\n  page.getByRole(...)` wrapped over three lines
  .trim();
const realLines = new Set(expand(real).split('\n').map(norm));
const realBlob = norm(expand(real));

const dir = path.join(__dirname, 'behaviours');
const all = fs.readdirSync(dir).filter((f) => f.endsWith('.beh'))
  .flatMap((f) => parse(fs.readFileSync(path.join(dir, f), 'utf8'), f));
const bindings = JSON.parse(fs.readFileSync(path.join(__dirname, 'bindings.json'), 'utf8'));
const { behaviours, symbols } = resolve(all);

let exact = 0, substring = 0, absent = 0;
const misses = [];

console.log(`── every generated line vs ${REPO}@origin/dev:${SPEC} ──\n`);
for (const b of behaviours) {
  const { code } = generate(b, bindings, symbols);
  for (const raw of code.split('\n')) {
    const line = raw.trim();
    if (!line.startsWith('await ')) continue;
    const n = norm(line);
    if (realLines.has(n)) { exact++; console.log(`  YES exact     ${line}`); }
    else if (realBlob.includes(n.replace(/;$/, ''))) { substring++; console.log(`  YES in-file   ${line}`); }
    else { absent++; misses.push(line); console.log(`  NO  absent    ${line}`); }
  }
}

console.log('\n── measured ──');
console.log(`  identical to a line a human wrote   ${exact}`);
console.log(`  present in the file, reflowed       ${substring}`);
console.log(`  not in the hand-written suite       ${absent}`);
if (misses.length) {
  console.log('\n  the misses, stated rather than buried:');
  for (const m of misses) console.log(`    ${m}`);
}
