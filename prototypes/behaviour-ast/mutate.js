#!/usr/bin/env node
'use strict';
/**
 * Does each rule in kit.js have a test that FAILS without it?
 *
 * A test written in the same commit as the code it tests proves nothing until
 * you break the code and watch the test go red. Every rule below is one the
 * suite claims to enforce; a SURVIVED line means the claim is unbacked.
 *
 * This lived in a gitignored scratch directory for two wakes, which meant the
 * "5/5 killed" quoted on kit#3 was backed by a file nobody else could run and
 * nothing would notice going stale. It is tracked now for that reason alone.
 *
 * It mutates kit.js on disk and restores from an in-memory copy — deliberately
 * NOT `git checkout --`, which reverts to HEAD and destroys uncommitted work in
 * the file under test. It also re-runs the suite after restoring and exits 2 if
 * that is not green, so a crash mid-run cannot leave a mutated kit.js behind
 * looking like a passing tree.
 *
 *   node prototypes/behaviour-ast/mutate.js
 */
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const K = path.join(__dirname, 'kit.js');
const T = path.join(__dirname, 'kit-test.js');
const original = fs.readFileSync(K, 'utf8');

const MUTANTS = [
  // adjudication (#68: "default included but marked unreviewed")
  ['inference defaults to approved, not unreviewed',
    "cur.review = { state: 'unreviewed', note: null };", "cur.review = { state: 'approved', note: null };"],
  ['a bare denial is allowed through',
    "if (r[1] === 'denied' && !r[2]) throw", 'if (false) throw'],
  ['untraceable behaviours are never reported',
    'behaviours.filter((b) => !b.source.ref)', 'behaviours.filter(() => false)'],
  ['an explicit review is overwritten by a later source line',
    "if (s[1] === 'inferred' && !cur.reviewExplicit) {", "if (s[1] === 'inferred') {"],
  ['an unknown source origin is accepted',
    '/^(defined|inferred)(?:\\s+(.+))?$/', '/^(\\w+)(?:\\s+(.+))?$/'],

  // displayed surface (kit#3: "expose only what is required to display")
  ['a dangling serves link is tolerated',
    'if (!target) {', 'if (false) {'],
  ['an inference may serve another inference',
    "} else if (target.source.origin !== 'defined') {", '} else if (false) {'],
  ['a defined behaviour may carry a serves line',
    "if (b.source.origin === 'defined') {", 'if (false) {'],
  ['DEFINED behaviours are counted as unserved surface too',
    "const inferred = behaviours.filter((b) => b.source.origin === 'inferred');\n  return {\n    errors,",
    'const inferred = behaviours;\n  return {\n    errors,'],
  ['nothing is ever reported as unserved',
    'unserved: inferred.filter((b) => !b.serves.length),', 'unserved: [],'],
  ['serves accepts prose instead of a behaviour id',
    'const s = /^([A-Z][A-Z0-9-]*)$/.exec(rest);', 'const s = [rest, rest];'],
];

const run = () => {
  try { execFileSync('node', [T], { encoding: 'utf8' }); return 0; }
  catch (e) { return (String(e.stdout || '').match(/FAIL/g) || []).length || 1; }
};

let killed = 0;
const survived = [];
for (const [name, from, to] of MUTANTS) {
  // An anchor that stopped matching is a SURVIVOR, not a skip: it means the
  // mutation silently stopped being applied and the rule stopped being measured.
  if (!original.includes(from)) { console.log(`  ⚠️  ANCHOR MISSING  ${name}`); survived.push(name); continue; }
  fs.writeFileSync(K, original.replace(from, to));
  const fails = run();
  fs.writeFileSync(K, original);
  if (fails > 0) { killed++; console.log(`  killed (${fails} failing)  ${name}`); }
  else { survived.push(name); console.log(`  SURVIVED             ${name}`); }
}
console.log(`\n${killed}/${MUTANTS.length} killed, ${survived.length} survived`);
if (run() !== 0) { console.error('HARNESS BROKEN: suite is not green after restore'); process.exit(2); }
process.exit(survived.length ? 1 : 0);
