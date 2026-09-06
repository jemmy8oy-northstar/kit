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

const T = path.join(__dirname, 'kit.test.js');
// Mutants name their file; kit.js is the default because it was the only one
// until `check.js` existed. A gate whose rules are never mutated is exactly the
// unbacked claim this harness exists to catch, so the harness had to grow rather
// than the gate go unmeasured.
const SUBJECTS = { 'kit.js': null, 'check.js': null, 'prose-audit.js': null, 'saturation.js': null, 'self-host.js': null, 'project.js': null, 'ui.js': null };
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
  ['the independent count declines for JS again, so nothing cross-checks the reader',
    "if (!file.endsWith('.cs')) return jsDeclarationCount(src);", "if (!file.endsWith('.cs')) return null;"],

  // reading JS tests. Every one of these shipped as real behaviour for the life
  // of the regex reader, and none of them was caught by anything, because until
  // now the JS half had no second count to contradict it.
  ['the reader goes back to matching quote-shaped text anywhere, not declarations',
    'JS_DECL.lastIndex = 0;', 'JS_DECL.lastIndex = 0; JS_DECL = JS_TITLE;'],
  ['a parameterised test loses its table skip, so its title is never reached',
    'const past = skipGroup(src, at);', 'const past = at;'],
  ['the group skip stops counting depth, so a bracket inside a table row ends it',
    'else if (c === close || (open === \'(\' && c === \']\') || (open === \'[\' && c === \')\')) { depth--; if (depth === 0) return i + 1; }',
    'else if (c === close) return i + 1;'],
  ['quoted text inside a .each table is read as structure',
    "if (c === '\"' || c === \"'\") { i = skipQuoted(src, i); if (i < 0) return -1; continue; }", 'if (false) { continue; }'],
  ['the second count stops stripping strings, so a fixture literal counts as a test',
    "if (c === '\"' || c === \"'\") {\n      const e = skipQuoted(src, i);", "if (false) {\n      const e = skipQuoted(src, i);"],
  ['the second count stops stripping comments, so a commented-out test counts',
    "if (c === '/' && src[i + 1] === '/') { const e = src.indexOf('\\n', i); if (e < 0) break; out += ' '; i = e - 1; continue; }", ''],
  ['a JS count disagreement reports xUnit attributes as its evidence',
    "const evidence = f.endsWith('.cs')", 'const evidence = true', 'check.js'],
  ['the lookbehind goes, so every SOME_RE.test(x) counts as a test declaration',
    '/(?<![.\\w$])(?:test|it)(?:\\.(?:only|skip|fixme|concurrent|each))?\\s*[([`]/g',
    '/\\b(?:test|it)(?:\\.(?:only|skip|fixme|concurrent|each))?\\s*[([`]/g'],

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

  // saturation (gap #8). The measurement argues AGAINST Kit's central bet, so
  // every rule here is one that, removed, makes the answer more flattering:
  // three of the five turn "no saturation" into "saturation".
  ['behaviours with no UI step are counted, and the curve saturates for free',
    'const bearing = behaviours.filter((b) => b.nouns.length > 0);',
    'const bearing = behaviours;', 'saturation.js'],
  ['the null median is not the shuffled corpus, so a trivial decline reads as evidence',
    'const r = halfRatio(marginal(shuffled(bearing, rnd)));',
    'const r = halfRatio(marginal(bearing));', 'saturation.js'],
  ['ties go to the flattering side of the percentile',
    'percentile: nullRatios.length ? (below + ties / 2) / nullRatios.length : null,',
    'percentile: nullRatios.length ? below / nullRatios.length : null,', 'saturation.js'],
  ['the front/back halves are swapped, so growth reads as decline',
    'return f === 0 ? null : mean(back) / f;',
    'return mean(back) === 0 ? null : f / mean(back);', 'saturation.js'],
  ['the two counts may disagree without a refusal',
    'if (r.crosscheck.onlyAst.length || r.crosscheck.onlyText.length) {',
    'if (false) {', 'saturation.js'],
  ['a corpus that binds nothing is measured instead of refused',
    'if (r.nouns === 0) problems.push', 'if (false) problems.push', 'saturation.js'],
  ['a corpus too small to halve is measured anyway',
    'if (results.every((r) => r.bearing < 4)) {', 'if (false) {', 'saturation.js'],
  ['a quoted literal containing a colon is read as a noun',
    'const stripped = rest.replace(/"[^"]*"/g, \'""\');', 'const stripped = rest;', 'saturation.js'],
  ['the write-up may drift from the corpora without going red',
    'if (drift.length) {', 'if (false) {', 'saturation.js'],
];

const run = () => {
  try { execFileSync('node', [T], { encoding: 'utf8' }); return 0; }
  catch (e) { return (String(e.stdout || '').match(/FAIL/g) || []).length || 1; }
};

