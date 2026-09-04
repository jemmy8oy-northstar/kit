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

const T = path.join(__dirname, 'kit-test.js');
// Mutants name their file; kit.js is the default because it was the only one
// until `check.js` existed. A gate whose rules are never mutated is exactly the
// unbacked claim this harness exists to catch, so the harness had to grow rather
// than the gate go unmeasured.
const SUBJECTS = { 'kit.js': null, 'check.js': null, 'prose-audit.js': null };
for (const f of Object.keys(SUBJECTS)) SUBJECTS[f] = fs.readFileSync(path.join(__dirname, f), 'utf8');
const restoreAll = () => {
  for (const [f, src] of Object.entries(SUBJECTS)) fs.writeFileSync(path.join(__dirname, f), src);
};

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

  // the question sheet (kit#3: "a behaviour question sheet... with Gemini")
  ['everything unreviewed is tiered as a decision — the ranking stops ranking',
    "tier: detected || b.asks ? 'decision' : 'review',", "tier: 'decision',"],
  ['a human can no longer promote a served inference with asks',
    "tier: detected || b.asks ? 'decision' : 'review',", "tier: detected ? 'decision' : 'review',"],
  ['an already-adjudicated behaviour stays on the sheet forever',
    "if (b.source.origin !== 'inferred' || b.review.state !== 'unreviewed') continue;",
    "if (b.source.origin !== 'inferred') continue;"],
  ['a decision may ship with no question stated',
    "if (q.tier === 'decision' && !q.asks) {", 'if (false) {'],
  ['a recommendation may ship with no counter-case — advocacy passes the gate',
    'if (q.recommend && !q.against) {', 'if (false) {'],
  ['a recommendation may name an option that does not exist',
    'if (q.recommend && !q.options.some((o) => o.label === q.recommend.label)) {', 'if (false) {'],
  ['a question may ship with a single option',
    'if (q.options.length < 2) errors.push', 'if (false) errors.push'],
  ['a cited behaviour is ALSO listed as its own review row — the double-ask returns',
    'if (citedBy.has(b.id)) continue;', 'if (false) continue;'],
  ['a cites naming nothing is silently dropped instead of refused',
    'if (!c.ref) errors.push', 'if (false) errors.push'],
  ['cites accepts prose instead of a behaviour id',
    'if (!/^BEH-[A-Z0-9-]+$/.test(rest)) throw', 'if (false) throw'],

  // reading an app's tests — the reader every number downstream rests on
  ['the [Theory] lookahead goes back to a fixed six lines — the 16% under-read',
    'for (let j = i + 1; j < lines.length; j++) {', 'for (let j = i + 1; j < Math.min(i + 6, lines.length); j++) {'],
  ['[InlineData] rows are no longer skipped, so no theory finds its method',
    'if (CS_SKIP.test(lines[j])) continue;', 'if (false) continue;'],
  ['the walk keeps scanning past the first non-attribute line, grabbing a later method',
    'break; // matched or not, the first non-attribute line settles it', 'continue;'],
  ['a DisplayName is ignored and the method name reported instead',
    'raw: display ? display[1] : m[1], style:', 'raw: m[1], style:'],
  ['the independent count declines for C# too, so nothing cross-checks the walk',
    "if (!file.endsWith('.cs')) return null;", 'if (true) return null;'],

  // the mapping (option C) — every one of these is the mapping rotting SILENTLY,
  // which is the single property the option is chosen for.
  ['a mapping naming a test that no longer exists is accepted',
    'if (n === 0) {', 'if (false) {'],
  ['an ambiguous title is accepted as if it addressed one test',
    'if (n > 1) {', 'if (false) {'],
  ['duplicate titles are never counted, so ambiguity cannot be seen',
    'index.set(k, (index.get(k) || 0) + 1);', 'index.set(k, 1);'],
  ['a mapping entry for a behaviour the corpus dropped is accepted',
    'if (!ids.has(id)) {', 'if (false) {'],
  ['a mapping naming a file that is not a test file is accepted',
    'if (!files.has(e.file)) {', 'if (false) {'],
  ['_ metadata keys are treated as behaviour ids',
    "if (id.startsWith('_')) continue; // reserved for metadata", 'if (false) continue;'],

  // the gate itself. `could not look` collapsing into `looked and was fine` is
  // the failure mode that makes a green CI meaningless.
  ['a repo with zero test files reports no problems instead of refusing',
    'if (!read.files.length) {', 'if (false) {', 'check.js'],
  ['a reader that lost tests is trusted anyway',
    'if (read.fatal) {', 'if (false) {', 'check.js'],
  ['an unknown --via silently falls back to a default',
    "if (via !== 'mapping' && via !== 'markers') {", 'if (false) {', 'check.js'],
  ['an id named by a test but absent from the corpus is not reported',
    'errors = result.orphanTests.map(', 'errors = [].map(', 'check.js'],
  ['uncovered behaviours no longer affect the exit code',
    'if (!errors.length && !result.uncovered.length) {', 'if (true) {', 'check.js'],

  // the prose accounting. Every rule here exists to stop the corpus reporting a
  // flattering fraction of a document it only partly encoded, so a survivor
  // means the flattering version would ship unnoticed.
  ['a behaviour no acceptance criterion asked for is not reported',
    'if (!asked.has(id)) problems.push', 'if (false) problems.push', 'prose-audit.js'],
  ['a ledger entry may name a behaviour the corpus does not have',
    'if (!ids.has(id)) problems.push', 'if (false) problems.push', 'prose-audit.js'],
  ['an unknown disposition is tallied instead of refused',
    'if (!DISPOSITIONS.has(ac.disposition)) {', 'if (false) {', 'prose-audit.js'],
  ['any invented shape is accepted into the taxonomy',
    'if (!SHAPES.has(s)) problems.push', 'if (false) problems.push', 'prose-audit.js'],
  ['"inexpressible" no longer has to name a missing shape',
    "if (ac.disposition === 'inexpressible' && !hasS)", 'if (false)', 'prose-audit.js'],
  ['"partial" no longer has to say what did not fit',
    "if (ac.disposition === 'partial' && !(hasB && hasS))", 'if (false)', 'prose-audit.js'],
  ['"encoded" may quietly leave something unmet',
    "if (ac.disposition === 'encoded' && (!hasB || hasS))", 'if (false)', 'prose-audit.js'],
  ['a contract or refusal need not name the behaviour it lives on',
    "if ((ac.disposition === 'contract' || ac.disposition === 'refused') && !hasB)", 'if (false)', 'prose-audit.js'],
  ['a source document with zero acceptance criteria is treated as clean',
    'if (!live.length) {', 'if (false) {', 'prose-audit.js'],
  ['drift in an AC\'s wording goes unnoticed',
    'else if (rec.text !== l.text) drift.push', 'else if (false) drift.push', 'prose-audit.js'],
  ['an acceptance criterion added after the ledger was written is not counted',
    'if (live.length !== ledger.acs.length) {', 'if (false) {', 'prose-audit.js'],
  ['drift is computed and then does not affect the exit code',
    'if (problems.length || drift.length) {', 'if (problems.length) {', 'prose-audit.js'],
  ['the AC extractor also swallows completed [x] criteria',
    '/^- \\[ \\]/.test(l)', '/^- \\[.\\]/.test(l)', 'prose-audit.js'],
];

