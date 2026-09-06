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

// Async tests go through the SAME helper, deliberately. `ui.js` needs a real
// listening socket to be tested at all, and a separate `atest(...)` would be
// invisible to `testTitles` — the reader keys on `test(`/`it(` — so every async
// test would vanish from the count, `check.js` would see a mapping naming a
// title it cannot find, and the stage-0 gate would go red for a reason that has
// nothing to do with the change. One helper, one name, one reader.
const pending = [];
const test = (name, fn) => {
  const ok = () => { pass++; console.log(`  ok   ${name}`); };
  const no = (e) => { fail++; console.log(`  FAIL ${name}\n       ${e.message}`); };
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

console.log('\n── parse ──');

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

console.log('\n── resolve: the cross-behaviour symbol table ──');

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

console.log('\n── generate: the refusals, which are the design claim ──');

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

console.log('\n── coverage: the only part that can go red ──');

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

console.log('\n── adjudication: "default included but marked unreviewed" (James, #68) ──');

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

console.log('\n── displayed surface (his frontend-first answer, kit#3) ──');
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

console.log('\n── the pilot corpus is real material, not a fixture ──');

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

console.log('\n── the question sheet (his kit#3 ask: "a behaviour question sheet... I can work through it with Gemini") ──');
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

console.log('\n── reading an app\'s tests ──');
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

console.log('\n── the mapping (option C) ──');
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

console.log('\n── the gate: kit check exit codes ──');
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
const REAL_LOG = console.log;
const REAL_ERR = console.error;
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

console.log('\n── prose-audit: does the corpus account for the whole document? ──');
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

console.log('\n── saturation: does binding glue grow 1:1 with the UI? (gap #8) ──');
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

console.log('\n── self-host: can Kit describe Kit? (James, claude-code-bot#89) ──');
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

console.log('\n── project: the read model a UI consumes (docs/design/ui.md) ──');
const proj = require('./project.js');

test('the projection carries every panel the UI needs, for a real corpus', () => {
  const p = proj.project('snip-it');
  assert.strictEqual(p.behaviours.length, 8);
  for (const k of ['app', 'corpus', 'behaviours', 'conflicts', 'generated', 'coverage', 'adjudication', 'surface', 'questions']) {
    assert.ok(k in p, `missing ${k}`);
  }
  assert.strictEqual(p.generated.length, 8, 'one generated test per behaviour — the output pane');
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

console.log('\n── ui (the read API) ──');

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

test('a write verb never reaches a handler — decision 2 is open', () => {
  // Not "does not write" — cannot. A doc saying the UI is read-only and a
  // server that refuses every write verb are different assurances.
  for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) {
    const r = ui.route(method, '/api/projects', { dir: uiDir });
    assert.strictEqual(r.status, 405, `${method} was not refused`);
    assert.match(r.body.reason, /decision 2/);
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

Promise.all(pending).then(() => {
  console.log(`\n${pass} passed, ${fail} failed\n`);
  process.exit(fail ? 1 : 0);
});
