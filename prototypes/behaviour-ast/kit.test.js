#!/usr/bin/env node
'use strict';
/**
 * Tests for the prototype.
 *
 * The numbers quoted on claude-code-bot#68 come out of kit.js and compare.js,
 * so a bug in either makes the argument false rather than merely rough. The
 * load-bearing cases are the REFUSALS — "it generates a test" is easy, and
 * "it declines to generate a wrong test" is the entire design claim.
 *
 * Every refusal test has a paired positive control, so a version that refuses
 * everything (which would pass all the refusal tests) fails the suite.
 */

const assert = require('assert');
const { parse, resolve, generate } = require('./kit');

let pass = 0, fail = 0;

// The real console, captured at module load — before anything can have replaced
// it — and used by the RUNNER for everything it prints. `quiet()` below stubs
// `console.log` to silence the code under test, and that stub is global while
// async tests are pending, so module-scope code running in the same window is
// silenced too.
//
// That is not a theoretical hazard. It swallowed a section header and eight
// `ok` lines from this file's own output, and the run still ended
// `223 passed, 0 failed` — the tally survives because it prints once the depth
// counter is back to zero. A FAIL would have been counted and NOT NAMED, which
// is the worst of the three possible outcomes: a red run you cannot read.
// The rule is one line long — **the runner never prints through the global it
// lets tests replace.**
const REAL_LOG = console.log;
const REAL_ERR = console.error;

/** A section heading in the run's output. Goes through REAL_LOG, see above. */
const section = (name) => REAL_LOG(`\n── ${name} ──`);

// Async tests go through the SAME helper, deliberately. `ui.js` needs a real
// listening socket to be tested at all, and a separate `atest(...)` would be
// invisible to `testTitles` — the reader keys on `test(`/`it(` — so every async
// test would vanish from the count, `check.js` would see a mapping naming a
// title it cannot find, and the stage-0 gate would go red for a reason that has
// nothing to do with the change. One helper, one name, one reader.
const pending = [];
const test = (name, fn) => {
  const ok = () => { pass++; REAL_LOG(`  ok   ${name}`); };
  const no = (e) => { fail++; REAL_LOG(`  FAIL ${name}\n       ${e.message}`); };
  try {
    const result = fn();
    if (result && typeof result.then === 'function') { pending.push(result.then(ok, no)); return; }
    ok();
  } catch (e) { no(e); }
};

const BIND = {
  'page:Home': { route: './' },
  'page:Editor': { route: './editor/:id', urlPattern: '/editor/x$' },
  'button:Go': { role: 'button', name: 'Go' },
  'field:Email': { label: 'Email' },
};
const build = (src) => {
  const { behaviours, conflicts, symbols } = resolve(parse(src, 'test.beh'));
  return { behaviours, conflicts, symbols };
};
const gen = (src, bindings = BIND) => {
  const { behaviours, symbols } = build(src);
  return behaviours.map((b) => generate(b, bindings, symbols));
};

section('parse');

test('reads an id and a title', () => {
  const [b] = parse('behaviour BEH-1 "does a thing"');
  assert.strictEqual(b.id, 'BEH-1');
  assert.strictEqual(b.title, 'does a thing');
});

test('a step outside a behaviour is an error, not silently dropped', () => {
  assert.throws(() => parse('when opens page:Home'), /outside a behaviour/);
});

test('an unrecognised keyword is an error, not silently dropped', () => {
  // A spec language that ignores what it does not understand is how a spec
  // becomes descriptive: the author believes a line is enforced and it is not.
  assert.throws(() => parse('behaviour B "t"\n  wibble page:Home'), /unrecognised keyword/);
});

test('a bare noun with no verb parses as a state precondition', () => {
  const [b] = parse('behaviour B "t"\n  given transcription:Completed');
  assert.strictEqual(b.steps[0].verb, 'state');
});

test('holes and nouns are told apart on the same line', () => {
  const [b] = parse('behaviour B "t"\n  when fills form:Upload with ?fields');
  assert.deepStrictEqual(b.steps[0].holes.map((h) => h.slot), ['fields']);
  assert.deepStrictEqual(b.steps[0].refs.map((r) => `${r.kind}:${r.name}`), ['form:Upload']);
});

section('resolve: the cross-behaviour symbol table');

test('a hole is filled by a DIFFERENT behaviour', () => {
  const { behaviours } = build(
    'behaviour A "a"\n  when fills form:Upload with ?fields\n' +
    'behaviour B "b"\n  provides form:Upload.fields = Email');
  assert.deepStrictEqual(behaviours[0].filled.map((f) => f.value[0]), ['Email']);
  assert.strictEqual(behaviours[0].open.length, 0);
});

test('an unfilled hole stays OPEN rather than being quietly dropped', () => {
  const { behaviours } = build('behaviour A "a"\n  when fills form:Upload with ?fields');
  assert.strictEqual(behaviours[0].filled.length, 0);
  assert.strictEqual(behaviours[0].open[0].key, 'form:Upload.fields');
});

test('two behaviours agreeing is agreement, not a conflict', () => {
  const { conflicts, symbols } = build(
    'behaviour A "a"\n  provides form:U.fields = Email\n' +
    'behaviour B "b"\n  provides form:U.fields = Email');
  assert.strictEqual(conflicts.length, 0);
  assert.deepStrictEqual(symbols.get('form:U.fields').contributors, ['A', 'B']);
});

test('two behaviours disagreeing IS a conflict, with both sides named', () => {
  const { conflicts } = build(
    'behaviour A "a"\n  provides form:U.fields = Email\n' +
    'behaviour B "b"\n  provides form:U.fields = Phone');
  assert.strictEqual(conflicts.length, 1);
  assert.deepStrictEqual(conflicts[0].holders, ['A']);
  assert.strictEqual(conflicts[0].challengers[0].from, 'B');
});

section('generate: the refusals, which are the design claim');

test('CONTROL: a fully bound behaviour generates', () => {
  const [{ code, stats }] = gen('behaviour A "a"\n  when opens page:Home\n  when activates button:Go');
  assert.match(code, /page\.goto\("\.\/"\)/);
  assert.match(code, /getByRole\("button", \{ name: "Go" \}\)\.click\(\)/);
  assert.strictEqual(stats.ungenerated, 0);
});

test('an unbound noun is REFUSED, and the missing noun is named', () => {
  const [{ code, missing, stats }] = gen('behaviour A "a"\n  when activates button:Nope');
  assert.match(code, /UNGENERATED/);
  assert.deepStrictEqual(missing, ['button:Nope']);
  assert.strictEqual(stats.ungenerated, 1);
});

test('an unbound noun never produces a guessed locator', () => {
  // The failure being prevented: emitting getByRole('button', {name: 'Nope'})
  // from the noun's own name would produce a test that runs and asserts nothing
  // about the app the spec describes.
  const [{ code }] = gen('behaviour A "a"\n  when activates button:Nope');
  assert.ok(!/getByRole/.test(code), `guessed a locator: ${code}`);
});

test('an unsupplied ROUTE PARAM is refused — the real bug this caught', () => {
  // First version stripped `:id` and emitted `./editor/`: a test that runs,
  // navigates to the wrong page, and looks correct in review.
  const [{ code }] = gen('behaviour A "a"\n  when opens page:Editor');
  assert.match(code, /UNGENERATED/);
  assert.ok(!/goto/.test(code), `emitted a truncated route: ${code}`);
});

test('CONTROL: the same route generates once the param is provided', () => {
  const [a] = gen(
    'behaviour A "a"\n  when opens page:Editor\n' +
    'behaviour B "b"\n  provides page:Editor.id = abc');
  assert.match(a.code, /page\.goto\("\.\/editor\/abc"\)/);
});

test('a hole filled from elsewhere actually GENERATES, not just reports', () => {
  // Reporting the fill without using it would make the whole mechanism
  // decorative — it would look resolved and emit nothing.
  const [a] = gen(
    'behaviour A "a"\n  when fills form:Upload with ?fields\n' +
    'behaviour B "b"\n  provides form:Upload.fields = Email');
  assert.match(a.code, /getByLabel\("Email"\)/);
});

