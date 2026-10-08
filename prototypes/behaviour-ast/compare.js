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

// This tool implements NO flags — its one input is the positional above — so every
// `--`-prefixed token is a typo, and the empty list says that rather than leaving it
// to be inferred (cli.js, kit#67). It matters more here than it looks: the entire
// output is a per-line YES/NO about a NAMED spec in a NAMED repo, and the obvious
// typo is a `--repo`-shaped one borrowed from the five sibling tools that DO take
// flags. Unguarded, `compare.js --repo /x` made `--repo` the repo path, and the only
// reason that was not silent is that `git -C --repo` happens to die — which is worse
// than it sounds, because dying while quoting your typo is indistinguishable from
// refusing it, and that is precisely how this file passed the guard test for months
// without having a guard. `unknownFlag` skips positionals, so `argv[2]` is untouched.
const KNOWN_FLAGS = [];
{
  const bad = require('./cli.js').unknownFlag(process.argv.slice(2), KNOWN_FLAGS);
  if (bad) {
    process.exit(require('./cli.js').refuse(bad,
      'usage: compare.js [<path-to-snip-it>]   (or set SNIPIT_REPO)'));
  }
}

// kit#107: this read used to be unguarded, so a repo path that does not exist — or a
// clone with no `origin/dev`, or no such spec on it — dumped a raw `spawnSync` result
// object and a Node stack trace at exit 1.
//
// 🔑 Why that mattered beyond tidiness: every OTHER tool here answers "could not look"
// at exit 2, and the three-valued rule is that a refusal is never conflated with a
// verdict. At exit 1 with a stack trace, this was the one tool whose "I could not
// measure" was indistinguishable from a crash in the harness around it — and exit 1 is
// also what a real comparison uses to mean something, so a caller reading the status
// could not tell "snip-it has drifted" from "I never found snip-it".
//
// The three failures are deliberately NOT told apart. Each one means the same thing to
// a caller — the named spec could not be read — and git's own stderr already says
// which it was, so classifying them here would only add a way to be wrong about it.
let real;
try {
  real = execFileSync('git', ['-C', REPO, 'show', `origin/dev:${SPEC}`],
    { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
} catch (err) {
  const said = String(err.stderr || err.message || '').trim().split('\n')[0];
  process.stderr.write(`cannot look: could not read origin/dev:${SPEC} from ${REPO}\n`);
  if (said) process.stderr.write(`  git said: ${said}\n`);
  process.stderr.write('  pass a path to a snip-it clone, or set SNIPIT_REPO.\n');
  process.exit(2);
}

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

// ONE corpus, because the spec above is snip-it's. This read every `.beh` in the
// directory until kit#78, so each new corpus added its lines to the denominator —
// kit-ui's headings reported "absent" from snip-it's spec — and the score fell
// from 26 of 28 matched to 26 of 77 with no commit touching this file. The
// directory is a population that grows; the subject is not.
const dir = path.join(__dirname, 'behaviours');
const CORPUS = 'snip-it.beh';
const all = parse(fs.readFileSync(path.join(dir, CORPUS), 'utf8'), CORPUS);
// Each behaviour generates against ITS OWN corpus's bindings (kit#66), never a
// merged map — the same rule kit.js follows, so this comparison and the report
// cannot disagree about what a corpus binds.
const bindingsOf = require('./bindings.js');
const byApp = bindingsOf.readAll(dir);
const { behaviours, symbols } = resolve(all);

let exact = 0, substring = 0, absent = 0;
const misses = [];

console.log(`── every generated line vs ${REPO}@origin/dev:${SPEC} ──\n`);
for (const b of behaviours) {
  const { code } = generate(b, byApp[bindingsOf.corpusOf(b)] || {}, symbols);
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