// self-host (#89: "how does kit look as a kit managed project"). Every rule here
// exists to stop ONE number being read as something it is not, so a mutation
// that survives means the write-up's headline is unguarded.
MUTANTS.push(
  ['a `state` step counts as derived, so "0 derived" becomes "10 generated"',
    "const derived = bound.generated - (byVerb.get('state') || 0);", 'const derived = bound.generated;', 'self-host.js'],
  ['the discriminating step stops binding, so the null result is trivially true',
    'const bound = tally(generousBindings(behaviours));', 'const bound = tally({});', 'self-host.js'],
  ['the generous binding drops `state`, which under-reports what bindings CAN do',
    'state: `setUp(${JSON.stringify(key)})` };', '};', 'self-host.js'],
  ['the generator vocabulary is hard-coded instead of read from generate()',
    "return new Set([...body.slice(0, end).matchAll(/^\\s*case '([a-z]+)':/gm)].map((m) => m[1]));",
    "return new Set(['opens', 'activates', 'sees', 'shows', 'attaches', 'lands', 'fills', 'state', 'runs']);", 'self-host.js'],
  ['a corpus that parsed to nothing is reported instead of refused',
    'if (m.behaviours === 0 || m.steps === 0) {', 'if (false) {', 'self-host.js'],
  ['--check accepts drift silently',
    'if (JSON.stringify(was) !== JSON.stringify(now)) {', 'if (false) {', 'self-host.js'],
  // The original defect, reinstated: count the whole global bindings file
  // instead of this corpus's nouns. Every app then reports the same number and
  // a corpus binding nothing reports the same headline as one binding all.
  ['the bound-noun count goes back to counting the global bindings file',
    'const bound = [...referenced].filter((n) => Object.prototype.hasOwnProperty.call(bindings, n));',
    'const bound = Object.keys(bindings);', 'kit.js'],
  ['the referenced-noun set counts repeats, so a noun named twice inflates the denominator',
    'for (const b of behaviours) for (const n of nounsOf(b)) referenced.add(n);',
    'const _all = []; for (const b of behaviours) for (const n of nounsOf(b)) _all.push(n); referenced.add = Set.prototype.add; _all.forEach((n) => Set.prototype.add.call(referenced, n + Math.random()));',
    'kit.js'],
  ['saturation stops excluding a corpus that declares it has no UI',
    'if (NO_UI.test(text)) { skipped.push(f); return false; }', '', 'saturation.js'],
  ['saturation stops excluding a corpus for an app that does not exist',
    'if (NOT_REAL.test(text)) { excluded.push(f); return false; }', '', 'saturation.js'],
  ['a not-a-real-app corpus is excluded SILENTLY, so an invented app leaves the population invisibly',
    'for (const f of excluded) console.log(`  (skipping ${f}: declares "# kit:not-a-real-app" — a corpus for software that does not exist cannot evidence how real apps reuse nouns)`);', '', 'saturation.js'],
  ['saturation excludes the no-ui corpus SILENTLY, so the population is invisible',
    'for (const f of skipped) console.log(`  (skipping ${f}: declares "# kit:no-ui" — it describes no UI, so binding saturation has no meaning for it)`);', '', 'saturation.js'],
);

// project (docs/design/ui.md). The read model's whole job is to be believed by
// a UI that cannot check it, so every rule here is about not lying quietly.
MUTANTS.push(
  ['unavailable coverage becomes a ZERO-covered result, which a UI renders as an alarm',
    "return { available: false, reason };", 'return { available: false, reason, covered: [], uncovered: [] };', 'project.js'],
  ['a missing mapping is treated as "nothing covered" instead of "no mapping"',
    'coverage = unavailable(`no ${app}.tests.json', 'coverage = unavailable_UNUSED(`no ${app}.tests.json', 'project.js'],
  ['a reader that is losing tests is projected anyway',
    'if (read.fatal) {', 'if (false) {', 'project.js'],
  ['the over-claim caveat is dropped from the payload',
    "proves: 'someone LINKED each covered behaviour to a test. NOT that the test asserts the behaviour.',", '', 'project.js'],
  ['a corpus that parsed to nothing is projected instead of refused',
    'if (!behaviours.length) return { fatal: `${app}.beh parsed to zero behaviours` };', '', 'project.js'],
  // ⚠️ There is deliberately NO mutant here for stripping `_` metadata keys.
  // project.js had that rule, a mutation removing it survived, and the reason
  // was that `mapping()` in kit.js already skips them — the copy was dead code.
  // The rule is mutated where it actually lives.
);

// ui (docs/design/ui.md). Two of these four are security properties, and a
// security property that is only asserted in a comment is a wish. The other two
// are the read model's honesty rules surviving the trip through the transport.
MUTANTS.push(
  ['a write verb reaches a handler while decision 2 is still open',
    "if (method !== 'GET') {", 'if (false) {', 'ui.js'],
  ['the server binds every interface, publishing every corpus on the network',
    "const host = opts.host ?? DEFAULT_HOST;", "const host = opts.host ?? '0.0.0.0';", 'ui.js'],
  ['an app name is joined to a path instead of looked up, so ../ traverses',
    'if (!known.includes(app)) {', 'if (false) {', 'ui.js'],
  ['the list reports unavailable coverage as zero covered',
    ': { available: false, covered: null, uncovered: null, reason: cov.reason },',
    ': { available: false, covered: 0, uncovered: 0, reason: cov.reason },', 'ui.js'],
  ['a corpus that will not parse is listed as an app with no behaviours',
    'if (p.fatal) {', 'if (false) {', 'ui.js'],
);

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
