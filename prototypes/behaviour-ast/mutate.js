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
const marker = require('./mutation-marker');
const { unknownFlag, refuse, looksLikeAFlag } = require('./cli.js');

// kit#67. The last two tools in this directory to take a flag they do not know
// and run anyway — and the sharpest instance of it, because this one EDITS THE
// WORKING TREE: `node mutate.js --recoverr` did not recover, it discarded the
// typo and started a destructive 5½-minute run on a tree you had just asked it
// to put back.
//
// 🔑 THE GUARD IS FIRST, before even `--recover`, and that ordering is the whole
// reason this tool is testable at all. It reads argv and nothing else, so it is
// the one step here that neither touches the tree nor depends on it — which is
// what lets `kit.test.js` spawn this file in a directory containing none of the
// files below. Move it down and the refusal stops being provable without
// running the mutation.
//
// ONE list, rendered twice: flag name → the value it takes (empty for none), so
// the accept-list and the usage line cannot disagree. kit#100 fixed a tool with
// three hand-written copies of its flag list and left a gate holding two of them;
// having one copy is the version of that fix that cannot drift.
// 🔑 The value is the PLACEHOLDER, not the whole rendering. It started as the
// whole rendering (`'--recover': '--recover'`), which read fine and quietly made
// the key/value mixup untestable: `Object.values` still contained `--recover`, so
// a guard built from the wrong half of the map refused nothing and the red control
// for it came back green.
const FLAGS = { '--recover': '', '--only': '<substring>' };
const usage = () => Object.entries(FLAGS).map(([f, v]) => (v ? `${f} ${v}` : f)).join('] [');
const bad = unknownFlag(process.argv.slice(2), Object.keys(FLAGS));
if (bad) process.exit(refuse(bad, `usage: node mutate.js [${usage()}]`));

// `--only` runs the mutants whose name or file contains the substring — the slice
// a new rule needs, without the full ~2-hour run. Until it existed here, proving a new mutant meant a
// hand-rolled runner outside the repo, twice, and one of them could not see
// SUBJECTS: a mutant on a file this list did not name looked killed locally and
// was a survivor in CI.
// Checked on argv alone, beside the guard, so the sandbox can prove it: a bare
// `--only` would otherwise leave the filter empty and start the FULL run while
// the operator believes they asked for a slice ([[empty-means-two-things]]).
const onlyIdx = process.argv.indexOf('--only');
const only = onlyIdx === -1 ? null : process.argv[onlyIdx + 1];
if (onlyIdx !== -1 && (only === undefined || only === '' || looksLikeAFlag(only))) {
  console.error('cannot look: --only needs a substring to match, e.g. `--only requires.js`');
  process.exit(2);
}

// Recovery runs before anything reads the tree, so a run killed by an
// uncatchable signal is undoable from either mutation tool.
if (process.argv.includes('--recover')) process.exit(marker.recover());
marker.refuseIfStale('mutate.js');

