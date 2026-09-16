#!/usr/bin/env node
'use strict';
//
// self-host — can Kit describe Kit?
// ─────────────────────────────────
// James, claude-code-bot#89: *"think about how kit looks as a kit managed
// project"*. This measures it rather than arguing about it.
//
// The question decomposes into two, and keeping them apart is the whole point:
//
//   1. Can the NOTATION express Kit's behaviour?  (a parse question)
//   2. Can the GENERATOR emit a test for it?      (a target question)
//
// A single "N% generated" number answers neither, and the board already carries
// a warning about exactly this figure: james-habits-app reported 0% generated,
// where the cause was an app missing nouns and Kit working as designed. If Kit
// reports 0% for itself and nobody separates the two questions, the two look
// like the same finding and they are not.
//
// THE DISCRIMINATING STEP is `--bound`: bind every noun the corpus mentions,
// generously, and re-measure. If the number moves, it is a bindings gap that
// more authoring would fix. If it does not, the verbs are the wall.
//
//   node self-host.js            # measure and report
//   node self-host.js --check    # exit 1 if docs/pilots/kit-self-hosting.md has drifted
//   node self-host.js --record   # rewrite the findings file AND the write-up's
//                                # generated blocks (see the warning in the JSON)
//
// Exit 0 = measured. Exit 1 = --check and the write-up no longer matches the
// corpus. Exit 2 = could not look, which is deliberately not 0 — including the
// case where the write-up's markers cannot be found at all.

const fs = require('fs');
const path = require('path');
const kit = require('./kit.js');

const BEH = path.join(__dirname, 'behaviours', 'kit.beh');
const FINDINGS = path.join(__dirname, '..', '..', 'docs', 'pilots', 'kit-self-hosting.json');
const WRITEUP = path.join(__dirname, '..', '..', 'docs', 'pilots', 'kit-self-hosting.md');

// ── The write-up's numbers are GENERATED, not checked ───────────────────────
//
// `--check` used to compare the findings JSON against the corpus and stop there,
// so it stayed green from 2026-09-05 to 2026-09-16 while the prose it exists to
// protect said 14 behaviours / 42 steps / 21 contracts and the machine record
// beside it said 20 / 60 / 36. Someone re-recorded and did not touch the prose —
// the one thing the findings file's own header forbids.
//
// The fix is not a cleverer checker. The write-up already states the principle,
// about a different fact: *"That list is read out of `generate()`'s switch at
// runtime, not re-typed here. A hard-coded copy is how this document would start
// lying about the code."* The verb vocabulary was read at runtime and never
// drifted; the counts were re-typed and did. So the counts stop being re-typed:
// `--record` writes them into the markdown between markers, `--check`
// regenerates and compares. There is no second copy left to disagree.
//
// ⚠️ What is deliberately NOT generated: a number that is HISTORY. "derived was
// 0 at ten behaviours and still 0 at fourteen" is a dated observation about past
// corpora, not a measurement of this one, and regenerating it would erase the
// evidence the argument rests on. A historical number is not drift — it just has
// to say when it was taken.
const BEGIN = (key) => `<!-- self-host:begin ${key} -->`;
const END = (key) => `<!-- self-host:end ${key} -->`;

const BLOCKS = {
  numbers: (m) => [
    '| | |',
    '|---|---|',
    `| behaviours parsed | **${m.behaviours} of ${m.behaviours}**, no parse error |`,
    `| steps | ${m.steps} |`,
    `| prose \`contract\` lines | ${m.contracts} |`,
    `| steps **derived** from a behaviour | **${m.derived}** |`,
  ],
  saturation: (m) => [
    '| | generated | ungenerated |',
    '|---|---|---|',
    `| no bindings | ${m.unbound.generated} | ${m.unbound.ungenerated} |`,
    `| **every noun bound** | **${m.bound.generated}** | ${m.bound.ungenerated} |`,
    `| of which **derived** | **${m.derived}** | |`,
  ],
  verbs: (m) => [
    '| verb | steps | in the generator? |',
    '|---|---|---|',
    ...m.verbs.map((v) => `| \`${v.verb}\` | ${v.steps} | ${v.known ? 'yes' : '**no**'} |`),
  ],
  vocabulary: (m) => [`\`${m.generatorVerbs.join(', ')}\``],
};