test('a wire contract is never generated, and never uncounted', () => {
  const [{ code, stats }] = gen('behaviour A "a"\n  contract POST /api/x is sent once');
  assert.match(code, /CONTRACT \(not derivable/);
  assert.strictEqual(stats.contract, 1);
  assert.strictEqual(stats.generated, 0);
  assert.strictEqual(stats.ungenerated, 0);
});

test('the test name carries the behaviour id, which is what coverage greps for', () => {
  const [{ code }] = gen('behaviour BEH-9 "a"');
  assert.match(code, /\[BEH-9\]/);
});

section('coverage: the only part that can go red');

const { coverage } = require('./kit');

test('a behaviour no test names is uncovered', () => {
  const bs = parse('behaviour BEH-1 "a"\nbehaviour BEH-2 "b"');
  const r = coverage(bs, ['test("[BEH-1] a", () => {})']);
  assert.deepStrictEqual(r.uncovered.map((b) => b.id), ['BEH-2']);
  assert.deepStrictEqual(r.covered.map((b) => b.id), ['BEH-1']);
});

test('a test naming a behaviour that no longer exists is an orphan', () => {
  // The other direction of rot: the behaviour was deleted or renamed and the
  // test kept passing, still claiming to cover it.
  const r = coverage(parse('behaviour BEH-1 "a"'), ['test("[BEH-7] gone", () => {})']);
  assert.deepStrictEqual(r.orphanTests, ['BEH-7']);
});

section('adjudication: "default included but marked unreviewed" (James, #68)');

const { adjudication } = require('./kit');

test('an inference defaults to unreviewed WITHOUT anyone writing review', () => {
  // The load-bearing one. He chose default-INCLUDE, so the only thing keeping a
  // machine guess from passing as a requirement is that it arrives unreviewed
  // by default. If this ever defaults to approved, the mechanism is decorative.
  const [b] = parse('behaviour BEH-1 "a"\n  source inferred tests/X.cs:name');
  assert.strictEqual(b.source.origin, 'inferred');
  assert.strictEqual(b.review.state, 'unreviewed');
});

test('a behaviour with no source line is defined and approved — old corpora still parse', () => {
  // The positive control for the test above: a version that marked EVERYTHING
  // unreviewed would pass it and be useless. Silence means a human wrote it.
  const [b] = parse('behaviour BEH-1 "a"\n  actor visitor');
  assert.strictEqual(b.source.origin, 'defined');
  assert.strictEqual(b.review.state, 'approved');
});

test('an explicit review survives a later source line', () => {
  const [b] = parse('behaviour BEH-1 "a"\n  review approved\n  source inferred tests/X.cs:n');
  assert.strictEqual(b.review.state, 'approved', 'an adjudicated inference must not revert to unreviewed');
});

test('a denial without a correction is refused', () => {
  // His #68 point: "on a deny a required indication of what correct behaviour
  // actually looks like should take place". A bare denial deletes a line; a
  // denial with a correction compounds into the corpus.
  assert.throws(() => parse('behaviour BEH-1 "a"\n  review denied'), /must state the correction/);
  assert.doesNotThrow(() => parse('behaviour BEH-1 "a"\n  review denied streaks reset at midnight UTC'));
});

test('an unknown source origin is refused rather than silently ignored', () => {
  assert.throws(() => parse('behaviour BEH-1 "a"\n  source guessed'), /defined.*inferred/);
});

test('the never-adjudicated count counts only unreviewed INFERENCES', () => {
  const bs = parse([
    'behaviour BEH-1 "human wrote this"',
    '  source defined docs/DESIGN.md#A1',
    'behaviour BEH-2 "model guessed this"',
    '  source inferred tests/X.cs:a',
    'behaviour BEH-3 "model guessed, human approved"',
    '  source inferred tests/X.cs:b',
    '  review approved',
  ].join('\n'));
  const a = adjudication(bs);
  assert.strictEqual(a.defined, 1);
  assert.strictEqual(a.inferred, 2);
  assert.deepStrictEqual(a.unreviewed.map((b) => b.id), ['BEH-2']);
  assert.deepStrictEqual(a.approved.map((b) => b.id), ['BEH-3']);
});

test('a behaviour with no traceable ref is reported separately from an unreviewed one', () => {
  // Worse than unreviewed: an approve/deny needs something to point AT six weeks
  // on, and a behaviour citing nothing cannot be checked against anything.
  const a = adjudication(parse('behaviour BEH-1 "a"\nbehaviour BEH-2 "b"\n  source defined docs/D.md#x'));
  assert.deepStrictEqual(a.untraceable.map((b) => b.id), ['BEH-1']);
});

section('displayed surface (his frontend-first answer, kit#3)');
const { surface } = require('./kit');

// A DEFINED behaviour is served, never serving. Without this control the report
// would count every documented behaviour as unserved surface and read as
// catastrophe on a healthy corpus.
const SERVED_BY = 'behaviour BEH-UI "screen" \n  source defined docs/D.md#1\n';

test('an inferred behaviour that serves a documented one is not a finding', () => {
  const s = surface(build(SERVED_BY +
    'behaviour BEH-API "route"\n  source inferred code.cs:X\n  serves BEH-UI\n').behaviours);
  assert.deepStrictEqual(s.errors, []);
  assert.deepStrictEqual(s.unserved.map((b) => b.id), []);
  assert.deepStrictEqual(s.served.map((b) => b.id), ['BEH-API']);
});

test('an inferred behaviour serving nothing IS the finding', () => {
  const s = surface(build(SERVED_BY +
    'behaviour BEH-API "route"\n  source inferred code.cs:X\n').behaviours);
  assert.deepStrictEqual(s.unserved.map((b) => b.id), ['BEH-API']);
  assert.deepStrictEqual(s.errors, [], 'unserved is a report, not an error — it needs a human, not a fix');
});

test('a DEFINED behaviour serving nothing is not a finding (the control)', () => {
  const s = surface(build(SERVED_BY).behaviours);
  assert.deepStrictEqual(s.unserved.map((b) => b.id), [],
    'a documented behaviour is served, not serving — flagging it would flood the report');
});

test('a serves link to an id that does not exist breaks the build', () => {
  const s = surface(build('behaviour BEH-API "route"\n  source inferred code.cs:X\n  serves BEH-GHOST\n').behaviours);
  assert.strictEqual(s.errors.length, 1);
  assert.match(s.errors[0], /BEH-GHOST, which is not in the corpus/);
});

test('an inference serving an inference breaks the build — the chain must reach a human', () => {
  const s = surface(build(
    'behaviour BEH-A "one"\n  source inferred code.cs:A\n' +
    'behaviour BEH-B "two"\n  source inferred code.cs:B\n  serves BEH-A\n').behaviours);
  assert.ok(s.errors.some((e) => /itself inferred/.test(e)));
});

test('a defined behaviour carrying a serves line breaks the build', () => {
  const s = surface(build(SERVED_BY +
    'behaviour BEH-OTHER "x"\n  source defined docs/D.md#2\n  serves BEH-UI\n').behaviours);
  assert.ok(s.errors.some((e) => /is defined, so it is served rather than serving/.test(e)));
});

test('serves wants a behaviour id, not prose', () => {
  assert.throws(() => parse('behaviour BEH-A "a"\n  serves the today screen\n', 't.beh'), /serves wants a behaviour id/);
});

section('the pilot corpus is real material, not a fixture');

test('james-habits-app parses and its spec-vs-spec conflict is detected', () => {
  // The pilot's headline finding, pinned so it cannot silently stop being found.
  // CORRECTED SINCE #3: this is DEFINED-vs-DEFINED, not doc-vs-code. MVP 5 fixes
  // the window at 30; the Architecture section of the SAME document parameterises
  // it. Both sides now assert `source defined`, and the assertion below is what
  // stops the corpus quietly sliding back to the flattering version.
  const fs = require('fs');
  const path = require('path');
  const src = fs.readFileSync(path.join(__dirname, 'behaviours/james-habits-app.beh'), 'utf8');
  const { behaviours, conflicts } = resolve(parse(src, 'james-habits-app.beh'));
  assert.ok(behaviours.length >= 20, `expected a real corpus, got ${behaviours.length}`);

  const clash = conflicts.find((c) => c.key === 'region:CompletionGrid.days');
  assert.ok(clash, 'the 30-day-vs-caller-supplied contradiction must be detected');
  assert.deepStrictEqual(clash.held, ['30']);
  assert.ok(clash.challengers.some((x) => x.from === 'BEH-WINDOW-API'));

  const byId = new Map(behaviours.map((b) => [b.id, b]));
  for (const id of ['BEH-WINDOW-MVP', 'BEH-WINDOW-API']) {
    assert.strictEqual(byId.get(id).source.origin, 'defined',
      `${id} must stay DEFINED — spec-vs-spec is the axis Kiro's spec-to-code testing does not cover`);
  }

  // Every inferred behaviour must cite a real file:symbol. A corpus that cites
  // nothing looks identical to one that cites everything, right up to the moment
  // someone tries to check it.
  const a = adjudication(behaviours);
  assert.strictEqual(a.untraceable.length, 0, 'every pilot behaviour must cite its source');
  assert.ok(a.inferred >= 10, 'the inferred half is the whole point of the pilot');
});

test('the pilot names exactly the two behaviours nothing documented displays', () => {
  // His frontend-first answer, measured on real material. Pinning the IDENTITIES
  // rather than the count: a corpus that grew a third unserved behaviour would
  // still pass a `=== 2`, and the whole value of this report is which ones.
  const fs = require('fs');
  const path = require('path');
  const src = fs.readFileSync(path.join(__dirname, 'behaviours/james-habits-app.beh'), 'utf8');
  const { behaviours } = resolve(parse(src, 'james-habits-app.beh'));
  const s = surface(behaviours);
  assert.deepStrictEqual(s.errors, [], 'the pilot corpus must have no broken serves links');
  assert.deepStrictEqual(s.unserved.map((b) => b.id).sort(), ['BEH-ARCHIVE-2', 'BEH-ERROR-1']);
  // The positive control that stops "refuse everything" passing: most of the
  // inferred half DOES serve something, so an empty `served` set is a bug.
  assert.ok(s.served.length >= 8, `expected most inferences to serve a screen, got ${s.served.length}`);
});

test('language-vocab is a different shape from habits, and the corpus says so', () => {
  // The second pilot app James named on #68. Its value is the CONTRAST: habits
  // has a full backend and no frontend; vocab has neither, only a domain. If a
  // future change made the two corpora report the same shape, one of them would
  // have stopped describing its app.
  const fs = require('fs');
  const path = require('path');
  const src = fs.readFileSync(path.join(__dirname, 'behaviours/language-vocab.beh'), 'utf8');
  const { behaviours } = resolve(parse(src, 'language-vocab.beh'));
  const s = surface(behaviours);
  assert.deepStrictEqual(s.errors, []);
  assert.strictEqual(adjudication(behaviours).untraceable.length, 0, 'every vocab behaviour must cite its source');

  // All three unserved behaviours are the SAME kind here — code built for
  // something DESIGN.md explicitly parked under "Explicitly deferred". habits
  // produced two that needed opposite fixes; a report that collapsed either set
  // into one recommendation would be wrong.
  assert.deepStrictEqual(
    s.unserved.map((b) => b.id).sort(),
    ['BEH-DETERMINISM-1', 'BEH-ITEMTYPE-1', 'BEH-LANG-1'],
  );

  // The whole documented UI is missing, and that is the finding, not a failure of
  // the corpus. Asserting it stops someone "fixing" the zero by inventing nouns.
  const { generate } = require('./kit');
  const bindings = JSON.parse(fs.readFileSync(path.join(__dirname, 'bindings.json'), 'utf8'));
  const { symbols } = resolve(parse(src, 'language-vocab.beh'));
  const missing = new Set();
  for (const b of behaviours) for (const m of generate(b, bindings, symbols).missing) missing.add(m);
  assert.ok(missing.has('page:Drill'), 'the drill screen does not exist on origin/dev — the refusal is the report');
  assert.ok(missing.has('page:Stats'));
});

section('the question sheet (his kit#3 ask: "a behaviour question sheet... I can work through it with Gemini")');
const { questions, questionErrors, renderSheet } = require('./kit');

// A whole pack, so the gate tests below can remove ONE part each and show that
// the part is what the gate catches. An unserved inference is the cheapest way
// to get a `decision` tier without needing a conflict.
const PACK = 'behaviour BEH-SCREEN "documented screen"\n  source defined docs/DESIGN.md#1\n  when opens page:Home\n' +
  'behaviour BEH-LOOSE "a route nothing displays"\n  source inferred code.cs:X\n  review unreviewed\n' +
  '  contract GET /api/thing takes a flag\n' +
  '  asks "keep it or drop it?"\n' +
  '  option "keep" "the design gains a screen"\n' +
  '  option "drop" "the route and one test go"\n' +
  '  recommend "keep" "because the capability is half-promised already"\n' +
  '  against "it is scope on an app with no screens"\n';

test('an unserved inference is a DECISION and a served one is a REVIEW', () => {
  // The tier split is the whole ranking claim: the two sections buy different
  // amounts of a reader's attention, so a bug that flattened them would make the
  // sheet a form — the exact thing the adjudication count already failed to be.
  const { behaviours, conflicts } = build(PACK +
    'behaviour BEH-FINE "a route a screen displays"\n  source inferred code.cs:Y\n  review unreviewed\n  serves BEH-SCREEN\n');
  const qs = questions(behaviours, conflicts);
  assert.strictEqual(qs.find((q) => q.id === 'BEH-LOOSE').tier, 'decision');
  // The positive control. Without it, a version that tiers EVERYTHING as a
  // decision passes the line above and the ranking silently stops ranking.
  assert.strictEqual(qs.find((q) => q.id === 'BEH-FINE').tier, 'review');
});

test('a human can PROMOTE a routine-looking inference by writing asks on it', () => {
  // Mechanism sets the floor, not the ceiling: the tool cannot see that a
  // parameter's NAME is wrong. BEH-HISTORY-3 is the real case.
  const { behaviours, conflicts } = build(PACK +
    'behaviour BEH-NAMED "served, but the name is wrong"\n  source inferred code.cs:Y\n  review unreviewed\n  serves BEH-SCREEN\n' +
    '  asks "is this name right?"\n  option "a" "x"\n  option "b" "y"\n');
  assert.strictEqual(questions(behaviours, conflicts).find((q) => q.id === 'BEH-NAMED').tier, 'decision');
});

test('an adjudicated inference drops off the sheet entirely', () => {
  // Answering must REMOVE the question, or the sheet never shortens and working
  // through it produces no visible progress.
  const { behaviours, conflicts } = build(PACK.replace('review unreviewed', 'review approved'));
  assert.strictEqual(questions(behaviours, conflicts).some((q) => q.id === 'BEH-LOOSE'), false);
});

test('a decision with no question is refused', () => {
  const { behaviours, conflicts } = build(
    'behaviour BEH-SCREEN "s"\n  source defined docs/DESIGN.md#1\n  when opens page:Home\n' +
    'behaviour BEH-LOOSE "nothing displays it"\n  source inferred code.cs:X\n  review unreviewed\n');
  const errs = questionErrors(questions(behaviours, conflicts));
  assert.ok(errs.some((e) => e.includes('BEH-LOOSE') && e.includes('asks')), errs.join(' | '));
});

test('a recommendation with no counter-case is refused', () => {
  // The half a reader most needs and I am least inclined to write, so the gate
  // requires it rather than trusting me (his claude-code-bot#82 shape).
  const { behaviours, conflicts } = build(PACK.replace(/^  against .*\n/m, ''));
  const errs = questionErrors(questions(behaviours, conflicts));
  assert.ok(errs.some((e) => e.includes('advocacy')), errs.join(' | '));
});

test('a recommendation pointing at no option is refused', () => {
  // The rot case: an option gets relabelled and the recommendation quietly
  // starts naming nothing while still reading as a recommendation.
  const { behaviours, conflicts } = build(PACK.replace('recommend "keep"', 'recommend "kepe"'));
  const errs = questionErrors(questions(behaviours, conflicts));
  assert.ok(errs.some((e) => e.includes('not one of its options')), errs.join(' | '));
});

test('a one-option question is refused', () => {
  const { behaviours, conflicts } = build(PACK.replace(/^  option "drop" .*\n/m, ''));
  const errs = questionErrors(questions(behaviours, conflicts));
  assert.ok(errs.some((e) => e.includes('at least 2 options')), errs.join(' | '));
});

test('a complete pack passes the gate — the control for all four refusals above', () => {
  // Without this, a questionErrors() that returned an error unconditionally
  // would pass every refusal test in this section.
  const { behaviours, conflicts } = build(PACK);
  assert.deepStrictEqual(questionErrors(questions(behaviours, conflicts)), []);
});

test('a cited behaviour moves INTO the decision and out of the review list', () => {
  // The double-ask this field was built for. The first real sheet asked which of
  // `days`/`historyDays` wins as D1, and separately asked him to tick
  // "the parameter is named historyDays" as a routine review — ticking the cheap
  // one silently answers the expensive one.
  const src = PACK +
    'behaviour BEH-EVIDENCE "the flag is spelled thisWay"\n  source inferred code.cs:Z\n  review unreviewed\n' +
    '  serves BEH-SCREEN\n  contract the flag is spelled thisWay\n';
  const before = questions(...(({ behaviours, conflicts }) => [behaviours, conflicts])(build(src)));
  assert.strictEqual(before.find((q) => q.id === 'BEH-EVIDENCE').tier, 'review', 'control: uncited, it is its own row');

  const { behaviours, conflicts } = build(src.replace('  asks "keep it or drop it?"', '  cites BEH-EVIDENCE\n  asks "keep it or drop it?"'));
  const qs = questions(behaviours, conflicts);
  assert.strictEqual(qs.some((q) => q.id === 'BEH-EVIDENCE'), false, 'cited: it must not also be its own row');
  assert.deepStrictEqual(qs.find((q) => q.id === 'BEH-LOOSE').cites.map((c) => c.id), ['BEH-EVIDENCE']);
});

test('a cites naming nothing is refused, not silently dropped', () => {
  // Worse than a broken link in prose: a typo here SUPPRESSES a behaviour from
  // the sheet, so the question disappears leaving no trace anywhere.
  const { behaviours, conflicts } = build(PACK.replace('  asks "keep', '  cites BEH-GHOST\n  asks "keep'));
  const errs = questionErrors(questions(behaviours, conflicts));
  assert.ok(errs.some((e) => e.includes('BEH-GHOST') && e.includes('silently drops')), errs.join(' | '));
});

test('cites wants a behaviour id, not prose', () => {
  // Found by a SURVIVED mutant, not by design: the id check was unexercised
  // because every test wrote a well-formed id. It is not redundant with the
  // dangling-cites gate — this fails at PARSE with a file:line, which is where a
  // typo is cheap, and the gate's message ("names nothing in this corpus") sends
  // a reader looking for a missing behaviour rather than at their own syntax.
  assert.throws(() => build(PACK.replace('  asks "keep', '  cites the naming one\n  asks "keep')), /cites wants a behaviour id/);
});

test('the habits sheet renders, and its shape is the one he was handed', () => {
  // Against the REAL corpus, not a fixture: the sheet is an artefact he opens,
  // and every fixture I write is one I already believe.
  const fs = require('fs');
  const path = require('path');
  const src = fs.readFileSync(path.join(__dirname, 'behaviours/james-habits-app.beh'), 'utf8');
  const { behaviours, conflicts } = resolve(parse(src, 'james-habits-app.beh'));
  const qs = questions(behaviours, conflicts);
  assert.deepStrictEqual(questionErrors(qs), [], 'the shipped sheet must pass its own gate');

  const decisions = qs.filter((q) => q.tier === 'decision');
  // Pinned by IDENTITY, not count — a corpus that grew a different third
  // decision would still pass a `=== 3`, and which ones is the whole value.
  assert.deepStrictEqual(
    decisions.map((q) => q.key).sort(),
    ['BEH-ARCHIVE-2', 'BEH-ERROR-1', 'region:CompletionGrid.days'],
  );
  // BEH-HISTORY-3 is D1's evidence. If it ever reappears as its own row, the
  // sheet is double-asking again and the second ask is in the cheap section.
  assert.strictEqual(qs.some((q) => q.id === 'BEH-HISTORY-3'), false);

  const md = renderSheet('james-habits-app', qs);
  // The brief is what stops a helpful assistant ratifying my own recommendation.
  assert.ok(md.includes('pressure-test'), 'the assistant brief must survive rendering');
  assert.ok(md.includes('strongest case against'), 'every decision owes a counter-case');
  // The second reader cannot open the repo, so evidence must be inline.
  assert.ok(md.includes('HabitRoutes.cs:68'), 'the citation must be in the document, not just in the repo');
});

test('the committed sheet is byte-identical to what the generator produces now', () => {
  // The failure this exists for: someone answers a question, edits the corpus,
  // and the sheet in docs/ keeps asking it — or hand-edits the sheet and the
  // corpus never learns. Either way the artefact reads as current while being
  // stale, which is the exact defect the sheet was built to remove from the app.
  // It is checkable only because the sheet carries no timestamp.
  const fs = require('fs');
  const path = require('path');
  const committed = path.join(__dirname, '../../docs/sheets/james-habits-app.md');
  const src = fs.readFileSync(path.join(__dirname, 'behaviours/james-habits-app.beh'), 'utf8');
  const { behaviours, conflicts } = resolve(parse(src, 'james-habits-app.beh'));
  const fresh = renderSheet('james-habits-app', questions(behaviours, conflicts),
    { rev: 'james-habits-app@e75de89' });
  assert.strictEqual(fs.readFileSync(committed, 'utf8'), fresh,
    'docs/sheets/james-habits-app.md is stale — re-run `node kit.js sheet james-habits --rev james-habits-app@e75de89`');
});

section('reading an app\'s tests');
const { testTitles, expectedTestCount } = require('./kit');

test('a JS spec file yields one title per test()', () => {
  const got = testTitles('a.spec.ts', "test('alpha', () => {});\nit('beta', async () => {});\n");
  assert.deepStrictEqual(got.map((g) => g.raw), ['alpha', 'beta']);
});

test('test.only / it.skip still count — a skipped test is a title, not an absence', () => {
  const got = testTitles('a.spec.ts', "test.only('alpha', () => {});\nit.skip('beta', () => {});\n");
  assert.deepStrictEqual(got.map((g) => g.raw), ['alpha', 'beta']);
});

test('a C# [Fact] yields its method name', () => {
  const got = testTitles('T.cs', '    [Fact]\n    public void Does_A_Thing()\n    {\n    }\n');
  assert.deepStrictEqual(got.map((g) => g.raw), ['Does_A_Thing']);
});

test('a DisplayName overrides the method name', () => {
  const got = testTitles('T.cs', '    [Fact(DisplayName = "a nicer name")]\n    public void Does_A_Thing()\n');
  assert.deepStrictEqual(got.map((g) => g.raw), ['a nicer name']);
  assert.strictEqual(got[0].style, 'DisplayName');
});

// THE REGRESSION. A fixed six-line lookahead between the attribute and its
// method lost every [Theory] with five or more cases — 7 tests across the pilot
// repos, language-vocab under-read by 16%, silently. Eight rows here so a
// six-line window cannot pass this by luck.
test('a [Theory] with eight InlineData rows still finds its method', () => {
  const src = '    [Theory]\n' +
    '    // a comment in the middle, because they are there in real code\n' +
    Array.from({ length: 8 }, (_, i) => `    [InlineData(${i})]\n`).join('') +
    '\n    public void Theory_Method(int n)\n    {\n    }\n';
  assert.deepStrictEqual(testTitles('T.cs', src).map((g) => g.raw), ['Theory_Method']);
});

test('an attribute with no method after it yields nothing rather than grabbing the next one', () => {
  const src = '    [Fact]\n    private readonly int _notATest = 1;\n\n    [Fact]\n    public void Real_Test()\n';
  assert.deepStrictEqual(testTitles('T.cs', src).map((g) => g.raw), ['Real_Test']);
});

test('expectedTestCount counts a second way for BOTH ecosystems', () => {
  // It used to return null for JS, reasoning that a regex "counts occurrences
  // directly and has nothing to lose". The regex lost every `it.each` test and
  // invented one per test-shaped string literal, both silently, for as long as
  // this declined to look. An unmeasured half is not a safe half.
  assert.strictEqual(expectedTestCount('T.cs', '[Fact]\n[Theory]\n[InlineData(1)]\n'), 2);
  assert.strictEqual(expectedTestCount('a.spec.ts', "test('x', () => {})"), 1);
});

test('a parameterised test is READ, not silently dropped — the real bug', () => {
  // `it.each([...])('%s', fn)` puts the title after the table, so a regex
  // expecting a quote straight after the paren never reaches it. Measured over
  // 28 real JS test files in four repos: 6 tests vanished this way, and every
  // one of the 5 disagreeing files was explained by `.each` exactly.
  const src = "it.each([[1, 'a'], [2, 'b']])('handles %s', (n, s) => {});";
  const got = testTitles('a.spec.ts', src);
  assert.deepStrictEqual(got.map((t) => t.raw), ['handles %s']);
  assert.strictEqual(got[0].style, 'each');
  assert.strictEqual(expectedTestCount('a.spec.ts', src), 1);
});

test('a .each table containing brackets INSIDE strings does not end the group early', () => {
  // The naive skip is a bracket counter. A table row like "]" or "(" is data,
  // and a counter that reads it as structure stops in the middle of the table
  // and then reads a fragment of data as the title.
  const src = `it.each([['a)]', 1], ['b((', 2]])('closes over %s', (s, n) => {});`;
  assert.deepStrictEqual(testTitles('a.spec.ts', src).map((t) => t.raw), ['closes over %s']);
});

test('a .each table containing a NESTED CALL does not end the group early', () => {
  // The sibling test above puts the brackets inside strings, so the quote-skip
  // handles them and the depth counter is never exercised — a mutation removing
  // the counter survived it. Real tables contain real calls.
  const src = "it.each([[Math.max(1, 2), 'a']])('computes %s', (n, s) => {});";
  assert.deepStrictEqual(testTitles('a.spec.ts', src).map((t) => t.raw), ['computes %s']);
});

test('a .each tagged-template table is read too', () => {
  const src = 'it.each`\n  a | b\n  ${1} | ${2}\n`("$a plus $b", () => {});';
  assert.deepStrictEqual(testTitles('a.spec.ts', src).map((t) => t.raw), ['$a plus $b']);
  assert.strictEqual(expectedTestCount('a.spec.ts', src), 1);
});

test('a test-shaped STRING is not a test — the reason kit could not read itself', () => {
  // Kit's own suite is the suite of a test generator, so it is full of literals
  // like `['test("[BEH-1] a", () => {})']`. The old regex read 108 tests out of
  // 97 real ones; 11 phantoms, every one a fixture. A gate that miscounts its
  // own tests cannot be pointed at its own repo.
  const src = "const fixture = ['test(\"[BEH-1] a\", () => {})'];\ntest('the real one', () => {});";
  assert.deepStrictEqual(testTitles('a.spec.ts', src).map((t) => t.raw), ['the real one']);
  assert.strictEqual(expectedTestCount('a.spec.ts', src), 1);
});

test('a test-shaped line inside a COMMENT is not a test either', () => {
  const src = "// test('not this one', () => {})\ntest('the real one', () => {});";
  assert.deepStrictEqual(testTitles('a.spec.ts', src).map((t) => t.raw), ['the real one']);
  assert.strictEqual(expectedTestCount('a.spec.ts', src), 1);
});

test('a member call named .test() is not a declaration — the lookbehind', () => {
  // `\\b` matches straight after a dot, so `SOME_RE.test(x)` counted as a test.
  // Kit's own file has 16 of them: the second count came back 113 against 97
  // and would have refused a file that was completely fine.
  const src = 'const ok = TEST_FILE_RE.test(name);\ntest("real", () => {});';
  assert.strictEqual(expectedTestCount('a.spec.ts', src), 1);
  assert.deepStrictEqual(testTitles('a.spec.ts', src).map((t) => t.raw), ['real']);
});

test('THE CONTROL: the two counts DISAGREE when a declaration is not at a statement start', () => {
  // The whole point of a second count is that it can contradict the first. The
  // reader keys on position; the count keys on lexical structure after strings
  // and comments are removed. A test declared after a semicolon on a shared
  // line is invisible to the first and visible to the second — so the pair
  // disagrees and check.js refuses, rather than quietly reporting one test.
  const src = 'beforeEach(() => {}); test("shared line", () => {});';
  assert.strictEqual(testTitles('a.spec.ts', src).length, 0);
  assert.strictEqual(expectedTestCount('a.spec.ts', src), 1);
});

test('nesting inside describe blocks still reads — indentation is allowed', () => {
  const src = 'describe("g", () => {\n  it("indented", () => {});\n  await test("awaited", () => {});\n});';
  assert.deepStrictEqual(testTitles('a.spec.ts', src).map((t) => t.raw), ['indented', 'awaited']);
  assert.strictEqual(expectedTestCount('a.spec.ts', src), 2);
});

section('the mapping (option C)');
const { mapping } = require('./kit');

const MB = parse('behaviour BEH-1 "one"\nbehaviour BEH-2 "two"', 'm.beh');
const TITLES = [
  { file: 'a.spec.ts', raw: 'covers one' },
  { file: 'a.spec.ts', raw: 'covers two' },
  { file: 'b.spec.ts', raw: 'covers one' },
];

test('a behaviour named by an existing test is covered', () => {
  const r = mapping(MB, { 'BEH-1': [{ file: 'a.spec.ts', title: 'covers one' }] }, TITLES);
  assert.deepStrictEqual(r.covered.map((b) => b.id), ['BEH-1']);
  assert.deepStrictEqual(r.uncovered.map((b) => b.id), ['BEH-2']);
  assert.deepStrictEqual(r.errors, []);
});

test('metadata keys beginning with _ are ignored, not treated as behaviours', () => {
  const r = mapping(MB, { _note: 'prose', 'BEH-1': [{ file: 'a.spec.ts', title: 'covers one' }] }, TITLES);
  assert.deepStrictEqual(r.errors, []);
});

// The four refusals below are the entire argument for option C — a mapping that
// cannot rot loudly is just a second place for the truth to go stale. Each is
// paired with the positive control above, which uses the same shape and passes.
test('REFUSES a mapping naming a file that is not a test file in the app', () => {
  // Asserting the DIAGNOSIS, not just that something errored: without the file
  // check this still errors, via "no test titled" — the same failure dressed as
  // a renamed test, sending a reader to fix the wrong thing. Matching on the
  // filename alone let a mutation of this rule survive.
  const r = mapping(MB, { 'BEH-1': [{ file: 'gone.spec.ts', title: 'covers one' }] }, TITLES);
  assert.strictEqual(r.errors.length, 1);
  assert.ok(/not a test file/.test(r.errors[0]), r.errors[0]);
  assert.ok(!/renamed or deleted/.test(r.errors[0]), 'a wrong path must not be reported as a renamed test');
  assert.deepStrictEqual(r.covered, [], 'a broken entry must not also count as covered');
});

test('REFUSES a mapping naming a title that file does not have — the renamed-test case', () => {
  const r = mapping(MB, { 'BEH-1': [{ file: 'a.spec.ts', title: 'covers one, renamed' }] }, TITLES);
  assert.strictEqual(r.errors.length, 1);
  assert.ok(/renamed or deleted/.test(r.errors[0]), r.errors[0]);
  assert.deepStrictEqual(r.covered, []);
});

test('REFUSES a title that is ambiguous within its file, and says why', () => {
  // Two tests with one name in one file: file+title cannot address either. This
  // is the measured limit of option C's key, not a hypothetical — and it must
  // read differently from "no such title", because the fix is different.
  const dup = [...TITLES, { file: 'a.spec.ts', raw: 'covers two' }];
  const r = mapping(MB, { 'BEH-2': [{ file: 'a.spec.ts', title: 'covers two' }] }, dup);
  assert.strictEqual(r.errors.length, 1);
  assert.ok(/appears 2×/.test(r.errors[0]), r.errors[0]);
  assert.ok(!/renamed or deleted/.test(r.errors[0]), 'ambiguity must not be reported as a missing test');
});

test('REFUSES a mapping entry for a behaviour the corpus does not have', () => {
  // Rot in the other direction: the corpus dropped a behaviour and the mapping
  // still claims it. Nothing else notices, because coverage only ever asks the
  // question the other way round.
  const r = mapping(MB, { 'BEH-9': [{ file: 'a.spec.ts', title: 'covers one' }] }, TITLES);
  assert.strictEqual(r.errors.length, 1);
  assert.ok(/no such behaviour/.test(r.errors[0]), r.errors[0]);
});

test('the same title in a DIFFERENT file is not ambiguous', () => {
  // Control for the ambiguity rule: it must key on file+title, not title. If
  // this fails, the rule is really "no duplicate titles anywhere", which would
  // refuse a mapping that is perfectly addressable.
  const r = mapping(MB, { 'BEH-1': [{ file: 'b.spec.ts', title: 'covers one' }] }, TITLES);
  assert.deepStrictEqual(r.errors, []);
  assert.deepStrictEqual(r.covered.map((b) => b.id), ['BEH-1']);
});

section('the gate: kit check exit codes');
const check = require('./check');

// Fixtures are BUILT HERE, never read from /data/repos: a suite that depends on
// a clone silently skips wherever clones do not exist — which is exactly how a
// past change took the mutation harness's control run down without saying so.
const os = require('os');
const fsx = require('fs');
const pathx = require('path');
const fixture = (files) => {
  const dir = fsx.mkdtempSync(pathx.join(os.tmpdir(), 'kit-check-'));
  for (const [f, src] of Object.entries(files)) {
    fsx.mkdirSync(pathx.join(dir, pathx.dirname(f)), { recursive: true });
    fsx.writeFileSync(pathx.join(dir, f), src);
  }
  return dir;
};
// Async-aware, and therefore re-entrancy-aware. Two things, learned in that
// order:
//
//   1. An `fn` returning a promise must stay silenced until it SETTLES. A plain
//      try/finally restores the moment the promise is handed back, which is
//      before the function has written anything.
//   2. Once (1) is true, two async `quiet` calls OVERLAP — the second starts
//      while the first is still pending. A version that saves `console.log` on
//      entry then saves the first one's STUB, and restoring it silences the
//      suite permanently. The symptom is not a failure: the run simply stops
//      printing, ends at exit 0, and the tally never appears.
//
// So the real console is captured once and the depth counter decides when to
// put it back.
let quietDepth = 0;
const quiet = (fn) => {
  const restore = () => {
    if (--quietDepth === 0) { console.log = REAL_LOG; console.error = REAL_ERR; }
  };
  quietDepth++;
  console.log = console.error = () => {};
  try {
    const result = fn();
    if (result && typeof result.then === 'function') return result.finally(restore);
    restore();
    return result;
  } catch (e) { restore(); throw e; }
};

test('exit 2 when there is no corpus to check — could-not-look is not green', () => {
  const dir = fixture({ 'a.spec.ts': "test('x', () => {});" });
  assert.strictEqual(quiet(() => check.main(['nosuchapp', '--repo', dir])), 2);
});

test('exit 2 when the repo has no test files at all', () => {
  // The failure this exists for: a gate pointed at the wrong directory reads
  // zero tests, finds no problems, and is indistinguishable in CI from a pass.
  const dir = fixture({ 'README.md': 'no tests here' });
  assert.strictEqual(quiet(() => check.main(['snip-it', '--repo', dir])), 2);
});

test('exit 2 when the repo does not exist', () => {
  assert.strictEqual(quiet(() => check.main(['snip-it', '--repo', '/no/such/path'])), 2);
});

test('exit 2 on an unknown --via, rather than silently falling back to a default', () => {
  const dir = fixture({ 'a.spec.ts': "test('x', () => {});" });
  assert.strictEqual(quiet(() => check.main(['snip-it', '--repo', dir, '--via', 'guess'])), 2);
});

test('exit 2 when the C# reader loses a test — a bad read is not a verdict', () => {
  // Three [Fact]s, one of which has no method: the walk finds 2 and the count
  // says 3. Under markers that under-read would look like MORE failures and
  // under a mapping like fewer; either way the number is wrong, so it refuses.
  //
  // ⚠️ The filename must satisfy TEST_FILE_RE or the file is never collected and
  // this exits 2 for a completely different reason — it did, as `T.cs`, and
  // passed while measuring nothing. A mutation of the reader-loss branch
  // survived, which is the only thing that said so.
  const dir = fixture({
    'MyTests.cs': '[Fact]\npublic void A()\n{\n}\n[Fact]\npublic void B()\n{\n}\n[Fact]\nprivate int notAMethod;\n',
  });
  assert.ok(require('./kit').TEST_FILE_RE.test('MyTests.cs'), 'fixture is not collected as a test file');
  assert.strictEqual(quiet(() => check.main(['snip-it', '--repo', dir])), 2);
});

test('exit 2 when the JS reader and the JS count disagree — the half that had no guard', () => {
  // The C# refusal above has existed since the vocab under-read. The JS half had
  // NO second count, so the same class of bug (every `it.each` dropped, every
  // test-shaped fixture string invented) went unreported for the life of the
  // regex. This is that guard, reached through the real CLI.
  const dir = fixture({ 'a.spec.ts': 'beforeEach(() => {}); test("shared line", () => {});\ntest("normal", () => {});\n' });
  assert.strictEqual(quiet(() => check.main(['snip-it', '--repo', dir])), 2);
});

test('the disagreement message names JS evidence for a JS file, not xUnit attributes', () => {
  // It said "[Fact]/[Theory] attributes exist" for a .spec.ts, sending whoever
  // read it looking for xUnit in a TypeScript file. The refusal was right and
  // the reason it gave was from the other ecosystem.
  const dir = fixture({ 'a.spec.ts': 'beforeEach(() => {}); test("shared line", () => {});\ntest("normal", () => {});\n' });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  try { check.main(['snip-it', '--repo', dir]); } finally { console.log = log; console.error = err; }
  assert.ok(/test declaration\(s\) survive stripping/.test(said), `said: ${said}`);
  assert.ok(!/\[Fact\]/.test(said), `said: ${said}`);
});

test('exit 1 when a behaviour has no test naming it — the gate can go RED', () => {
  const dir = fixture({ 'a.spec.ts': "test('unrelated', () => {});" });
  assert.strictEqual(quiet(() => check.main(['snip-it', '--repo', dir, '--via', 'markers'])), 1);
});

test('exit 0 when every behaviour is named — and it is reachable, not just theoretical', () => {
  // The control that stops all of the above passing on a gate that only ever
  // returns non-zero. Every id in the shipped snip-it corpus, marked.
  const ids = resolve(parse(fsx.readFileSync(pathx.join(__dirname, 'behaviours/snip-it.beh'), 'utf8'), 's.beh'))
    .behaviours.map((b) => b.id);
  const dir = fixture({
    'a.spec.ts': ids.map((id) => `test('[${id}] covers it', () => {});`).join('\n'),
  });
  assert.strictEqual(quiet(() => check.main(['snip-it', '--repo', dir, '--via', 'markers'])), 0);
});

test('exit 1 when a test names an id the corpus does not have', () => {
  // Orphan in the marker direction. Paired with the control above, which uses
  // the identical fixture shape minus the extra id.
  const ids = resolve(parse(fsx.readFileSync(pathx.join(__dirname, 'behaviours/snip-it.beh'), 'utf8'), 's.beh'))
    .behaviours.map((b) => b.id);
  const dir = fixture({
    'a.spec.ts': [...ids, 'BEH-GHOST'].map((id) => `test('[${id}] covers it', () => {});`).join('\n'),
  });
  assert.strictEqual(quiet(() => check.main(['snip-it', '--repo', dir, '--via', 'markers'])), 1);
});

test('the shipped snip-it mapping is RED today, and for the two behaviours it says', () => {
  // Ships red on purpose (behaviours/snip-it.tests.json): BEH-UP-2 and BEH-EDIT-0
  // have no test. A gate only ever observed passing has never been shown to
  // discriminate — so this pins the failure, and turns green only when snip-it
  // grows those two tests, which is the moment the mapping should be revisited.
  const src = fsx.readFileSync(pathx.join(__dirname, 'behaviours/snip-it.beh'), 'utf8');
  const { behaviours } = resolve(parse(src, 'snip-it.beh'));
  const map = JSON.parse(fsx.readFileSync(pathx.join(__dirname, 'behaviours/snip-it.tests.json'), 'utf8'));
  // Titles as they stand on snip-it's dev, asserted here rather than read from a
  // clone so this test states its own premise.
  const titles = Object.entries(map).filter(([k]) => !k.startsWith('_'))
    .flatMap(([, es]) => es.map((e) => ({ file: e.file, raw: e.title })));
  const r = mapping(behaviours, map, titles);
  assert.deepStrictEqual(r.errors, []);
  assert.deepStrictEqual(r.uncovered.map((b) => b.id), ['BEH-UP-2', 'BEH-EDIT-0']);
});

section('prose-audit: does the corpus account for the whole document?');
const pa = require('./prose-audit');

// One clean AC + the one behaviour it names. Every test below mutates a COPY of
// this, so each asserts exactly one rule and the control asserts the rest hold.
const CLEAN_BEH = resolve(parse('behaviour BEH-A "a"\n  actor v\n  when opens page:Home', 'p.beh')).behaviours;
const CLEAN_LEDGER = () => ({
  source: { path: 'x.md', rev: 'deadbeefdeadbeef' },
  acs: [{ line: 1, story: 'Story 1', text: 'a thing', disposition: 'encoded', shapes: [], behaviours: ['BEH-A'], note: '' }],
});

test('a clean ledger has no problems — the control the rest of these need', () => {
  assert.deepStrictEqual(pa.audit(CLEAN_LEDGER(), CLEAN_BEH).problems, []);
});

test('a behaviour NO acceptance criterion names is reported', () => {
  // The rule this file exists for. Nothing else in the repo stops a corpus
  // growing a flattering behaviour the source document never asked for, and
  // encoding prose is precisely where that temptation lives.
  const beh = resolve(parse('behaviour BEH-A "a"\nbehaviour BEH-INVENTED "nobody asked"', 'p.beh')).behaviours;
  const { problems } = pa.audit(CLEAN_LEDGER(), beh);
  assert.strictEqual(problems.length, 1, problems.join(' | '));
  assert.match(problems[0], /BEH-INVENTED.*no acceptance criterion names it/);
});

test('a ledger entry naming a behaviour the corpus lacks is reported', () => {
  const l = CLEAN_LEDGER();
  l.acs[0].behaviours = ['BEH-GONE'];
  const { problems } = pa.audit(l, CLEAN_BEH);
  assert.ok(problems.some((p) => /names BEH-GONE, which is not in the corpus/.test(p)), problems.join(' | '));
});

test('an unknown disposition is unaccounted, not silently tallied', () => {
  const l = CLEAN_LEDGER();
  l.acs[0].disposition = 'TODO';
  const { problems, tally } = pa.audit(l, CLEAN_BEH);
  assert.match(problems[0], /unaccounted/);
  assert.strictEqual(Object.keys(tally).length, 0, 'a TODO must not be counted as carried');
});

test('a shape outside the taxonomy is reported', () => {
  const l = CLEAN_LEDGER();
  l.acs[0].disposition = 'inexpressible';
  l.acs[0].behaviours = [];
  l.acs[0].shapes = ['too-hard'];
  const { problems } = pa.audit(l, CLEAN_BEH);
  assert.ok(problems.some((p) => /"too-hard" is not in the taxonomy/.test(p)), problems.join(' | '));
});

test('"inexpressible" with no shape is refused — otherwise it means "too hard"', () => {
  const l = CLEAN_LEDGER();
  l.acs[0].disposition = 'inexpressible';
  l.acs[0].behaviours = [];
  const { problems } = pa.audit(l, CLEAN_BEH);
  assert.ok(problems.some((p) => /must name which missing shape/.test(p)), problems.join(' | '));
});

test('"partial" must name BOTH what carried it and what did not fit', () => {
  const l = CLEAN_LEDGER();
  l.acs[0].disposition = 'partial'; // behaviours set, shapes empty
  const { problems } = pa.audit(l, CLEAN_BEH);
  assert.ok(problems.some((p) => /"partial" must name both/.test(p)), problems.join(' | '));
});

test('"encoded" with something left over is refused — that is a partial', () => {
  const l = CLEAN_LEDGER();
  l.acs[0].shapes = ['cardinality'];
  const { problems } = pa.audit(l, CLEAN_BEH);
  assert.ok(problems.some((p) => /"encoded" must name a behaviour and leave nothing unmet/.test(p)), problems.join(' | '));
});

test('"contract" and "refused" must name the behaviour they live on', () => {
  for (const d of ['contract', 'refused']) {
    const l = CLEAN_LEDGER();
    l.acs[0].disposition = d;
    l.acs[0].behaviours = [];
    const { problems } = pa.audit(l, CLEAN_BEH);
    assert.ok(problems.some((p) => p.includes(`"${d}" must name the behaviour`)), `${d}: ${problems.join(' | ')}`);
  }
});

test('the AC extractor reads checkbox lines and their story, and nothing else', () => {
  const acs = pa.extractAcs('## Story 3 — x\n\n- [ ] first\nsome prose\n- [x] already done\n- [ ] second\n');
  assert.deepStrictEqual(acs.map((a) => a.text), ['first', 'second']);
  assert.deepStrictEqual(acs.map((a) => a.line), [3, 6]);
  assert.strictEqual(acs[0].story, 'Story 3');
});

test('the shipped ledger accounts for every AC in the shipped corpus', () => {
  // The real artefact, not a fixture. A fixture-only suite passes over a ledger
  // that has drifted from the corpus it describes.
  const led = JSON.parse(fsx.readFileSync(pathx.join(__dirname, '..', '..', 'docs', 'pilots', 'macro-metrics-prose.ledger.json'), 'utf8'));
  const { behaviours } = resolve(parse(fsx.readFileSync(pathx.join(__dirname, 'behaviours/macro-metrics.beh'), 'utf8'), 'm.beh'));
  const { problems, tally } = pa.audit(led, behaviours);
  assert.deepStrictEqual(problems, []);
  assert.strictEqual(led.acs.length, 46);
  // Pins the finding, so a later edit that quietly promotes inexpressible ACs
  // into "encoded" has to change this line and say so.
  assert.strictEqual(tally.encoded, 2, 'fully-carried count moved');
  assert.strictEqual(tally.inexpressible, 24, 'inexpressible count moved');
});

test('exit 2 when the --source path yields no acceptance criteria', () => {
  const dir = fixture({ 'empty.md': '# nothing here\n\njust prose.\n' });
  assert.strictEqual(quiet(() => pa.main(['--source', pathx.join(dir, 'empty.md')])), 2);
});

test('exit 1 when the source document has drifted from the ledger', () => {
  // Reconstructs the real doc's AC lines from the ledger, then edits ONE word.
  // Without the edit this must exit 0, which the next test asserts — a drift
  // check that fires on everything detects nothing.
  const led = JSON.parse(fsx.readFileSync(pathx.join(__dirname, '..', '..', 'docs', 'pilots', 'macro-metrics-prose.ledger.json'), 'utf8'));
  const lines = [];
  for (const ac of led.acs) lines[ac.line - 1] = `- [ ] ${ac.text}`;
  for (let i = 0; i < lines.length; i++) if (lines[i] === undefined) lines[i] = '';
  const good = lines.join('\n');
  const bad = lines.map((l, i) => (i === led.acs[0].line - 1 ? `${l} AND ONE MORE THING` : l)).join('\n');
  const dir = fixture({ 'good.md': good, 'bad.md': bad });
  assert.strictEqual(quiet(() => pa.main(['--source', pathx.join(dir, 'good.md')])), 0, 'the control drifted');
  assert.strictEqual(quiet(() => pa.main(['--source', pathx.join(dir, 'bad.md')])), 1);
});

test('the COUNT of acceptance criteria is checked, not just each one that is present', () => {
  // ⚠️ This test was originally "the source gained an AC", and a mutation of the
  // count rule SURVIVED it: an added line also has no ledger entry, so the
  // per-line rule made it red and the count rule was never what fired. Both
  // directions are asserted now, and the DELETION is the one only the count can
  // catch — every remaining line still matches, so the ledger goes on accounting
  // for a requirement the document no longer has ([[red-for-the-right-reason]]).
  const led = JSON.parse(fsx.readFileSync(pathx.join(__dirname, '..', '..', 'docs', 'pilots', 'macro-metrics-prose.ledger.json'), 'utf8'));
  const lines = [];
  for (const ac of led.acs) lines[ac.line - 1] = `- [ ] ${ac.text}`;
  for (let i = 0; i < lines.length; i++) if (lines[i] === undefined) lines[i] = '';
  const dir = fixture({
    'same.md': lines.join('\n'),
    'more.md': [...lines, '- [ ] a brand new criterion added after the ledger was written'].join('\n'),
    // One AC blanked out: line numbers of the rest are untouched, so no per-line
    // rule can fire and only the count is left to notice.
    'fewer.md': lines.map((l, i) => (i === led.acs[led.acs.length - 1].line - 1 ? '' : l)).join('\n'),
  });
  assert.strictEqual(quiet(() => pa.main(['--source', pathx.join(dir, 'same.md')])), 0, 'the control drifted');
  assert.strictEqual(quiet(() => pa.main(['--source', pathx.join(dir, 'more.md')])), 1);
  assert.strictEqual(quiet(() => pa.main(['--source', pathx.join(dir, 'fewer.md')])), 1);
});

section('saturation: does binding glue grow 1:1 with the UI? (gap #8)');
const sat = require('./saturation');

// A corpus generator, so the cases below differ in ONE property — how the nouns
// are distributed across behaviours — and nothing else.
const corpus = (perBehaviour) => perBehaviour.map((nouns, i) =>
  [`behaviour BEH-${i} "b${i}"`, ...nouns.map((n) => `when activates button:${n}`)].join('\n')).join('\n');
const SATURATING = corpus([['A', 'B', 'C', 'D'], ['E', 'F'], ['A'], ['B'], ['C'], ['A', 'E']]);
const ONE_TO_ONE = corpus([['A', 'B'], ['C', 'D'], ['E', 'F'], ['G', 'H'], ['I', 'J'], ['K', 'L']]);

test('THE CONTROL: the statistic separates a saturating corpus from a 1:1 one', () => {
  // Without this the whole measurement is decorative. Every other test here can
  // pass over a statistic that returns the same number for both shapes, and the
  // number I report on the PR would then be unfalsifiable.
  const s = sat.measureCorpus('sat.beh', SATURATING);
  const o = sat.measureCorpus('one.beh', ONE_TO_ONE);
  assert.ok(s.ratio < 0.5, `saturating corpus should collapse, got ratio ${s.ratio}`);
  assert.ok(s.percentile < 0.5, `saturating corpus should beat its own shuffles, got ${s.percentile}`);
  assert.strictEqual(o.ratio, 1, `a 1:1 corpus has no decline, got ${o.ratio}`);
  assert.strictEqual(o.percentile, 0.5, 'a 1:1 corpus is order-invariant, so it must sit exactly at the null median');
});

test('a behaviour with no noun reference is EXCLUDED from the curve', () => {
  // The finding this rule produces: habits looks like it saturates after 6 of
  // 23 behaviours. It has 8 that touch the UI at all; the other 15 are API and
  // domain behaviours, and counting them drives the marginal to zero for a
  // reason that has nothing to do with bindings.
  const withInert = SATURATING + '\n' + [0, 1, 2, 3, 4, 5, 6].map((i) =>
    `behaviour BEH-INERT-${i} "i${i}"\nthen contract GET /api/x returns 200`).join('\n');
  const a = sat.measureCorpus('a.beh', SATURATING);
  const b = sat.measureCorpus('b.beh', withInert);
  assert.strictEqual(b.behaviours, a.behaviours + 7);
  assert.strictEqual(b.inert, 7);
  assert.deepStrictEqual(b.curve, a.curve, 'inert behaviours leaked into the marginal curve');
});

test('the null is the SHUFFLED same corpus, not a fresh random one', () => {
  // A null drawn from anything but these exact behaviours would not isolate the
  // coupon-collector floor, which is the only thing the percentile is for.
  const s = sat.measureCorpus('sat.beh', SATURATING);
  const reordered = corpus([['A'], ['B'], ['A', 'E'], ['C'], ['A', 'B', 'C', 'D'], ['E', 'F']]);
  const r = sat.measureCorpus('r.beh', reordered);
  assert.strictEqual(r.nouns, s.nouns, 'the reordering changed the noun set — not a reordering');
  assert.strictEqual(r.nullMedian, s.nullMedian, 'the null must not depend on authoring order');
  assert.ok(r.ratio > s.ratio, 'a back-loaded order must score worse than a front-loaded one');
});

test('ties are split, so the percentile does not depend on < versus <=', () => {
  // macro-metrics lands exactly ON its own null median, where `<=` reports 71%
  // and `<` reports 39% for identical data. Both are defensible readings of the
  // wrong question; mid-rank is the one that is not chosen after seeing it.
  const o = sat.measureCorpus('one.beh', ONE_TO_ONE);
  assert.ok(o.tieShare > 0.9, 'expected an order-invariant corpus to be almost all ties');
  assert.strictEqual(o.percentile, 0.5);
});

test('the second count reads the raw text, and a step the parser drops is caught', () => {
  // The failure mode: a reader that silently loses steps reports a SMALLER noun
  // set, which reads as saturation. So the cross-check has to be able to fail —
  // asserted here by making the two disagree on purpose.
  const src = corpus([['A', 'B'], ['C'], ['D'], ['E']]);
  const ast = new Set(sat.measureCorpus('x.beh', src).nounSet);
  const text = sat.nounsFromText(src);
  assert.deepStrictEqual([...text].sort(), [...ast].sort(), 'the control disagrees before any mutation');
  const dropped = sat.nounsFromText(src.replace('when activates button:C', '# when activates button:C'));
  assert.ok(!dropped.has('button:C'), 'the text reader is not reading the steps it claims to');
});

test('a literal containing a noun-shaped token is not counted as a noun', () => {
  // ⚠️ The first version of this test used the literal "Ratio: 1.4" and a
  // mutation removing the quote-stripping SURVIVED it: `Ratio` is capitalised
  // and a space follows the colon, so the noun regex never matched inside the
  // quotes and the rule was never what made it pass. The literal has to contain
  // a token of the exact shape `kind:Name` for the stripping to be load-bearing.
  const src = 'behaviour BEH-1 "x"\nthen shows region:Main "unbound noun button:Save"';
  assert.deepStrictEqual([...sat.nounsFromText(src)], ['region:Main']);
  // And the consequence, not just the reader: without stripping, the raw text
  // finds button:Save, the AST correctly does not, and the tool refuses to
  // measure a corpus that is entirely fine.
  const dir = fixture({ 'lit.beh': SATURATING + '\n' + src });
  assert.strictEqual(quiet(() => sat.main(['--dir', dir])), 0);
});

test('exit 2 when ONE corpus of several parses zero nouns', () => {
  // ⚠️ This was originally a single empty corpus, and the mutation SURVIVED: a
  // corpus with no nouns also has no noun-bearing behaviours, so the "too small
  // to halve" rule fired and the zero-noun rule was never what made it red. It
  // takes a healthy corpus ALONGSIDE the empty one to leave the zero-noun rule
  // as the only thing that can refuse ([[red-for-the-right-reason]]).
  const empty = 'behaviour BEH-1 "x"\nthen contract GET /api/x';
  assert.strictEqual(quiet(() => sat.main(['--dir', fixture({ 'big.beh': SATURATING })])), 0, 'the control drifted');
  assert.strictEqual(quiet(() => sat.main(['--dir', fixture({ 'big.beh': SATURATING, 'empty.beh': empty })])), 2);
});

test('exit 2 when no corpus has enough UI behaviours to halve', () => {
  const dir = fixture({ 'tiny.beh': corpus([['A'], ['B'], ['C']]) });
  assert.strictEqual(quiet(() => sat.main(['--dir', dir])), 2);
  const ok = fixture({ 'big.beh': SATURATING });
  assert.strictEqual(quiet(() => sat.main(['--dir', ok])), 0, 'the control drifted');
});

test('exit 2 when the two counts disagree — the refusal path fires, it is not decorative', () => {
  // The two readers agree on every corpus kit.parse will even accept, so this
  // branch is unreachable from a fixture. That is exactly why it needs
  // asserting: an untriggered refusal is a claim. The lossy reader stands in
  // for the real failure — a parser that silently drops steps, which reports a
  // SMALLER noun set and reads as saturation.
  const dir = fixture({ 'skew.beh': SATURATING });
  const lossy = (text) => {
    const n = sat.nounsFromText(text);
    n.delete('button:A');
    return n;
  };
  assert.strictEqual(quiet(() => sat.main(['--dir', dir])), 0, 'the control drifted — it refuses before any reader is swapped');
  assert.strictEqual(quiet(() => sat.main(['--dir', dir], lossy)), 2);
});

test('exit 2 when pointed at a directory that does not exist', () => {
  assert.strictEqual(quiet(() => sat.main(['--dir', '/no/such/behaviours'])), 2);
});

test('--check goes RED when the write-up drifts from the corpora', () => {
  // The write-up quotes numbers. Nothing but this stops them ageing into
  // fiction the way what-we-can-leverage.md quoted 19 tests against a suite of
  // 72 — kit's own repo drifting in the way kit exists to catch.
  assert.strictEqual(quiet(() => sat.main(['--check'])), 0, 'the recorded findings already disagree with the corpora');
  const real = pathx.join(__dirname, 'behaviours');
  const dir = fixture({});
  for (const f of fsx.readdirSync(real).filter((f) => f.endsWith('.beh'))) {
    fsx.copyFileSync(pathx.join(real, f), pathx.join(dir, f));
  }
  fsx.appendFileSync(pathx.join(dir, 'snip-it.beh'), '\nbehaviour BEH-DRIFT-1 "a behaviour nobody recorded"\nwhen activates button:BrandNew\n');
  assert.strictEqual(quiet(() => sat.main(['--dir', dir, '--check'])), 1);
});

section('self-host: can Kit describe Kit? (James, claude-code-bot#89)');
const selfhost = require('./self-host.js');

test('THE CONTROL: binding nouns DOES move the number, when the verbs are known', () => {
  // Everything this tool concludes rests on "binding every noun changed
  // nothing". That sentence is worthless unless binding can change something —
  // otherwise the measurement is indistinguishable from a broken tally. A
  // browser-verb corpus is the positive control ([[red-for-the-right-reason]]).
  const m = selfhost.measure('behaviour BEH-1 "x"\n  when opens page:Home\n  when activates button:Go\n');
  assert.strictEqual(m.unbound.generated, 0, 'the control should generate nothing unbound');
  assert.ok(m.bound.generated > 0, 'binding every noun must be able to help, or the null result means nothing');
  assert.ok(m.derived > 0, 'opens/activates are DERIVED steps, not copied setup strings');
});

test('the null result: for the real kit corpus, binding every noun derives nothing', () => {
  const m = selfhost.measure(fsx.readFileSync(pathx.join(__dirname, 'behaviours', 'kit.beh'), 'utf8'));
  assert.strictEqual(m.unbound.generated, 0);
  assert.strictEqual(m.derived, 0, 'no step is derived from a behaviour, however generously bound');
  assert.ok(m.bound.generated > 0, 'and it is NOT zero-generated — the state steps do emit, which is the honest number');
});

test('a `state` step is not counted as derived — the number that flatters', () => {
  // Under full bindings a `state` step emits the setup string a human wrote in
  // bindings.json. Counting those as "generated" makes "10 of 42" sound like
  // the notation nearly works. `derived` is the honest column.
  const m = selfhost.measure('behaviour BEH-1 "x"\n  given thing:Ready\n');
  assert.strictEqual(m.bound.generated, 1);
  assert.strictEqual(m.derived, 0);
});

test('the generator vocabulary is READ from the generator, not re-typed', () => {
  // A hard-coded copy is how the write-up starts lying about the code: add a
  // verb to generate() and a list here would go on reporting the old eight.
  const verbs = selfhost.generatorVerbs();
  assert.ok(verbs.has('opens') && verbs.has('activates') && verbs.has('state'), [...verbs].join(','));
  assert.ok(!verbs.has('runs'), 'runs is not a generator verb and must not appear');
  const src = fsx.readFileSync(pathx.join(__dirname, 'kit.js'), 'utf8');
  const cases = (src.match(/^\s*case '[a-z]+':/gm) || []).length;
  assert.ok(verbs.size > 0 && verbs.size <= cases, `read ${verbs.size} verbs from ${cases} case labels`);
});

test('exit 2 when the corpus does not exist — could-not-look is not green', () => {
  assert.strictEqual(quiet(() => selfhost.main(['--corpus', '/no/such/kit.beh'])), 2);
});

test('exit 2 when the corpus parses to nothing, rather than reporting a dramatic zero', () => {
  // A run that read an empty file would print "0 derived" — the same headline
  // as the real finding, from a completely different cause.
  const dir = fixture({ 'empty.beh': '# only a comment\n' });
  assert.strictEqual(quiet(() => selfhost.main(['--corpus', pathx.join(dir, 'empty.beh')])), 2);
});

test('--check goes RED when the corpus drifts from the recorded findings', () => {
  assert.strictEqual(quiet(() => selfhost.main(['--check'])), 0, 'the recorded findings already disagree with the corpus');
  const dir = fixture({});
  const beh = pathx.join(dir, 'kit.beh');
  fsx.copyFileSync(pathx.join(__dirname, 'behaviours', 'kit.beh'), beh);
  fsx.appendFileSync(beh, '\nbehaviour BEH-DRIFT-1 "nobody recorded this"\n  when runs command:New\n');
  assert.strictEqual(quiet(() => selfhost.main(['--corpus', beh, '--check'])), 1);
});

test('saturation EXCLUDES a corpus that declares it has no UI, and says so', () => {
  // kit.beh's nouns are command:KitCheck and status:One. Leaving it in a study
  // about UI binding glue is a category error; excluding it silently is worse.
  const dir = fixture({
    'ui.beh': SATURATING,
    'cli.beh': '# kit:no-ui\nbehaviour BEH-C1 "x"\n  when runs command:Thing\n',
  });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  let code;
  try { code = sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.strictEqual(code, 0, said);
  assert.ok(/skipping cli\.beh/.test(said), said);
  assert.ok(!/cli\.beh:/.test(said.replace(/skipping cli\.beh[^\n]*/g, '')), 'the excluded corpus must not appear in the results');
});

const { boundNouns } = require('./kit');

test('the bound-noun count is scoped to THIS corpus, not to the whole bindings file', () => {
  // The bug this replaces: the report counted Object.keys(bindings), a global
  // file, so every app printed the same number — and a corpus that binds NOTHING
  // printed the same headline as one that binds everything (claude-code-bot#92).
  const behaviours = parse('behaviour BEH-1 "x"\n  when opens page:Upload\n  then sees link:Download\n');
  const bindings = {
    'page:Upload': { route: './editor' },
    'link:Download': { role: 'link', name: 'Download' },
    'button:Unrelated': { role: 'button', name: 'Not in this corpus' },
    'page:Elsewhere': { route: './elsewhere' },
  };
  const { referenced, bound } = boundNouns(behaviours, bindings);
  assert.strictEqual(referenced.size, 2, 'only the nouns this corpus names');
  assert.strictEqual(bound, 2);
  assert.ok(bound < Object.keys(bindings).length, 'the global file is bigger — that difference is the bug');
});

test('a corpus that binds NOTHING reports zero, not the size of bindings.json', () => {
  // The forward case: an app that does not exist yet has no locators to copy,
  // so every noun is unbound. This must not read as "27 nouns bound".
  const behaviours = parse('behaviour BEH-1 "x"\n  when opens page:DoesNotExist\n');
  const { referenced, bound } = boundNouns(behaviours, { 'page:Upload': { route: './editor' } });
  assert.strictEqual(referenced.size, 1);
  assert.strictEqual(bound, 0);
});

test('a noun named by two behaviours is counted once, not twice', () => {
  const behaviours = parse(
    'behaviour BEH-1 "x"\n  when opens page:Upload\n\nbehaviour BEH-2 "y"\n  when opens page:Upload\n');
  const { referenced, bound } = boundNouns(behaviours, { 'page:Upload': { route: './editor' } });
  assert.strictEqual(referenced.size, 1, 'a set of nouns, not a tally of references');
  assert.strictEqual(bound, 1);
});

test('saturation EXCLUDES a corpus that declares the app does not exist, and says so', () => {
  // A different axis from kit:no-ui — this corpus is FULL of UI nouns, so the
  // no-ui directive would not catch it. Cross-app noun reuse measured over an
  // app I invented measures my own naming habits (claude-code-bot#92).
  const dir = fixture({
    'ui.beh': SATURATING,
    'trial.beh': '# kit:not-a-real-app\n' + SATURATING,
  });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  let code;
  try { code = sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.strictEqual(code, 0, said);
  assert.ok(/skipping trial\.beh/.test(said), said);
  assert.ok(/not-a-real-app/.test(said), 'the reason must name the directive, not just the file');
  assert.ok(!/trial\.beh:/.test(said.replace(/skipping trial\.beh[^\n]*/g, '')), 'the excluded corpus must not appear in the results');
});

test('CONTROL: the same corpus WITHOUT the not-a-real-app directive is not excluded', () => {
  // Otherwise the exclusion could be the tool dropping any corpus it dislikes,
  // and the study's population would shrink for reasons nobody declared.
  const dir = fixture({ 'ui.beh': SATURATING, 'trial.beh': SATURATING });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  try { sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.ok(!/skipping trial\.beh/.test(said), said);
});

test('the two exclusion directives are independent, not one rule spelled twice', () => {
  // A corpus can be both (a CLI trial), and the reason reported must be the
  // existence one — a study over real apps excludes it even if it had a UI.
  const dir = fixture({
    'ui.beh': SATURATING,
    'both.beh': '# kit:no-ui\n# kit:not-a-real-app\nbehaviour BEH-B1 "x"\n  when runs command:Thing\n',
  });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  try { sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.ok(/not-a-real-app/.test(said), said);
  assert.ok(!/skipping both\.beh: declares "# kit:no-ui"/.test(said), 'must report the existence reason, not the UI one');
});

test('saturation EXCLUDES a second corpus for an app already in the study, and names the app', () => {
  // The THIRD axis. A forward trial written from a brief for an app that DOES
  // exist (cc-bot#92) is neither no-ui nor not-a-real-app — both directives
  // would be FALSE of it — and it still must not join a study that counts apps.
  const dir = fixture({
    'james-habits-app.beh': SATURATING,
    'trial-habits-a.beh': '# kit:duplicate-corpus james-habits-app\n' + SATURATING,
  });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  let code;
  try { code = sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.strictEqual(code, 0, said);
  assert.ok(/skipping trial-habits-a\.beh/.test(said), said);
  assert.ok(/duplicate-corpus james-habits-app/.test(said), 'the reason must name WHICH app is doubled, or the reader cannot check it');
  // And the app it duplicates must still be IN the study — excluding both would
  // silently drop a real app, which is the failure this whole axis guards.
  assert.match(said, /^\s*james-habits-app\s+\d+/m, 'the duplicated app itself must still be measured');
});

test('saturation EXCLUDES a corpus whose app and bindings share its author', () => {
  // The FOURTH axis, and all three of the others are FALSE of the corpus that
  // forced it: `kit-ui.beh` describes a UI, for software that exists and was
  // running while it was written, and no other corpus in the study describes
  // that app. The disqualifying property is that one author wrote the app, the
  // corpus and the bindings in one sitting — so its noun order measures the
  // author. Found by the number, not the argument: it scored 10% against the
  // shuffled null where every real corpus scores 59–76%.
  const dir = fixture({
    'ui.beh': SATURATING,
    'kit-ui.beh': '# kit:self-authored\n' + SATURATING,
  });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  let code;
  try { code = sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.strictEqual(code, 0, said);
  assert.ok(/skipping kit-ui\.beh/.test(said), said);
  assert.ok(/self-authored/.test(said), 'the reason must be announced — a population you cannot see is one you cannot check');
  assert.ok(!/^\s*kit-ui\s+\d+/m.test(said), 'the excluded corpus must not appear in the results');
});

test('CONTROL: the same corpus WITHOUT the self-authored directive IS measured', () => {
  // Without this, a directive that matched nothing would look identical to one
  // that excluded correctly — the shelf-life failure from kit#27, where a check
  // was green because its matcher hit no text at all.
  const dir = fixture({ 'ui.beh': SATURATING, 'kit-ui.beh': SATURATING });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  try { sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.ok(!/skipping kit-ui\.beh/.test(said), 'nothing declared it, so nothing may exclude it');
  assert.match(said, /^\s*kit-ui\s+\d+/m, said);
});

test('the real kit-ui.beh really does declare it, or the exclusion above is theatre', () => {
  // The population, asserted against the artefact rather than against a fixture.
  // The two tests above prove the MECHANISM; this proves it is switched on for
  // the corpus that needed it ([[test-the-reader-against-the-artefact]]).
  const real = fsx.readFileSync(pathx.join(__dirname, 'behaviours', 'kit-ui.beh'), 'utf8');
  assert.match(real, /^#\s*kit:self-authored\b/m);
  assert.ok(!/^#\s*kit:no-ui\b/m.test(real), 'kit-ui.beh DOES describe a UI — that directive would be a lie');
  assert.ok(!/^#\s*kit:not-a-real-app\b/m.test(real), 'the Kit UI exists and was running when this was written');
});

test('CONTROL: the same corpus WITHOUT the duplicate directive is not excluded', () => {
  const dir = fixture({ 'james-habits-app.beh': SATURATING, 'trial-habits-a.beh': SATURATING });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  try { sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.ok(!/skipping trial-habits-a\.beh/.test(said), said);
});

test('a duplicate corpus is never SILENTLY excluded — the population stays visible', () => {
  // Same rule the other two axes carry. An exclusion nobody announces is a
  // population nobody can check, and gap #8's numbers are quoted externally.
  const dir = fixture({
    'james-habits-app.beh': SATURATING,
    'trial-habits-a.beh': '# kit:duplicate-corpus james-habits-app\n' + SATURATING,
  });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  try { sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.ok(/skipping/.test(said), 'excluded without a word about it');
});

test('the duplicate marker names the app in the project payload, and is null when absent', () => {
  // `null`, not undefined: "not a duplicate" and "this reader has stopped
  // reporting duplicates" must not be the same reading. `notReal` already
  // carries a mutant for exactly that confusion.
  const dir = fixture({
    'plain.beh': 'behaviour BEH-P "x"\n  when opens page:Home\n',
    'dup.beh': '# kit:duplicate-corpus plain\nbehaviour BEH-D "x"\n  when opens page:Home\n',
  });
  // Required locally: `proj` is declared further down this file, so the module
  // const is still in its temporal dead zone when this test body runs.
  const P = require('./project');
  assert.strictEqual(P.project('dup', { behDir: dir }).duplicateOf, 'plain');
  assert.strictEqual(P.project('plain', { behDir: dir }).duplicateOf, null);
});

test('CONTROL: the same CLI corpus WITHOUT the directive is not excluded', () => {
  // Otherwise "it was skipped" could be the tool ignoring anything it dislikes.
  const dir = fixture({
    'ui.beh': SATURATING,
    'cli.beh': 'behaviour BEH-C1 "x"\n  when runs command:Thing\n',
  });
  let said = '';
  const log = console.log, err = console.error;
  console.log = console.error = (...a) => { said += a.join(' ') + '\n'; };
  try { sat.main(['--dir', dir]); } finally { console.log = log; console.error = err; }
  assert.ok(!/skipping cli\.beh/.test(said), said);
});

section('selfhost/run: Kit\'s output, EXECUTED — the half of it a browser is not needed for');
const selfrun = require('./selfhost/run.js');

// The doc's `kit-ui` table quotes four numbers and a browser is needed for only
// two of them. The other two — how many tests Kit generates and how many steps
// it DERIVES — are a pure function of the corpus, the bindings and the
// generator, so they belong in the gated suite rather than in a manual run.
// `self-host.js --check` already gates the kit.beh table this way; the kit-ui
// table shipped with nothing gating it at all.

test('the recorded kit-ui numbers still come out of the real corpus', () => {
  const want = JSON.parse(fsx.readFileSync(selfrun.EXPECTED, 'utf8'));
  const { stats } = selfrun.emitSpec();
  assert.strictEqual(stats.tests, want.tests, 'a test per behaviour');
  assert.strictEqual(stats.derived, want.derived, 'derived steps');
  assert.strictEqual(stats.ungenerated, want.ungenerated, 'refused steps');
});

test('and all 20 of them are DERIVED, not `state` strings copied out of bindings.json', () => {
  // The distinction that made kit.beh's headline honest: `state` generates a
  // setup string a human wrote, so counting it as derived is how "0 derived"
  // would have become "14 generated". kit-ui's number needs the same audit, or
  // the two rows of the doc's table are not measuring the same thing.
  const { stats } = selfrun.emitSpec();
  assert.strictEqual(stats.state, 0, 'kit-ui.beh has no state steps, so generated === derived here');
  assert.strictEqual(stats.derived, stats.generated);
});

test('THE FINDING, pinned: the refusal is a comment, and the next line acts as if it happened', () => {
  // BEH-ADJ-2 fills a correction before clicking Deny. The generator correctly
  // refuses to derive the fill — and emits the refusal as a COMMENT, so the
  // test runs straight on into a click on a button that is disabled *because
  // the fill never happened*. It fails like an application bug.
  //
  // Whether the emitter should test.fixme(), throw at the refused line, or keep
  // today's behaviour changes what Kit emits for EVERY corpus, so it is not
  // decided here. This test is the half that is true under all three options:
  // whatever he picks, the suite has to be able to SEE a refused step sitting
  // above an action that depends on it. Today it can, and it goes red the
  // moment that changes — which is the point of writing it before the decision
  // rather than after.
  const { source } = selfrun.emitSpec();
  const refused = selfrun.refusals(source);
  assert.strictEqual(refused.length, 1, 'exactly one refusal in this corpus');
  assert.match(refused[0].step, /fills field:KitCorrection/, 'and it is the fill, not something else');
  assert.match(refused[0].next, /^await page\.getByRole\("button", \{ name: "Deny" \}\)\.click\(\)/,
    'the line after the refusal is the click that depends on it — this adjacency IS the defect');
});

test('the refusal is inert at runtime, which is exactly why it is lost', () => {
  // The positive half of the same fact: nothing in the emitted artefact makes a
  // runner stop. A reader sees the refusal; a runner cannot.
  const { source } = selfrun.emitSpec();
  const line = source.split('\n').find((l) => l.includes('UNGENERATED'));
  assert.match(line.trim(), /^\/\//, 'a comment, so no runner will ever surface it');
  assert.ok(!/test\.fixme|test\.skip|throw new/.test(source), 'nothing in the spec halts on the refused step');
});

test('the harness REFUSES rather than skipping when it has no Playwright', async () => {
  // Exit 2 is "could not look", and it is not the same as 0. A measurement tool
  // that silently succeeded on a machine with no browser would report a clean
  // bill of health for a run that never happened
  // ([[clean-bill-is-never-rechecked]]).
  assert.strictEqual(selfrun.resolvePlaywright([], {}), null);
  assert.strictEqual(selfrun.resolvePlaywright(['--playwright', '/no/such/bin'], {}), null,
    'a path that does not exist is not a Playwright');
  const said = [];
  const err = console.error;
  console.error = (...a) => said.push(a.join(' '));
  let code;
  try { code = await selfrun.main([]); } finally { console.error = err; }
  assert.strictEqual(code, 2, 'could not look, not fine');
  assert.ok(said.join('\n').includes('PLAYWRIGHT_BIN'), 'and it says how to fix it');
});

test('an unreadable tally is could-not-look too, not zero failures', () => {
  // The reporter's output is parsed with a regex, and a regex that matches
  // nothing would otherwise read as "0 failed" — a broken run presenting as a
  // green one. This is the parse, tested against the real reporter's wording.
  assert.deepStrictEqual(selfrun.parseResults('  5 passed, 1 failed\n'), { passed: 5, failed: 1, failing: [] });
  assert.deepStrictEqual(selfrun.parseResults('Error: No tests found'), { passed: 0, failed: 0, failing: [] });
  const real = '  1 failed\n    specs/kit-ui.spec.ts:35:5 › [BEH-ADJ-2] Denying an inference asks for the correction it must carry \n\n  5 passed (17.8s)\n';
  const got = selfrun.parseResults(real);
  assert.strictEqual(got.passed, 5);
  assert.strictEqual(got.failed, 1);
  assert.deepStrictEqual(got.failing, ['[BEH-ADJ-2] Denying an inference asks for the correction it must carry']);
});

test('derived SUBTRACTS state steps — over a corpus that actually has one', () => {
  // kit-ui.beh has no `state` steps, so the subtraction that makes this number
  // honest is dead code as far as the real corpus is concerned, and a mutant
  // deleting it would survive. Drive it over a fixture that has one instead:
  // this is the exact distinction that stopped kit.beh's "0 derived" from being
  // published as "14 generated".
  const dir = fixture({
    's.beh': 'behaviour BEH-S "one"\n  given state cart:Full\n  when opens page:Home\n',
    'b.json': JSON.stringify({ 'cart:Full': { state: 'seed()' }, 'page:Home': { route: '/' } }),
  });
  const { stats } = selfrun.emitSpec(pathx.join(dir, 's.beh'), pathx.join(dir, 'b.json'));
  assert.strictEqual(stats.state, 1, 'the fixture must actually contain a state step');
  assert.strictEqual(stats.generated, 2, 'both steps generate');
  assert.strictEqual(stats.derived, 1, 'but only the `opens` is DERIVED — the state step is a copied string');
});

test('an unreadable run and a drifted run are separate answers, and neither is "fine"', () => {
  // Both of these guards live downstream of a spawned browser in main(), which
  // is why they are functions: inline, the only way to reach them is a full
  // Playwright run, and a guard that can only be reached by the slow path is
  // one nobody ever proves.
  assert.strictEqual(selfrun.unreadable({ passed: 0, failed: 0 }), true, 'no tally at all is could-not-look');
  assert.strictEqual(selfrun.unreadable({ passed: 0, failed: 6 }), false, 'six failures is a READ run that failed');
  assert.strictEqual(selfrun.unreadable({ passed: 5, failed: 1 }), false);
  assert.strictEqual(selfrun.drifted({ passed: 5 }, { passed: 5 }), false);
  assert.strictEqual(selfrun.drifted({ passed: 5 }, { passed: 6 }), true, 'one more pass is drift, not an improvement to wave through');
  assert.strictEqual(selfrun.drifted({ failing: ['a'] }, { failing: ['b'] }), true,
    'the same tally with a DIFFERENT test failing is drift — that is the case a count alone misses');
});

test('the write-up quotes the numbers the harness records — with the markdown stripped', () => {
  // kit#27's shelf-life check was green because a BACKTICK sat where its
  // matcher expected a space. Strip the emphasis before matching, never match
  // through it ([[test-the-reader-against-the-artefact]]).
  const doc = fsx.readFileSync(pathx.join(__dirname, '..', '..', 'docs', 'pilots', 'kit-self-hosting.md'), 'utf8');
  // ⚠️ The character class is built from a STRING, not written as a regex
  // literal, and that is not a style choice — see the jsDeclarationCount test
  // below. A backtick inside a regex literal blinds this repo's own test
  // reader to everything after it. Writing it the obvious way is what found
  // that.
  const plain = doc.replace(new RegExp('[*`_]', 'g'), '');
  const want = JSON.parse(fsx.readFileSync(selfrun.EXPECTED, 'utf8'));
  assert.ok(plain.includes(`| ${want.derived} |`) || plain.includes(`| ${want.derived}  `),
    `the doc's table must quote ${want.derived} derived steps`);
  assert.ok(plain.includes(`${want.passed} of ${want.tests}`),
    `the doc must quote "${want.passed} of ${want.tests}" — the harness records ${want.passed}/${want.tests}`);
});

section('project: the read model a UI consumes (docs/design/ui.md)');
const proj = require('./project.js');

test('the projection carries every panel the UI needs, for a real corpus', () => {
  const p = proj.project('snip-it');
  assert.strictEqual(p.behaviours.length, 8);
  for (const k of ['app', 'corpus', 'behaviours', 'conflicts', 'generated', 'coverage', 'adjudication', 'surface', 'questions', 'requires']) {
    assert.ok(k in p, `missing ${k}`);
  }
  assert.strictEqual(p.generated.length, 8, 'one generated test per behaviour — the output pane');
});

test('the projection says what each noun OWES, not just that it is missing', () => {
  // `requires.js` shipped in kit#22 and nothing in the UI read it, so the
  // behaviour page could name the nouns that caused a refusal and not say what
  // any of them needed. A noun in `missing` with an empty `needs` would be the
  // same dead end one field further in.
  const p = proj.project('james-habits-app');
  assert.ok(p.requires.missing.length >= 5, `only ${p.requires.missing.length} unbound nouns to describe`);
  for (const n of p.requires.missing) {
    assert.strictEqual(n.bound, false, `${n.noun} is in missing and claims to be bound`);
    assert.ok(n.needs.length >= 1, `${n.noun} owes nothing, so the form would have nothing to ask for`);
    for (const need of n.needs) {
      assert.strictEqual(typeof need.surface, 'string');
      assert.ok(need.surface.length > 0, `${n.noun}'s ${need.id} has no sentence a human could act on`);
      assert.ok(need.verbs.length >= 1, `${n.noun}'s ${need.id} names no verb that wanted it`);
      assert.strictEqual(need.met, false, `${n.noun} is unbound and yet ${need.id} is met`);
    }
  }
});

test('the projection keeps missing and insufficient apart', () => {
  // The distinction requires.js exists to make, carried through to the payload
  // rather than collapsed into "unbound". `bound` is what boundNouns() counts —
  // a binding that exists and satisfies no verb is invisible to it, and is the
  // case the behaviour page has no other way to explain.
  const p = proj.project('james-habits-app');
  const missing = new Set(p.requires.missing.map((n) => n.noun));
  const insufficient = new Set(p.requires.insufficient.map((n) => n.noun));
  for (const n of insufficient) assert.strictEqual(missing.has(n), false, `${n} is in both populations`);
  for (const n of p.requires.insufficient) {
    assert.strictEqual(n.bound, true, `${n.noun} is insufficient without being bound`);
    assert.strictEqual(n.satisfied, false);
    assert.notStrictEqual(n.binding, null, 'an insufficient noun must show the binding that fell short');
  }
  // Every noun lands in exactly one of the three.
  const total = p.requires.missing.length + p.requires.insufficient.length + p.requires.satisfied.length;
  assert.strictEqual(total, p.requires.nouns.length);
});

test('the projection says who ELSE feels a binding, because the namespace is global', () => {
  // The write-side hazard, surfaced on the read side so the form can show it
  // BEFORE the click rather than the CLI reporting it after.
  const p = proj.project('james-habits-app');
  const addHabit = p.requires.nouns.find((n) => n.noun === 'button:AddHabit');
  assert.ok(addHabit, 'button:AddHabit left the corpus — re-measure before trusting this test');
  assert.ok(addHabit.sharedWith.includes('trial-habits-a'), 'a noun two corpora reference reported no sharing');
  assert.strictEqual(addHabit.sharedWith.includes('james-habits-app'), false, 'a corpus was told it shares with itself');
  // And the empty case is an array, not absent.
  for (const n of p.requires.nouns) assert.ok(Array.isArray(n.sharedWith), `${n.noun}.sharedWith is not an array`);
});

test('🔴 a binding that EXISTS and satisfies no verb is reported as insufficient', () => {
  // Every test above ran against the shipped corpora, and a mutant that
  // replaced `insufficient` with `[]` SURVIVED all of them — because not one
  // real corpus currently has an insufficient noun. All nine were
  // reverse-engineered from apps that already shipped, so every binding was
  // written with the verb in front of the author, which is requires.js's own
  // explanation for why nothing had ever caught this class.
  //
  // So the case is CONSTRUCTED, and it is the exact one requires.js exists
  // for: `opens` needs `route` and `lands` needs `urlPattern`, and a page
  // bound with only `route` is counted BOUND by boundNouns(), reports no
  // missing noun, and still refuses the `lands` step.
  const dir = fixture({
    'eta.beh': 'behaviour BEH-E "eta"\n  actor engineer\n  when opens page:Shelf\n  then lands page:Shelf\n',
  });
  const bindings = pathx.join(dir, 'bindings.json');
  fsx.writeFileSync(bindings, `${JSON.stringify({ 'page:Shelf': { route: './shelf' } }, null, 2)}\n`);

  const p = proj.project('eta', { behDir: dir, bindingsFile: bindings });
  assert.deepStrictEqual(p.requires.missing.map((n) => n.noun), [], 'it is bound, so it is not missing');
  assert.deepStrictEqual(p.requires.insufficient.map((n) => n.noun), ['page:Shelf']);

  const [shelf] = p.requires.insufficient;
  assert.strictEqual(shelf.bound, true);
  assert.strictEqual(shelf.satisfied, false);
  assert.deepStrictEqual(shelf.binding, { route: './shelf' }, 'the screen must show the binding that fell short');
  assert.deepStrictEqual(shelf.needs.filter((n) => n.met).map((n) => n.id), ['route']);
  assert.deepStrictEqual(shelf.needs.filter((n) => !n.met).map((n) => n.id), ['urlPattern']);

  // And the generator agrees, which is what makes this a real state and not a
  // report about one: the `opens` step generates and the `lands` step does not.
  const g = p.generated[0];
  assert.match(g.code, /page\.goto\("\.\/shelf"\)/);
  assert.match(g.code, /UNGENERATED: then lands page:Shelf/);
  assert.deepStrictEqual(g.missing, [], 'boundNouns-style "missing" cannot see this, which is the point');
});

test('CONTROL: a fully-bound corpus reports nothing missing, so the tests above discriminate', () => {
  // kit-ui is the one corpus every noun of which is bound — it is what made the
  // 5-of-6 execution possible. If this ever reports missing nouns, the checks
  // above are passing for the wrong reason.
  const p = proj.project('kit-ui');
  assert.deepStrictEqual(p.requires.missing.map((n) => n.noun), []);
  assert.deepStrictEqual(p.requires.insufficient.map((n) => n.noun), []);
  assert.ok(p.requires.satisfied.length >= 10, `only ${p.requires.satisfied.length} satisfied nouns`);
});

test('a trial corpus is projected as notReal, and a real one is not', () => {
  // The UI is a viewer, so it must SHOW a trial corpus — hiding one would make
  // the list lie about what corpora Kit reads. But 0% against a real app and 0%
  // against an app that does not exist mean opposite things, so the projection
  // has to carry which (claude-code-bot#92).
  assert.strictEqual(proj.project('trial-lend').notReal, true);
  assert.strictEqual(proj.project('snip-it').notReal, false, 'CONTROL: a real corpus is not marked');
});

test('notReal is false, never undefined, so a UI cannot read "absent" as "real"', () => {
  // An optional boolean that is sometimes missing makes `!p.notReal` true for
  // two different reasons ([[empty-means-two-things]]).
  for (const app of ['snip-it', 'kit', 'trial-lend']) {
    assert.strictEqual(typeof proj.project(app).notReal, 'boolean', app);
  }
});

test('coverage UNAVAILABLE is distinguishable from coverage ZERO', () => {
  // A UI that cannot tell "no mapping exists" from "nothing is covered" will
  // render the second, and the second is an alarm ([[empty-means-two-things]]).
  const p = proj.project('snip-it');
  assert.strictEqual(p.coverage.available, false);
  assert.ok(/no --repo/.test(p.coverage.reason), p.coverage.reason);
  assert.ok(!('covered' in p.coverage), 'an unavailable coverage must not carry a covered list at all');
});

test('a missing mapping is unavailable, not zero-covered', () => {
  const p = proj.project('macro-metrics', { repo: pathx.join(__dirname) });
  assert.strictEqual(p.coverage.available, false);
  assert.ok(/has no mapping/.test(p.coverage.reason), p.coverage.reason);
});

test('CONTROL: with a repo AND a mapping, coverage is available and real', () => {
  const p = proj.project('kit', { repo: pathx.join(__dirname, '..', '..') });
  assert.strictEqual(p.coverage.available, true);
  // Against the corpus's own size rather than a literal. A literal here was 10,
  // and adding four behaviours to kit.beh failed this control for a reason that
  // had nothing to do with the rule it guards. `> 0` keeps it from passing
  // vacuously on an empty read, which is the thing the literal was really for.
  assert.ok(p.coverage.covered.length > 0);
  assert.strictEqual(p.coverage.covered.length, p.behaviours.length);
  assert.deepStrictEqual(p.coverage.uncovered, []);
  assert.ok(/NOT that the test asserts/.test(p.coverage.proves), 'the caveat must travel IN the payload');
});

test('a reader losing tests makes coverage unavailable, not wrong', () => {
  // The same refusal check.js makes. A projection built on a bad read is wrong
  // in the same direction everywhere and silently.
  const repo = fixture({ 'a.spec.ts': 'beforeEach(() => {}); test("shared", () => {});\ntest("n", () => {});\n' });
  const dir = fixture({
    'x.beh': 'behaviour BEH-1 "one"\n  when opens page:Home\n',
    'x.tests.json': '{"BEH-1":[{"file":"a.spec.ts","title":"n"}]}',
  });
  const p = proj.project('x', { repo, behDir: dir });
  assert.strictEqual(p.coverage.available, false);
  assert.ok(/losing or inventing/.test(p.coverage.reason), p.coverage.reason);
});

test('A BACKTICK INSIDE A REGEX LITERAL BLINDS THE INDEPENDENT COUNT, and the fail-safe holds', () => {
  // Found by accident, and it cost this suite three red tests. `jsDeclarationCount`
  // strips strings, template literals and comments before counting call heads,
  // and it has no idea what a REGEX LITERAL is — nothing in JS can tell `/` as
  // division from `/` as a regex without parsing properly. So a regex whose
  // body contains a backtick reads as a template-literal opener, and the
  // scanner swallows everything up to the next backtick in the file. Writing
  // `doc.replace(/[*\`_]/g, '')` in this very file lost 60 declarations and
  // took kit's own coverage measurement offline.
  //
  // 🔑 THE POINT OF THIS TEST IS THAT THE FAIL-SAFE WORKED. An under-read makes
  // the two counts DISAGREE, and disagreement is reported as `available: false`
  // — never as a smaller coverage percentage. That is the difference between a
  // measurement that stops and one that quietly lies, and it is the property
  // worth pinning ([[empty-means-two-things]]). Do not "fix" the scanner into
  // guessing at regex literals to make this test go away.
  // ⚠️ TWO backticks, and the second one is the whole mechanism. An
  // UNTERMINATED backtick is handled safely — skipTemplate returns -1 and the
  // scanner shrugs and carries on — so a one-backtick fixture passes while
  // proving nothing, which is what my first attempt at this test did. The
  // damage needs a LATER backtick for the phantom template to close on, and a
  // real test file is full of them.
  const withBacktick = "test('a', () => {});\nconst r = /[*`_]/g;\ntest('b', () => {});\nconst s = `x`;\ntest('c', () => {});\n";
  const withoutIt = "test('a', () => {});\nconst r = new RegExp('[*`_]', 'g');\ntest('b', () => {});\nconst s = `x`;\ntest('c', () => {});\n";
  const { jsDeclarationCount } = require('./kit');
  assert.strictEqual(jsDeclarationCount(withoutIt), 3, 'the control: all three are seen');
  assert.ok(jsDeclarationCount(withBacktick) < 3, 'the regex literal hides the tests after it');

  // And the consequence, end to end: the disagreement must surface as
  // could-not-look, not as a number.
  const repo = fixture({ 'a.spec.ts': withBacktick });
  const dir = fixture({
    'y.beh': 'behaviour BEH-1 "one"\n  when opens page:Home\n',
    'y.tests.json': '{"BEH-1":[{"file":"a.spec.ts","title":"a"}]}',
  });
  const p = proj.project('y', { repo, behDir: dir });
  assert.strictEqual(p.coverage.available, false, 'a blinded read must not report a coverage number');
  assert.ok(/losing or inventing/.test(p.coverage.reason), p.coverage.reason);
});

test('exit 2 when there is no corpus — could-not-look is not an empty projection', () => {
  assert.strictEqual(quiet(() => proj.main(['nosuchapp'])), 2);
});

test('exit 2 when the corpus parses to zero behaviours', () => {
  const dir = fixture({ 'empty.beh': '# just a comment\n' });
  assert.strictEqual(quiet(() => proj.main(['empty', '--dir', dir])), 2);
});

test('exit 2 when no app is named, rather than projecting an arbitrary one', () => {
  assert.strictEqual(quiet(() => proj.main([])), 2);
});

test("kit's own shipped mapping projects with no errors, metadata keys and all", () => {
  // End-to-end over the real committed mapping. `_`, `_authored` and `_honest`
  // are skipped by `mapping()` itself; project.js briefly had its own copy of
  // that rule and a mutation proved the copy was dead code.
  const p = proj.project('kit', { repo: pathx.join(__dirname, '..', '..') });
  assert.deepStrictEqual(p.coverage.errors, [], p.coverage.errors.join('; '));
});

section('ui (the read API)');

const ui = require('./ui.js');

// A fixture corpus directory, so the routing tests do not depend on which
// corpora happen to be committed.
const uiDir = fixture({
  'alpha.beh': 'behaviour BEH-A "alpha does a thing"\n  actor engineer\n  when opens page:Home\n',
  'beta.beh': 'behaviour BEH-B "beta does another"\n  actor engineer\n  when opens page:Home\n',
});

test('lists every corpus in the directory, and only those', () => {
  assert.deepStrictEqual(ui.corpora(uiDir), ['alpha', 'beta']);
});

test('POST and GET are the whole surface — every other verb reaches no handler', () => {
  // Decision 2 landed, so POST now has a handler. PUT/PATCH/DELETE deliberately
  // do not: there is one write shape (append), and a route that accepts a verb
  // it has no meaning for is a 405 waiting to be discovered as a 500.
  for (const method of ['PUT', 'PATCH', 'DELETE']) {
    const r = ui.route(method, '/api/projects', { dir: uiDir });
    assert.strictEqual(r.status, 405, `${method} was not refused`);
    assert.match(r.body.allow, /GET, POST/);
  }
});

test('GET is not refused — the control for the rule above', () => {
  // Without this, a server that refused EVERYTHING would pass the test above.
  assert.strictEqual(ui.route('GET', '/api/projects', { dir: uiDir }).status, 200);
});

test('a traversal in the app name is a 404, not a read outside the corpus dir', () => {
  // Two different refusals, and both matter. A raw `../` never matches the
  // route pattern at all; a PERCENT-ENCODED one does — it is a single path
  // segment — so it reaches the name check and has to be refused there. Testing
  // only the first would leave the case that actually needs the rule uncovered.
  for (const name of ['../../etc/passwd', '..']) {
    const r = ui.route('GET', `/api/projects/${name}`, { dir: uiDir });
    assert.strictEqual(r.status, 404, `${name} was not refused`);
  }

  const encoded = ui.route('GET', '/api/projects/%2e%2e%2f%2e%2e%2fetc%2fpasswd', { dir: uiDir });
  assert.strictEqual(encoded.status, 404);
  assert.strictEqual(encoded.body.error, 'no-such-project');
});

test('a real app name is served — the control for the traversal rule', () => {
  const r = ui.route('GET', '/api/projects/alpha', { dir: uiDir });
  assert.strictEqual(r.status, 200);
  assert.strictEqual(r.body.behaviours[0].id, 'BEH-A');
});

test('unavailable coverage is null in the list, never zero', () => {
  // project.js's rule, carried through the summary. A UI that cannot tell "no
  // mapping exists" from "nothing is covered" renders the second, and the
  // second is an alarm.
  const r = ui.route('GET', '/api/projects', { dir: uiDir });
  const alpha = r.body.projects.find((p) => p.app === 'alpha');
  assert.strictEqual(alpha.coverage.available, false);
  assert.strictEqual(alpha.coverage.covered, null);
  assert.ok(alpha.coverage.reason, 'an unavailable coverage must say why');
});

test('the list carries the trial marker through to the UI, with a real corpus as control', () => {
  // Asserted HERE, in the Node suite, and not only in the UI's vitest suite:
  // mutate.js runs kit.test.js alone, so a rule whose only assertion lives in
  // vitest is unmutated. Dropping `notReal: p.notReal` from ui.js SURVIVED
  // until this test existed ([[an-uncaught-mutation-is-a-finding]]).
  const dir = fixture({
    'alpha.beh': 'behaviour BEH-A "alpha does a thing"\n  actor engineer\n  when opens page:Home\n',
    'trial.beh': '# kit:not-a-real-app\nbehaviour BEH-T "a trial does a thing"\n  actor engineer\n  when opens page:Home\n',
  });
  const rows = ui.route('GET', '/api/projects', { dir }).body.projects;
  assert.strictEqual(rows.find((p) => p.app === 'trial').notReal, true);
  assert.strictEqual(rows.find((p) => p.app === 'alpha').notReal, false, 'CONTROL');
});

test('the list carries the duplicate marker, naming the app, with a real corpus as control', () => {
  // Same reasoning as the test above, for the third exclusion axis. A trial
  // corpus written forwards for an app that DOES exist is not `notReal`, so
  // that marker cannot carry it, and without this one the UI would list the
  // trial and its subject as two indistinguishable projects.
  const dir = fixture({
    'alpha.beh': 'behaviour BEH-A "alpha does a thing"\n  actor engineer\n  when opens page:Home\n',
    'trial-alpha.beh': '# kit:duplicate-corpus alpha\nbehaviour BEH-T "a trial does a thing"\n  actor engineer\n  when opens page:Home\n',
  });
  const rows = ui.route('GET', '/api/projects', { dir }).body.projects;
  assert.strictEqual(rows.find((p) => p.app === 'trial-alpha').duplicateOf, 'alpha',
    'the marker must name the app, not merely flag a duplicate');
  assert.strictEqual(rows.find((p) => p.app === 'alpha').duplicateOf, null, 'CONTROL');
});

test('available coverage reports a number — the control for null-not-zero', () => {
  // kit's own corpus against kit's own repo: a mapping exists, so `covered` is
  // a count. Without this, a summary that reported null unconditionally would
  // pass the test above.
  const s = ui.summary('kit', { dir: pathx.join(__dirname, 'behaviours'), repos: null });
  const withRepo = ui.summary('kit', {
    dir: pathx.join(__dirname, 'behaviours'),
    repos: pathx.join(__dirname, '..', '..', '..'),
  });
  assert.strictEqual(s.coverage.available, false, 'no repos dir must be unavailable');
  assert.strictEqual(withRepo.coverage.available, true, 'kit beside its own repo must be available');
  assert.ok(withRepo.coverage.covered > 0);
});

test('a corpus that will not parse is reported as an error, not as zero behaviours', () => {
  const broken = fixture({ 'broken.beh': 'when opens page:Home\n' });
  const r = ui.route('GET', '/api/projects', { dir: broken });
  assert.strictEqual(r.body.projects.length, 1);
  assert.ok(r.body.projects[0].error, 'a broken corpus must carry an error');
  assert.strictEqual(r.body.projects[0].behaviours, undefined);
});

test('an unknown route is a 404 naming the path, not an empty 200', () => {
  const r = ui.route('GET', '/api/nope', { dir: uiDir });
  assert.strictEqual(r.status, 404);
  assert.match(r.body.reason, /\/api\/nope/);
});

test('main exits 2 when there are no corpora, rather than serving an empty list', async () => {
  const empty = fixture({ 'notes.md': 'no corpora here\n' });
  assert.strictEqual(await quiet(() => ui.main(['--dir', empty])), 2);
});

test('main exits 2 on a port that is not a port', async () => {
  assert.strictEqual(await quiet(() => ui.main(['--dir', uiDir, '--port', 'banana'])), 2);
});

// ── the delivery, over a real socket ────────────────────────────────────────
// The tests above drive `route()`, which is a function. A handler returning the
// right object and a server delivering it are different claims, and only the
// second is what a browser meets ([[test-the-delivery-not-just-the-value]]).

const get = (port, path) => new Promise((resolve, reject) => {
  require('http').get({ host: '127.0.0.1', port, path }, (res) => {
    let body = '';
    res.on('data', (c) => { body += c; });
    res.on('end', () => resolve({ status: res.statusCode, body }));
  }).on('error', reject);
});

test('a real listening server answers /api/projects with the corpus list', async () => {
  const server = await ui.serve({ dir: uiDir, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();
    const res = await get(port, '/api/projects');
    assert.strictEqual(res.status, 200);
    assert.deepStrictEqual(
      JSON.parse(res.body).projects.map((p) => p.app),
      ['alpha', 'beta'],
    );
  } finally {
    server.close();
  }
});

test('the server binds the loopback interface and not every interface', async () => {
  // The default is a decision: 0.0.0.0 would publish every corpus in the tree,
  // and the absolute path of every repo beside it, to anything reaching the
  // host. Asserted on the socket rather than on the argument, because the
  // argument is what a refactor drops.
  const server = await ui.serve({ dir: uiDir, port: 0 });
  try {
    assert.strictEqual(server.address().address, '127.0.0.1');
  } finally {
    server.close();
  }
});

section('ui (serving the built bundle: rules 5, 6, 7)');

// A fixture bundle, not the real `ui/dist`. `dist/` is gitignored, so on a
// runner there is nothing there at all — a suite that read the real one would be
// green here and skip silently in CI, which is the failure mode these very rules
// are about ([[empty-means-two-things]]).
//
// The names mirror what Vite actually emits: one content-hashed asset per type,
// an unhashed root file, and a sub-directory under assets that must NOT be
// servable.
const distDir = fixture({
  'index.html': '<!doctype html><div id="root"></div><script src="/assets/index-abc123.js"></script>',
  'assets/index-abc123.js': 'console.log("bundle")',
  'assets/index-def456.css': ':root{}',
  'assets/nested/secret.js': 'never served',
  'favicon.svg': '<svg/>',
});
const NO_DIST = pathx.join(distDir, 'not-built');

test('an asset is served only when its name is in the bundle listing, never by joining', () => {
  // The refusal and its control. `ui/README.md` gave "it means joining a path to
  // serve a file" as the reason this file served no HTML; rule 5 is the answer,
  // and it is the same answer rule 3 already gives for app names.
  const ok = ui.route('GET', '/assets/index-abc123.js', { dist: distDir });
  assert.strictEqual(ok.status, 200, 'CONTROL: a real asset must be served');
  assert.strictEqual(ok.contentType, 'text/javascript; charset=utf-8');

  assert.strictEqual(ui.route('GET', '/assets/index-nope.js', { dist: distDir }).status, 404);
});

test('a directory under assets is not servable, so a lookup cannot resolve to an EISDIR', () => {
  // `filesIn` filters to files. Without that the name IS in the listing, the
  // read throws, and the caller meets a 500 that says nothing about the bundle.
  assert.strictEqual(ui.route('GET', '/assets/nested', { dist: distDir }).status, 404);
});

test('a traversal in a bundle path reaches no file outside dist, encoded or not', () => {
  // Same pair as the app-name traversal test: a raw `../` never matches the
  // route patterns, an encoded one is a single segment and does reach the check.
  for (const p of ['/assets/../../../etc/passwd', '/../package.json']) {
    assert.strictEqual(ui.route('GET', p, { dist: distDir }).status, 404, `${p} was not refused`);
  }
  const encoded = ui.route('GET', '/assets/%2e%2e%2f%2e%2e%2findex.html', { dist: distDir });
  assert.strictEqual(encoded.status, 404);
  assert.strictEqual(encoded.body.error, 'no-such-file');
});

test('a path that NAMES A FILE and is not in the bundle is a 404, not the shell', () => {
  // Rule 6, and it is the rule with a track record. Every unmatched path on
  // balenthiran.co.uk answered 200 with the portfolio's SPA for the life of the
  // site, which is why a status code there could prove an app was up when the
  // app did not exist ([[green-over-the-clients-question]]).
  const missing = ui.route('GET', '/missing.png', { dist: distDir });
  assert.strictEqual(missing.status, 404);
  assert.strictEqual(missing.contentType, 'application/json');

  // CONTROL, and it is the half that makes the rule narrow rather than a ban:
  // an extensionless path IS a client route and must reach the shell, because
  // App.tsx uses BrowserRouter and reloading on a project page goes through here.
  const deep = ui.route('GET', '/projects/snip-it', { dist: distDir });
  assert.strictEqual(deep.status, 200);
  assert.match(deep.contentType, /^text\/html/);
  assert.match(String(deep.raw), /id="root"/);
});

test('/api answers JSON to the end, including its 404 — it never falls back to the shell', () => {
  // Rule 6's second half. A fetch that has gone wrong must be told so; handing
  // it HTML moves the error three layers away, to a JSON parse
  // ([[the-message-names-the-layer]]). Asserted WITH a bundle present, because
  // without one every answer is a 503 and the test would pass for free.
  const r = ui.route('GET', '/api/nope', { dir: uiDir, dist: distDir });
  assert.strictEqual(r.status, 404);
  assert.strictEqual(r.contentType, 'application/json');
  assert.strictEqual(r.body.error, 'no-such-route');
});

test('a bundle that was never built is a 503 naming the build command, not a 404', () => {
  // Rule 7, which is rule 4 one layer out: "nobody has built it" and "you typed
  // the wrong URL" are two states, and a 404 renders the second.
  const r = ui.route('GET', '/', { dist: NO_DIST });
  assert.strictEqual(r.status, 503);
  assert.match(String(r.raw), /npm --prefix prototypes\/behaviour-ast\/ui run build/,
    'the 503 must carry the command, not merely say it is missing');
  assert.ok(String(r.raw).includes(ui.BUILD_CMD), 'and it must be THE command, not a second spelling of it');
});

test('the API keeps answering while the bundle is absent — they are two states, not one', () => {
  // The sentence rule 7's page makes ("the API is running and answering") has to
  // be true, or it is a lie printed by the server itself.
  const r = ui.route('GET', '/api/projects', { dir: uiDir, dist: NO_DIST });
  assert.strictEqual(r.status, 200);
  assert.deepStrictEqual(r.body.projects.map((p) => p.app), ['alpha', 'beta']);
});

// ── the bundle, over a real socket ──────────────────────────────────────────
// `route()` returning the right object and the server delivering it are still
// different claims, and for static files there is one more: the headers. A
// cache-control returned by the router and dropped by `send` is invisible to
// every test above and presents as a blank page after a rebuild.

const getFull = (port, path) => new Promise((resolve, reject) => {
  require('http').get({ host: '127.0.0.1', port, path }, (res) => {
    let body = '';
    res.on('data', (c) => { body += c; });
    res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body }));
  }).on('error', reject);
});

test('a real listening server delivers the shell as HTML, and the hashed asset immutably', async () => {
  const server = await ui.serve({ dir: uiDir, dist: distDir, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();

    const shell = await getFull(port, '/');
    assert.strictEqual(shell.status, 200);
    assert.match(shell.headers['content-type'], /^text\/html/);
    assert.match(shell.body, /id="root"/);
    // The shell is the only unhashed file that NAMES the hashed ones, so a
    // cached copy outlives a rebuild and asks for assets that no longer exist.
    assert.strictEqual(shell.headers['cache-control'], 'no-store');

    const asset = await getFull(port, '/assets/index-abc123.js');
    assert.strictEqual(asset.status, 200);
    assert.strictEqual(asset.body, 'console.log("bundle")');
    assert.match(asset.headers['cache-control'], /immutable/,
      'a content-hashed name can be cached forever, and that is the point of hashing it');
  } finally {
    server.close();
  }
});

// ══════════════ the corpus writer (writer.js, claude-code-bot#59 / kit#16) ══════════════
//
// Decision 2 in docs/design/ui.md lapsed on 2026-09-08 and its stated default is
// now the decision: **write the file, never touch git.** These assert the four
// rules writer.js's header claims, and every one of them has a mutant.
//
// The corpus used below is a REAL one — `behaviours/snip-it.beh`, read from disk
// and edited in memory. A fixture I write here would carry exactly the comments
// I remembered to put in it, and the rule under test is about the comments a
// person actually wrote ([[test-the-reader-against-the-artefact]]).

const W = require('./writer');
const REAL_CORPUS = fsx.readFileSync(pathx.join(__dirname, 'behaviours', 'snip-it.beh'), 'utf8');

test('writer: a step lands at the end of its own block, not after the blank line', () => {
  const r = W.addStep(REAL_CORPUS, 'BEH-EDIT-1', 'then sees button:Save');
  assert.ok(r.ok, r.reason);
  const lines = r.text.split('\n');
  const start = lines.findIndex((l) => l.startsWith('behaviour BEH-EDIT-1 '));
  const next = lines.findIndex((l, i) => i > start && l.startsWith('behaviour '));
  const added = lines.indexOf('  then sees button:Save');
  assert.ok(added > start && added < next, `landed at ${added}, block is ${start}..${next}`);
  // The one that matters: it is the LAST content line, so the blank separating
  // the blocks is still a blank.
  assert.strictEqual(lines[added + 1].trim(), '', 'the block separator was consumed');
});

test('writer: every comment in the corpus survives an edit, byte for byte', () => {
  // The reason this file exists rather than a parse/serialise round-trip.
  // snip-it.beh opens with nine lines of caveat that parse() does not keep, and
  // a writer that dropped them would look correct in every AST comparison.
  const r = W.addStep(REAL_CORPUS, 'BEH-CUT-1', 'then sees button:Save');
  assert.ok(r.ok, r.reason);
  const comments = (t) => t.split('\n').filter((l) => l.trim().startsWith('#'));
  assert.deepStrictEqual(comments(r.text), comments(REAL_CORPUS));
  assert.ok(comments(REAL_CORPUS).length >= 8, 'the fixture must actually have comments to lose');
});

test('writer: an edit that would not parse is refused, and the text is unchanged', () => {
  for (const bad of ['wibble sees button:Save', 'source nonsense', 'option "only-a-label"']) {
    const r = W.addStep(REAL_CORPUS, 'BEH-EDIT-1', bad);
    assert.strictEqual(r.ok, false, `${bad} was accepted`);
    assert.strictEqual(r.error, 'would-not-parse');
    assert.strictEqual(r.text, undefined, 'a refusal must not hand back text to write');
  }
});

test('writer: an edit that changes a NEIGHBOURING behaviour is refused', () => {
  // Rule 3, and the only way to test it is to hand `validate` an "after" that a
  // correct addStep would never produce — an off-by-one splice is exactly this.
  const lines = REAL_CORPUS.split('\n');
  const victim = lines.findIndex((l) => l.startsWith('behaviour BEH-EDIT-2 '));
  lines.splice(victim + 2, 0, '  then sees button:Save');
  const r = W.validate(REAL_CORPUS, lines.join('\n'), 'BEH-EDIT-1');
  assert.strictEqual(r.ok, false, 'a step landing in the next behaviour was accepted');
  assert.strictEqual(r.error, 'collateral-change');
  assert.match(r.reason, /BEH-EDIT-2/);
});

test('writer: an edit that DELETES a behaviour is refused', () => {
  // The other half of rule 3, and a different failure from a neighbour changing:
  // a splice whose range is too long removes a whole block, and what is left
  // parses perfectly. Nothing else in Kit would notice the behaviour was gone —
  // the coverage number would simply be over a smaller set.
  const lines = REAL_CORPUS.split('\n');
  const victim = lines.findIndex((l) => l.startsWith('behaviour BEH-EDIT-2 '));
  const next = lines.findIndex((l, i) => i > victim && l.startsWith('behaviour '));
  lines.splice(victim, next - victim);
  const r = W.validate(REAL_CORPUS, lines.join('\n'), 'BEH-EDIT-1');
  assert.strictEqual(r.ok, false, 'a deleted behaviour was accepted');
  assert.match(r.reason, /BEH-EDIT-2 disappeared/);
});

test('writer: a correct edit is NOT reported as collateral — the control', () => {
  // Without this, a rule 3 that rejected everything would pass the test above.
  assert.strictEqual(W.addStep(REAL_CORPUS, 'BEH-EDIT-1', 'then sees button:Save').ok, true);
});

test('writer: an unknown behaviour id is refused and names what does exist', () => {
  const r = W.addStep(REAL_CORPUS, 'BEH-NOPE', 'then sees button:Save');
  assert.strictEqual(r.error, 'no-such-behaviour');
  assert.ok(r.known.includes('BEH-EDIT-1'), 'a refusal must say what the ids actually are');
});

test('writer: a new behaviour defaults to `inferred`, which parse() marks unreviewed', () => {
  // His #68 call, carried into the one place that can silently spend it. A UI
  // writes on a machine's behalf; silence in a corpus means a human wrote it.
  const r = W.addBehaviour(REAL_CORPUS, 'BEH-NEW-1', 'a new thing', { actor: 'visitor', steps: ['when opens page:Home'] });
  assert.ok(r.ok, r.reason);
  const b = parse(r.text).find((x) => x.id === 'BEH-NEW-1');
  assert.strictEqual(b.source.origin, 'inferred');
  assert.strictEqual(b.review.state, 'unreviewed');
});

test('writer: a duplicate id, a bad id and a quoted title are all refused', () => {
  assert.strictEqual(W.addBehaviour(REAL_CORPUS, 'BEH-EDIT-1', 't').error, 'duplicate-id');
  assert.strictEqual(W.addBehaviour(REAL_CORPUS, 'beh-lower', 't').error, 'bad-id');
  assert.strictEqual(W.addBehaviour(REAL_CORPUS, 'BEH-NEW-2', 'he said "hi"').error, 'bad-title');
});

test('writer: a multi-line step is refused rather than spliced in as two', () => {
  const r = W.addStep(REAL_CORPUS, 'BEH-EDIT-1', 'then sees button:Save\n  then sees button:Cancel');
  assert.strictEqual(r.error, 'multiline-step');
});

test('writer: appending twice produces the same shape as appending once, twice', () => {
  // A trailing-newline bug shows up here and nowhere else: the second edit is
  // the first one that meets the file the writer itself produced.
  const one = W.addBehaviour(REAL_CORPUS, 'BEH-N1', 'one');
  assert.ok(one.ok, one.reason);
  const two = W.addBehaviour(one.text, 'BEH-N2', 'two');
  assert.ok(two.ok, two.reason);
  assert.strictEqual(two.text.match(/\n\n\nbehaviour/g), null, 'a blank line accumulated between edits');
  assert.strictEqual(parse(two.text).length, parse(REAL_CORPUS).length + 2);
});

// ── adjudication: the first write that CHANGES a line (his #68, kit#16) ─────
//
// `language-vocab.beh` rather than snip-it's corpus, because this is the only
// writer function whose subject has to already exist: fifteen of its behaviours
// carry `source inferred` + `review unreviewed`, written by a real pass over a
// real repo. A fixture would carry the review line I remembered to put in it,
// and the interesting case is the behaviour that has one already.

const VOCAB_CORPUS = fsx.readFileSync(pathx.join(__dirname, 'behaviours', 'language-vocab.beh'), 'utf8');

test('writer: the corpus this section reads really does hold unreviewed inferences', () => {
  // The population, asserted before anything loops over it. Every test below is
  // vacuous if the corpus stops carrying one, and a vacuous test passes.
  const unreviewed = parse(VOCAB_CORPUS).filter((b) => b.source.origin === 'inferred' && b.review.state === 'unreviewed');
  assert.ok(unreviewed.length >= 5, `only ${unreviewed.length} unreviewed inferences to adjudicate`);
  assert.ok(VOCAB_CORPUS.includes('  review unreviewed'), 'the corpus states the review line explicitly');
});

test('writer: approving REPLACES the review line rather than adding a second one', () => {
  // The failure this exists to stop is not a parse error — `parse()` takes the
  // last `review` line and would report `approved` quite happily. It is a corpus
  // that says two things to the person reading it.
  const r = W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', 'approved');
  assert.ok(r.ok, r.reason);
  const b = parse(r.text).find((x) => x.id === 'BEH-GRADE-1');
  assert.strictEqual(b.review.state, 'approved');
  assert.strictEqual(b.review.note, null);
  const lines = r.text.split('\n');
  const start = lines.findIndex((l) => l.startsWith('behaviour BEH-GRADE-1 '));
  const next = lines.findIndex((l, i) => i > start && l.startsWith('behaviour '));
  const reviews = lines.slice(start, next).filter((l) => l.trim().split(/\s+/)[0] === 'review');
  assert.strictEqual(reviews.length, 1, `the block carries ${reviews.length} review lines`);
  assert.strictEqual(r.text.split('\n').length, VOCAB_CORPUS.split('\n').length, 'the file grew or shrank');
});

test('writer: a denial carries the correction into the corpus', () => {
  const r = W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', 'denied', 'the grader returns four verdicts, not three');
  assert.ok(r.ok, r.reason);
  const b = parse(r.text).find((x) => x.id === 'BEH-GRADE-1');
  assert.strictEqual(b.review.state, 'denied');
  assert.strictEqual(b.review.note, 'the grader returns four verdicts, not three');
});

test('writer: a denial with no correction is refused, in the parser\'s own words', () => {
  // His #68 point: a bare denial deletes a line, a denial with a correction
  // compounds into the corpus. The rule lives in `parse()` and is NOT restated
  // in writer.js — this asserts that the sentence survives the trip, because the
  // sentence is what reaches the screen.
  const r = W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', 'denied');
  assert.strictEqual(r.ok, false);
  assert.strictEqual(r.error, 'would-not-parse');
  assert.match(r.reason, /must state the correction/);
  assert.strictEqual(r.text, undefined, 'a refusal must not hand back text to write');
});

test('writer: a newline in the note is refused — rule 3 exempts the TARGET', () => {
  // 🔴 The one hole free text could reach. `validate()` compares every behaviour
  // EXCEPT the one being edited, so a note of `wrong\n  actor attacker` splices a
  // line into the target that nothing downstream would question. A note that
  // opens a new `behaviour` block IS caught by rule 3 — which is the trap, since
  // the caught case is the loud one and the uncaught case is the quiet one.
  const inside = W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', 'denied', 'wrong\n  actor attacker');
  assert.strictEqual(inside.error, 'multiline-review');
  const outside = W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', 'denied', 'wrong\nbehaviour BEH-EVIL "evil"');
  assert.strictEqual(outside.error, 'multiline-review');
  // And the state, by the same argument — it is concatenated into the same line.
  assert.strictEqual(W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', 'approved\n  actor attacker').error, 'multiline-review');
});

test('writer: a state outside the three is refused, and the vocabulary is not restated here', () => {
  assert.strictEqual(W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', 'approvedd').error, 'would-not-parse');
  assert.strictEqual(W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', '').error, 'empty-review');
  assert.strictEqual(W.setReview(VOCAB_CORPUS, 'BEH-NOPE', 'approved').error, 'no-such-behaviour');
  // The vocabulary lives in `parse()` and must not be duplicated in writer.js,
  // or the two are free to drift and only one of them is the file's meaning.
  const src = fsx.readFileSync(pathx.join(__dirname, 'writer.js'), 'utf8');
  const code = src.split('\n').filter((l) => !l.trim().startsWith('//')).join('\n');
  assert.strictEqual(/unreviewed\|approved\|denied/.test(code), false,
    'writer.js has grown its own copy of the review vocabulary');
});

test('writer: a behaviour with NO review line gets one, under source, and neighbours are untouched', () => {
  // `parse()` defaults a `defined` behaviour to approved without any line saying
  // so, so this is the insert path rather than the replace path.
  const defined = parse(VOCAB_CORPUS).find((b) => b.source.origin === 'defined');
  assert.ok(defined, 'the corpus has no defined behaviour to deny');
  const r = W.setReview(VOCAB_CORPUS, defined.id, 'denied', 'this was never the desired behaviour');
  assert.ok(r.ok, r.reason);
  const after = parse(r.text);
  assert.strictEqual(after.find((b) => b.id === defined.id).review.state, 'denied');
  assert.strictEqual(after.length, parse(VOCAB_CORPUS).length, 'the behaviour count changed');
  // Rule 3 already refuses collateral change; this asserts the count of lines it
  // added, which rule 3 cannot see because the target is the exempt one.
  assert.strictEqual(r.text.split('\n').length, VOCAB_CORPUS.split('\n').length + 1);

  // WHERE it landed, and this is not decoration. The parser does not care, so
  // nothing else in the system can ever notice a `review` line sitting above the
  // `actor` it belongs under — a mutation survived here until this assertion
  // existed, which is the definition of a rule with no test
  // ([[an-uncaught-mutation-is-a-finding]]). Every corpus orders its preamble
  // actor → source → review, and the corpus is a document for a person.
  const lines = r.text.split('\n');
  const start = lines.findIndex((l) => l.startsWith(`behaviour ${defined.id} `));
  const kwAt = (kw) => lines.findIndex((l, i) => i > start && l.trim().split(/\s+/)[0] === kw);
  const reviewAt = kwAt('review');
  const sourceAt = kwAt('source');
  const actorAt = kwAt('actor');
  assert.ok(reviewAt > start, 'the review line is not inside the block');
  if (sourceAt !== -1) assert.strictEqual(reviewAt, sourceAt + 1, 'the review line is not directly under source');
  else if (actorAt !== -1) assert.ok(reviewAt > actorAt, 'the review line landed above actor');
});

test('writer: adjudicating twice ends at the second answer, not at two answers', () => {
  const once = W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', 'approved');
  assert.ok(once.ok, once.reason);
  const twice = W.setReview(once.text, 'BEH-GRADE-1', 'denied', 'changed my mind');
  assert.ok(twice.ok, twice.reason);
  const b = parse(twice.text).find((x) => x.id === 'BEH-GRADE-1');
  assert.strictEqual(b.review.state, 'denied');
  assert.strictEqual(twice.text.split('\n').length, VOCAB_CORPUS.split('\n').length,
    'a second adjudication grew the file');
});

test('writer: every comment survives an adjudication too', () => {
  const r = W.setReview(VOCAB_CORPUS, 'BEH-GRADE-1', 'approved');
  assert.ok(r.ok, r.reason);
  const comments = (t) => t.split('\n').filter((l) => l.trim().startsWith('#'));
  assert.deepStrictEqual(comments(r.text), comments(VOCAB_CORPUS));
  assert.ok(comments(VOCAB_CORPUS).length >= 8, 'the corpus must actually have comments to lose');
});

test('writer: it contains no path to git at all — decision 2, checked not promised', () => {
  // A property of the file, not of any run. Read from source for the same reason
  // converge.js's "there is no threshold" test does: the claim is that this
  // cannot commit, and only the absence of the machinery proves it.
  const src = fsx.readFileSync(pathx.join(__dirname, 'writer.js'), 'utf8');
  const code = src.split('\n').filter((l) => !l.trim().startsWith('//')).join('\n');
  assert.strictEqual(/child_process|execFile|execSync|\bspawn\b/.test(code), false, 'writer.js can shell out');
  assert.strictEqual(/require\(['"]child_process/.test(code), false);
});

// ── binding: the write that reaches every corpus at once (stage 4) ─────────
//
// `docs/design/process.md` calls bind-by-noun "the most important decision in
// the design, and the fix for the thing that killed Cucumber", and it was the
// only verb of the loop with no write path. Measured across all nine corpora on
// 2026-09-10: 129 of 172 nouns (75%) unbound.
//
// The population is asserted first, because every test below is about a file
// that could stop having the property they are written against.

section('binding: the one write that is not scoped to one corpus');

const { parseStep } = require('./kit');
const BINDINGS_TEXT = fsx.readFileSync(pathx.join(__dirname, 'bindings.json'), 'utf8');

test('binding: the real bindings.json is one FLAT map shared by every corpus', () => {
  // The premise the whole `sharedWith` mechanism rests on. If bindings ever
  // became per-app, the collision hazard disappears and these tests become
  // theatre — so the shape is asserted rather than assumed.
  const b = JSON.parse(BINDINGS_TEXT);
  const real = Object.keys(b).filter((k) => !W.isComment(k));
  assert.ok(real.length >= 20, `only ${real.length} bindings to reason about`);
  for (const k of real) {
    assert.ok(W.isNoun(k), `${k} is not a <kind>:<Name> noun, so the map is not keyed by noun`);
    assert.strictEqual(typeof b[k], 'object', `${k} is not an object`);
  }
});

test('binding: the noun namespace really is global, and one collision is cross-APP', () => {
  // The measurement the mechanism exists for, re-run rather than quoted. Six of
  // the seven shared names are james-habits-app described three ways, where
  // sharing IS the point of binding by noun; `region:EmptyState` spans two
  // genuinely different apps. If this ever reports zero, `sharedWith` is
  // guarding nothing and should be reconsidered, not left in place looking busy.
  const corpora = W.corpusNouns();
  assert.ok(Object.keys(corpora).length >= 5, 'not enough corpora to collide');
  const counts = new Map();
  for (const nouns of Object.values(corpora)) {
    for (const n of nouns) counts.set(n, (counts.get(n) || 0) + 1);
  }
  const shared = [...counts.entries()].filter(([, c]) => c > 1).map(([n]) => n);
  assert.ok(shared.length >= 1, 'no noun name is used by two corpora');
  assert.ok(
    W.sharedWith('region:EmptyState', corpora, 'trial-habits-a').includes('trial-lend'),
    'region:EmptyState no longer spans two different apps — re-measure before trusting this section',
  );
});

test('binding: sharedWith names the OTHER corpora and never the one you are in', () => {
  const corpora = { a: ['button:Save', 'page:Home'], b: ['button:Save'], c: ['page:Other'] };
  assert.deepStrictEqual(W.sharedWith('button:Save', corpora, 'a'), ['b']);
  assert.deepStrictEqual(W.sharedWith('button:Save', corpora, 'b'), ['a']);
  assert.deepStrictEqual(W.sharedWith('page:Home', corpora, 'a'), []);
  // No `self` at all is a legitimate caller — a CLI run that named no app —
  // and must list every corpus rather than silently excluding none of them.
  assert.deepStrictEqual(W.sharedWith('button:Save', corpora, undefined), ['a', 'b']);
});

test('binding: a new noun is added and every existing binding is untouched', () => {
  const corpora = W.corpusNouns();
  const before = JSON.parse(BINDINGS_TEXT);
  const r = W.addBinding(BINDINGS_TEXT, 'button:BrandNewThing', { role: 'button', name: 'Brand new' }, { corpora, app: 'kit-ui' });
  assert.ok(r.ok, r.reason);
  const after = JSON.parse(r.text);
  assert.deepStrictEqual(after['button:BrandNewThing'], { role: 'button', name: 'Brand new' });
  for (const k of Object.keys(before)) {
    assert.deepStrictEqual(after[k], before[k], `${k} changed`);
  }
  assert.strictEqual(Object.keys(after).length, Object.keys(before).length + 1);
  // The prose in the file is the reason a JSON round-trip is safe here at all.
  assert.ok(Object.keys(after).some((k) => W.isComment(k)), 'the _comment keys were lost');
});

test('binding: the warning fires on the real cross-app collision, and is silent otherwise', () => {
  const corpora = W.corpusNouns();
  const clash = W.addBinding(BINDINGS_TEXT, 'region:EmptyState', { role: 'region', name: 'Nothing yet' }, { corpora, app: 'trial-habits-a' });
  assert.ok(clash.ok, clash.reason);
  assert.deepStrictEqual(clash.sharedWith, ['trial-lend']);

  const clean = W.addBinding(BINDINGS_TEXT, 'button:SomethingNobodyElseUses', { role: 'button', name: 'x' }, { corpora, app: 'kit-ui' });
  assert.ok(clean.ok, clean.reason);
  assert.deepStrictEqual(clean.sharedWith, [], 'a warning fired for a noun no other corpus references');
});

test('binding: sharedWith is always an array, even when the caller passes no corpora', () => {
  // The empty case, spelled out. A caller that renders `sharedWith.length` must
  // not have to know whether the field is there — and "I did not check" and
  // "nothing collides" must not both arrive as undefined.
  const r = W.addBinding(BINDINGS_TEXT, 'button:NoCorporaGiven', { role: 'button', name: 'x' });
  assert.ok(r.ok, r.reason);
  assert.deepStrictEqual(r.sharedWith, []);
});

test('binding: rebinding an existing noun is REFUSED, and the refusal shows what is there', () => {
  // Not a validation nicety. A new binding turns a refusal into a generated
  // step you can see; a rebind silently changes what every corpus mentioning
  // the noun already generates, including ones the clicker never opened.
  const r = W.addBinding(BINDINGS_TEXT, 'page:Home', { route: './somewhere-else' }, { corpora: W.corpusNouns(), app: 'snip-it' });
  assert.strictEqual(r.ok, false);
  assert.strictEqual(r.error, 'already-bound');
  assert.deepStrictEqual(r.current, { route: './' });
});

test('binding: a _comment key cannot be written as a binding — by the NOUN grammar', () => {
  // The error code is the interesting half. `addBinding` used to carry an
  // explicit `isComment` refusal and a mutant deleting it SURVIVED: a
  // `_comment*` key has no colon, so `isNoun` refuses it first and the second
  // check could never fire. The guard was removed rather than left in place
  // looking load-bearing, and `bad-noun` — not some `bad-comment` code — is
  // what records that the grammar is what stops it.
  for (const k of ['_comment', '_comment_kit_ui', '_comment_macro_metrics']) {
    const r = W.addBinding(BINDINGS_TEXT, k, { role: 'button' });
    assert.strictEqual(r.ok, false, `${k} was accepted`);
    assert.strictEqual(r.error, 'bad-noun', k);
  }
  // And the reader-side helper still knows one when it sees one, which is what
  // it is for now that it has no job on the write path.
  assert.strictEqual(W.isComment('_comment_kit_ui'), true);
  assert.strictEqual(W.isComment('button:Save'), false);
});

test('binding: a malformed noun is refused before anything is written', () => {
  for (const bad of ['NotANoun', 'Button:Save', 'button:', ':Save', 'button:Sa ve', 'x button:Save', 'button:Save extra', '', null, 42]) {
    const r = W.addBinding(BINDINGS_TEXT, bad, { role: 'button', name: 'x' });
    assert.strictEqual(r.ok, false, `${JSON.stringify(bad)} was accepted as a noun`);
    assert.strictEqual(r.error, 'bad-noun', `${JSON.stringify(bad)}`);
  }
});

test('binding: the noun check is the PARSER\'s, not a second grammar beside it', () => {
  // The first draft of this was a hand-written regex and it disagreed with
  // `kit.js` in both directions — it banned a digit-led name, which would have
  // rejected a sibling of the real shipped binding `file:talk_mp4`, and allowed
  // a digit in the kind, which the parser never accepts. So the property under
  // test is agreement, checked against the parser rather than against my idea
  // of the grammar.
  const viaParser = (s) => {
    const { refs, holes } = parseStep(s, 'x');
    return holes.length === 0 && refs.length === 1 && refs[0].kind !== 'literal'
      && `${refs[0].kind}:${refs[0].name}` === s;
  };
  for (const s of ['button:Save', 'file:talk_mp4', 'field:x2', 'page:Home', 'button2:Save', 'Button:Save', 'button:Sa ve', 'notanoun', '"literal"']) {
    assert.strictEqual(W.isNoun(s), viaParser(s), `disagreed with the parser about ${JSON.stringify(s)}`);
  }
  // And the lowercase name the first draft would have rejected is genuinely
  // accepted — the case that made the drift matter rather than merely exist.
  assert.strictEqual(W.isNoun('button:save'), true);
  assert.strictEqual(W.addBinding('{}', 'file:talk_mp4', { fixture: { name: 'talk.mp4' } }).ok, true);
});

test('binding: a value that is not a non-empty object is refused', () => {
  for (const bad of [null, 'button', 42, [], [1, 2], {}]) {
    const r = W.addBinding(BINDINGS_TEXT, 'button:Whatever', bad);
    assert.strictEqual(r.ok, false, `${JSON.stringify(bad)} was accepted as a binding`);
    assert.strictEqual(r.error, 'bad-binding');
  }
});

test('binding: 🔴 a key that JSON.stringify would DELETE is refused, not silently emptied', () => {
  // The guard that was inert in its first draft, and the reason it is written
  // against `Object.keys(value)` rather than against `JSON.stringify(value)`:
  // for `{role: undefined}` both sides stringify to `{}`, so the check compared
  // the damage to itself and passed. This is the mutation that matters —
  // `boundNouns()` counts the KEY, so an emptied binding reads as progress
  // while satisfying no verb.
  for (const bad of [{ role: undefined }, { role: 'button', name: undefined }, { role: () => 'x' }]) {
    const r = W.addBinding(BINDINGS_TEXT, 'button:Whatever', bad);
    assert.strictEqual(r.ok, false, `${Object.keys(bad).join(',')} was accepted`);
    assert.strictEqual(r.error, 'bad-binding');
    assert.match(r.reason, /would not survive/);
  }
  // The control: the same shapes with real values go through, so the test above
  // is measuring the loss and not just the key names.
  assert.strictEqual(W.addBinding(BINDINGS_TEXT, 'button:Whatever', { role: 'button', name: 'x' }).ok, true);
});

test('binding: a bindings file that was already broken says so, and blames the file', () => {
  const r = W.addBinding('{ not json', 'button:X', { role: 'button', name: 'x' });
  assert.strictEqual(r.error, 'bindings-already-invalid');
  assert.match(r.reason, /did not parse before this edit/);
  // An array parses as JSON and is not a map of nouns.
  assert.strictEqual(W.addBinding('[]', 'button:X', { role: 'button', name: 'x' }).error, 'bindings-already-invalid');
});

test('binding: what it writes is what the emitter reads — a refusal becomes a real step', () => {
  // The seam that matters, driven end to end rather than asserted on the JSON.
  // Before: the step cannot generate and the noun is in `missing`. After: the
  // same behaviour, the same generator, and a real Playwright line. Nothing
  // here restates the binding format — `emit()` is the only definition of it,
  // and this is the test that would go red if the two ever disagreed.
  const behaviours = parse('behaviour BEH-B1 "bind me"\n  actor engineer\n  then sees button:UnboundOnPurpose\n', 'b.beh');
  const empty = {};
  const before = generate(behaviours[0], empty);
  assert.deepStrictEqual(before.missing, ['button:UnboundOnPurpose']);
  assert.match(before.code, /\/\/ UNGENERATED: then sees button:UnboundOnPurpose/);

  const r = W.addBinding('{}', 'button:UnboundOnPurpose', { role: 'button', name: 'Unbound on purpose' });
  assert.ok(r.ok, r.reason);
  const after = generate(behaviours[0], JSON.parse(r.text));
  assert.deepStrictEqual(after.missing, []);
  assert.match(after.code, /getByRole\("button", \{ name: "Unbound on purpose" \}\)/);
  assert.doesNotMatch(after.code, /UNGENERATED/);
});

test('binding: appending twice produces a file the writer can read back', () => {
  // The same property `addBehaviour` is tested for: the second edit is the
  // first one that meets the file this function itself produced.
  const one = W.addBinding('{}', 'button:One', { role: 'button', name: 'One' });
  assert.ok(one.ok, one.reason);
  const two = W.addBinding(one.text, 'button:Two', { role: 'button', name: 'Two' });
  assert.ok(two.ok, two.reason);
  assert.deepStrictEqual(JSON.parse(two.text), {
    'button:One': { role: 'button', name: 'One' },
    'button:Two': { role: 'button', name: 'Two' },
  });
  assert.ok(two.text.endsWith('\n'), 'the file lost its trailing newline');
});

test('binding: corpusNouns skips a corpus that will not parse rather than throwing', () => {
  // One broken corpus elsewhere in the directory must not be able to block
  // binding a noun — but the caller is told, because the answer is now
  // incomplete and a silent gap in a collision check is the quiet direction.
  const dir = fixture({
    'good.beh': 'behaviour BEH-G "g"\n  actor engineer\n  then sees button:Save\n',
    'broken.beh': 'this is not a corpus at all\n',
  });
  const skipped = [];
  const nouns = W.corpusNouns(dir, (app) => skipped.push(app));
  assert.deepStrictEqual(nouns.good, ['button:Save']);
  assert.deepStrictEqual(skipped, ['broken']);
  assert.strictEqual(Object.prototype.hasOwnProperty.call(nouns, 'broken'), false);
});

// ── the write path over the transport ───────────────────────────────────────

// `contentType` is a parameter and not a constant because the attacker picks it,
// not us. Hardcoding `application/json` here is what hid the cross-origin write
// below: it is the ONE content-type that forces a preflight, so a suite that can
// only speak it can only ever test the case that was already safe.
const post = (port, path, body, origin, contentType = 'application/json') => new Promise((resolve, reject) => {
  const data = Buffer.from(JSON.stringify(body));
  const req = require('http').request({
    host: '127.0.0.1', port, path, method: 'POST',
    headers: { 'content-type': contentType, 'content-length': data.length, ...(origin ? { origin } : {}) },
  }, (res) => {
    let b = '';
    res.on('data', (c) => { b += c; });
    res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body: b }));
  });
  req.on('error', reject);
  req.end(data);
});

// Its own directory, written to on purpose. `uiDir` is shared by the read tests
// above and a write into it would make them depend on the order they run in.
const wDir = () => fixture({
  'gamma.beh': '# a comment that must survive\nbehaviour BEH-G "gamma"\n  actor engineer\n  when opens page:Home\n',
});

test('a POST appends a step to the real file on disk, and says it did not commit', async () => {
  const dir = wDir();
  const server = await ui.serve({ dir, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();
    const res = await post(port, '/api/projects/gamma/behaviours/BEH-G/steps', { step: 'then sees region:Main' });
    assert.strictEqual(res.status, 200);
    assert.strictEqual(JSON.parse(res.body).committed, false, 'the response must state that nothing was committed');
    const after = fsx.readFileSync(pathx.join(dir, 'gamma.beh'), 'utf8');
    assert.match(after, /then sees region:Main/);
    assert.match(after, /# a comment that must survive/);
  } finally { server.close(); }
});

test('a POST that writes a step Kit cannot parse is a 409 and leaves the file alone', async () => {
  const dir = wDir();
  const before = fsx.readFileSync(pathx.join(dir, 'gamma.beh'), 'utf8');
  const server = await ui.serve({ dir, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();
    const res = await post(port, '/api/projects/gamma/behaviours/BEH-G/steps', { step: 'wibble region:Main' });
    assert.strictEqual(res.status, 409, 'a refused edit is a statement about the request, not a server fault');
    assert.strictEqual(fsx.readFileSync(pathx.join(dir, 'gamma.beh'), 'utf8'), before);
  } finally { server.close(); }
});

test('the write path is refused when the server is not bound to loopback', () => {
  // The two decisions are coupled: local tool (1) is what makes an
  // unauthenticated writer (2) acceptable, so the code refuses the combination
  // rather than trusting whoever passes --host.
  const r = ui.route('POST', '/api/projects/alpha/behaviours/BEH-A/steps', { dir: uiDir, host: '0.0.0.0' }, { step: 'then sees region:Main' });
  assert.strictEqual(r.status, 403);
  assert.strictEqual(r.error, undefined);
  assert.strictEqual(r.body.error, 'not-loopback');
});

test('the loopback refusal comes BEFORE the corpus is looked up', () => {
  // A remote caller must not be able to learn which apps exist by watching 404
  // and 403 differ. Same request, unknown app: still 403, not 404.
  const r = ui.route('POST', '/api/projects/definitely-not-an-app/behaviours/BEH-A/steps', { dir: uiDir, host: '10.0.0.5' }, {});
  assert.strictEqual(r.body.error, 'not-loopback');
});

test('isLoopback accepts the loopback range and nothing that merely looks like it', () => {
  for (const ok of ['127.0.0.1', '127.1.2.3', 'localhost', '::1']) assert.ok(ui.isLoopback(ok), ok);
  for (const no of ['0.0.0.0', '10.0.0.5', '127.0.0.1.evil.com', 'x127.0.0.1', '::ffff:127.0.0.1', '1270.0.1']) {
    assert.strictEqual(ui.isLoopback(no), false, `${no} was treated as loopback`);
  }
});

test('CORS: a loopback origin is reflected, and any other origin gets no header', () => {
  // `*` was defensible while the server could not write. With a write route it
  // means any page the developer has open can edit their working tree.
  assert.strictEqual(ui.cors('http://localhost:5173')['access-control-allow-origin'], 'http://localhost:5173');
  assert.strictEqual(ui.cors('http://127.0.0.1:5173')['access-control-allow-origin'], 'http://127.0.0.1:5173');
  for (const evil of ['https://evil.com', 'http://127.0.0.1.evil.com', 'not a url']) {
    assert.strictEqual(ui.cors(evil)['access-control-allow-origin'], undefined, `${evil} was allowed`);
  }
  assert.strictEqual(ui.cors('http://localhost:5173').vary, 'Origin');
});

test('a cross-origin POST from a hostile page gets no CORS header from the socket', async () => {
  // The header, on a real response, from a real origin — the value tested above
  // has to survive the trip through serve() to mean anything.
  const dir = wDir();
  const server = await ui.serve({ dir, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();
    const res = await post(port, '/api/projects/gamma/behaviours/BEH-G/steps', { step: 'then sees region:Main' }, 'https://evil.com');
    assert.strictEqual(res.headers['access-control-allow-origin'], undefined);
  } finally { server.close(); }
});

// ── the cross-origin write, which the CORS rule above does NOT stop ──────────
//
// The test above asserts a hostile origin gets no CORS header, and that is true
// and worthless on its own: withholding the header stops the attacker READING
// the reply, and for a write the request itself is the damage. These three pin
// the rule that actually defends it — the Origin check in `ui.write()`.
//
// Every one of them asserts the FILE, not the status code. A 403 that still
// wrote is the exact failure being guarded against, and a status-only assertion
// passes over it.

test('a cross-origin text/plain POST — no preflight — does not reach the corpus', async () => {
  // `text/plain` is a CORS *simple* content-type, so a browser sends this with
  // no preflight at all; `<form enctype="text/plain">` sends it with no JS.
  // This is the shape that actually got through, measured before it was fixed.
  const fs = require('fs');
  const path = require('path');
  const dir = wDir();
  const before = fs.readFileSync(path.join(dir, 'gamma.beh'), 'utf8');
  const server = await ui.serve({ dir, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();
    const res = await post(port, '/api/projects/gamma/behaviours',
      { id: 'BEH-EVIL', title: 'written by a page on another origin' },
      'https://evil.example', 'text/plain;charset=UTF-8');
    assert.strictEqual(res.status, 403);
    assert.match(JSON.parse(res.body).error, /cross-origin-write/);
    assert.strictEqual(fs.readFileSync(path.join(dir, 'gamma.beh'), 'utf8'), before,
      'the corpus was edited by a cross-origin request');
  } finally { server.close(); }
});

test('a cross-origin JSON POST is refused too, by the same rule', async () => {
  const fs = require('fs');
  const path = require('path');
  const dir = wDir();
  const before = fs.readFileSync(path.join(dir, 'gamma.beh'), 'utf8');
  const server = await ui.serve({ dir, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();
    const res = await post(port, '/api/projects/gamma/behaviours/BEH-G/steps',
      { step: 'then sees region:Main' }, 'https://evil.example');
    assert.strictEqual(res.status, 403);
    assert.strictEqual(fs.readFileSync(path.join(dir, 'gamma.beh'), 'utf8'), before);
  } finally { server.close(); }
});

test('the UI\'s own origin still writes, and a CLI sending no Origin still writes', async () => {
  // The other half: a guard that refuses everything is not a guard, it is an
  // outage. The Vite dev server on another loopback port is the real caller.
  const fs = require('fs');
  const path = require('path');
  const dir = wDir();
  const server = await ui.serve({ dir, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();
    const fromUi = await post(port, '/api/projects/gamma/behaviours/BEH-G/steps',
      { step: 'then sees region:Main' }, 'http://localhost:5173');
    assert.strictEqual(fromUi.status, 200);

    // No Origin header at all — curl, the suite, writer.js's CLI.
    const fromCli = await post(port, '/api/projects/gamma/behaviours/BEH-G/steps',
      { step: 'then sees region:Footer' });
    assert.strictEqual(fromCli.status, 200);

    const text = fs.readFileSync(path.join(dir, 'gamma.beh'), 'utf8');
    assert.match(text, /region:Main/);
    assert.match(text, /region:Footer/);
  } finally { server.close(); }
});

test('a body over the limit is refused rather than buffered', async () => {
  const dir = wDir();
  const server = await ui.serve({ dir, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();
    const res = await post(port, '/api/projects/gamma/behaviours/BEH-G/steps', { step: 'x'.repeat(ui.MAX_BODY + 1) });
    assert.strictEqual(res.status, 413);
  } finally { server.close(); }
});

// ══════════════ the required surface (requires.js, claude-code-bot#92) ══════════════
//
// These live HERE rather than in a requires.test.js of their own, and that is a
// direct consequence of the previous trial: mutate.js runs `kit.test.js` alone,
// so any rule whose only assertion sits in another file is UNMUTATED — it reports
// the same "0 survived" as a killed mutant. A separate suite would have looked
// tidier and been invisible to the harness.

const R = require('./requires');

const rqBeh = (body) => {
  const bs = parse(`behaviour BEH-R-1 "r"\n  source defined r\n  actor u\n${body}`);
  const { symbols } = resolve(bs);
  return { bs, symbols };
};
const rqGen = (body, bindings) => {
  const { bs, symbols } = rqBeh(body);
  return generate(bs[0], bindings, symbols);
};

// ── the coupling test: the table is not trusted, it is checked against emit() ──
//
// requires.js's requirement table is a hand-copy of the switch in emit(). A
// hand-copy that drifts is worse than no table, because it would state the app's
// obligations confidently and wrongly — the exact failure this project argues
// against. So every verb is run through the REAL generator twice: once with a
// binding the predicate calls satisfying, once with one it calls insufficient.
// If emit() changes and requires.js does not, this goes red.
//
// The `insufficient` binding is always a PRESENT key with the needed field
// missing, never an absent key — an absent key already refuses for the old
// reason, so testing that would prove nothing about this file.
const COUPLED = [
  { verb: 'state',     step: '  given thing:Ready',                          ok: { 'thing:Ready': { state: 'seed(page)' } },        thin: { 'thing:Ready': { note: 'no state' } } },
  { verb: 'opens',     step: '  when opens page:P',                          ok: { 'page:P': { route: './p' } },                    thin: { 'page:P': { urlPattern: '/p$' } } },
  { verb: 'lands',     step: '  then lands on page:P',                       ok: { 'page:P': { urlPattern: '/p$' } },               thin: { 'page:P': { route: './p' } } },
  { verb: 'activates', step: '  when activates button:B',                    ok: { 'button:B': { role: 'button', name: 'B' } },     thin: { 'button:B': { note: 'no locator' } } },
  { verb: 'sees',      step: '  then sees region:X',                         ok: { 'region:X': { locator: "locator('main')" } },    thin: { 'region:X': { note: 'no locator' } } },
  { verb: 'shows',     step: '  then shows region:X "hi"',                   ok: { 'region:X': { locator: "locator('main')" } },    thin: { 'region:X': { note: 'no locator' } } },
  {
    verb: 'attaches', step: '  when attaches file:F to field:D',
    ok:   { 'file:F': { fixture: { name: 'a.mp4', mimeType: 'video/mp4' } }, 'field:D': { label: 'File' } },
    thin: { 'file:F': { note: 'no fixture' },                                'field:D': { label: 'File' } },
  },
];

for (const c of COUPLED) {
  test(`requires: the table agrees with emit() for \`${c.verb}\` — satisfied binding generates`, () => {
    const g = rqGen(c.step, c.ok);
    assert.strictEqual(g.stats.ungenerated, 0, `emit() refused a binding requires.js calls satisfying:\n${g.code}`);
    const { bs } = rqBeh(c.step);
    const rep = R.requirements(bs, c.ok);
    assert.strictEqual(rep.insufficient.length, 0, 'requires.js called a generatable binding insufficient');
    assert.strictEqual(rep.missing.length, 0, 'requires.js called a present binding missing');
  });

  test(`requires: the table agrees with emit() for \`${c.verb}\` — thin binding refuses`, () => {
    const g = rqGen(c.step, c.thin);
    assert.strictEqual(g.stats.generated, 0, `emit() GENERATED from a binding requires.js calls too thin — a false green:\n${g.code}`);
    const { bs } = rqBeh(c.step);
    const rep = R.requirements(bs, c.thin);
    assert.ok(rep.insufficient.length > 0, 'requires.js called a non-generatable binding satisfied');
  });
}

test('requires: `fills` needs a label specifically, not any locator', () => {
  // The one case where the same noun kind owes different things to different
  // verbs. `attaches` reaches its field through loc(); `fills` only ever emits
  // getByLabel(). A role-and-name field is addressable and still unusable here.
  const body = '  when fills form:F with ?fields\n  provides form:F.fields = Item\n';
  const roleOnly = { 'field:Item': { role: 'textbox', name: 'Item' } };
  assert.strictEqual(rqGen(body, roleOnly).stats.generated, 0, 'getByLabel(undefined) is not a test');
  const { bs } = rqBeh(body);
  assert.ok(R.requirements(bs, roleOnly).insufficient.length > 0);
  // Positive control, so a version refusing every `fills` passes neither test.
  assert.ok(rqGen(body, { 'field:Item': { label: 'Item' } }).stats.generated > 0);
});

test('requires: every verb emit() handles has a row in the requirement table', () => {
  // The drift this cannot otherwise catch: someone adds a `case` to emit() and
  // requires.js silently reports no obligation for it, so the contract omits a
  // surface the app genuinely needs. Read from the source, because the switch is
  // the only place that list exists.
  const fs = require('fs'), path = require('path');
  const src = fs.readFileSync(path.join(__dirname, 'kit.js'), 'utf8');
  const emitBody = src.slice(src.indexOf('function emit('));
  const cases = [...emitBody.slice(0, emitBody.indexOf('\n}')).matchAll(/^\s*case '([a-z]+)':/gm)].map((m) => m[1]);
  assert.ok(cases.length >= 8, `read only ${cases.length} verbs out of emit() — the reader has stopped matching`);
  for (const v of cases) {
    assert.ok(Object.prototype.hasOwnProperty.call(R.VERBS, v), `emit() handles \`${v}\` and requires.js has no row for it`);
  }
  for (const v of Object.keys(R.VERBS)) {
    assert.ok(cases.includes(v), `requires.js has a row for \`${v}\` and emit() does not handle it`);
  }
});

// ── the three defects this file was written from, each pinned ──

test('requires: a page reached by both `opens` and `lands` owes BOTH keys', () => {
  // The defect in miniature. page:Home is bound, reports NO missing noun, counts
  // as 1/1 bound — and still refuses, because `lands` wants `urlPattern` and
  // `opens` wants `route`. Nothing in Kit said so before requires.js.
  const body = '  when opens page:Home\n  then lands on page:Home\n';
  const routeOnly = { 'page:Home': { route: './' } };
  const g = rqGen(body, routeOnly);
  assert.strictEqual(g.stats.generated, 1);
  assert.strictEqual(g.stats.ungenerated, 1);
  assert.deepStrictEqual(g.missing, [], 'the old diagnostic reports nothing here — that is the bug');

  const { bs } = rqBeh(body);
  assert.strictEqual(boundNouns(bs, routeOnly).bound, 1, 'boundNouns counts key presence, so it says fully bound');
  const rep = R.requirements(bs, routeOnly);
  assert.strictEqual(rep.insufficient.length, 1, 'requires.js is the only thing that can see this');
  assert.deepStrictEqual(rep.nouns[0].needs.filter((n) => !n.met).map((n) => n.id), ['urlPattern']);
  assert.deepStrictEqual(rep.nouns[0].needs.filter((n) => n.met).map((n) => n.id), ['route']);
});

test('requires: reasonsFor names the missing key rather than just "unbound"', () => {
  const { bs } = rqBeh('  then lands on page:Home\n');
  const [why] = R.reasonsFor(bs[0].steps[0], { 'page:Home': { route: './' } });
  assert.strictEqual(why.noun, 'page:Home');
  assert.strictEqual(why.need, 'urlPattern');
  assert.match(why.why, /binding exists but has no/);
  // And it must still distinguish the genuinely absent case, or it has merely
  // renamed one message.
  const [absent] = R.reasonsFor(bs[0].steps[0], {});
  assert.strictEqual(absent.why, 'no binding');
});

test('requires: `attaches` refuses a thin binding instead of emitting page.null (regression)', () => {
  // Before this change kit.js emitted, and COUNTED AS GENERATED:
  //     await page.null.setInputFiles(undefined);
  // A line that throws the moment it runs, contributing to the headline
  // percentage. A false green is strictly worse than the refusal the design is
  // built on, and no corpus caught it because all five were built backwards.
  const thin = { 'file:F': { note: 'exists, no fixture' }, 'field:D': { note: 'exists, no locator' } };
  const g = rqGen('  when attaches file:F to field:D', thin);
  assert.strictEqual(g.stats.generated, 0);
  assert.strictEqual(g.stats.ungenerated, 1);
  assert.doesNotMatch(g.code, /page\.null|undefined/, 'generated a line that cannot run');
});

test('requires: a field named only by a `provides` value is still a required surface', () => {
  // trial-lend's field:DueDate appears in no step, only inside
  // `provides form:NewLoan.fields = ItemName, Borrower, DueDate`. boundNouns()
  // walks step refs, so every existing measurement is blind to it while the app
  // must genuinely have it.
  const { bs } = rqBeh('  when fills form:F with ?fields\n  provides form:F.fields = Seen, OnlyProvided\n');
  const nouns = R.requirements(bs, {}).nouns.map((n) => n.noun);
  assert.ok(nouns.includes('field:OnlyProvided'));
  assert.ok(!boundNouns(bs, {}).referenced.has('field:OnlyProvided'), 'if boundNouns ever sees it, this note is stale');
});

test('requires: a `form:` noun is not an obligation on the app', () => {
  // The same measurement wrong in the other direction. emit()'s `fills` case
  // never looks up `bindings["form:X"]` — it reads the resolved field names — so
  // a form noun can NEVER be bound, yet boundNouns() counts it in the
  // denominator. Any corpus using a form is therefore capped below 100% bound by
  // a noun no binding could ever satisfy.
  const { bs } = rqBeh('  when fills form:F with ?fields\n  provides form:F.fields = Item\n');
  assert.ok(boundNouns(bs, {}).referenced.has('form:F'), 'boundNouns counts the form');
  assert.ok(!R.requirements(bs, {}).nouns.some((n) => n.noun === 'form:F'), 'requires.js does not');
});

// ── controls: the report must be able to say "nothing needed" ──

test('requires: a fully satisfied corpus reports no requirements outstanding', () => {
  // Without this, a version that called everything unsatisfied would pass every
  // test above.
  const body = '  when opens page:P\n  then sees button:B\n';
  const rep = R.requirements(rqBeh(body).bs, { 'page:P': { route: './p' }, 'button:B': { role: 'button', name: 'B' } });
  assert.strictEqual(rep.missing.length, 0);
  assert.strictEqual(rep.insufficient.length, 0);
  assert.strictEqual(rep.satisfied.length, 2);
  assert.match(R.render('x', rep), /Every noun this corpus references is satisfied/);
});

test('requires: the real corpora are unchanged by the emit() fixes', () => {
  // The fixes tighten two verbs, so they COULD have moved published numbers.
  // Measured rather than assumed: snip-it is the only corpus using attaches or
  // fills, and its bindings carry both keys, so nothing it generates changes.
  const fs = require('fs'), path = require('path');
  const src = fs.readFileSync(path.join(__dirname, 'behaviours', 'snip-it.beh'), 'utf8');
  const bs = parse(src);
  const { symbols } = resolve(bs);
  const bindings = JSON.parse(fs.readFileSync(path.join(__dirname, 'bindings.json'), 'utf8'));
  let generated = 0, ungenerated = 0;
  for (const b of bs) {
    const g = generate(b, bindings, symbols);
    generated += g.stats.generated; ungenerated += g.stats.ungenerated;
  }
  assert.strictEqual(generated, 28, 'snip-it generated line count moved');
  assert.strictEqual(ungenerated, 2, 'snip-it ungenerated count moved');
});

// ══════════════ noun convergence (converge.js, claude-code-bot#92) ══════════════

const CV = require('./converge');

const cvDir = () => fixture({
  'a.beh': 'behaviour BEH-A1 "x"\n  when opens page:Today\n  when activates checkbox:HabitDone\n',
  'b.beh': 'behaviour BEH-B1 "x"\n  when opens page:Today\n  when activates checkbox:HabitItem\n',
  'same.beh': 'behaviour BEH-S1 "x"\n  when opens page:Today\n  when activates checkbox:HabitDone\n',
  'kindonly.beh': 'behaviour BEH-K1 "x"\n  when opens screen:Today\n  when activates checkbox:HabitDone\n',
  'empty.beh': '# only a comment\n',
});

test('converge: a corpus compared with an identical one is total agreement — the CONTROL', () => {
  // Without this, a tool that reported low agreement unconditionally would pass
  // every "they disagreed" assertion below and be measuring nothing.
  const dir = cvDir();
  const a = CV.load('a', { dir }), s = CV.load('same', { dir });
  const r = CV.compare(a, s);
  assert.strictEqual(r.strict.ratio, 1);
  assert.deepStrictEqual(r.onlyA, []);
  assert.deepStrictEqual(r.onlyB, []);
});

test('converge: two names for one control is a disagreement, not a near-miss', () => {
  // The finding this tool was built to make sayable. `checkbox:HabitDone` and
  // `checkbox:HabitItem` are the same control described twice; Kit binds by
  // noun, so they are two bindings and nothing anywhere says they collide.
  const dir = cvDir();
  const r = CV.compare(CV.load('a', { dir }), CV.load('b', { dir }));
  assert.strictEqual(r.shared.length, 1, 'only the page is shared');
  assert.deepStrictEqual(r.shared, ['page:Today']);
  assert.ok(r.onlyA.includes('checkbox:HabitDone'));
  assert.ok(r.onlyB.includes('checkbox:HabitItem'));
  // And loose matching must NOT rescue it — if it did, the headline claim that
  // the divergence survives normalisation would be false.
  assert.strictEqual(r.loose.ratio, r.strict.ratio);
});

test('converge: the loose score forgives kind and case, and says which nouns collided', () => {
  // The other direction, and the one that keeps the loose score honest as an
  // OPTIMISTIC bound: `page:Today` vs `screen:Today` really is one thing named
  // twice, so a normaliser that could not see it would understate agreement.
  const dir = cvDir();
  const r = CV.compare(CV.load('a', { dir }), CV.load('kindonly', { dir }));
  assert.ok(r.loose.ratio > r.strict.ratio, 'ignoring kind must forgive something here');
  assert.deepStrictEqual(r.kindMismatch, [['page:Today', 'screen:Today']]);
  assert.ok(!r.onlyA.includes('page:Today'), 'a loose match must not also be reported as unique to A');
});

test('converge: loose() flattens kind, case and separators — and stops there', () => {
  assert.strictEqual(CV.loose('button:LogToday'), 'logtoday');
  assert.strictEqual(CV.loose('control:log-today'), 'logtoday');
  assert.strictEqual(CV.loose('Button:LOG_TODAY'), 'logtoday');
  // The line it must not cross. Deciding these two mean the same control is a
  // semantic judgement, and faking it here would let the tool report agreement
  // that the generator will not honour — two bindings either way.
  assert.notStrictEqual(CV.loose('button:LogToday'), CV.loose('button:MarkDone'));
});

test('converge: nothing to compare is null, never a ratio', () => {
  // 0-of-0 must not render as 0% ("they agreed on nothing") or 100% ("perfect
  // agreement"). Both are readings of a measurement that did not happen.
  assert.strictEqual(CV.jaccard(new Set(), new Set()), null);
  assert.strictEqual(CV.jaccard(new Set(['x']), new Set()).ratio, 0);
});

test('converge: total disagreement SAYS so rather than printing an empty section', () => {
  const dir = fixture({
    'p.beh': 'behaviour BEH-P "x"\n  when opens page:One\n',
    'q.beh': 'behaviour BEH-Q "x"\n  when opens page:Two\n',
  });
  const r = CV.compare(CV.load('p', { dir }), CV.load('q', { dir }));
  assert.deepStrictEqual(r.shared, []);
  const out = CV.render([], [{ a: 'p', b: 'q', r }]);
  assert.match(out, /AGREED ON: nothing — not one noun in common/,
    'an empty section reports the most important finding by absence, which reads as an oversight');
});

test('converge: a corpus it cannot read is could-not-look, not zero agreement', () => {
  const dir = cvDir();
  assert.match(CV.load('nope', { dir }).fatal, /no corpus/);
  assert.match(CV.load('empty', { dir }).fatal, /zero behaviours/);
});

test('converge: main REFUSES on an unreadable corpus rather than comparing around it', () => {
  // Found by a surviving mutant, not by design: the test above proves `load()`
  // reports the fault, and `main()` deleting its own refusal SURVIVED, because
  // nothing here had ever run main. A fault detected and then walked past is
  // worse than one never detected — it produces a confident number
  // ([[an-uncaught-mutation-is-a-finding]]).
  let said = '';
  const err = console.error, w = process.stderr.write.bind(process.stderr);
  process.stderr.write = (s) => { said += s; return true; };
  console.error = (...a) => { said += a.join(' ') + '\n'; };
  let code;
  try {
    code = CV.main(['node', 'converge.js', 'snip-it', 'definitely-not-a-corpus']);
  } catch (e) {
    code = `threw: ${e.message}`;
  } finally { process.stderr.write = w; console.error = err; }
  assert.strictEqual(code, 2, `expected could-not-look, got ${code}`);
  assert.match(said, /no corpus/);
});

test('converge: it is a measurement and not a gate — no threshold, no exit 1', () => {
  // Deliberate: what counts as enough agreement is a product judgement about how
  // much reconciliation a user should do, and a number invented here would
  // answer it quietly. Read from source, because "there is no threshold" is a
  // property of the file, not of any one run.
  const fs = require('fs'), path = require('path');
  const src = fs.readFileSync(path.join(__dirname, 'converge.js'), 'utf8');
  const returns = [...src.matchAll(/^\s*return (\d);/gm)].map((m) => m[1]);
  assert.ok(!returns.includes('1'), `converge.js returns 1 somewhere — it has grown a gate: ${returns}`);
  assert.ok(returns.includes('2'), 'it must still be able to say it could not look');
});

// ── the write contract, from the server side ────────────────────────────────
//
// The other half of the pin in `ui/src/test/fixtures/write-contract.json`. The
// frontend suite stubs `fetch`, so it can only ever prove the client emits the
// paths and bodies IT believes in; this half takes the same literals out of the
// same file and drives them into a real `ui.js` over a real socket, then asserts
// the corpus on disk.
//
// Neither suite alone can see the two disagreeing. A client that built
// `/behaviour/BEH-G/steps` (singular) would keep the frontend green over its own
// stub and keep this file green over its own hand-typed path — the seam is
// exactly where a green suite proves the mock.

const CONTRACT = JSON.parse(fsx.readFileSync(
  pathx.join(__dirname, 'ui', 'src', 'test', 'fixtures', 'write-contract.json'), 'utf8'));

test('the write contract fixture is not empty, and covers both routes', () => {
  // A loop over an empty list passes, silently and forever. The fixture is the
  // shared artefact both suites read, so the population it declares is itself
  // worth asserting.
  assert.ok(CONTRACT.requests.length >= 4, `only ${CONTRACT.requests.length} contract requests`);
  const paths = CONTRACT.requests.map((r) => r.path);
  assert.ok(paths.some((p) => /\/steps$/.test(p)), 'no add-step request in the contract');
  assert.ok(paths.some((p) => /\/review$/.test(p)), 'no review request in the contract');
  assert.ok(paths.some((p) => /\/behaviours$/.test(p)), 'no add-behaviour request in the contract');
  assert.ok(paths.some((p) => /%20/.test(p)), 'nothing in the contract exercises a percent-encoded name');
  assert.ok(paths.some((p) => /\/bindings$/.test(p)), 'no bind request in the contract');
  assert.ok(CONTRACT.requests.some((r) => r.expect.status === 409), 'the contract only covers the happy path');
  // The bind route's whole reason for existing beside the other three is that
  // it writes a DIFFERENT file, so the fixture has to declare one — and the
  // sharing report has to be exercised in both directions or the empty case is
  // never distinguished from an unimplemented one.
  const binds = CONTRACT.requests.filter((r) => /\/bindings$/.test(r.path));
  assert.ok(binds.every((r) => r.targetFile === 'bindings.json'), 'a bind request does not declare its target file');
  assert.ok(binds.some((r) => (r.expect.sharedWith || []).length > 0), 'nothing in the contract binds a shared noun');
  assert.ok(binds.some((r) => r.expect.sharedWith && r.expect.sharedWith.length === 0), 'nothing in the contract binds an unshared noun');
});

test('the contract pins the READ path too, not only the writes', () => {
  // The gap `mutate-ui.js` found: every write path was driven through a
  // percent-encoded app name on both sides, and the one GET the client builds
  // was pinned by nobody, so dropping `encodeURIComponent` from `fetchProject`
  // left the whole frontend suite green.
  assert.ok(CONTRACT.reads && CONTRACT.reads.length >= 2, 'the contract declares no reads');
  assert.ok(CONTRACT.reads.some((r) => /%20/.test(r.path)),
    'no read in the contract exercises a percent-encoded name');
  assert.ok(CONTRACT.reads.some((r) => r.expect.status === 404),
    'the reads only cover the happy path');
});

for (const req of CONTRACT.reads) {
  test(`contract: ${req.method} ${req.path} — ${req.what}`, async () => {
    const dir = fixture(CONTRACT.corpus);
    const bindings = pathx.join(dir, 'bindings.json');
    fsx.writeFileSync(bindings, `${JSON.stringify(CONTRACT.bindings, null, 2)}\n`);
    const server = await ui.serve({ dir, bindings, port: 0, host: '127.0.0.1' });
    try {
      const { port } = server.address();
      const res = await get(port, req.path);
      assert.strictEqual(res.status, req.expect.status,
        `${req.path} answered ${res.status}: ${res.body}`);

      const body = JSON.parse(res.body);
      for (const [k, v] of Object.entries(req.expect.jsonMustHave || {})) {
        // The DECODED name, which is the half that matters: a server that
        // answered 200 for `/api/projects/two%20words` while serving some other
        // corpus would satisfy a status-only assertion.
        assert.strictEqual(body[k], v, `${req.path} answered with ${k}=${JSON.stringify(body[k])}`);
      }
      if (req.expect.reasonMustExist) {
        assert.ok(body.reason, 'a 404 must carry a sentence, not just a code');
      }
    } finally {
      await new Promise((r) => server.close(r));
    }
  });
}

for (const req of CONTRACT.requests) {
  test(`contract: ${req.method} ${req.path} — ${req.what}`, async () => {
    // A fresh corpus per request: these write, and a shared directory would make
    // them depend on the order they run in.
    const dir = fixture(CONTRACT.corpus);
    // `bindings.json` lives beside the corpora in the fixture only; in the real
    // tree it sits one level up, which is exactly why `serve` takes its path
    // rather than deriving it — a test that had to write into the repo's own
    // bindings file could not be run twice.
    const bindings = pathx.join(dir, 'bindings.json');
    fsx.writeFileSync(bindings, `${JSON.stringify(CONTRACT.bindings, null, 2)}\n`);

    const target = req.targetFile
      ? pathx.join(dir, req.targetFile)
      : pathx.join(dir, `${req.call.args[0]}.beh`);
    const before = fsx.readFileSync(target, 'utf8');
    const server = await ui.serve({ dir, bindings, port: 0, host: '127.0.0.1' });
    try {
      const { port } = server.address();
      const res = await post(port, req.path, req.body, null, req.contentType);
      assert.strictEqual(res.status, req.expect.status,
        `${req.path} answered ${res.status}: ${res.body}`);

      const after = fsx.readFileSync(target, 'utf8');
      if (req.expect.fileMustNotChange) {
        // The FILE, not the status. A 409 that still wrote is the failure being
        // guarded against, and a status-only assertion passes straight over it.
        assert.strictEqual(after, before, 'a refused edit changed the corpus');
        assert.ok(JSON.parse(res.body).reason, 'a refusal must carry a sentence, not just a code');
      } else {
        assert.ok(after.includes(req.expect.fileMustMatch),
          `the corpus does not contain ${req.expect.fileMustMatch}`);
        // The absence half, and it is the whole point of the review route: an
        // approval that APPENDED `review approved` would satisfy the line above
        // while leaving `review unreviewed` in the file, and the corpus would
        // then say two things about the same behaviour.
        if (req.expect.fileMustNotMatch) {
          assert.ok(!after.includes(req.expect.fileMustNotMatch),
            `the corpus still contains ${req.expect.fileMustNotMatch}`);
        }
        assert.strictEqual(JSON.parse(res.body).committed, false);
        // The namespace report, asserted from the fixture rather than from what
        // the server happened to send. `sharedWith` is the mechanism replacing
        // the habit `bindings.json`'s own comment describes, so a response that
        // stopped carrying it — or carried the wrong corpora — must go red here
        // and not merely look tidier.
        if (req.expect.sharedWith) {
          assert.deepStrictEqual(JSON.parse(res.body).sharedWith, req.expect.sharedWith);
        }
      }
    } finally { server.close(); }
  });
}

test('🔴 a bind and the RE-READ that follows it use the same bindings file', async () => {
  // The defect running the server found and that every green test missed.
  //
  // `--bindings` shipped so a demo could exercise the write without dirtying
  // the repo it measures. The WRITE honoured it and `project.js` read
  // `__dirname/bindings.json` regardless, so the POST returned 200, the file
  // on disk changed, and the page re-read the OTHER file and showed the same
  // refusal. The loop's entire payoff — bind it and watch the comment become a
  // test — was silently absent.
  //
  // Nothing above could see it: the contract tests assert the FILE after a
  // write and never re-read the projection, and the frontend suite reads a
  // fixture. So this test is deliberately shaped as the loop rather than as
  // the write: refusal → bind → re-read → assertion.
  const dir = fixture({
    'zeta.beh': 'behaviour BEH-Z "zeta"\n  actor engineer\n  then sees button:Ping\n',
  });
  const bindings = pathx.join(dir, 'bindings.json');
  fsx.writeFileSync(bindings, '{}\n');

  const server = await ui.serve({ dir, bindings, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();

    const before = ui.route('GET', '/api/projects/zeta', { dir, bindings }).body;
    assert.deepStrictEqual(before.generated[0].missing, ['button:Ping'], 'the noun was not unbound to begin with');
    assert.match(before.generated[0].code, /UNGENERATED/);

    const res = await post(port, '/api/projects/zeta/bindings',
      { noun: 'button:Ping', binding: { role: 'button', name: 'Ping' } });
    assert.strictEqual(res.status, 200, res.body);

    // The re-read, through the same route the browser calls after a write.
    const after = ui.route('GET', '/api/projects/zeta', { dir, bindings }).body;
    assert.deepStrictEqual(after.generated[0].missing, [],
      'the page re-read a DIFFERENT bindings file from the one the write landed in');
    assert.match(after.generated[0].code, /getByRole\("button", \{ name: "Ping" \}\)/);
    assert.doesNotMatch(after.generated[0].code, /UNGENERATED/);
    assert.deepStrictEqual(after.requires.missing, []);
  } finally { server.close(); }
});

test('binding to an app with no corpus is a 404, not a write against nothing', async () => {
  // A mutant removing this check SURVIVED, so nothing was exercising it. It is
  // not a tidiness rule: `sharedWith` excludes the app you name, and an app
  // that does not exist excludes nothing and matches nothing — so the write
  // would succeed and report "no other corpus uses this noun" without having
  // looked at a corpus at all. The quiet wrong answer, not a loud one.
  const dir = fixture({ 'theta.beh': 'behaviour BEH-T "theta"\n  actor engineer\n  then sees button:Go\n' });
  const bindings = pathx.join(dir, 'bindings.json');
  fsx.writeFileSync(bindings, '{}\n');
  const before = fsx.readFileSync(bindings, 'utf8');

  const server = await ui.serve({ dir, bindings, port: 0, host: '127.0.0.1' });
  try {
    const { port } = server.address();
    const res = await post(port, '/api/projects/nosuchapp/bindings',
      { noun: 'button:Go', binding: { role: 'button', name: 'Go' } });
    assert.strictEqual(res.status, 404, res.body);
    assert.strictEqual(JSON.parse(res.body).error, 'no-such-project');
    assert.ok(JSON.parse(res.body).known.includes('theta'), 'the refusal must say what does exist');
    assert.strictEqual(fsx.readFileSync(bindings, 'utf8'), before, 'a refused bind wrote to the file');
  } finally { server.close(); }
});

test('contract: the paths in the fixture are the ones the CLIENT builds, character for character', () => {
  // The frontend asserts this from its side by running `encodeURIComponent`;
  // here it is re-derived from the same arguments, so a hand-edited fixture path
  // cannot make both suites agree on something the client would never send. This
  // is the assertion that catches a red test being "fixed" by editing the pin.
  const built = {
    addStep: (app, id) => `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/steps`,
    setReview: (app, id) => `/api/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}/review`,
    addBehaviour: (app) => `/api/projects/${encodeURIComponent(app)}/behaviours`,
    addBinding: (app) => `/api/projects/${encodeURIComponent(app)}/bindings`,
  };
  for (const req of CONTRACT.requests) {
    const [app, id] = req.call.args;
    // Looked up, never defaulted: a `fn` this table does not know about would
    // otherwise fall through to the `addBehaviour` spelling and quietly assert
    // the wrong path — the same "empty means two things" failure the read side
    // records against itself.
    const build = built[req.call.fn];
    assert.ok(build, `the contract calls ${req.call.fn}, which this test does not know how to build a path for`);
    assert.strictEqual(req.path, build(app, id), `${req.what}: the fixture path is not what the client builds`);
  }
});

test('nothing still advertises the UI as read-only, now that it writes', () => {
  // Found by looking at a screenshot, which is not a method. The header said
  // "read-only — the UI cannot write the corpus" for a whole PR after the write
  // routes shipped, and so did two READMEs: all three were TRUE when written and
  // none of them had any reason to be re-read.
  //
  // The invariant is capability-vs-claim, not a word: while ui.js has a POST
  // handler, no document or banner may say it has none. That is why this is
  // conditional on the code rather than a grep for a phrase — if decision 2 were
  // ever reversed, the claim becomes true again and this test stops firing
  // instead of demanding the claim be deleted.
  const uiSrc = fsx.readFileSync(pathx.join(__dirname, 'ui.js'), 'utf8');
  const canWrite = /function write\(/.test(uiSrc) && /writer\.addStep|writer\.addBehaviour/.test(uiSrc);
  if (!canWrite) return; // the claim would be true; nothing to enforce

  const claims = [
    ['README.md', pathx.join(__dirname, '..', '..', 'README.md')],
    ['ui/README.md', pathx.join(__dirname, 'ui', 'README.md')],
    ['ui/src/App.tsx', pathx.join(__dirname, 'ui', 'src', 'App.tsx')],
  ];
  for (const [name, file] of claims) {
    const text = fsx.readFileSync(file, 'utf8')
      // A comment explaining that the banner USED to say this is not the banner
      // saying it. Without this the fix for the finding trips its own check.
      .replace(/\/\*[\s\S]*?\*\//g, ' ')
      .replace(/^\s*(\/\/|#).*$/gm, ' ');
    assert.strictEqual(/\bread-only\b/i.test(text), false,
      `${name} still calls the UI read-only, but ui.js has write handlers`);
    assert.strictEqual(/cannot write/i.test(text), false,
      `${name} still says the UI cannot write, but ui.js has write handlers`);
  }
});

test('nothing still says ui.js cannot serve the bundle, now that it does', () => {
  // The second instance of the same shelf-life defect, and it is here because
  // the first one cost a PR to notice. `ui/README.md` carried a whole paragraph
  // saying **"`ui.js` does not serve this bundle"** with the reason it did not,
  // and that paragraph was TRUE for as long as it was there. Nothing in the
  // suite, the sweep or the mutation harness has any reason to re-read a true
  // sentence ([[a-capability-claim-has-a-shelf-life]]).
  //
  // Conditional on the code, exactly like the check above: it fires only while
  // ui.js actually has a bundle handler, so reversing the decision retires the
  // check rather than demanding a true sentence be deleted.
  const uiSrc = fsx.readFileSync(pathx.join(__dirname, 'ui.js'), 'utf8');
  const canServe = /function bundle\(/.test(uiSrc) && /DIST_DIR/.test(uiSrc);
  if (!canServe) return; // the claim would be true; nothing to enforce

  const claims = [
    ['README.md', pathx.join(__dirname, '..', '..', 'README.md')],
    ['ui/README.md', pathx.join(__dirname, 'ui', 'README.md')],
    ['docs/design/ui.md', pathx.join(__dirname, '..', '..', 'docs', 'design', 'ui.md')],
  ];
  for (const [name, file] of claims) {
    if (!fsx.existsSync(file)) continue;
    const text = fsx.readFileSync(file, 'utf8')
      .replace(/\/\*[\s\S]*?\*\//g, ' ')
      .replace(/^\s*(\/\/|#).*$/gm, ' ')
      // ⚠️ Markdown, stripped before matching, and this line is the whole reason
      // the check works. The first version matched `ui\.js\s+does not serve` and
      // the sentence it was written to catch is **`ui.js` does not serve this
      // bundle** — a backtick sits between the name and the space, so the probe
      // that put the claim back scored a clean pass. A check that has only ever
      // been green has not been shown to discriminate
      // ([[ship-the-check-while-its-red]]).
      //
      // ⚠️ `\x60` is a backtick and must NOT be written as one. `jsDeclarationCount`
      // does not recognise regex literals, so a bare backtick in this character
      // class reads as the start of a template literal and blanks everything up to
      // the next backtick in the FILE — the sibling test above pins exactly that
      // blind spot. It cost nothing for as long as this was the last assertion in
      // the file, because the swallowed run reached EOF and was discarded. The
      // moment a section was appended below, the scan resumed in the wrong state,
      // the two independent counts disagreed and coverage went unavailable — a
      // fail-safe firing correctly, hundreds of lines from its cause.
      .replace(/[\x60*_]/g, '')
      .replace(/\s+/g, ' ');

    // Present tense only. This README now *describes* the old claim — "this
    // section used to say ui.js did NOT serve the bundle" — and a check that
    // could not tell a retraction from the thing retracted would forbid the
    // sentence that fixes it.
    // ⚠️ The contractions are written `doesn.t`, not `doesn't`, and must stay
    // that way. `jsDeclarationCount` does not recognise regex literals — the
    // sibling test above pins the same blind spot for a backtick — so an
    // apostrophe here reads as the start of a string, and the scan swallows
    // every `test(` between it and the next apostrophe in the file. That cost
    // nothing while this was the last assertion in the file, because the run
    // to end-of-file is discarded. The moment ANY section was appended below,
    // eight declarations vanished from the count, the independent counts
    // disagreed, and coverage went unavailable — the fail-safe doing its job,
    // for a reason nowhere near where it surfaced. `.` matches the apostrophe.
    assert.strictEqual(/\b(does not|doesn.t|cannot|can not|can.t|will not|won.t) serve (this|the) bundle/i.test(text), false,
      `${name} still says ui.js does not serve the bundle, but ui.js has a bundle handler`);
  }
});

// ── the mutation marker: surviving a kill that no handler can catch ─────────
//
// `mutate.js` runs THIS suite once per mutant, so a test that wrote the real
// marker at the repo root would delete the live marker of the run executing it.
// Every test below builds its own marker over a temp directory via
// `createMarker`, which is why that factory exists.
section('mutation marker');

const { createMarker } = require('./mutation-marker');
// A killed run, reconstructed exactly: `arm()` records the originals, then the
// file on disk is made wrong and nothing gets to clean up after it.
const killedRun = (files, damage) => {
  const root = fixture(files);
  const m = createMarker({ root, markerPath: pathx.join(root, 'MUTATION-IN-PROGRESS') });
  const originals = {};
  for (const f of Object.keys(files)) originals[f] = files[f];
  m.arm({
    tool: 'mutate-test.js', base: '.', originals, warn: 'w', restoreAll: () => {},
  });
  for (const [f, wrong] of Object.entries(damage)) fsx.writeFileSync(pathx.join(root, f), wrong);
  return { root, m };
};
const silent = { log: () => {}, error: () => {} };

test('marker: a run killed mid-mutant is restored exactly from the marker alone', () => {
  const { root, m } = killedRun({ 'a.js': 'const ok = 1\n' }, { 'a.js': 'const ok = 999\n' });
  assert.strictEqual(fsx.readFileSync(pathx.join(root, 'a.js'), 'utf8'), 'const ok = 999\n');

  assert.strictEqual(m.recover(silent), 0);
  assert.strictEqual(fsx.readFileSync(pathx.join(root, 'a.js'), 'utf8'), 'const ok = 1\n');
  // The marker goes with it, or the next run refuses forever.
  assert.strictEqual(fsx.existsSync(m.MARKER), false);
});

// The whole reason recovery exists rather than `git checkout -- <file>`: when
// the 211th's mutant was found, an uncommitted edit to mutate-ui.js was sitting
// beside it, and checking the file out would have destroyed that work.
test('marker: recovery restores ONLY what was mutated, leaving other work alone', () => {
  const { root, m } = killedRun(
    { 'a.js': 'const ok = 1\n', 'b.js': 'const b = 1\n' },
    { 'a.js': 'const ok = 999\n', 'b.js': 'REAL UNCOMMITTED WORK\n' },
  );
  // `b.js` is not in the damage the harness caused — it is a human's edit that
  // happens to be in the same tree. Recovery must not have an opinion on it...
  const originals = m.payload().files;
  delete originals['b.js'];
  fsx.writeFileSync(m.MARKER, fsx.readFileSync(m.MARKER, 'utf8').replace(/\{"tool".*\}/, JSON.stringify({ tool: 't', base: '.', files: originals })));

  assert.strictEqual(m.recover(silent), 0);
  assert.strictEqual(fsx.readFileSync(pathx.join(root, 'a.js'), 'utf8'), 'const ok = 1\n');
  assert.strictEqual(fsx.readFileSync(pathx.join(root, 'b.js'), 'utf8'), 'REAL UNCOMMITTED WORK\n');
});

// The silent hazard, and the one that made this worth building: without the
// refusal, the next run reads the leftover mutant as pristine and restores TO
// it, so the defect becomes permanent while the run still reports every mutant
// killed. A clean sweep over a poisoned baseline is the worst possible output.
test('marker: a stale marker REFUSES the next run rather than baselining the mutant', () => {
  const { m } = killedRun({ 'a.js': 'const ok = 1\n' }, { 'a.js': 'const ok = 999\n' });
  const codes = [];
  m.refuseIfStale('mutate-test.js', (c) => codes.push(c), silent);
  assert.deepStrictEqual(codes, [2], 'a stale marker must stop the run with exit 2');
});

test('marker: the positive control — with no marker, a run is NOT refused', () => {
  const root = fixture({ 'a.js': 'x\n' });
  const m = createMarker({ root, markerPath: pathx.join(root, 'MUTATION-IN-PROGRESS') });
  const codes = [];
  m.refuseIfStale('mutate-test.js', (c) => codes.push(c), silent);
  assert.deepStrictEqual(codes, [], 'no marker means nothing to refuse');
});

// "Could not restore" must never read as "nothing was wrong". A marker written
// by an older build of these tools carries no originals, and the only honest
// answer is exit 2 plus the instruction not to reach for `git checkout --`.
test('marker: a marker with no originals is could-not-look (2), not a silent success', () => {
  const root = fixture({ 'a.js': 'x\n' });
  const markerPath = pathx.join(root, 'MUTATION-IN-PROGRESS');
  fsx.writeFileSync(markerPath, 'mutate.js is running and the working tree is deliberately WRONG.\n');
  const m = createMarker({ root, markerPath });

  assert.strictEqual(m.recover(silent), 2);
  // And it must NOT drop a marker it could not act on — that would erase the
  // only remaining sign that a mutant is live.
  assert.strictEqual(fsx.existsSync(markerPath), true);
});

test('marker: nothing to recover is 0, and says so rather than inventing damage', () => {
  const root = fixture({ 'a.js': 'x\n' });
  const m = createMarker({ root, markerPath: pathx.join(root, 'MUTATION-IN-PROGRESS') });
  assert.strictEqual(m.recover(silent), 0);
});

// The two tools mutate different trees (`behaviour-ast` and `behaviour-ast/ui`)
// and share one marker path, so recovery keys off the base recorded IN the
// marker. A version that assumed its own base would write the right contents
// to the wrong paths — and report success doing it.
test('marker: recovery uses the base recorded in the marker, not the base of the caller', () => {
  const root = fixture({ 'ui/src/a.ts': 'const ok = 1\n' });
  const markerPath = pathx.join(root, 'MUTATION-IN-PROGRESS');
  const m = createMarker({ root, markerPath });
  m.arm({ tool: 'mutate-ui.js', base: 'ui', originals: { 'src/a.ts': 'const ok = 1\n' }, warn: 'w', restoreAll: () => {} });
  fsx.writeFileSync(pathx.join(root, 'ui/src/a.ts'), 'const ok = 999\n');

  assert.strictEqual(m.recover(silent), 0);
  assert.strictEqual(fsx.readFileSync(pathx.join(root, 'ui/src/a.ts'), 'utf8'), 'const ok = 1\n');
});

test('marker: the marker tells a reader how to recover, and warns off git checkout', () => {
  const { m } = killedRun({ 'a.js': 'x\n' }, {});
  const text = fsx.readFileSync(m.MARKER, 'utf8');
  assert.ok(/--recover/.test(text), 'the marker must name the command that fixes it');
  assert.ok(/git checkout/.test(text), 'the marker must warn off the destructive manual fix');
  // The prose has to come first: the payload is machine-readable bulk and the
  // first thing anyone does with this file is read the top of it.
  assert.ok(text.indexOf('deliberately WRONG') < text.indexOf('{"tool"'));
});

// ── one command to run Kit (kit#37) ──────────────────────────────────────────
// The failure this guards is the quiet one. A fresh clone with no bundle serves
// a 503, which at least says something is wrong; a bundle built before your
// last edit serves a Kit that is silently not the one in your tree.
const start = require('../../start.js');

// Sets mtimes explicitly rather than writing files and hoping: a fixture whose
// files are all created inside the same millisecond makes "newer than" untestable.
const agedTree = (files) => {
  const dir = fixture(files.paths);
  for (const [f, seconds] of Object.entries(files.ages)) {
    const t = new Date(Date.now() - seconds * 1000);
    fsx.utimesSync(pathx.join(dir, f), t, t);
  }
  return dir;
};

test('start: a fresh clone has no bundle at all, which is maximally stale', () => {
  const dir = agedTree({ paths: { 'index.html': '<html>', 'src/App.tsx': 'x' }, ages: {} });
  assert.strictEqual(start.needsBuild(dir, pathx.join(dir, 'dist', 'index.html')), true);
});

test('start: a bundle older than a source file is rebuilt', () => {
  const dir = agedTree({
    paths: { 'src/App.tsx': 'x', 'dist/index.html': 'built' },
    ages: { 'dist/index.html': 60, 'src/App.tsx': 10 },
  });
  assert.strictEqual(start.needsBuild(dir, pathx.join(dir, 'dist', 'index.html')), true);
});

// Note `src` itself is aged, not just the file in it. A directory's mtime moves
// when a file is ADDED OR REMOVED in it, and that has to count as stale — the
// deletion of a component changes the bundle and touches no surviving file. So
// a fixture that ages the file but not its directory is not a tree that can
// exist, and the first version of this test failed for exactly that reason.
test('start: a bundle newer than every source file is left alone', () => {
  const dir = agedTree({
    paths: { 'src/App.tsx': 'x', 'index.html': '<html>', 'dist/index.html': 'built' },
    ages: { 'dist/index.html': 10, 'src/App.tsx': 60, src: 60, 'index.html': 60 },
  });
  assert.strictEqual(start.needsBuild(dir, pathx.join(dir, 'dist', 'index.html')), false);
});

test('start: DELETING a source file is stale too, though no surviving file changed', () => {
  const dir = agedTree({
    paths: { 'src/App.tsx': 'x', 'src/Gone.tsx': 'y', 'index.html': '<html>', 'dist/index.html': 'built' },
    ages: { 'dist/index.html': 10, 'src/App.tsx': 60, 'src/Gone.tsx': 60, src: 60, 'index.html': 60 },
  });
  assert.strictEqual(start.needsBuild(dir, pathx.join(dir, 'dist', 'index.html')), false);
  fsx.unlinkSync(pathx.join(dir, 'src', 'Gone.tsx'));
  assert.strictEqual(start.needsBuild(dir, pathx.join(dir, 'dist', 'index.html')), true);
});

// The reason SOURCE_ENTRIES is more than `src`. A dependency bump changes the
// bundle exactly as much as a component does, and a staleness check that only
// watches src serves yesterday's dependencies out of a bundle it believes fresh.
test('start: a lockfile newer than the bundle counts as stale, not just src', () => {
  const dir = agedTree({
    paths: { 'src/App.tsx': 'x', 'package-lock.json': '{}', 'dist/index.html': 'built' },
    ages: { 'dist/index.html': 30, 'src/App.tsx': 60, 'package-lock.json': 5 },
  });
  assert.strictEqual(start.needsBuild(dir, pathx.join(dir, 'dist', 'index.html')), true);
});

// An interrupted `npm ci` leaves a node_modules that exists and cannot build.
// "Is the directory there" and "can it build" are different questions and only
// the second one is worth asking.
test('start: a node_modules with no vite binary still needs installing', () => {
  const half = agedTree({ paths: { '.package-lock.json': '{}' }, ages: {} });
  assert.strictEqual(start.needsInstall(half), true);
  const done = agedTree({ paths: { '.bin/vite': '#!/bin/sh' }, ages: {} });
  assert.strictEqual(start.needsInstall(done), false);
});

// ── the harness must name its own failure (kit#39) ───────────────────────────
// mutate-ui.js runs its loop at require time, so its classifier cannot be
// imported and driven the way mutation-marker.js can. It is asserted from
// source for the same reason writer.js's "no path to git" test is: the claim is
// about what the file contains, and the event it guards against has never been
// reproduced on demand.
//
// What went wrong: the unmatched branch of run() returned a fixed sentence
// saying only that nothing had identified itself. Three runs stopped on it and
// three sessions each invented a different cause. A child taken by the OOM
// killer and an ordinary non-zero exit produced character-identical output.
test('mutate-ui: the unmatched failure branch reports the signal and status, not a fixed sentence', () => {
  const src = fsx.readFileSync(pathx.join(__dirname, 'mutate-ui.js'), 'utf8');
  const code = src.split('\n').filter((l) => !l.trim().startsWith('*') && !l.trim().startsWith('//')).join('\n');

  // The two fields that name the layer. Without them the message describes the
  // absence of evidence instead of carrying any.
  assert.ok(/e\.signal/.test(code), 'the fallback must report the signal that killed the child');
  assert.ok(/e\.status/.test(code), 'the fallback must report the exit status');

  // ...and the branch must be reached only when nothing else matched, or a
  // recognised cause would be replaced by a less specific one.
  const idxKnown = code.indexOf('Transform failed');
  assert.ok(idxKnown !== -1 && idxKnown < code.indexOf('e.signal'),
    'the known-cause scan must come first, so a nameable failure keeps its name');
});

// ── git write-back: a write has to reach the repo (kit#43) ───────────────────
//
// James chose git over a database for a deployed Kit (kit#41): "Ok let's stick
// to git for now and park db". Deployed there is no working tree anyone will
// ever look at, so decision 2's review step — the edit lands in your tree and
// you read the diff — has nowhere to happen unless the edit is committed.
//
// 🔑 These run against a REAL bare repo and a REAL clone, not a stubbed git.
// The states worth having are the failures, and a stub asserts only that the
// stub was called. Nothing here touches the network: a bare repo on disk is a
// perfectly good remote.
const gitStore = require('./git-store.js');
const { spawnSync: spawnx } = require('child_process');

/** A real remote, a real clone, and one committed corpus file inside it. */
function gitFixture() {
  const root = fsx.mkdtempSync(pathx.join(os.tmpdir(), 'kit-git-'));
  const bare = pathx.join(root, 'bare.git');
  const clone = pathx.join(root, 'clone');
  const sh = (args, cwd) => {
    const r = spawnx('git', args, { cwd, encoding: 'utf8' });
    if (r.status !== 0) throw new Error(`fixture: git ${args.join(' ')} — ${r.stderr}`);
    return r.stdout;
  };
  sh(['init', '-q', '--bare', '-b', 'main', bare]);
  sh(['clone', '-q', bare, clone]);
  sh(['-C', clone, 'config', 'user.name', 'fixture']);
  sh(['-C', clone, 'config', 'user.email', 'fixture@example.com']);
  fsx.mkdirSync(pathx.join(clone, 'behaviours'));
  const file = pathx.join(clone, 'behaviours', 'demo.beh');
  fsx.writeFileSync(file, 'behaviour BEH-1 "a thing"\n  actor visitor\n');
  sh(['-C', clone, 'add', '-A']);
  sh(['-C', clone, 'commit', '-q', '-m', 'initial']);
  sh(['-C', clone, 'push', '-q', 'origin', 'main']);
  return { root, bare, clone, file, sh };
}

test('git-store: it is off unless asked for, so the local tool is decision 2 unchanged', () => {
  const f = gitFixture();
  fsx.appendFileSync(f.file, '  when opens page:Home\n');
  const r = gitStore.writeBack(f.file, {});
  assert.strictEqual(r.committed, false);
  assert.strictEqual(r.pushed, false);
  // And the tree really is untouched — the claim is about git, not about a flag.
  assert.ok(f.sh(['-C', f.clone, 'status', '--short']).includes('demo.beh'),
    'the edit must still be sitting uncommitted in the working tree');
});

test('git-store: an edit is committed AND reaches the remote, not just the local clone', () => {
  const f = gitFixture();
  fsx.appendFileSync(f.file, '  when opens page:Home\n');
  const r = gitStore.writeBack(f.file, { enabled: true, summary: 'add a step to BEH-1', app: 'demo' });
  assert.strictEqual(r.committed, true, r.reason);
  assert.strictEqual(r.pushed, true, r.reason);

  // 🔑 Asked of the BARE repo. Reading the clone would prove only that a commit
  // happened locally, which is exactly the state this module exists to
  // distinguish from a successful push.
  const remote = f.sh(['-C', f.bare, 'show', 'main:behaviours/demo.beh']);
  assert.ok(remote.includes('when opens page:Home'), 'the edit did not reach the remote');
  assert.ok(f.sh(['-C', f.bare, 'log', '--oneline', '-1']).includes('add a step to BEH-1'),
    'the commit message must describe the edit, or the history is as opaque as the db he refused');
});

test('git-store: a write that changes nothing makes no commit, rather than an empty one', () => {
  const f = gitFixture();
  const before = f.sh(['-C', f.clone, 'rev-parse', 'HEAD']).trim();
  const r = gitStore.writeBack(f.file, { enabled: true, summary: 'no-op' });
  assert.strictEqual(r.committed, false);
  assert.ok(r.reason.includes('unchanged'), r.reason);
  assert.strictEqual(f.sh(['-C', f.clone, 'rev-parse', 'HEAD']).trim(), before,
    're-adjudicating to the state it already had must not add a commit');
});

test('git-store: switched on outside a work tree is reported, not thrown and not silent', () => {
  const root = fsx.mkdtempSync(pathx.join(os.tmpdir(), 'kit-nogit-'));
  const loose = pathx.join(root, 'x.beh');
  fsx.writeFileSync(loose, 'behaviour BEH-9 "z"\n');
  const r = gitStore.writeBack(loose, { enabled: true, summary: 'x' });
  assert.strictEqual(r.committed, false);
  assert.ok(r.reason.includes('not inside a git work tree'), r.reason);
});

// 🔴 The one the module exists for. By the time git runs, commitToDisk has
// ALREADY written the file — so a push failure never means "your edit was
// lost", it means "your edit is on a disk nobody will read again". Reporting
// that as success is how a deployed Kit loses work silently.
test('git-store: when the push fails the commit still happened, and both facts are reported', () => {
  const f = gitFixture();
  fsx.appendFileSync(f.file, '  when opens page:Home\n');
  // Move the remote out from under the clone: a real push failure, not a stub.
  fsx.renameSync(f.bare, f.bare + '.gone');
  const r = gitStore.writeBack(f.file, { enabled: true, summary: 'add a step' });

  assert.strictEqual(r.committed, true, 'the commit half succeeded and must say so');
  assert.strictEqual(r.pushed, false, 'the push half failed and must say so');
  assert.ok(r.commit, 'a local commit that exists must be nameable, so a person can find it');
  // The reason has to carry git's own words. A summary of them is how kit#39
  // spent three sessions unable to say why a run stopped.
  assert.ok(r.reason.includes('push'), r.reason);
  assert.ok(r.reason.includes('128') || r.reason.includes('exited'), r.reason);
});

test('git-store: the commit carries only the corpus, not whatever else the tree was dirty with', () => {
  const f = gitFixture();
  fsx.appendFileSync(f.file, '  when opens page:Home\n');
  // A pod's tree can be dirty for reasons that have nothing to do with this
  // edit. `git commit -a` would sweep them into his history under a message
  // describing one behaviour.
  const stray = pathx.join(f.clone, 'behaviours', 'unrelated.txt');
  fsx.writeFileSync(stray, 'not part of this edit\n');
  f.sh(['-C', f.clone, 'add', '--', 'behaviours/unrelated.txt']);

  const r = gitStore.writeBack(f.file, { enabled: true, summary: 'add a step' });
  assert.strictEqual(r.pushed, true, r.reason);
  const touched = f.sh(['-C', f.bare, 'show', '--name-only', '--format=', 'main']).trim().split('\n');
  assert.deepStrictEqual(touched, ['behaviours/demo.beh'],
    'the commit must contain the corpus alone');
});

test('git-store: a detached HEAD is refused rather than pushed to a guessed branch', () => {
  const f = gitFixture();
  f.sh(['-C', f.clone, 'checkout', '-q', '--detach', 'HEAD']);
  fsx.appendFileSync(f.file, '  when opens page:Home\n');
  const r = gitStore.writeBack(f.file, { enabled: true, summary: 'add a step' });
  assert.strictEqual(r.committed, false);
  assert.ok(r.reason.includes('detached'), r.reason);
});

// The lesson of kit#39, applied to a different child process. Three outcomes
// that need three different fixes — the binary is absent, it exited non-zero,
// it was signalled — must not collapse into one sentence.
test('git-store: a failed git call names the layer that failed, not a fixed sentence', () => {
  const f = gitFixture();
  const exited = gitStore.git(['rev-parse', '--verify', 'refs/heads/does-not-exist'], f.clone);
  assert.strictEqual(exited.ok, false);
  assert.ok(exited.failure.includes('exited'), exited.failure);

  // A missing binary must be distinguishable from a rejected command, because
  // "git is not installed in this image" and "your push was rejected" send
  // whoever reads them to entirely different places.
  const gone = gitStore.git(['rev-parse', 'HEAD'], pathx.join(f.root, 'no-such-dir'));
  assert.strictEqual(gone.ok, false);
  assert.ok(gone.failure.length > 0);
});

test('git-store: the commit message describes the edit and names the app', () => {
  assert.strictEqual(gitStore.message('add BEH-7', 'snip-it'), 'kit: add BEH-7 (snip-it)');
  assert.strictEqual(gitStore.message('bind page:Home', null), 'kit: bind page:Home');
});

Promise.all(pending).then(() => {
  console.log(`\n${pass} passed, ${fail} failed\n`);
  process.exit(fail ? 1 : 0);
});