const T = path.join(__dirname, 'kit.test.js');
// Mutants name their file; kit.js is the default because it was the only one
// until `check.js` existed. A gate whose rules are never mutated is exactly the
// unbacked claim this harness exists to catch, so the harness had to grow rather
// than the gate go unmeasured.
// 🔴 `cli.js` is DELIBERATELY NOT A SUBJECT, and the reason is a conflict rather
// than an oversight. `looksLikeAFlag` is now one line deciding whether twelve tools
// refuse a flag or swallow it, so a mutant over it is exactly what you would want —
// and adding one is IMPOSSIBLE while kit#101's sandbox stands. That sandbox spawns
// this tool in a temp directory holding only the tool and the two modules it
// requires at load, one of which is `cli.js`, precisely so the destructive path
// cannot start: its first act is to read a subject that is not there. A `cli.js`
// that is BOTH sandboxed and a subject gives the unguarded run something real to
// mutate inside the box, and `kit.test.js`'s "the sandbox holds none of the files
// mutate.js mutates" went red when this was tried. The invariant is worth more than
// the mutant: the predicate is gated by "ONE dash makes a token a flag" instead,
// which was run RED first and named five defects. **Do not "finish the job" by
// adding `cli.js` here — the test will tell you, but this says why.**
const SUBJECTS = { 'kit.js': null, 'compare.js': null, 'requires.js': null, 'check.js': null, 'prose-audit.js': null, 'saturation.js': null, 'self-host.js': null, 'project.js': null, 'converge.js': null, 'writer.js': null, 'selfhost/run.js': null };
for (const f of Object.keys(SUBJECTS)) SUBJECTS[f] = fs.readFileSync(path.join(__dirname, f), 'utf8');
// 🔴 RESTORING THE SOURCE IS NOT RESTORING THE TREE, and a whole class of mutant
// proves it. Two of the kit#66 mutants make a write land in THIS checkout's
// `behaviours/` instead of the directory it was told to use — that is the defect,
// and the suite kills them for it. But the file the mutant wrote is still there
// afterwards: `restoreAll()` only rewrites the subjects it read at startup, so a
// mutant's SIDE EFFECT outlives it.
//
// What that cost: the run ended `179/180 killed` and then `HARNESS BROKEN: suite
// is not green after restore` — because two stray `.bindings.json` files had
// joined the corpus directory and a test that counts what is in there noticed
// ([[a-directory-is-a-population]]). The message names the harness, so the
// obvious reading is that your change broke something, and the real cause is a
// mutant behaving exactly as designed.
//
// So the corpus directory is snapshotted too, and anything that appeared during a
// mutant is removed with it. Only ADDITIONS are cleaned: a mutant that deletes or
// edits a committed corpus file must still reach the final green check, because
// that is damage no snapshot here should be quietly papering over.
const CORPUS_DIR = path.join(__dirname, 'behaviours');
const CORPUS_BEFORE = new Set(fs.readdirSync(CORPUS_DIR));
const restoreAll = () => {
  for (const [f, src] of Object.entries(SUBJECTS)) fs.writeFileSync(path.join(__dirname, f), src);
  for (const f of fs.readdirSync(CORPUS_DIR)) {
    if (!CORPUS_BEFORE.has(f)) fs.rmSync(path.join(CORPUS_DIR, f), { force: true, recursive: true });
  }
};