const run = () => {
  try { execFileSync('node', [T], { encoding: 'utf8' }); return 0; }
  catch (e) { return (String(e.stdout || '').match(/FAIL/g) || []).length || 1; }
};

let killed = 0;
const survived = [];
for (const [name, from, to, file = 'kit.js'] of MUTANTS) {
  const original = SUBJECTS[file];
  // An anchor that stopped matching is a SURVIVOR, not a skip: it means the
  // mutation silently stopped being applied and the rule stopped being measured.
  if (original === undefined) { console.log(`  ⚠️  UNKNOWN SUBJECT ${file}  ${name}`); survived.push(name); continue; }
  if (!original.includes(from)) { console.log(`  ⚠️  ANCHOR MISSING  ${name}`); survived.push(name); continue; }
  fs.writeFileSync(path.join(__dirname, file), original.replace(from, to));
  const fails = run();
  restoreAll();
  if (fails > 0) { killed++; console.log(`  killed (${fails} failing)  ${name}`); }
  else { survived.push(name); console.log(`  SURVIVED             ${name}`); }
}
console.log(`\n${killed}/${MUTANTS.length} killed, ${survived.length} survived`);
if (run() !== 0) { console.error('HARNESS BROKEN: suite is not green after restore'); process.exit(2); }
process.exit(survived.length ? 1 : 0);
