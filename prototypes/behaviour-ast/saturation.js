#!/usr/bin/env node
'use strict';

// ─────────────────────────── gap #8 ───────────────────────────
// "Binding glue SATURATES rather than growing 1:1 with the UI."
//
// That is Kit's core bet against Gherkin, stated in bindings.json's own header:
// the file "grows with the app's vocabulary rather than with the spec". It has
// never been measured. This script measures it, and is built to make the
// FLATTERING answer hard to reach:
//
//   1. A behaviour with no noun references is EXCLUDED from the curve. Half of
//      every corpus is API/domain/inference behaviours with no UI step at all,
//      and counting them drives the marginal to zero for a reason that has
//      nothing to do with saturation. That single filter changes habits from
//      "23 behaviours, saturated after 6" to "8 behaviours, never saturated".
//
//   2. A NULL MODEL, because the obvious measurement is worthless without one.
//      Accumulating a FIXED finite set of nouns must show a declining marginal
//      — you cannot introduce a noun twice. So "the curve declines" is
//      arithmetic, not evidence. The null is the same corpus in a shuffled
//      order: that isolates the coupon-collector floor. Only a decline steeper
//      than shuffling produces is a fact about the corpus.
//
//   3. A second, differently-shaped count of the noun set (raw text vs AST),
//      because a reader that silently loses steps would report a small noun
//      count as saturation. Disagreement is a hard refusal, not a warning.
//
// Exit 0 = measured. Exit 1 = --check and the recorded findings have drifted
// from what the corpora now say. Exit 2 = could not look, which is deliberately
// not 0: a saturation script that reads nothing and exits green is
// indistinguishable from one that measured saturation.

const fs = require('fs');
const path = require('path');
const kit = require('./kit.js');

const BEH_DIR = path.join(__dirname, 'behaviours');
const STEP_KEYS = ['given', 'when', 'then'];
const SHUFFLES = 5000;
const SEED = 20260903;