// ⚠️ THIS TOOL EDITS THE WORKING TREE. For the duration of a run, the files on
// disk are deliberately wrong, and anything else reading the tree in that window
// reads a mutant — `git add -A` during a background run committed
// `index.set(k, 1)` into a pushed branch (claude-code-bot#92). It was invisible
// locally, because restoreAll() had already put the correct line back by the
// time anyone looked; only the CI runner ever saw it.
//
// So the window announces itself where the danger actually shows up: an
// untracked marker at the repo root, which `git status` prints as `??` right
// next to the files you were about to stage. Untracked and root-level on
// purpose — inside the prototype directory it would be one more line in a
// listing nobody reads, and tracked it would be a file to clean up.
//
// ⚠️ This used to say a marker left behind after a crash was "a false alarm".
// That was wrong twice over and is why `mutation-marker.js` exists — see its
// header. A crash leaves a live mutant on disk AND poisons the next run's idea
// of what pristine looks like.
marker.arm({
  tool: 'mutate.js',
  base: path.relative(path.join(__dirname, '..', '..'), __dirname),
  originals: SUBJECTS,
  warn: [
    'Do not commit, stage, or read prototypes/behaviour-ast/*.js while this file',
    'exists — you will capture a mutant. It is removed when the run ends.',
  ].join('\n'),
  restoreAll,
});

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
  // kit#73 / kit#93: a forward corpus is all `defined`
  ['an author\'s question on a defined behaviour is dropped again — a forward corpus reports 0 decisions',
    ".filter((b) => b.source.origin === 'defined' && b.asks && !owners.has(b.id) && !cited.has(b.id))",
    ".filter((b) => false)"],
  ['every defined behaviour is "asked", question or not — the sheet reprints the spec',
    ".filter((b) => b.source.origin === 'defined' && b.asks && !owners.has(b.id) && !cited.has(b.id))",
    ".filter((b) => b.source.origin === 'defined' && !owners.has(b.id) && !cited.has(b.id))"],
  ['a conflict\'s question is asked twice, once as the conflict and once as authored',
    'if (owner) owners.add(owner.id);', 'if (false) owners.add(owner.id);'],
  ['an authored option list is answered with serves-or-delete again',
    'if (q.options.length) {\n    return `the option you pick', 'if (false) {\n    return `the option you pick'],
  ['an empty evidence block prints a bare heading again',
    "if (!q.contracts.length && !q.serves.length) L.push('- _none recorded", "if (false) L.push('- _none recorded"],

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
    'if (!errors.length && !uncovered.length) {', 'if (true) {', 'check.js'],
  // kit#155's `pending` marker is an excuse the gate grants. The mutants further
  // down catch it granting too little; this one catches it excusing everything.
  ['every uncovered behaviour counts as pending, so a missing test reads as unbuilt work',
    'const uncovered = result.uncovered.filter((b) => !b.pending);',
    'const uncovered = [];', 'check.js'],

  // --dir. The gate can now read a corpus that lives with its project (kit#52),
  // which means it can also read the WRONG one and report a confident verdict
  // over it. Every mutant here is a version that still exits 0 on something.
  ['--dir is accepted and ignored, so the gate reads kit own corpus instead',
    'const behPath = path.join(dir, `${app}.beh`);',
    'const behPath = path.join(DEFAULT_DIR, `${app}.beh`);', 'check.js'],
  // ⚠️ The PLAUSIBLE half-fix, and the reason it gets its own mutant: moving the
  // corpus lookup and leaving the mapping behind passes every test that only
  // checks --via markers, because that path never opens the mapping at all.
  ['the mapping stays on __dirname, so a relocated project is gated against kit own claims',
    'const mapPath = path.join(dir, `${app}.tests.json`);',
    'const mapPath = path.join(DEFAULT_DIR, `${app}.tests.json`);', 'check.js'],
  ['an unknown flag is ignored again, so a typo silently gates the default corpus',
    'return { error: `unknown option ${a}` };', 'continue;', 'check.js'],
  ['a value flag with nothing after it eats the next flag instead of refusing',
    'if (v === undefined || looksLikeAFlag(v)) return { error: `${a} needs a value` };',
    'if (false) return { error: `${a} needs a value` };', 'check.js'],
  ['a second positional is taken as the app, so two corpus names is first-one-wins',
    'return { error: `two app names given, "${opts.app}" and "${a}" — this gate checks one corpus` };',
    'opts.app = a;', 'check.js'],

  // ── selectCorpora: the rule that a NAME names one corpus ───────────────────
  // Both hand-verified before being written down. The first reddens 2 tests (the
  // rule's own and the end-to-end spawn), the second reddens 2 (the refusal and
  // the saturation.js drift guard) — and the drift guard is why the second one
  // matters most: it is what stops saturation.js quietly keeping its own copy of
  // this rule again, which is the shape the defect had for as long as it existed.
  ['an exact corpus name stops winning, so `kit.js kit` merges kit.beh and kit-ui.beh again',
    'if (exact.length) return { files: exact };', 'if (false) return { files: exact };', 'kit.js'],
  ['a name that matches two corpora merges them silently instead of refusing',
    'if (matches.length > 1) {', 'if (false) {', 'kit.js'],

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
  // The bug this file's own write-up carried for eleven days: `--check` compared
  // the JSON to the corpus, agreed with itself, and never opened the markdown
  // anyone actually reads.
  ['--check stops reading the write-up, so the prose can say 14 while the corpus says 20',
    'if (text !== md) {', 'if (false) {', 'self-host.js'],
  // One level up again: a checker whose markers stop matching must say COULD NOT
  // LOOK. If this survives, renaming a marker is a silent way to switch the
  // check off.
  ['a marker the parser cannot find reads as agreement instead of could-not-look',
    'if (begin === -1 || end === -1 || end < begin) { missing.push(key); continue; }',
    'if (begin === -1 || end === -1 || end < begin) { continue; }', 'self-host.js'],
  // A splice that inserts beside the stale block instead of replacing it leaves
  // BOTH numbers in the document, and the wrong one reads like the right one.
  ['the splice keeps the stale block and writes the fresh one beside it',
    "${out.slice(end)}`;", '${out.slice(begin + BEGIN(key).length)}`;', 'self-host.js'],
  // 🔴 kit#66's central rule, as a mutant: the all-corpora run merges every
  // corpus's bindings into one map, which is the flat namespace rebuilt inside
  // the loop that replaced it. A behaviour in a corpus that binds nothing would
  // then resolve some OTHER project's noun and emit a test that runs against the
  // wrong app — the exact emission his decision exists to make impossible.
  ['the all-corpora run merges every corpus\x27s bindings, restoring the global namespace',
    'const { code, missing, stats } = generate(b, bindingsFor(b), symbols);',
    'const { code, missing, stats } = generate(b, Object.assign({}, ...Object.values(byApp)), symbols);',
    'kit.js'],
  // The original defect, reinstated: count the whole bindings file instead of
  // this corpus's nouns. Every app then reports the same number and a corpus
  // binding nothing reports the same headline as one binding all.
  ['the bound-noun count goes back to counting the whole bindings file',
    'const bound = [...referenced].filter((n) => Object.prototype.hasOwnProperty.call(bindings, n));',
    'const bound = Object.keys(bindings);', 'kit.js'],
  ['the referenced-noun set counts repeats, so a noun named twice inflates the denominator',
    'for (const b of behaviours) for (const n of nounsOf(b)) referenced.add(n);',
    'const _all = []; for (const b of behaviours) for (const n of nounsOf(b)) _all.push(n); referenced.add = Set.prototype.add; _all.forEach((n) => Set.prototype.add.call(referenced, n + Math.random()));',
    'kit.js'],
  // `fills` is the ONE verb whose nouns are not written in the step — they are
  // resolved out of another behaviour's `provides`. Reading `bindings` directly
  // instead of going through bind() is therefore invisible everywhere else, and
  // it is how the CLI came to report 16 unbound nouns on a corpus where the
  // requires panel reported 18 (#38, #61). The second mutant is the plausible
  // WRONG fix: routing through bind() but refusing on the first miss, which
  // names one field per round instead of all of them.
  ['a field reached through a `provides` goes back to bypassing bind(), so the CLI stops naming it',
    "const bound = fields.map((f) => bind({ kind: 'field', name: f }));",
    'const bound = fields.map((f) => bindings[`field:${f}`] || null);', 'kit.js'],
  ['fills refuses on the FIRST unbound field, so a user is told about them one round at a time',
    "const bound = fields.map((f) => bind({ kind: 'field', name: f }));\n      if (bound.some((fb) => !fb)) return null;",
    "const bound = []; for (const f of fields) { const fb = bind({ kind: 'field', name: f }); if (!fb) return null; bound.push(fb); }",
    'kit.js'],
  // kit#78: the comparison's subject is snip-it, and the directory is a population
  // that grows underneath it.
  ['compare.js reads every corpus again, so other apps\' lines are scored against snip-it\'s spec',
    "const all = parse(fs.readFileSync(path.join(dir, CORPUS), 'utf8'), CORPUS);",
    "const all = fs.readdirSync(dir).filter((f) => f.endsWith('.beh')).flatMap((f) => parse(fs.readFileSync(path.join(dir, f), 'utf8'), f));",
    'compare.js'],
  // kit#76: one population under `── measured ──`. The first mutant is the old
  // denominator coming back (every noun a step names); the second hides the nouns
  // no binding could satisfy, which is what made 0/43 read as 0% of the work.
  ['the bound fraction counts every referenced noun again, so it no longer sums with the unbound list',
    'for (const n of ownKeys) targets.add(`${app}\\0${n}`);',
    'for (const n of [...ownKeys, ...boundNouns(bs, {}).referenced]) targets.add(`${app}\\0${n}`);', 'kit.js'],
  ['the nouns no binding could satisfy stop being named',
    'if (!ownKeys.has(n)) notBindable.add(n);', 'if (false) notBindable.add(n);', 'kit.js'],
  // kit#155: the `pending` marker. Each is one half of "not built yet" going
  // missing — the parser dropping it, the gate ignoring it in either direction,
  // or the writer's collateral rule no longer seeing it.
  ['the parser reads `pending` and drops it, so a spec\'d behaviour fails the gate as untested',
    '      cur.pending = true;\n', '', 'kit.js'],
  ['`pending` accepts an argument, inviting a second state the gate has no opinion on',
    "if (rest) throw new Error(`${at}: pending takes", "if (false) throw new Error(`${at}: pending takes", 'kit.js'],
  ['the gate counts a pending behaviour as uncovered again',
    'const uncovered = result.uncovered.filter((b) => !b.pending);', 'const uncovered = result.uncovered;', 'check.js'],
  ['a pending marker that outlived its build passes the gate',
    'for (const b of result.covered.filter((x) => x.pending)) {', 'for (const b of [].filter((x) => x.pending)) {', 'check.js'],
  ['the writer\'s collateral rule stops seeing `pending`, so an edit can strip another behaviour\'s marker',
    'review: b.review, pending: b.pending,', 'review: b.review,', 'writer.js'],
  // kit#151: `fills field:X with …`. Each of these is the silent drop coming back
  // by a different door — the field unnamed when the value refuses, a two-value
  // `provides` re-joined into a guess, the obligation gone from requires.js.
  ['a single-field fill binds only when it has a value, so an unbound field with an open hole goes unnamed again',
    '        const fb = bind(field);',
    '        const fb = (literal || providedValue(step) !== null) ? bind(field) : null;', 'kit.js'],
  ['a provided value that split on a comma fills with its first half instead of refusing',
    'return v && v.length === 1 ? v[0] : null;',
    'return v && v.length ? v[0] : null;', 'kit.js'],
  ['requires.js forgets the field a single-field fill names, so it leaves the contract',
    "    if (field) return [{ nounKey: key(field), kind: 'field', name: field.name, req: LABEL, verb: 'fills', at: step.at }];",
    '', 'requires.js'],
  // kit.js's own CLI. The first of these is the defect as it actually shipped:
  // every `--flag` this tool does not know was dropped in silence, so
  // `kit.js kit --dir /elsewhere` reported on Kit's own corpus and said nothing
  // (`--dir` is a real flag here since kit#71; the hazard is any flag it lacks).
  // The rest are the guards written alongside it (#67).
  ['an unknown flag goes back to being silently dropped, so a flag it lacks reports on the wrong corpus',
    '    } else if (looksLikeAFlag(a)) {\n      return { error: `unknown option ${a}` };\n',
    '    } else if (looksLikeAFlag(a)) {\n      continue;\n', 'kit.js'],
  ['--help stops being recognised, so asking for help runs the whole report',
    "    if (a === '--help' || a === '-h') {\n      opts.help = true;\n    } else if (CLI_VALUE_FLAGS.has(a)) {",
    '    if (CLI_VALUE_FLAGS.has(a)) {', 'kit.js'],
  ['a value flag at the end eats the following flag instead of refusing',
    'if (v === undefined || looksLikeAFlag(v)) return { error: `${a} needs a value` };',
    'if (v === undefined) return { error: `${a} needs a value` };', 'kit.js'],
  ['`sheet` is detected in last position rather than first, so the subcommand and the corpus swap',
    "if (argv[0] === 'sheet') { opts.sheet = true; i = 1; }",
    "if (argv[argv.length - 1] === 'sheet') { opts.sheet = true; i = 1; }", 'kit.js'],
  // The NaN: a corpus that parses to nothing used to render a full report whose
  // every number was 0 and whose last one was not a number.
  ['a corpus that parses to zero behaviours is reported on instead of refused',
    '  if (!behaviours.length) {\n    console.error(`cannot look: ${files.join(\', \')} parsed to 0 behaviours — nothing to report on yet`);\n    process.exit(2);\n  }\n',
    '', 'kit.js'],
  // selfhost/run.js — the harness that executes Kit's own output. Every mutant
  // here is reachable WITHOUT a browser, on purpose: the parts that need one
  // are gated by `--check`, which is a manual run, so anything only a browser
  // could catch would be a mutant nobody ever kills.
  ['derived stops subtracting `state` steps, so a copied setup string counts as generated code',
    'stats.derived = stats.generated - stats.state;', 'stats.derived = stats.generated;', 'selfhost/run.js'],
  ['a --playwright path that does not exist is accepted, so the refusal becomes a crash later',
    'return fs.existsSync(bin) ? bin : null;', 'return bin;', 'selfhost/run.js'],
  ['an unreadable run reports as zero failures — a clean bill of health for a run that never happened',
    'return got.passed === 0 && got.failed === 0;', 'return false;', 'selfhost/run.js'],
  ['--check accepts drift silently, so the write-up and the run can part company',
    'return JSON.stringify(want) !== JSON.stringify(now);', 'return false;', 'selfhost/run.js'],
  ['a browser that would not start is read as a tally, so a broken pod reports Kit regressing to zero',
    'return /browserType\\.launch:/.test(output);', 'return false;', 'selfhost/run.js'],
  ['the reporter\'s padding stays inside the recorded test name, so a rename reads as drift',
    '(.*?)(?:\\s+─+)?\\s*$', '(.*?)\\s*$', 'selfhost/run.js'],
  ['the line after a refusal is not recorded, so the adjacency THAT IS THE FINDING cannot be asserted',
    "if (m) out.push({ step: m[1], next: (lines[i + 1] || '').trim() });", "if (m) out.push({ step: m[1], next: '' });", 'selfhost/run.js'],
  ['saturation stops excluding a corpus that declares it has no UI',
    'if (NO_UI.test(text)) { skipped.push(f); return false; }', '', 'saturation.js'],
  ['saturation stops excluding a corpus for an app that does not exist',
    'if (NOT_REAL.test(text)) { excluded.push(f); return false; }', '', 'saturation.js'],
  ['a not-a-real-app corpus is excluded SILENTLY, so an invented app leaves the population invisibly',
    'for (const f of excluded) console.log(`  (skipping ${f}: declares "# kit:not-a-real-app" — a corpus for software that does not exist cannot evidence how real apps reuse nouns)`);', '', 'saturation.js'],
  ['a self-authored corpus rejoins the study, so one author\'s naming reads as independent evidence',
    'if (SELF_AUTHORED.test(text)) { selfAuthored.push(f); return false; }', '', 'saturation.js'],
  ['saturation excludes the self-authored corpus SILENTLY, so the population is invisible',
    'for (const f of selfAuthored) console.log(`  (skipping ${f}: declares "# kit:self-authored" — the app, the corpus and the bindings share one author, so its noun order measures that author, not the app)`);', '', 'saturation.js'],
  ['saturation excludes the no-ui corpus SILENTLY, so the population is invisible',
    'for (const f of skipped) console.log(`  (skipping ${f}: declares "# kit:no-ui" — it describes no UI, so binding saturation has no meaning for it)`);', '', 'saturation.js'],
  ['saturation stops excluding a SECOND corpus for an app already in the study, doubling its weight',
    'if (dup) { duplicates.push([f, dup[1]]); return false; }', '', 'saturation.js'],
  ['a duplicate corpus is excluded SILENTLY, so one app counts twice with nothing said',
    'for (const [f, of] of duplicates) console.log(`  (skipping ${f}: declares "# kit:duplicate-corpus ${of}" — a second corpus for an app already in this study would weight ${of} twice while looking like independent evidence)`);', '', 'saturation.js'],
  // The one that would be easy to get subtly wrong: excluding the duplicate is
  // only right if the app it duplicates STAYS. A rule that dropped both would
  // silently shrink the population by a real app and still look like a working
  // exclusion — every "is it skipped" assertion above would still pass.
  ['the duplicate exclusion drops the ORIGINAL app too, silently shrinking the study',
    'const dup = DUPLICATE.exec(text);', 'const dup = DUPLICATE.exec(text) || /^behaviour/m.exec(text);', 'saturation.js'],
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
  ['a trial corpus is projected as a real app, so the UI shows an invented app as a project',
    'notReal: /^#\\s*kit:not-a-real-app\\b/m.test(src),', 'notReal: false,', 'project.js'],
  ['notReal goes undefined rather than false, so "absent" and "real" become the same reading',
    'notReal: /^#\\s*kit:not-a-real-app\\b/m.test(src),', 'notReal: undefined,', 'project.js'],
  ['a duplicate corpus is projected with no marker, so a trial and its subject list as two equal projects',
    "duplicateOf: (/^#\\s*kit:duplicate-corpus\\s+(\\S+)/m.exec(src) || [null, null])[1],", 'duplicateOf: null,', 'project.js'],
  ['duplicateOf names nothing, so the reader cannot tell WHICH app is doubled',
    "duplicateOf: (/^#\\s*kit:duplicate-corpus\\s+(\\S+)/m.exec(src) || [null, null])[1],",
    "duplicateOf: /^#\\s*kit:duplicate-corpus\\b/m.test(src) ? true : null,", 'project.js'],
  // ⚠️ There is deliberately NO mutant here for stripping `_` metadata keys.
  // project.js had that rule, a mutation removing it survived, and the reason
  // was that `mapping()` in kit.js already skips them — the copy was dead code.
  // The rule is mutated where it actually lives.
);

