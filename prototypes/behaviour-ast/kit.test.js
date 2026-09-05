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
const test = (name, fn) => {
  try { fn(); pass++; console.log(`  ok   ${name}`); }
  catch (e) { fail++; console.log(`  FAIL ${name}\n       ${e.message}`); }
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
const quiet = (fn) => {
  const log = console.log, err = console.error;
  console.log = console.error = () => {};
  try { return fn(); } finally { console.log = log; console.error = err; }
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

console.log(`\n${pass} passed, ${fail} failed\n`);
process.exit(fail ? 1 : 0);
