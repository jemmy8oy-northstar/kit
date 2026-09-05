#!/usr/bin/env node
//
// prose-audit — does the corpus account for the WHOLE document?
// ─────────────────────────────────────────────────────────────
// `kit.js macro-metrics` reports 29 generated lines out of 37 = 78%. That number
// is true and it is theatre, because its denominator is the acceptance criteria
// that happened to fit the notation. The honest denominator is the source
// document. This tool holds the two together.
//
//   node prose-audit.js                                  # ledger only
//   node prose-audit.js --source <path-to-user-stories>  # + drift check
//   node prose-audit.js --demo-collision                 # the global-noun hazard
//
// Exit codes follow check.js, and the middle one is the point:
//   0  every AC in the ledger is accounted for, and every claim it makes holds
//   1  it looked and something is wrong — an unaccounted AC, a ledger entry
//      naming a behaviour the corpus does not have, a behaviour no AC asked for,
//      or (with --source) the source document has drifted from the ledger
//   2  IT COULD NOT LOOK — no ledger, no corpus, zero ACs, or a --source path
//      that yields no acceptance criteria. Deliberately not 0: a run that read
//      nothing must not be indistinguishable from a clean one.
//
// The check that earns this file is `unasked`. Nothing else in the repo stops me
// writing a flattering behaviour that no acceptance criterion ever asked for and
// then counting it in the coverage; encoding prose is exactly where that
// temptation lives, because the notation's failures are invisible in the output
// and its successes are printed.

'use strict';
const fs = require('fs');
const path = require('path');
const { parse, resolve, generate } = require('./kit');

const LEDGER = path.join(__dirname, '..', '..', 'docs', 'pilots', 'macro-metrics-prose.ledger.json');
const CORPUS = path.join(__dirname, 'behaviours', 'macro-metrics.beh');

// Closed sets on purpose. A disposition or a shape I can invent per-AC is a
// disposition that means whatever I need it to mean the day I write it.
const DISPOSITIONS = new Set(['encoded', 'partial', 'contract', 'refused', 'implementation', 'inexpressible']);
const SHAPES = new Set([
  'cardinality',    // no quantification over a collection: "all 9", "each card"
  'negation',       // no absence assertion: "no axis labels", "does not affect others"
  'transient',      // no during-clause: "while fetching, a skeleton is shown"
  'attribute',      // no assertion on a noun's state or value: "default is Max"
  'relation',       // no relation between two nouns: "excluded from the other dropdown"
  'ordering',       // no ordering over a collection
  'spatial',        // no layout or viewport: "side by side", "sticky as you scroll"
  'timing',         // no timing claim: "renders instantly"
  'interpolation',  // no templated literal: "{Numerator label} / {Denominator label}"
  'scoping',        // no way to scope a noun to a region: "the Max button in #compare"
  'verb',           // the 8-word step vocabulary has no word for the action (hover, scroll)
  'orphaned',       // `contract` needs a host behaviour, and this AC's behaviour is unexpressible
]);

function extractAcs(src) {
  const out = [];
  let story = '';
  src.split('\n').forEach((l, i) => {
    const s = /^## (Story \d+)/.exec(l);
    if (s) story = s[1];
    if (/^- \[ \]/.test(l)) out.push({ line: i + 1, story, text: l.replace(/^- \[ \] /, '') });
  });
  return out;
}

function audit(ledger, behaviours) {
  const problems = [];
  const ids = new Set(behaviours.map((b) => b.id));
  const asked = new Set();
  const tally = {};
  const shapes = {};

  for (const ac of ledger.acs) {
    const at = `AC line ${ac.line}`;
    if (!DISPOSITIONS.has(ac.disposition)) {
      problems.push(`${at}: unaccounted — disposition "${ac.disposition}" is not one of ${[...DISPOSITIONS].join('|')}`);
      continue;
    }
    tally[ac.disposition] = (tally[ac.disposition] || 0) + 1;

    for (const s of ac.shapes || []) {
      if (!SHAPES.has(s)) problems.push(`${at}: shape "${s}" is not in the taxonomy — invent a shape and the taxonomy stops meaning anything`);
      else shapes[s] = (shapes[s] || 0) + 1;
    }
    for (const id of ac.behaviours || []) {
      if (!ids.has(id)) problems.push(`${at}: names ${id}, which is not in the corpus`);
      asked.add(id);
    }

    // The invariants that stop a disposition being a shrug.
    const hasB = (ac.behaviours || []).length > 0;
    const hasS = (ac.shapes || []).length > 0;
    if (ac.disposition === 'encoded' && (!hasB || hasS)) problems.push(`${at}: "encoded" must name a behaviour and leave nothing unmet`);
    if (ac.disposition === 'partial' && !(hasB && hasS)) problems.push(`${at}: "partial" must name both the behaviour that carries it AND what did not fit`);
    if ((ac.disposition === 'contract' || ac.disposition === 'refused') && !hasB) problems.push(`${at}: "${ac.disposition}" must name the behaviour it lives on`);
    if (ac.disposition === 'inexpressible' && !hasS) problems.push(`${at}: "inexpressible" must name which missing shape — otherwise it is "too hard"`);
    if (ac.disposition === 'implementation' && (hasB || !ac.note)) problems.push(`${at}: "implementation" carries no behaviour and must say what it prescribes`);
  }

  // The one that guards against ME. A behaviour nothing asked for is a behaviour
  // written to make the corpus look better than the document it came from.
  for (const id of ids) {
    if (!asked.has(id)) problems.push(`corpus: ${id} is in the corpus but no acceptance criterion names it — where did it come from?`);
  }

  return { problems, tally, shapes, asked };
}