// writer + the write path (docs/design/ui.md decision 2, lapsed 2026-09-08).
//
// Every mutant here makes the writer LOSE something quietly — a comment, a
// neighbouring behaviour, the loopback refusal, the sentence saying nothing was
// committed. That is the only direction worth mutating: a writer that fails
// loudly is a bug someone fixes, and a writer that succeeds wrongly edits the
// file everything else in Kit measures against.
MUTANTS.push(
  ['the edit drops the corpus comments — the failure mode this whole file exists to prevent',
    "const lines = text.split('\\n'); // the whole file, comments and blanks included",
    "const lines = text.split('\\n').filter((l) => !l.trim().startsWith('#'));", 'writer.js'],
  ['a block ends at the next header, so an appended step lands past the blank line',
    "if (line.trim() && !line.trim().startsWith('#')) end = i;", 'end = i;', 'writer.js'],
  ['an edit that will not parse is written anyway',
    "return { ok: false, error: 'would-not-parse', reason: e.message };",
    'return { ok: true, text: after };', 'writer.js'],
  ['a change to a NEIGHBOURING behaviour is accepted',
    'if (shape(n) !== shape(b))', 'if (false)', 'writer.js'],
  ['a behaviour DELETED by the edit is accepted',
    'if (!n) return', 'if (false) return', 'writer.js'],
  ['shape() ignores steps, so a neighbour losing a step compares as unchanged',
    "steps: b.steps.map((s) => [s.kind, s.verb, s.noun, s.text].join('|')),", 'steps: [],', 'writer.js'],
  ['a behaviour written by a machine defaults to `defined`, spending the silence a human owns',
    "opts.source || 'inferred'", "opts.source || 'defined'", 'writer.js'],
  ['a duplicate behaviour id is appended, so one id names two behaviours',
    'if (ids(text).includes(id)) {', 'if (false) {', 'writer.js'],
  ['a step containing a newline is spliced in as two lines',
    'if (/\\n/.test(String(line)))', 'if (false)', 'writer.js'],
  // ── setReview: the first writer that CHANGES a line ───────────────────────
  // Both of these were verified by hand before being written down here, and
  // both were red for the right reason: the first reddens three tests across
  // the unit and the transport layer, the second reddens two. A mutant that
  // only ever passes has never shown it discriminates anything.
  ['an adjudication is APPENDED, so the corpus keeps saying `review unreviewed` as well',
    'lines[at] = line;', 'lines.splice(at + 1, 0, line);', 'writer.js'],
  ['a newline in the review note is spliced into the TARGET, which rule 3 exempts from comparison',
    "if (/\\n/.test(String(state ?? '')) || /\\n/.test(String(note ?? ''))) {", 'if (false) {', 'writer.js'],
  ['the review line lands above `actor`/`source` instead of under them',
    'lines.splice(anchor + 1, 0, line);', 'lines.splice(b.start + 1, 0, line);', 'writer.js'],
  ['setReview ignores the note entirely, so a denial silently loses its correction',
    'const line = INDENT + (nt ? `review ${st} ${nt}` : `review ${st}`);',
    'const line = INDENT + `review ${st}`;', 'writer.js'],
);

