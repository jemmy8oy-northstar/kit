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
const t = (name, fn) => {
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

t('reads an id and a title', () => {
  const [b] = parse('behaviour BEH-1 "does a thing"');
  assert.strictEqual(b.id, 'BEH-1');
  assert.strictEqual(b.title, 'does a thing');
});

t('a step outside a behaviour is an error, not silently dropped', () => {
  assert.throws(() => parse('when opens page:Home'), /outside a behaviour/);
});

t('an unrecognised keyword is an error, not silently dropped', () => {
  // A spec language that ignores what it does not understand is how a spec
  // becomes descriptive: the author believes a line is enforced and it is not.
  assert.throws(() => parse('behaviour B "t"\n  wibble page:Home'), /unrecognised keyword/);
});

t('a bare noun with no verb parses as a state precondition', () => {
  const [b] = parse('behaviour B "t"\n  given transcription:Completed');
  assert.strictEqual(b.steps[0].verb, 'state');
});

t('holes and nouns are told apart on the same line', () => {
  const [b] = parse('behaviour B "t"\n  when fills form:Upload with ?fields');
  assert.deepStrictEqual(b.steps[0].holes.map((h) => h.slot), ['fields']);
  assert.deepStrictEqual(b.steps[0].refs.map((r) => `${r.kind}:${r.name}`), ['form:Upload']);
});

console.log('\n── resolve: the cross-behaviour symbol table ──');

t('a hole is filled by a DIFFERENT behaviour', () => {
  const { behaviours } = build(
    'behaviour A "a"\n  when fills form:Upload with ?fields\n' +
    'behaviour B "b"\n  provides form:Upload.fields = Email');
  assert.deepStrictEqual(behaviours[0].filled.map((f) => f.value[0]), ['Email']);
  assert.strictEqual(behaviours[0].open.length, 0);
});

t('an unfilled hole stays OPEN rather than being quietly dropped', () => {
  const { behaviours } = build('behaviour A "a"\n  when fills form:Upload with ?fields');
  assert.strictEqual(behaviours[0].filled.length, 0);
  assert.strictEqual(behaviours[0].open[0].key, 'form:Upload.fields');
});

t('two behaviours agreeing is agreement, not a conflict', () => {
  const { conflicts, symbols } = build(
    'behaviour A "a"\n  provides form:U.fields = Email\n' +
    'behaviour B "b"\n  provides form:U.fields = Email');
  assert.strictEqual(conflicts.length, 0);
  assert.deepStrictEqual(symbols.get('form:U.fields').contributors, ['A', 'B']);
});

t('two behaviours disagreeing IS a conflict, with both sides named', () => {
  const { conflicts } = build(
    'behaviour A "a"\n  provides form:U.fields = Email\n' +
    'behaviour B "b"\n  provides form:U.fields = Phone');
  assert.strictEqual(conflicts.length, 1);
  assert.deepStrictEqual(conflicts[0].holders, ['A']);
  assert.strictEqual(conflicts[0].challengers[0].from, 'B');
});

console.log('\n── generate: the refusals, which are the design claim ──');

t('CONTROL: a fully bound behaviour generates', () => {
  const [{ code, stats }] = gen('behaviour A "a"\n  when opens page:Home\n  when activates button:Go');
  assert.match(code, /page\.goto\("\.\/"\)/);
  assert.match(code, /getByRole\("button", \{ name: "Go" \}\)\.click\(\)/);
  assert.strictEqual(stats.ungenerated, 0);
});

t('an unbound noun is REFUSED, and the missing noun is named', () => {
  const [{ code, missing, stats }] = gen('behaviour A "a"\n  when activates button:Nope');
  assert.match(code, /UNGENERATED/);
  assert.deepStrictEqual(missing, ['button:Nope']);
  assert.strictEqual(stats.ungenerated, 1);
});

t('an unbound noun never produces a guessed locator', () => {
  // The failure being prevented: emitting getByRole('button', {name: 'Nope'})
  // from the noun's own name would produce a test that runs and asserts nothing
  // about the app the spec describes.
  const [{ code }] = gen('behaviour A "a"\n  when activates button:Nope');
  assert.ok(!/getByRole/.test(code), `guessed a locator: ${code}`);
});

t('an unsupplied ROUTE PARAM is refused — the real bug this caught', () => {
  // First version stripped `:id` and emitted `./editor/`: a test that runs,
  // navigates to the wrong page, and looks correct in review.
  const [{ code }] = gen('behaviour A "a"\n  when opens page:Editor');
  assert.match(code, /UNGENERATED/);
  assert.ok(!/goto/.test(code), `emitted a truncated route: ${code}`);
});

t('CONTROL: the same route generates once the param is provided', () => {
  const [a] = gen(
    'behaviour A "a"\n  when opens page:Editor\n' +
    'behaviour B "b"\n  provides page:Editor.id = abc');
  assert.match(a.code, /page\.goto\("\.\/editor\/abc"\)/);
});

t('a hole filled from elsewhere actually GENERATES, not just reports', () => {
  // Reporting the fill without using it would make the whole mechanism
  // decorative — it would look resolved and emit nothing.
  const [a] = gen(
    'behaviour A "a"\n  when fills form:Upload with ?fields\n' +
    'behaviour B "b"\n  provides form:Upload.fields = Email');
  assert.match(a.code, /getByLabel\("Email"\)/);
});

t('a wire contract is never generated, and never uncounted', () => {
  const [{ code, stats }] = gen('behaviour A "a"\n  contract POST /api/x is sent once');
  assert.match(code, /CONTRACT \(not derivable/);
  assert.strictEqual(stats.contract, 1);
  assert.strictEqual(stats.generated, 0);
  assert.strictEqual(stats.ungenerated, 0);
});

t('the test name carries the behaviour id, which is what coverage greps for', () => {
  const [{ code }] = gen('behaviour BEH-9 "a"');
  assert.match(code, /\[BEH-9\]/);
});

console.log('\n── coverage: the only part that can go red ──');

const { coverage } = require('./kit');

t('a behaviour no test names is uncovered', () => {
  const bs = parse('behaviour BEH-1 "a"\nbehaviour BEH-2 "b"');
  const r = coverage(bs, ['test("[BEH-1] a", () => {})']);
  assert.deepStrictEqual(r.uncovered.map((b) => b.id), ['BEH-2']);
  assert.deepStrictEqual(r.covered.map((b) => b.id), ['BEH-1']);
});

t('a test naming a behaviour that no longer exists is an orphan', () => {
  // The other direction of rot: the behaviour was deleted or renamed and the
  // test kept passing, still claiming to cover it.
  const r = coverage(parse('behaviour BEH-1 "a"'), ['test("[BEH-7] gone", () => {})']);
  assert.deepStrictEqual(r.orphanTests, ['BEH-7']);
});

console.log('\n── adjudication: "default included but marked unreviewed" (James, #68) ──');

const { adjudication } = require('./kit');

t('an inference defaults to unreviewed WITHOUT anyone writing review', () => {
  // The load-bearing one. He chose default-INCLUDE, so the only thing keeping a
  // machine guess from passing as a requirement is that it arrives unreviewed
  // by default. If this ever defaults to approved, the mechanism is decorative.
  const [b] = parse('behaviour BEH-1 "a"\n  source inferred tests/X.cs:name');
  assert.strictEqual(b.source.origin, 'inferred');
  assert.strictEqual(b.review.state, 'unreviewed');
});

t('a behaviour with no source line is defined and approved — old corpora still parse', () => {
  // The positive control for the test above: a version that marked EVERYTHING
  // unreviewed would pass it and be useless. Silence means a human wrote it.
  const [b] = parse('behaviour BEH-1 "a"\n  actor visitor');
  assert.strictEqual(b.source.origin, 'defined');
  assert.strictEqual(b.review.state, 'approved');
});

t('an explicit review survives a later source line', () => {
  const [b] = parse('behaviour BEH-1 "a"\n  review approved\n  source inferred tests/X.cs:n');
  assert.strictEqual(b.review.state, 'approved', 'an adjudicated inference must not revert to unreviewed');
});

t('a denial without a correction is refused', () => {
  // His #68 point: "on a deny a required indication of what correct behaviour
  // actually looks like should take place". A bare denial deletes a line; a
  // denial with a correction compounds into the corpus.
  assert.throws(() => parse('behaviour BEH-1 "a"\n  review denied'), /must state the correction/);
  assert.doesNotThrow(() => parse('behaviour BEH-1 "a"\n  review denied streaks reset at midnight UTC'));
});

t('an unknown source origin is refused rather than silently ignored', () => {
  assert.throws(() => parse('behaviour BEH-1 "a"\n  source guessed'), /defined.*inferred/);
});

t('the never-adjudicated count counts only unreviewed INFERENCES', () => {
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

t('a behaviour with no traceable ref is reported separately from an unreviewed one', () => {
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

t('an inferred behaviour that serves a documented one is not a finding', () => {
  const s = surface(build(SERVED_BY +
    'behaviour BEH-API "route"\n  source inferred code.cs:X\n  serves BEH-UI\n').behaviours);
  assert.deepStrictEqual(s.errors, []);
  assert.deepStrictEqual(s.unserved.map((b) => b.id), []);
  assert.deepStrictEqual(s.served.map((b) => b.id), ['BEH-API']);
});

t('an inferred behaviour serving nothing IS the finding', () => {
  const s = surface(build(SERVED_BY +
    'behaviour BEH-API "route"\n  source inferred code.cs:X\n').behaviours);
  assert.deepStrictEqual(s.unserved.map((b) => b.id), ['BEH-API']);
  assert.deepStrictEqual(s.errors, [], 'unserved is a report, not an error — it needs a human, not a fix');
});

t('a DEFINED behaviour serving nothing is not a finding (the control)', () => {
  const s = surface(build(SERVED_BY).behaviours);
  assert.deepStrictEqual(s.unserved.map((b) => b.id), [],
    'a documented behaviour is served, not serving — flagging it would flood the report');
});

t('a serves link to an id that does not exist breaks the build', () => {
  const s = surface(build('behaviour BEH-API "route"\n  source inferred code.cs:X\n  serves BEH-GHOST\n').behaviours);
  assert.strictEqual(s.errors.length, 1);
  assert.match(s.errors[0], /BEH-GHOST, which is not in the corpus/);
});

t('an inference serving an inference breaks the build — the chain must reach a human', () => {
  const s = surface(build(
    'behaviour BEH-A "one"\n  source inferred code.cs:A\n' +
    'behaviour BEH-B "two"\n  source inferred code.cs:B\n  serves BEH-A\n').behaviours);
  assert.ok(s.errors.some((e) => /itself inferred/.test(e)));
});

t('a defined behaviour carrying a serves line breaks the build', () => {
  const s = surface(build(SERVED_BY +
    'behaviour BEH-OTHER "x"\n  source defined docs/D.md#2\n  serves BEH-UI\n').behaviours);
  assert.ok(s.errors.some((e) => /is defined, so it is served rather than serving/.test(e)));
});

t('serves wants a behaviour id, not prose', () => {
  assert.throws(() => parse('behaviour BEH-A "a"\n  serves the today screen\n', 't.beh'), /serves wants a behaviour id/);
});

console.log('\n── the pilot corpus is real material, not a fixture ──');

t('james-habits-app parses and its spec-vs-spec conflict is detected', () => {
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

t('the pilot names exactly the two behaviours nothing documented displays', () => {
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

t('language-vocab is a different shape from habits, and the corpus says so', () => {
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

console.log(`\n${pass} passed, ${fail} failed\n`);
process.exit(fail ? 1 : 0);