/**
 * Rewrite every marked block in `md` from the live measurement.
 *
 * Returns `{ text, missing }`. A key in `missing` has no usable marker pair —
 * which callers must treat as COULD NOT LOOK, never as agreement. A parser that
 * silently passes when it stops finding its markers is the failure mode that let
 * this document drift in the first place, one level up.
 */
function spliceBlocks(md, m) {
  const missing = [];
  let out = md;
  for (const [key, render] of Object.entries(BLOCKS)) {
    const begin = out.indexOf(BEGIN(key));
    const end = out.indexOf(END(key));
    if (begin === -1 || end === -1 || end < begin) { missing.push(key); continue; }
    out = `${out.slice(0, begin + BEGIN(key).length)}\n${render(m).join('\n')}\n${out.slice(end)}`;
  }
  return { text: out, missing };
}

/** The write-up, or `null` if it cannot be opened — which is could-not-look. */
function writeUp(file) {
  try {
    return fs.readFileSync(file, 'utf8');
  } catch {
    console.error(`self-host: cannot read ${path.relative(process.cwd(), file)} — could not look`);
    return null;
  }
}

// The generator's whole vocabulary, read from the source of truth rather than
// re-typed here — a hard-coded copy of this list is how the doc starts lying
// about the code. `generate()` switches on step.verb; these are its cases.
function generatorVerbs() {
  const src = fs.readFileSync(path.join(__dirname, 'kit.js'), 'utf8');
  const body = src.slice(src.indexOf('switch (step.verb) {'));
  const end = body.indexOf('\n  }\n');
  return new Set([...body.slice(0, end).matchAll(/^\s*case '([a-z]+)':/gm)].map((m) => m[1]));
}

// Bind every noun the corpus mentions, as generously as the binding format
// allows: a route, a role+name, a label AND a state snippet. Anything this
// cannot generate, no bindings file could.
//
// ⚠️ `state` is the one verb whose binding is a free-form setup string rather
// than a locator, and the first version of this probe omitted it — which made
// the answer look stronger than the evidence. Leaving it out reported 0
// generated under full bindings; including it reports 10. Both numbers are
// "0 steps derived", but only one of them is honest about what a `state`
// binding does ([[scrutinise-the-check-that-flatters-you]]).
function generousBindings(behaviours) {
  const out = {};
  for (const b of behaviours) {
    for (const s of b.steps) {
      for (const r of s.refs) {
        if (r.kind === 'literal') continue;
        const key = `${r.kind}:${r.name}`;
        out[key] = { route: './x', role: 'button', name: r.name, label: r.name, state: `setUp(${JSON.stringify(key)})` };
      }
    }
  }
  return out;
}

function measure(src) {
  const { behaviours, symbols } = kit.resolve(kit.parse(src, 'kit.beh'));
  const known = generatorVerbs();

  const byVerb = new Map();
  let steps = 0, contracts = 0;
  for (const b of behaviours) {
    for (const s of b.steps) {
      if (s.kind === 'contract') { contracts++; continue; }
      steps++;
      byVerb.set(s.verb, (byVerb.get(s.verb) || 0) + 1);
    }
  }

  const tally = (bind) => {
    let generated = 0, ungenerated = 0;
    for (const b of behaviours) {
      const r = kit.generate(b, bind, symbols);
      generated += r.stats.generated;
      ungenerated += r.stats.ungenerated;
    }
    return { generated, ungenerated };
  };

  const unbound = tally({});
  const bound = tally(generousBindings(behaviours));

  // A step is DERIVED when the generator turned the behaviour into a locator or
  // an action. A `state` step is not derived: it copies a string a human wrote
  // into the bindings file. Counting them together is what makes "10 generated"
  // sound like the notation nearly works.
  const derived = bound.generated - (byVerb.get('state') || 0);

  return {
    behaviours: behaviours.length,
    steps,
    contracts,
    verbs: [...byVerb].sort((a, b) => b[1] - a[1]).map(([verb, n]) => ({ verb, steps: n, known: known.has(verb) })),
    generatorVerbs: [...known].sort(),
    unbound,
    bound,
    derived,
  };
}