// converge (claude-code-bot#92). Its whole output is a claim about how far two
// specifications of one product disagree, and every failure mode here makes that
// claim FLATTERING rather than merely wrong — which is the direction that gets
// quoted.
MUTANTS.push(
  ['loose() stems and synonymises, so two names for one control score as agreement',
    "return noun.slice(noun.indexOf(':') + 1).toLowerCase().replace(/[^a-z0-9]/g, '');",
    "return noun.slice(noun.indexOf(':') + 1).toLowerCase().replace(/[^a-z0-9]/g, '').slice(0, 5);", 'converge.js'],
  ['loose() stops flattening kind, so one noun named twice reads as two disagreements',
    "return noun.slice(noun.indexOf(':') + 1).toLowerCase().replace(/[^a-z0-9]/g, '');",
    'return noun;', 'converge.js'],
  ['an empty comparison reports perfect agreement instead of "nothing to compare"',
    'return union.size === 0 ? null :', 'return union.size === 0 ? { inter: 0, union: 0, ratio: 1 } :', 'converge.js'],
  ['the shared nouns are counted but never named, so the score cannot be checked',
    'shared: [...a.nouns].filter((n) => b.nouns.has(n)).sort(),', 'shared: [],', 'converge.js'],
  ['total disagreement prints an empty section rather than saying so',
    "L.push(r.shared.length ? '  AGREED ON:' : '  AGREED ON: nothing — not one noun in common');",
    "if (r.shared.length) L.push('  AGREED ON:');", 'converge.js'],
  ['a corpus that will not load is measured as zero agreement instead of refused',
    'if (bad.length) {', 'if (false) {', 'converge.js'],
  ['onlyA is computed strictly, so a kind-only difference is reported as a unique noun',
    'onlyA: [...a.nouns].filter((n) => !b.byLoose.has(loose(n))).sort(),',
    'onlyA: [...a.nouns].filter((n) => !b.nouns.has(n)).sort(),', 'converge.js'],
);

