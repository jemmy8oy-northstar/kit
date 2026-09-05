#!/usr/bin/env node
//
// HOW DOES AN EXISTING APP GET TAGGED?
// ────────────────────────────────────
// `coverage()` in kit.js matches `[BEH-ID]` markers inside test sources. Run it
// over the three real corpora today and it reports **0 of 58 covered**, because
// there is not one `BEH-` marker in any of the three repos. snip-it is
// substantively ~6/8 covered and formally 0/8 — the gate measures whether a
// migration has happened, not whether anything is tested (cc-bot#84).
//
// So before `kit check` is worth writing, Kit needs an answer to a question no
// doc has ever asked. Three options:
//
//   A  hand-edit every test title to carry `[BEH-X]`
//   B  match a behaviour to a test by TITLE, no edits anywhere
//   C  the CORPUS carries the mapping, so the app is never touched
//
// The choice is James's — it decides what Kit demands of a consumer. This file
// exists so he decides against numbers rather than three plausible sentences.
// It measures; it does not recommend, and it is not `kit check`.
//
// It reads tests from `git show origin/dev:<path>` in each pilot repo, NOT the
// working tree: a clone sitting on a feature branch would otherwise silently
// change the answer (snip-it's clone was on `feat/no-server-side-storage` when
// this was written).
//
//   node measure-tagging.js            # the report
//   node measure-tagging.js --json     # machine-readable

'use strict';
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
// The reader lives in kit.js because `check.js` needs the same one. It existed
// twice for about an hour, which is how long it takes for two of them to drift.
const { parse, resolve, testTitles, expectedTestCount, TEST_FILE_RE } = require(path.join(__dirname, 'kit.js'));

const APPS = [
  { corpus: 'snip-it.beh', repo: '/data/repos/snip-it' },
  { corpus: 'james-habits-app.beh', repo: '/data/repos/james-habits-app' },
  { corpus: 'language-vocab.beh', repo: '/data/repos/language-vocab' },
];

const REF = 'origin/dev';

// ─────────────────────────── reading the apps ───────────────────────────
// Tests come from `git show origin/dev` rather than the working tree; the
// reader itself is kit.js's, shared with check.js.

function gitFiles(repo) {
  return execFileSync('git', ['-C', repo, 'ls-tree', '-r', '--name-only', REF], { encoding: 'utf8' })
    .split('\n').filter((f) => TEST_FILE_RE.test(f));
}

function gitShow(repo, file) {
  return execFileSync('git', ['-C', repo, 'show', `${REF}:${file}`], { encoding: 'utf8', maxBuffer: 32 << 20 });
}

// ─────────────────────────── the matcher (option B) ───────────────────────────
// Deliberately a GOOD-FAITH matcher, not a strawman: if title matching is going
// to lose, it should lose on its merits. Tokenise, split camelCase and
// snake_case, drop stop words, crudely stem, then score by how much of the
// BEHAVIOUR's vocabulary the title accounts for (not Jaccard — a long test
// title should not be punished for being descriptive).

const STOP = new Set(['a', 'an', 'the', 'is', 'are', 'be', 'to', 'of', 'and', 'or', 'in', 'on',
  'for', 'from', 'it', 'its', 'that', 'this', 'so', 'can', 'with', 'when', 'then', 'given',
  'should', 'does', 'do', 'was', 'were', 'has', 'have', 'at', 'by', 'as', 'not', 'one', 'all']);

function tokens(s) {
  return s
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_\-/]+/g, ' ')
    .toLowerCase()
    .split(/[^a-z0-9]+/)
    .filter((t) => t && !STOP.has(t))
    .map((t) => (t.length > 4 && t.endsWith('s') && !t.endsWith('ss') ? t.slice(0, -1) : t));
}

