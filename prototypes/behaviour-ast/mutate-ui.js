#!/usr/bin/env node
'use strict';
/**
 * Does each rule in the UI have a test that FAILS without it?
 *
 * `mutate.js` asks that question of the ten plain-Node files. It has never
 * asked it of `ui/src`, because its subjects are read with `require` and its
 * suite is `node kit.test.js` — no install, no transform, in-process. The
 * frontend is neither: it needs `npm ci`, a JSX transform and a jsdom
 * environment, which is exactly why `ci.yml` keeps `ui` as a second job.
 *
 * So this is a sibling rather than a flag on `mutate.js`. Putting the two in
 * one file would put a dependency install in front of the prototype gate that
 * deliberately has none, and `node mutate.js` would start failing in the
 * `prototype` job for reasons that have nothing to do with the prototype.
 *
 * Everything about ~1,500 lines of `ui/src` was, until this file, backed only
 * by tests written in the same commit as the code they test. That is the
 * unbacked claim `mutate.js`'s own header exists to catch, on the half of the
 * repo that is now the product.
 *
 *   node prototypes/behaviour-ast/mutate-ui.js
 *
 * Exit 0 = every mutant killed. 1 = something survived (or a mutant is
 * invalid). 2 = could not look — deliberately not 0, the same convention
 * `check.js` and `selfhost/run.js` use.
 */
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const UI = path.join(__dirname, 'ui');
const VITEST = path.join(UI, 'node_modules', '.bin', 'vitest');

// ── could not look ───────────────────────────────────────────────────────────
// A mutation harness that silently reports "0 survived" because it never ran
// anything is worse than no harness: it reads identically to a clean run. The
// two ways that happens are an absent install and an absent suite, and both
// exit 2 rather than 0.
if (!fs.existsSync(VITEST)) {
  console.error('cannot look: ui/node_modules/.bin/vitest is missing — run `npm ci` in prototypes/behaviour-ast/ui first');
  process.exit(2);
}

const SUBJECT_FILES = [
  'src/pages/Projects.tsx',
  'src/pages/Project.tsx',
  'src/pages/BehaviourPage.tsx',
  'src/components/Count.tsx',
  'src/components/CoverageBadge.tsx',
  'src/components/Resource.tsx',
  'src/components/WriteResultNote.tsx',
  'src/components/useWrite.ts',
  'src/hooks/useResource.ts',
  'src/api/client.ts',
];
const SUBJECTS = {};
for (const f of SUBJECT_FILES) SUBJECTS[f] = fs.readFileSync(path.join(UI, f), 'utf8');
const restoreAll = () => {
  for (const [f, src] of Object.entries(SUBJECTS)) fs.writeFileSync(path.join(UI, f), src);
};

// Same danger as `mutate.js`, so the same signal in the same place: for the
// duration of a run the files on disk are deliberately wrong, and anything that
// stages the tree in that window commits a mutant (claude-code-bot#92). The
// marker is untracked and at the repo root so `git status` prints it as `??`
// right next to the files you were about to stage.
const MARKER = path.join(__dirname, '..', '..', 'MUTATION-IN-PROGRESS');
const dropMarker = () => { try { fs.unlinkSync(MARKER); } catch { /* already gone */ } };
fs.writeFileSync(MARKER, [
  'mutate-ui.js is running and the working tree is deliberately WRONG.',
  '',
  'Do not commit, stage, or read prototypes/behaviour-ast/ui/src/** while this',
  'file exists — you will capture a mutant. It is removed when the run ends.',
  '',
  `started ${new Date().toISOString()} by pid ${process.pid}`,
  '',
].join('\n'));
process.on('exit', dropMarker);
process.on('SIGINT', () => { restoreAll(); dropMarker(); process.exit(130); });
process.on('SIGTERM', () => { restoreAll(); dropMarker(); process.exit(143); });