function report(m) {
  console.log('\n── can Kit describe Kit? (claude-code-bot#89) ──\n');
  console.log(`  behaviours parsed      ${m.behaviours}   every one of them, with no parse error`);
  console.log(`  steps                  ${m.steps}`);
  console.log(`  prose contracts        ${m.contracts}   assertions the tree cannot carry`);

  console.log('\n── verbs Kit needs to describe itself, against what the generator knows ──\n');
  for (const v of m.verbs) {
    console.log(`  ${v.verb.padEnd(12)} ${String(v.steps).padStart(3)} step(s)   ${v.known ? 'known' : '← the generator has no case for this'}`);
  }
  console.log(`\n  the generator's whole vocabulary: ${m.generatorVerbs.join(', ')}`);
  console.log('  every one of them is a BROWSER verb. Kit is a command-line tool.');

  console.log('\n── the discriminating step: bind every noun and re-measure ──\n');
  console.log('                    generated  ungenerated');
  console.log(`  no bindings       ${String(m.unbound.generated).padStart(9)}  ${String(m.unbound.ungenerated).padStart(11)}`);
  console.log(`  ALL nouns bound   ${String(m.bound.generated).padStart(9)}  ${String(m.bound.ungenerated).padStart(11)}`);
  console.log(`\n  steps actually DERIVED from a behaviour: ${m.derived}`);
  console.log(`  the ${m.bound.generated} that "generate" under full bindings are all \`state\` steps, and a`);
  console.log('  `state` binding is a setup string a human wrote — copied out, not derived.');

  console.log('\n── the finding ──\n');
  console.log('  Kit can DESCRIBE itself and can GENERATE nothing for itself. That is not a');
  console.log('  bindings gap and no amount of authoring closes it: the generator emits');
  console.log('  Playwright, and Kit has no browser to point it at.');
  console.log('');
  console.log('  So Kit becomes a kit-managed project exactly when Kit has a UI — which is');
  console.log('  the other half of #89. The two asks are one ask.');
  console.log('');
}

function snapshot(m) {
  return {
    behaviours: m.behaviours, steps: m.steps, contracts: m.contracts,
    verbs: m.verbs, generatorVerbs: m.generatorVerbs,
    unbound: m.unbound, bound: m.bound, derived: m.derived,
  };
}