function score(behTokens, titleTokens) {
  if (!behTokens.length) return 0;
  const set = new Set(titleTokens);
  let hit = 0;
  for (const t of new Set(behTokens)) if (set.has(t)) hit++;
  return hit / new Set(behTokens).size;
}

// ─────────────────────────── the control ───────────────────────────
// snip-it's corpus was reverse-engineered FROM `frontend/e2e/editor.spec.ts`,
// so six of its behaviours have a known-correct partner, established by reading
// both files (cc-bot#84, 2026-09-03). That is the only labelled data that
// exists, and without it "option B scored 0.61 on average" means nothing.
//
// A matcher that cannot reproduce pairs a human found by eye, on the single
// friendliest corpus in the estate, has answered the brittleness question.
const CONTROL = {
  'BEH-HOME-1': 'landing page renders',
  'BEH-EDIT-1': 'transcript editor loads the mock transcript',
  'BEH-EDIT-2': 'transcript editor renders in dark mode',
  'BEH-CUT-1': 'send-for-export submits a cut and surfaces the job',
  'BEH-UP-1': 'uploading a file transcribes it and opens the editor',
  'BEH-FILL-1': 'remove-filler-words changes the edit stats',
};

// A margin below which a runner-up makes the top match a coin toss. Not tuned
// to flatter any option: reported alongside the raw pairs so the number can be
// argued with.
const AMBIGUOUS_MARGIN = 0.15;

// Option B needs a THRESHOLD to be a gate at all — below it, "no test names
// this behaviour". The top match on its own is not a decision, because the
// matcher always returns something. Sweep it rather than pick one: the control
// says where a threshold would have to sit, and the two unlabelled apps say
// what that same threshold then claims about code nobody can check.
const THRESHOLDS = [0.4, 0.5, 0.6, 0.7, 0.8, 1.0];

// The marker `coverage()` looks for, verbatim from kit.js:420. Scanned over the
// real test sources to answer a question option A's cost depends on: does this
// syntax collide with anything already written? (`[Fact]`, `[Theory]`,
// `[InlineData]` do not match — they are not all-caps.)
const MARKER_RE = /\[([A-Z][A-Z0-9-]*)\]/g;

// ─────────────────────────── run ───────────────────────────

const report = [];