// binding a noun (stage 4). Every rule here guards a failure that is SILENT:
// a binding written with less in it than you gave, a rebind that changes every
// corpus at once, a collision report that quietly says "nothing". None of them
// throws, and all of them read as success on screen — which is why they are
// mutated rather than trusted to the tests that were written beside them.
MUTANTS.push(
  // The guard that was inert in its first draft. Reinstating the original
  // spelling is the point: `JSON.stringify(value)` compares the damage to
  // itself, so this mutant restores a check that passes while doing nothing.
  // ⚠️ The first spelling of this mutant SURVIVED, and the mutant was wrong,
  // not the code: it replaced `Object.keys(reread[key])` with
  // `Object.keys(JSON.parse(JSON.stringify(value)))`, which strips `undefined`
  // exactly the same way — an equivalent mutant, which is unkillable by
  // construction. The real historical defect was a stringify-to-stringify
  // COMPARISON, so that is what gets reinstated here.
  ['the round-trip guard compares stringify to stringify, so an emptied binding passes',
    'const survived = Object.keys(reread[key]);\n  const lost = keys.filter((k) => !survived.includes(k));\n  if (lost.length) {',
    'const lost = JSON.stringify(reread[key]) !== JSON.stringify(value) ? keys : [];\n  if (lost.length) {', 'writer.js'],
  ['a binding is written even when a key would be deleted by JSON.stringify',
    'if (lost.length) {', 'if (false) {', 'writer.js'],
  ['rebinding is allowed, so one click changes every corpus that mentions the noun',
    'if (Object.prototype.hasOwnProperty.call(bindings, key)) {', 'if (false) {', 'writer.js'],
  ['an empty binding is accepted — it satisfies no verb and still counts as bound',
    'if (keys.length === 0) {', 'if (false) {', 'writer.js'],
  ['the noun check goes back to a regex that disagrees with the parser',
    'function isNoun(s) {',
    'function isNoun(s) { return /^[a-z][a-z0-9]*:[A-Za-z][A-Za-z0-9_]*$/.test(String(s).trim()); } function _isNounUnused(s) {',
    'writer.js'],
  // ⚠️ Mutating the CHECK survived, and correctly: `{ ...bindings, [key]: value }`
  // cannot lose a key, so the collateral loop is unfalsifiable as written and
  // the mutant was equivalent. What the check is FOR is a future construction
  // that does lose one — so the damage is mutated instead of the guard, and
  // the guard is what has to notice.
  ['adding a binding drops the ones already there',
    'const after = { ...bindings, [key]: value };', 'const after = { [key]: value };', 'writer.js'],
  // sharedWith: the mechanism replacing the habit bindings.json's own comment
  // describes. Both directions, because a function that always reports nothing
  // and one that always reports everything are equally useless and only one of
  // them looks broken.
  ['sharedWith always reports nothing, so the global namespace is silent again',
    'if (nouns.includes(noun)) out.push(app);', '', 'writer.js'],
  ['sharedWith includes the corpus you are already in',
    "if (app === self) continue;", '', 'writer.js'],
  ['a corpus that will not parse is silently dropped with no report',
    'if (onSkip) onSkip(app, e);', '', 'writer.js'],
  // The defect running the server found: the write honoured --bindings and the
  // read did not, so the page re-read a different file and showed the same
  // refusal after a successful bind.
  //
  // ⚠️ RE-ANCHORED TWICE, and the second time the rule itself changed shape. It
  // was "the projection must honour the file it was given"; under kit#66 nobody
  // is given a file, because bindings live beside the corpus and `dir` selects
  // both. So the split can only be reopened by DROPPING THE DIRECTORY — a read
  // or a write that falls back to this checkout's own `behaviours/` while its
  // counterpart uses the one it was pointed at. The write side lived in the Node
  // server (deleted, kit#153) and is held by the C# tests now; the READ side's
  // mutant is what remains here ([[one-sided-assertion-blesses-the-wrong-fix]]).
  ['the projection reads THIS checkout\x27s bindings instead of the corpus directory it was given',
    'const bindings = require(\x27./bindings.js\x27).readFor(app, behDir);',
    'const bindings = require(\x27./bindings.js\x27).readFor(app);', 'project.js'],
  ['missing and insufficient are collapsed, hiding the binding that satisfies no verb',
    'insufficient: req.insufficient.map(noun),', 'insufficient: [],', 'project.js'],
);

// Filters a derived list only: SUBJECTS and restoreAll() still cover every file.
// A filter matching nothing is a typo, not a clean pass — exit 2, never `0/0 killed`.
const RUN = only ? MUTANTS.filter(([name, , , file = 'kit.js']) => name.includes(only) || file.includes(only)) : MUTANTS;
if (!RUN.length) {
  console.error(`cannot look: --only ${JSON.stringify(only)} matched no mutants`);
  process.exit(2);
}

let killed = 0;
const survived = [];
for (const [name, from, to, file = 'kit.js'] of RUN) {
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
console.log(`\n${killed}/${RUN.length} killed, ${survived.length} survived${only ? ` (--only ${JSON.stringify(only)}: ${RUN.length} of ${MUTANTS.length})` : ''}`);
if (run() !== 0) { console.error('HARNESS BROKEN: suite is not green after restore'); process.exit(2); }
process.exit(survived.length ? 1 : 0);