function main(argv) {
  // --corpus exists so the suite can drive this over a fixture. Without it the
  // only reachable input is the committed corpus, and the refusal paths below
  // would be untestable — which is how a refusal ends up decorative.
  //
  // --writeup is here for exactly the same reason, and the reason is the
  // sentence above: the markdown paths below REFUSE (exit 2 on a marker that
  // cannot be found), and a refusal no test can reach is decorative. It is the
  // whole failure this change exists to fix, one level up.
  //
  // --findings was added for a reason worth recording, because the gate found it
  // rather than a person: once the markdown blocks cover every field of the
  // snapshot, disabling the JSON comparison entirely left `--check` STILL going
  // red on a drifted corpus — via the markdown — so `mutate.js`'s "`--check`
  // accepts drift silently" mutant survived for the first time. It is not an
  // equivalent mutant; the JSON branch really did stop being proven, and a
  // hand-edited findings file is the one drift only it can catch.
  const ci = argv.indexOf('--corpus');
  const wi = argv.indexOf('--writeup');
  const fi = argv.indexOf('--findings');
  const findingsPath = fi >= 0 ? argv[fi + 1] : FINDINGS;
  const writeupPath = wi >= 0 ? argv[wi + 1] : WRITEUP;
  const beh = ci >= 0 ? argv[ci + 1] : BEH;
  if (!beh || !fs.existsSync(beh)) {
    console.error(`self-host: no corpus at ${beh} — could not look`);
    return 2;
  }
  const src = fs.readFileSync(beh, 'utf8');
  const m = measure(src);

  // A run that parsed nothing would make every number below look like the same
  // dramatic finding. Refuse instead of reporting it.
  if (m.behaviours === 0 || m.steps === 0) {
    console.error('self-host: the corpus parsed to zero behaviours or zero steps — could not look');
    return 2;
  }

  if (argv.includes('--record')) {
    fs.writeFileSync(findingsPath, JSON.stringify({
      _: [
        'Written by `node self-host.js --record`, which ALSO rewrites the generated',
        'blocks in kit-self-hosting.md in the same run — the prose no longer keeps a',
        'hand-typed copy of any of these numbers, because for eleven days it kept a',
        'WRONG one and `--check` never opened the file. `--check` recomputes both and',
        'exits 1 on any difference, 2 if it cannot find the markers to look at.',
      ],
      ...snapshot(m),
    }, null, 2) + '\n');
    console.log(`recorded ${path.relative(process.cwd(), findingsPath)}`);

    // The JSON alone is what a re-record used to mean, and re-recording without
    // touching the prose IS the drift. So the prose is written in the same
    // breath — and if its markers cannot be found, this refuses rather than
    // leaving the two halves disagreeing again.
    const wrote = writeUp(writeupPath);
    if (wrote === null) return 2;
    const { text, missing } = spliceBlocks(wrote, m);
    if (missing.length) {
      console.error(`self-host --record: no self-host:begin/end markers for ${missing.join(', ')} in ${path.relative(process.cwd(), writeupPath)} — could not look, and the JSON above is now AHEAD of the prose`);
      return 2;
    }
    fs.writeFileSync(writeupPath, text);
    console.log(`recorded ${path.relative(process.cwd(), writeupPath)} (${Object.keys(BLOCKS).length} block(s))`);
    return 0;
  }

  if (argv.includes('--check')) {
    if (!fs.existsSync(findingsPath)) {
      console.error(`self-host --check: no findings file at ${findingsPath} — could not look`);
      return 2;
    }
    const was = JSON.parse(fs.readFileSync(findingsPath, 'utf8'));
    delete was._;
    const now = snapshot(m);
    if (JSON.stringify(was) !== JSON.stringify(now)) {
      console.error('self-host --check: the corpus no longer says what the write-up claims.');
      console.error(`  recorded: ${JSON.stringify(was)}`);
      console.error(`  now:      ${JSON.stringify(now)}`);
      return 1;
    }

    // The half that was missing, and the reason this check could report green
    // for eleven days over a write-up six behaviours out of date: the JSON
    // agreeing with the corpus says nothing about the PROSE, and the prose is
    // the artefact anybody reads.
    const md = writeUp(writeupPath);
    if (md === null) return 2;
    const { text, missing } = spliceBlocks(md, m);
    if (missing.length) {
      console.error(`self-host --check: no self-host:begin/end markers for ${missing.join(', ')} in ${path.relative(process.cwd(), writeupPath)} — could not look, which is NOT the same as looking and finding it fine`);
      return 2;
    }
    if (text !== md) {
      console.error('self-host --check: the write-up\'s generated blocks no longer match the corpus.');
      const was = md.split('\n');
      const now = text.split('\n');
      for (let i = 0; i < Math.max(was.length, now.length); i++) {
        if (was[i] !== now[i]) console.error(`  line ${i + 1}\n    write-up: ${was[i] ?? '(end of file)'}\n    corpus:   ${now[i] ?? '(end of file)'}`);
      }
      console.error('  run `node self-host.js --record` — but read the JSON header first: a re-record with no edit to the surrounding prose is the drift, not the fix.');
      return 1;
    }
    console.log(`self-host --check: the recorded findings and all ${Object.keys(BLOCKS).length} generated block(s) in the write-up still match the corpus.`);
    return 0;
  }

  report(m);
  return 0;
}

module.exports = { measure, generatorVerbs, generousBindings, spliceBlocks, BLOCKS, main };

if (require.main === module) process.exit(main(process.argv.slice(2)));