for (const app of APPS) {
  const behPath = path.join(__dirname, 'behaviours', app.corpus);
  const behaviours = resolve(parse(fs.readFileSync(behPath, 'utf8'), app.corpus)).behaviours;

  const files = gitFiles(app.repo);
  const titles = [];
  const existingMarkers = new Map();
  const titlesPerFile = [];
  // ⚠️ The ecosystem split keys on the FILENAME, not on `t.style`. It used to be
  // `style !== 'title'`, which worked only while 'title' was the sole JS style —
  // the moment the reader grew a second one ('each', for parameterised tests)
  // every `it.each` in a TypeScript file was counted as a C# test and this
  // refused a repo that was fine. A label that happens to correlate is not a
  // discriminator.
  //
  // Independent count, by a different method, now for BOTH ecosystems: every
  // xUnit test is exactly one [Fact]/[Theory] however many [InlineData] rows
  // follow, and every JS test is one call head that survives having strings and
  // comments stripped. `testTitles()` reaches both by position and can lose one
  // silently; these just count, so a mismatch means the reader is wrong.
  const expected = { cs: 0, js: 0 };
  const got = { cs: 0, js: 0 };
  for (const f of files) {
    const src = gitShow(app.repo, f);
    const read = testTitles(f, src);
    titles.push(...read);
    titlesPerFile.push(read.length);
    const eco = f.endsWith('.cs') ? 'cs' : 'js';
    expected[eco] += expectedTestCount(f, src) || 0;
    got[eco] += read.length;
    MARKER_RE.lastIndex = 0;
    for (const m of src.matchAll(MARKER_RE)) existingMarkers.set(m[1], (existingMarkers.get(m[1]) || 0) + 1);
  }

  for (const [eco, evidence] of [['cs', '[Fact]/[Theory] attributes'], ['js', 'test declarations surviving a strip of strings and comments']]) {
    if (got[eco] !== expected[eco]) {
      console.error(`REFUSING TO REPORT: ${app.corpus} — read ${got[eco]} ${eco === 'cs' ? 'C#' : 'JS'} tests but ${expected[eco]} ` +
        `${evidence} exist. The reader is losing tests; every number below would be wrong.`);
      process.exit(2);
    }
  }

  const titleTok = titles.map((t) => ({ ...t, tok: tokens(t.raw) }));

  const matches = behaviours.map((b) => {
    const bt = tokens(b.title);
    const ranked = titleTok
      .map((t) => ({ t, s: score(bt, t.tok) }))
      .sort((x, y) => y.s - x.s);
    const best = ranked[0] || { t: null, s: 0 };
    const next = ranked[1] || { s: 0 };
    return {
      id: b.id,
      title: b.title,
      best: best.t ? best.t.raw : null,
      bestFile: best.t ? best.t.file : null,
      bestScore: +best.s.toFixed(3),
      margin: +(best.s - next.s).toFixed(3),
      runnerUp: ranked[1] && ranked[1].t ? ranked[1].t.raw : null,
    };
  });

  // Duplicate titles: a title matcher CANNOT distinguish two tests with the
  // same name, and neither can a corpus-carried mapping keyed on the title.
  const byRaw = new Map();
  for (const t of titles) byRaw.set(t.raw, (byRaw.get(t.raw) || 0) + 1);
  const duplicateTitles = [...byRaw].filter(([, n]) => n > 1);

  // Collisions: one title is the top match for two different behaviours. Under
  // option B that silently marks both covered by the same test.
  const byBest = new Map();
  for (const m of matches) if (m.best && m.bestScore > 0) {
    byBest.set(m.best, [...(byBest.get(m.best) || []), m.id]);
  }
  const collisions = [...byBest].filter(([, ids]) => ids.length > 1);

  // What option B would actually REPORT, at each candidate threshold. `tests`
  // is the number of DISTINCT tests carrying those claims: if it is lower than
  // the count of behaviours marked covered, one test is being credited with
  // more than one behaviour.
  const thresholds = THRESHOLDS.map((t) => {
    const cov = matches.filter((m) => m.bestScore >= t);
    return { t, covered: cov.length, tests: new Set(cov.map((m) => m.best)).size };
  });

  report.push({
    app: app.corpus.replace('.beh', ''),
    behaviours: behaviours.length,
    testFiles: files.length,
    testTitles: titles.length,
    csharpTitles: titles.filter((t) => t.style !== 'title').length,
    maxTitlesInOneFile: titlesPerFile.length ? Math.max(...titlesPerFile) : 0,
    existingMarkers: [...existingMarkers],
    duplicateTitles,
    collisions,
    thresholds,
    matches,
  });
}

// The control, scored.
const snipit = report.find((r) => r.app === 'snip-it');
const control = Object.entries(CONTROL).map(([id, want]) => {
  const m = snipit.matches.find((x) => x.id === id);
  return { id, want, got: m ? m.best : null, ok: !!m && m.best === want, score: m ? m.bestScore : 0, margin: m ? m.margin : 0 };
});
const controlPass = control.filter((c) => c.ok).length;

if (process.argv.includes('--json')) {
  console.log(JSON.stringify({ report, control, controlPass }, null, 2));
  process.exit(0);
}

// A run that found no tests would make every option look equally impossible.
// That is a broken reader, not a finding ([[test-the-reader-against-the-artefact]]).
const totalTitles = report.reduce((a, r) => a + r.testTitles, 0);
if (totalTitles === 0) {
  console.error('REFUSING TO REPORT: read 0 test titles across every app — the reader is wrong, not the apps.');
  process.exit(2);
}