const ANSI = /\[[0-9;]*m/g;

/**
 * Run the suite and say HOW it failed, not just whether.
 *
 * The distinction matters and is the one thing this runner does that
 * `mutate.js`'s does not need to. A `.tsx` mutant can break the esbuild
 * transform or the type-free parse instead of a test, and vitest exits
 * non-zero either way. Counting that as a kill would be counting a mutant the
 * suite never saw: it died at collection, one step before the assertions I am
 * trying to measure. So a run reports one of:
 *
 *   { failed: n }      n tests failed — a real kill, the suite noticed
 *   { failed: 0 }      green — the mutant SURVIVED
 *   { invalid: why }   the suite never ran the mutant (transform/collect error)
 *
 * `invalid` is not a kill and not a survivor. It means the mutant itself is
 * badly written and must be rewritten, and it is loud because an invalid
 * mutant counted as a kill inflates the score with a test that does not exist.
 */
const run = () => {
  let out;
  try {
    out = execFileSync(VITEST, ['run', '--reporter=basic'], {
      cwd: UI, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'],
      env: { ...process.env, CI: 'true', FORCE_COLOR: '0' },
    });
    // vitest exits 0 only when the whole suite is green.
    return { failed: 0, out };
  } catch (e) {
    out = (String(e.stdout || '') + String(e.stderr || '')).replace(ANSI, '');
    const m = /Tests\s+(\d+) failed/.exec(out);
    if (m) return { failed: Number(m[1]), out };
    // No "Tests N failed" line at all: vitest never got as far as reporting on
    // tests. Either it could not transform the mutated file, or it collected no
    // files. Both are "the mutant never reached the suite".
    const why = /Unhandled Error|Failed to load|Transform failed|No test files found|error TS\d+/.exec(out);
    return { invalid: why ? why[0] : `vitest exited non-zero with no test-failure line`, out };
  }
};

// ── mutants ──────────────────────────────────────────────────────────────────
// Each entry: [name, from, to, file].
//
// `name` states the WRONG BEHAVIOUR A USER WOULD SEE, not the line being
// edited. That phrasing is the design rule, not a style: a survivor is only
// actionable if it names the damage that ships unnoticed. A mutant whose name
// is "removes the filter" tells you nothing about whether you care.
//
// ⚠️ Mutate the damage, not the guard. Deleting a `data-testid` makes every
// test that queries it go red, which looks like a kill and proves nothing
// except that the element was findable. Those are excluded on purpose.
const MUTANTS = [
  ['CONTROL-KILL: an unmeasured project renders "0/0 covered" instead of "not measured"',
    'if (!coverage.available) {', 'if (false) {', 'src/components/CoverageBadge.tsx'],
  ['CONTROL-UNKNOWN: a count of one is pluralised — "1 conflicts"',
    '{n} {n === 1 ? one : (many ?? `${one}s`)}', '{n} {many ?? `${one}s`}', 'src/components/Count.tsx'],
  ['CONTROL-INVALID: deliberate syntax error, must report INVALID and not a kill',
    'export default function Count({', 'export default function Count({{', 'src/components/Count.tsx'],
];

let killed = 0;
const survived = [];
const invalid = [];

for (const [name, from, to, file] of MUTANTS) {
  const original = SUBJECTS[file];
  // An anchor that stopped matching is a SURVIVOR, not a skip. A refactor that
  // moves the line silently stops the rule from being measured, and a skip
  // reports identically to a kill — the failure mode that makes a mutation
  // score worth nothing.
  if (original === undefined) { console.log(`  ⚠️  UNKNOWN SUBJECT ${file}  ${name}`); survived.push(name); continue; }
  if (!original.includes(from)) { console.log(`  ⚠️  ANCHOR MISSING  ${name}`); survived.push(name); continue; }
  // A `from` that appears twice mutates only the first and the mutant means
  // something other than what its name says — also a survivor, not a skip.
  if (original.split(from).length > 2) { console.log(`  ⚠️  ANCHOR AMBIGUOUS  ${name}`); survived.push(name); continue; }

  fs.writeFileSync(path.join(UI, file), original.replace(from, to));
  const r = run();
  restoreAll();

  if (r.invalid) { invalid.push(name); console.log(`  ⚠️  INVALID MUTANT (${r.invalid})  ${name}`); }
  else if (r.failed > 0) { killed++; console.log(`  killed (${r.failed} failing)  ${name}`); }
  else { survived.push(name); console.log(`  SURVIVED             ${name}`); }
}

console.log(`\n${killed}/${MUTANTS.length} killed, ${survived.length} survived, ${invalid.length} invalid`);

// Restoring from an in-memory copy can itself be the thing that is broken, so
// the run does not get to end on trust: the suite must be green again before
// this exits, or the tree is left in a state that looks passing and is not.
const after = run();
if (after.invalid || after.failed > 0) {
  console.error('HARNESS BROKEN: suite is not green after restore');
  process.exit(2);
}
process.exit(survived.length || invalid.length ? 1 : 0);