// Deterministic RNG. The committed findings file records numbers produced by
// this exact stream; Math.random would make every run disagree with the doc and
// the drift check would be noise.
function mulberry32(a) {
  return function () {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Moved into kit.js, which owns the AST. Re-exported here so this file's public
// surface is unchanged and the cross-check below still has two INDEPENDENT
// shapes — nounsOf reads the AST, nounsFromText re-reads the raw file.
const nounsOf = kit.nounsOf;

// The second shape. Deliberately NOT a second pass over the AST: it reads the
// raw file and only trusts lines that open with a step keyword, so a parser
// that dropped a step or mis-scoped one shows up as a set difference rather
// than as a smaller, more flattering number.
function nounsFromText(text) {
  const found = new Set();
  for (const raw of text.split('\n')) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    const kw = line.split(/\s+/)[0];
    if (!STEP_KEYS.includes(kw)) continue;
    const rest = line.slice(kw.length);
    // Strip quoted literals first: a literal may contain a colon
    // (`then shows region:Main "Ratio: 1.4"`) and would otherwise be read as a
    // noun that the AST correctly never produced.
    const stripped = rest.replace(/"[^"]*"/g, '""');
    for (const m of stripped.matchAll(/([a-z]+):([A-Za-z0-9_]+)/g)) found.add(`${m[1]}:${m[2]}`);
  }
  return found;
}

// Marginal new nouns introduced by each behaviour, in the given order.
function marginal(order) {
  const seen = new Set();
  return order.map((b) => {
    let fresh = 0;
    for (const n of b.nouns) if (!seen.has(n)) { seen.add(n); fresh++; }
    return fresh;
  });
}

// The statistic: mean marginal over the back half divided by the front half.
// 1.0 = no decline (glue grows 1:1). 0.0 = fully saturated (the back half needs
// no new bindings at all).
function halfRatio(curve) {
  const h = Math.ceil(curve.length / 2);
  const front = curve.slice(0, h);
  const back = curve.slice(h);
  if (!back.length) return null;
  const mean = (a) => a.reduce((s, x) => s + x, 0) / a.length;
  const f = mean(front);
  return f === 0 ? null : mean(back) / f;
}

function shuffled(arr, rnd) {
  const a = arr.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(rnd() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

// `textReader` is injectable for ONE reason: the two readers agree on every
// corpus that kit.parse will even accept, so the disagreement branch below
// cannot be reached from a fixture. A refusal path that has never once fired is
// a claim, not a check — so the suite injects a lossy reader and watches it go
// red. Nothing in production passes this argument.
function measureCorpus(file, text, textReader = nounsFromText) {
  const parsed = kit.parse(text, file);
  const behaviours = parsed.map((b) => ({ id: b.id, steps: b.steps.length, nouns: nounsOf(b) }));
  const bearing = behaviours.filter((b) => b.nouns.length > 0);

  const astNouns = new Set(behaviours.flatMap((b) => b.nouns));
  const textNouns = textReader(text);
  const onlyAst = [...astNouns].filter((n) => !textNouns.has(n));
  const onlyText = [...textNouns].filter((n) => !astNouns.has(n));

  const observed = marginal(bearing);
  const observedRatio = halfRatio(observed);

  // Null: the same behaviours, order destroyed. Anything the observed curve
  // does that this also does is the arithmetic of a finite set, not saturation.
  let nullRatios = [];
  if (observedRatio !== null) {
    const rnd = mulberry32(SEED);
    for (let i = 0; i < SHUFFLES; i++) {
      const r = halfRatio(marginal(shuffled(bearing, rnd)));
      if (r !== null) nullRatios.push(r);
    }
    nullRatios.sort((a, b) => a - b);
  }
  // Mid-rank, not `<=`. The null distribution has large atoms (a 9-behaviour
  // corpus has few distinct ratios), and macro-metrics lands exactly ON its own
  // null median — where a plain `<=` count reports 71% and a plain `<` reports
  // 39% for the same data. Splitting the tie is the only reading that does not
  // depend on which comparison operator flatters the result.
  const below = nullRatios.filter((r) => r < observedRatio).length;
  const ties = nullRatios.filter((r) => r === observedRatio).length;

  return {
    file,
    behaviours: behaviours.length,
    bearing: bearing.length,
    inert: behaviours.length - bearing.length,
    nouns: astNouns.size,
    nounRefs: behaviours.reduce((s, b) => s + b.nouns.length, 0),
    nounsPerBearing: bearing.length ? astNouns.size / bearing.length : null,
    curve: observed,
    ids: bearing.map((b) => b.id),
    ratio: observedRatio,
    nullMedian: nullRatios.length ? nullRatios[Math.floor(nullRatios.length / 2)] : null,
    percentile: nullRatios.length ? (below + ties / 2) / nullRatios.length : null,
    tieShare: nullRatios.length ? ties / nullRatios.length : null,
    crosscheck: { onlyAst, onlyText, textCount: textNouns.size },
    nounSet: astNouns,
  };
}

function pct(x) { return x === null ? 'n/a' : (x * 100).toFixed(0) + '%'; }
function num(x, d = 2) { return x === null ? 'n/a' : x.toFixed(d); }

function main(argv = [], textReader = nounsFromText) {
  const checkMode = argv.includes('--check');
  const dirArg = argv.indexOf('--dir');
  const dir = dirArg >= 0 ? argv[dirArg + 1] : BEH_DIR;
  const only = argv.filter((a, i) => !a.startsWith('--') && argv[i - 1] !== '--dir')[0];

  if (!fs.existsSync(dir)) {
    console.error(`saturation: no behaviours directory at ${dir} — could not look`);
    return 2;
  }
  const all = fs.readdirSync(dir).filter((f) => f.endsWith('.beh'))
    .filter((f) => !only || f.includes(only));

  // This study asks whether the glue binding a SPEC to a UI saturates as the UI
  // grows. A corpus with no UI has no answer to give and would sit in the
  // population as a category error — kit.beh's nouns are `command:KitCheck` and
  // `status:One`, which no locator will ever bind.
  //
  // The exclusion is DECLARED BY THE CORPUS, not by a filename list here. A list
  // in this file is a thing a future corpus silently fails to be added to; a
  // directive travels with the file that needs it. And it is announced rather
  // than applied quietly — a population you cannot see is one you cannot check.
  const NO_UI = /^#\s*kit:no-ui\b/m;

  // The SECOND exclusion axis, and it is not the same one. `kit:no-ui` asks
  // "does this corpus describe a UI"; this asks "does the app it describes
  // EXIST". A trial corpus written forwards (claude-code-bot#92) has plenty of
  // UI nouns, so `kit:no-ui` would not catch it — and it must not join this
  // study, because cross-app noun reuse over a corpus I invented measures my
  // own naming habits, not two teams independently converging.
  //
  // Found the way these things should be found: adding a trial corpus turned
  // `--check` RED, because the recorded findings are over the four real apps.
  // Same principle as above — declared by the corpus, never a filename list here.
  const NOT_REAL = /^#\s*kit:not-a-real-app\b/m;
  const skipped = [];
  const excluded = [];
  const files = all.filter((f) => {
    const text = fs.readFileSync(path.join(dir, f), 'utf8');
    if (NOT_REAL.test(text)) { excluded.push(f); return false; }
    if (NO_UI.test(text)) { skipped.push(f); return false; }
    return true;
  });
  for (const f of skipped) console.log(`  (skipping ${f}: declares "# kit:no-ui" — it describes no UI, so binding saturation has no meaning for it)`);
  for (const f of excluded) console.log(`  (skipping ${f}: declares "# kit:not-a-real-app" — a corpus for software that does not exist cannot evidence how real apps reuse nouns)`);

  if (!files.length) {
    console.error(`saturation: no corpus matching "${only || ''}" — could not look`);
    return 2;
  }

  const results = files.map((f) => measureCorpus(f, fs.readFileSync(path.join(dir, f), 'utf8'), textReader));

  // ── refusals, before any number is printed ──
  const problems = [];
  for (const r of results) {
    if (r.nouns === 0) problems.push(`${r.file}: zero nouns parsed — a corpus that binds nothing cannot answer whether bindings saturate`);
    if (r.crosscheck.onlyAst.length || r.crosscheck.onlyText.length) {
      problems.push(`${r.file}: the two counts disagree — AST has ${r.nouns}, raw text has ${r.crosscheck.textCount}` +
        (r.crosscheck.onlyAst.length ? `; AST-only: ${r.crosscheck.onlyAst.join(', ')}` : '') +
        (r.crosscheck.onlyText.length ? `; text-only: ${r.crosscheck.onlyText.join(', ')}` : ''));
    }
  }
  if (results.every((r) => r.bearing < 4)) {
    problems.push('no corpus has 4 noun-bearing behaviours — a front-half/back-half comparison over fewer is not a measurement');
  }
  if (problems.length) {
    console.error('saturation: COULD NOT LOOK');
    for (const p of problems) console.error('  - ' + p);
    return 2;
  }

  console.log('gap #8 — does binding glue saturate, or grow 1:1 with the UI?\n');
  console.log('A "noun" is a kind:Name reference on a step: exactly one bindings.json entry.');
  console.log('Behaviours with NO noun reference are excluded from the curve — they are API,');
  console.log('domain and inference behaviours, and counting them drives the marginal to zero');
  console.log('for a reason that is not saturation.\n');

  const rows = [['corpus', 'beh', 'UI-beh', 'inert', 'nouns', 'refs', 'nouns/UI-beh']];
  for (const r of results) {
    rows.push([r.file.replace('.beh', ''), String(r.behaviours), String(r.bearing), String(r.inert),
      String(r.nouns), String(r.nounRefs), num(r.nounsPerBearing)]);
  }
  const w = rows[0].map((_, i) => Math.max(...rows.map((row) => row[i].length)));
  for (const row of rows) console.log('  ' + row.map((c, i) => c.padEnd(w[i])).join('  '));

  console.log('\nMarginal new nouns per UI behaviour, in authoring order:');
  for (const r of results) {
    console.log(`  ${r.file.replace('.beh', '').padEnd(18)} ${r.curve.join(',')}`);
  }

  console.log('\nBack half vs front half, against the shuffled null.');
  console.log('  ratio 1.00 = no decline. A fixed noun set makes SOME decline unavoidable,');
  console.log('  so the null (same behaviours, order destroyed) is the floor to beat.');
  console.log(`  percentile = mid-rank of the observed ratio among ${SHUFFLES} shuffles (ties split).`);
  console.log('  BELOW 50% would mean authoring order saturates faster than chance. ABOVE 50%');
  console.log('  means it saturates SLOWER — new nouns keep arriving late in the corpus.\n');
  const r2 = [['corpus', 'observed', 'null median', 'percentile']];
  for (const r of results) r2.push([r.file.replace('.beh', ''), num(r.ratio), num(r.nullMedian), pct(r.percentile)]);
  const w2 = r2[0].map((_, i) => Math.max(...r2.map((row) => row[i].length)));
  for (const row of r2) console.log('  ' + row.map((c, i) => c.padEnd(w2[i])).join('  '));

  console.log('\nCross-corpus reuse — the share of each corpus\'s nouns already bound by another app.');
  console.log('  This is the plan\'s literal question: build a second bindings.json, measure reuse.\n');
  for (const r of results) {
    const others = new Set(results.filter((o) => o !== r).flatMap((o) => [...o.nounSet]));
    const shared = [...r.nounSet].filter((n) => others.has(n));
    console.log(`  ${r.file.replace('.beh', '').padEnd(18)} ${shared.length}/${r.nounSet.size}` +
      ` = ${pct(shared.length / r.nounSet.size)}` + (shared.length ? `  (${shared.join(', ')})` : ''));
  }

  // The reuse figure above is keyed on the noun NAME, which the corpus author
  // controls: bindings.json says the macro-metrics nouns were hand-prefixed
  // (page:MacroHome, not page:Home) to dodge the global-namespace collision. So
  // a 0% keyed on names is partly a naming habit, and reporting only that would
  // be measuring my own convention. This measures what the bindings POINT AT —
  // role+name, label, locator or route — which no prefix can change. Two apps
  // with a "Toggle Theme" button share a target even under different keys.
  const bindPath = path.join(__dirname, 'bindings.json');
  if (fs.existsSync(bindPath)) {
    const bindings = JSON.parse(fs.readFileSync(bindPath, 'utf8'));
    const sig = (b) => {
      if (!b || typeof b !== 'object' || Array.isArray(b)) return null;
      if (b.role) return `${b.role}|${b.name}`;
      if (b.label) return `label|${b.label}`;
      if (b.route) return `route|${b.route}`;
      if (b.locator) return `locator|${b.locator}`;
      if (b.state) return `state|${b.state}`;
      if (b.fixture) return `fixture|${JSON.stringify(b.fixture)}`;
      return null;
    };
    const bySig = new Map();
    for (const r of results) {
      for (const noun of r.nounSet) {
        const s = sig(bindings[noun]);
        if (!s) continue;
        if (!bySig.has(s)) bySig.set(s, new Set());
        bySig.get(s).add(r.file);
      }
    }
    const shared = [...bySig.entries()].filter(([, apps]) => apps.size > 1);
    console.log('\nSame measure, keyed on what the binding POINTS AT rather than the noun name,');
    console.log('because the names were hand-prefixed to dodge the global namespace (bindings.json):');
    console.log(`  ${bySig.size} distinct bound targets across all corpora; ` +
      `${shared.length} reached by more than one app` +
      (shared.length ? ` — ${shared.map(([s]) => s).join(', ')}` : ''));
  }

  const findingsPath = path.join(__dirname, '..', '..', 'docs', 'pilots', 'binding-saturation.json');
  const snapshot = () => {
    const live = {};
    for (const r of results) {
      live[r.file] = {
        behaviours: r.behaviours, uiBehaviours: r.bearing, nouns: r.nouns,
        curve: r.curve, ratio: r.ratio === null ? null : Number(r.ratio.toFixed(4)),
        nullMedian: r.nullMedian === null ? null : Number(r.nullMedian.toFixed(4)),
      };
    }
    return live;
  };

  if (argv.includes('--record')) {
    fs.writeFileSync(findingsPath, JSON.stringify({
      _comment: [
        'What saturation.js measured, so the prose in binding-saturation.md cannot age',
        'into fiction. `node saturation.js --check` recomputes and exits 1 on any',
        'difference. Regenerate with --record ONLY after deciding the write-up is wrong;',
        'a re-record with no edit to the prose is the drift, not the fix.',
      ],
      shuffles: SHUFFLES, seed: SEED,
      corpora: snapshot(),
    }, null, 2) + '\n');
    console.log(`\nsaturation --record: wrote ${path.relative(process.cwd(), findingsPath)}`);
    return 0;
  }

  if (checkMode) {
    if (!fs.existsSync(findingsPath)) {
      console.error(`\nsaturation --check: no recorded findings at ${findingsPath} — could not look`);
      return 2;
    }
    const recorded = JSON.parse(fs.readFileSync(findingsPath, 'utf8'));
    const live = snapshot();
    const drift = [];
    for (const [file, want] of Object.entries(recorded.corpora)) {
      const got = live[file];
      if (!got) { drift.push(`${file}: recorded, but no such corpus now`); continue; }
      for (const k of Object.keys(want)) {
        const a = JSON.stringify(want[k]);
        const b = JSON.stringify(got[k]);
        if (a !== b) drift.push(`${file}.${k}: recorded ${a}, now ${b}`);
      }
    }
    for (const file of Object.keys(live)) {
      if (!recorded.corpora[file]) drift.push(`${file}: a corpus with no recorded finding — the write-up does not account for it`);
    }
    if (drift.length) {
      console.error('\nsaturation --check: THE WRITE-UP HAS DRIFTED FROM THE CORPORA');
      for (const d of drift) console.error('  - ' + d);
      console.error('\nFix docs/pilots/binding-saturation.md and .json, or explain the change.');
      return 1;
    }
    console.log('\nsaturation --check: the recorded findings still match the corpora.');
  }
  return 0;
}

if (require.main === module) process.exit(main(process.argv.slice(2)));

module.exports = { main, nounsOf, nounsFromText, marginal, halfRatio, measureCorpus, mulberry32 };