console.log('\n── How an existing app gets tagged: the three options, measured ──');
console.log(`   (tests read from ${REF} in each pilot repo, not the working tree)\n`);

console.log('app                 behaviours  testFiles  testTitles  (of which C#)  dupTitles');
for (const r of report) {
  console.log(
    r.app.padEnd(20) +
    String(r.behaviours).padStart(10) +
    String(r.testFiles).padStart(11) +
    String(r.testTitles).padStart(12) +
    String(r.csharpTitles).padStart(15) +
    String(r.duplicateTitles.length).padStart(11));
}
const tot = report.reduce((a, r) => ({
  b: a.b + r.behaviours, f: a.f + r.testFiles, t: a.t + r.testTitles,
}), { b: 0, f: 0, t: 0 });
console.log('\n' + `TOTAL: ${tot.b} behaviours, ${tot.t} test titles across ${tot.f} files.`);

console.log('\n── OPTION A: hand-edit every test to carry [BEH-X] ──');
console.log(`  edits: one marker per behaviour that has a test — an upper bound of ${tot.b},`);
console.log(`  spread over up to ${tot.f} files.`);
const csharpOnly = report.filter((r) => r.csharpTitles === r.testTitles);
console.log(`  ${csharpOnly.length} of ${report.length} apps are 100% C# method-named (${csharpOnly.map((r) => r.app).join(', ') || '—'}),`);
console.log('  which does NOT make them harder: kit.js:420 scans the whole test SOURCE, not the');
console.log('  title, so `// [BEH-X]` above the method is enough. No [Fact(DisplayName=…)] needed.');
const anyMarker = report.reduce((a, r) => a + r.existingMarkers.length, 0);
console.log(`  syntax collisions with code already written: ${anyMarker} distinct [ALLCAPS] token(s) ` +
            `across all ${tot.f} test files` + (anyMarker ? ':' : ' — the marker is free to use.'));
for (const r of report) for (const [tok, n] of r.existingMarkers) console.log(`      ${r.app}: [${tok}] x${n}`);
console.log('  ⚠️ the real cost is granularity, and no doc has said it: coverage() reads whole');
console.log('  FILES, so one marker anywhere in a file marks that behaviour covered. Largest test');
console.log(`  file in the estate holds ${Math.max(...report.map((r) => r.maxTitlesInOneFile))} titles — a marker there proves someone typed the id,`);
console.log('  not that any test asserts it. That is true of option C too, and of nothing else here.');

console.log('\n── OPTION B: match on test title ──');
console.log(`  CONTROL (snip-it, the 6 pairs a human established by eye): ${controlPass}/6 reproduced`);
for (const c of control) {
  console.log(`   ${c.ok ? '✅' : '❌'} ${c.id.padEnd(11)} score ${c.score.toFixed(2)} margin ${c.margin.toFixed(2)}`);
  if (!c.ok) {
    console.log(`        want: ${c.want}`);
    console.log(`        got : ${c.got === null ? '(nothing)' : c.got}`);
  }
}
const trueScores = control.filter((c) => c.ok).map((c) => c.score);
const missed = control.filter((c) => !c.ok);
console.log(`  the ${trueScores.length} it got RIGHT score ${Math.min(...trueScores).toFixed(2)}–${Math.max(...trueScores).toFixed(2)}, ` +
            `so a threshold accepting them all sits at or below ${Math.min(...trueScores).toFixed(2)}.`);