// Not a test — a demonstration, because "the noun namespace is global" is the
// kind of claim that reads as pedantry until you see the wrong line come out.
function demoCollision() {
  const bindings = JSON.parse(fs.readFileSync(path.join(__dirname, 'bindings.json'), 'utf8'));
  const src = 'behaviour BEH-DEMO "a macro-metrics behaviour that says page:Home"\n  actor visitor\n  when opens page:Home';
  const { behaviours, symbols } = resolve(parse(src, 'demo.beh'));
  const { code } = generate(behaviours[0], bindings, symbols);
  console.log('── --demo-collision: what a second app gets for free ──');
  console.log("   macro-metrics' home is /macro-metrics/. snip-it's page:Home is './'.");
  console.log('   A macro-metrics corpus that writes page:Home emits:\n');
  console.log(code.split('\n').map((l) => '   ' + l).join('\n'));
  console.log('\n   No unbound-noun warning, no conflict, and the test RUNS — against the');
  console.log('   wrong app. Every noun in bindings.json is global across every corpus.');
}

function main(argv) {
  if (argv.includes('--demo-collision')) { demoCollision(); return 0; }

  if (!fs.existsSync(LEDGER)) { console.error(`cannot look: no ledger at ${LEDGER}`); return 2; }
  if (!fs.existsSync(CORPUS)) { console.error(`cannot look: no corpus at ${CORPUS}`); return 2; }

  let ledger;
  try { ledger = JSON.parse(fs.readFileSync(LEDGER, 'utf8')); }
  catch (e) { console.error(`cannot look: ledger is not readable JSON — ${e.message}`); return 2; }
  if (!ledger.acs || !ledger.acs.length) { console.error('cannot look: ledger holds 0 acceptance criteria'); return 2; }

  const { behaviours } = resolve(parse(fs.readFileSync(CORPUS, 'utf8'), 'macro-metrics.beh'));
  if (!behaviours.length) { console.error('cannot look: the corpus resolved to 0 behaviours'); return 2; }

  const { problems, tally, shapes } = audit(ledger, behaviours);

  // Drift. The ledger vendors each AC's text so it is runnable with the source
  // repo absent; given the source it must still agree, or the accounting is of a
  // document that no longer exists.
  const si = argv.indexOf('--source');
  let drift = [];
  if (si >= 0) {
    const p = argv[si + 1];
    if (!p || !fs.existsSync(p)) { console.error(`cannot look: --source ${p || '(missing)'} does not exist`); return 2; }
    const live = extractAcs(fs.readFileSync(p, 'utf8'));
    if (!live.length) { console.error(`cannot look: no acceptance criteria found in ${p}`); return 2; }
    if (live.length !== ledger.acs.length) {
      drift.push(`the source has ${live.length} acceptance criteria; the ledger accounts for ${ledger.acs.length}`);
    }
    for (const l of live) {
      const rec = ledger.acs.find((a) => a.line === l.line);
      if (!rec) drift.push(`source line ${l.line} has no ledger entry: "${l.text.slice(0, 60)}…"`);
      else if (rec.text !== l.text) drift.push(`source line ${l.line} has changed since the ledger was written`);
    }
  }

  console.log(`── prose-audit: ${path.basename(ledger.source.path)} @ ${ledger.source.rev.slice(0, 7)} ──`);
  console.log(`   ${ledger.acs.length} acceptance criteria, ${behaviours.length} behaviours in the corpus\n`);
  const order = ['encoded', 'partial', 'contract', 'refused', 'implementation', 'inexpressible'];
  for (const d of order) console.log(`   ${d.padEnd(15)} ${String(tally[d] || 0).padStart(3)}`);
  console.log('\n   missing shapes, by how often the document needed one:');
  for (const [s, n] of Object.entries(shapes).sort((a, b) => b[1] - a[1])) console.log(`   ${s.padEnd(15)} ${String(n).padStart(3)}`);

  const carried = (tally.encoded || 0) + (tally.partial || 0) + (tally.contract || 0) + (tally.refused || 0);
  console.log(`\n   carried by the notation in some form   ${carried}/${ledger.acs.length}`);
  console.log(`   fully carried, nothing left over       ${tally.encoded || 0}/${ledger.acs.length}`);
  console.log('   ⚠️  "carried" counts partials and contracts. Read the ledger, not this line.');

  if (si < 0) console.log('\n   (no --source given: the ledger was checked against itself, NOT against the live document)');
  else if (!drift.length) console.log('\n   ✅ the source document still matches the ledger.');

  if (problems.length || drift.length) {
    console.log('');
    for (const d of drift) console.log(`   ✗ drift: ${d}`);
    for (const p of problems) console.log(`   ✗ ${p}`);
    console.log(`\n   ${problems.length + drift.length} problem(s).`);
    return 1;
  }
  return 0;
}

if (require.main === module) process.exit(main(process.argv.slice(2)));
module.exports = { main, audit, extractAcs, DISPOSITIONS, SHAPES };