if (missed.length) {
  console.log(`  ⚠️ the other ${missed.length} is not a threshold problem and no threshold fixes it — the`);
  console.log('  top match is a DIFFERENT test. Raising the bar rejects it; lowering the bar accepts');
  console.log('  the wrong test as proof the behaviour is covered, which is worse than reporting 0.');
}
console.log('  what a threshold then claims about the two apps nobody can check by eye:\n');
const COL = 20;
console.log('  threshold ' + report.map((r) => r.app.padStart(COL)).join(''));
for (let i = 0; i < THRESHOLDS.length; i++) {
  console.log('       ' + THRESHOLDS[i].toFixed(2) + ' ' +
    report.map((r) => `${r.thresholds[i].covered}/${r.behaviours} by ${r.thresholds[i].tests}`.padStart(COL)).join(''));
}
console.log('  (read "6/8 by 6" as: six behaviours reported covered, by six distinct tests. A second');
console.log('   number below the first means one test is credited with more than one behaviour.)\n');
// Stated in option B's favour where the evidence supports it: at 0.60 it is
// PRECISE on the labelled corpus. The case against it is not sloppiness.
//
// This is the one line here that FLATTERS the option the plan's default rejects,
// so it is computed against the control rather than asserted from the counts —
// "exactly the human pairs" and "five of them" are different claims, and only
// the second falls out of the table above ([[scrutinise-the-check-that-flatters-you]]).
const BEST_T = 0.6;
const snipBest = snipit.matches.filter((m) => m.bestScore >= BEST_T);
const okAt = new Set(control.filter((c) => c.ok && c.score >= BEST_T).map((c) => c.id));
const falsePos = snipBest.filter((m) => !okAt.has(m.id));
console.log(`  in its favour: at ${BEST_T} snip-it reports ${snipBest.length}/${snipit.behaviours}, of which ${snipBest.length - falsePos.length} are human-confirmed pairs`);
console.log(`  and ${falsePos.length} are unconfirmed${falsePos.length ? ': ' + falsePos.map((m) => m.id).join(', ') : ''}. Option B is not sloppy where truth is known.`);
console.log('  against it: that same 0.60 says ' +
  report.filter((r) => r.app !== 'snip-it').map((r) => `${r.app} is ${r.thresholds.find((x) => x.t === 0.6).covered}/${r.behaviours}`).join(' and ') + ',');
const swingApp = report.reduce((a, r) => {
  const s = r.thresholds.find((x) => x.t === 0.4).covered - r.thresholds.find((x) => x.t === 0.6).covered;
  return s > a.s ? { s, r } : a;
}, { s: -1, r: null });
console.log(`  and moving the bar 0.60→0.40 swings ${swingApp.r.app} by ${swingApp.s} of ${swingApp.r.behaviours} behaviours. The number the gate`);
console.log('  produces depends more on a constant nobody can justify than on the code under test.\n');
for (const r of report) {
  const zero = r.matches.filter((m) => m.bestScore === 0).length;
  const ambiguous = r.matches.filter((m) => m.bestScore > 0 && m.margin < AMBIGUOUS_MARGIN).length;
  console.log(`  ${r.app.padEnd(20)} ${zero} behaviour(s) match nothing at all; ` +
              `${ambiguous} have a runner-up within ${AMBIGUOUS_MARGIN}; ` +
              `${r.collisions.length} title(s) are top match for >1 behaviour`);
  for (const [title, ids] of r.collisions) console.log(`      collision: "${title}" <- ${ids.join(', ')}`);
}

console.log('\n── OPTION C: the corpus carries the mapping ──');
console.log(`  authoring cost: ${tot.b} lines, once, in files Kit already owns; ZERO edits in any app.`);
console.log('  the interesting property is not the cost — it is that a mapping is CHECKABLE:');
console.log('  a corpus naming a test that no longer exists is a hard failure, computable today.');
for (const r of report) {
  const dup = r.duplicateTitles.length;
  console.log(`  ${r.app.padEnd(20)} ${r.testTitles} title(s) addressable; ${dup} duplicate title(s)` +
              (dup ? ' — a mapping keyed on the TITLE ALONE cannot address these; key it on file+title' : ''));
  for (const [title, n] of r.duplicateTitles) console.log(`      duplicate x${n}: "${title}"`);
}

console.log('\nPer-behaviour detail: --json\n');
