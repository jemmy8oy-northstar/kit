#!/usr/bin/env node
'use strict';
/**
 * ───────────────── do two readings of one brief converge? ─────────────────
 *
 * James, claude-code-bot#92 (2026-09-06): "branch off the initial commit or
 * something and give a go building them from scratch based off my descriptions
 * and comments."
 *
 * WHY THIS TOOL EXISTS. Kit's central design bet is that glue binds to NOUNS, so
 * that it grows with the app's vocabulary rather than with the spec. Everything
 * measured so far measured that bet BACKWARDS: every corpus was reverse-
 * engineered from software that already shipped, so the noun names were read off
 * a running app and the question "would anyone else have picked these names?"
 * could not arise.
 *
 * Forwards it is the whole game. A noun is an identifier: `button:LogToday` binds
 * once and every behaviour mentioning it reuses that binding. If two people
 * reading the same brief write `button:LogToday` and `control:MarkDone`, they have
 * written two incompatible specifications of the same product, and NOTHING in Kit
 * would say so — both parse, both refuse identically for want of bindings, and
 * both report the same honest 0%.
 *
 * So this measures agreement between corpora that are supposed to describe the
 * same thing. It is deliberately a MEASUREMENT AND NOT A GATE: there is no
 * threshold here, no exit 1 on low agreement, because what counts as enough
 * agreement is a product judgement (how much reconciliation a user should have to
 * do) and inventing a number would be me answering that quietly.
 *
 * ⚠️ WHAT A HIGH SCORE WOULD AND WOULD NOT MEAN. Agreement between two readings
 * is an UPPER BOUND on how well a forward corpus could match an app built from a
 * different reading — not evidence that either reading is right. Both can agree
 * and both be wrong about what James wanted. The brief is the only authority on
 * that and this tool never reads it.
 */

const fs = require('fs');
const path = require('path');
const { parse } = require('./kit');

const BEH_DIR = path.join(__dirname, 'behaviours');

/** Every kind:Name a corpus references on a step, as a Set. */
function nounsOf(behaviours) {
  const out = new Set();
  for (const b of behaviours) {
    for (const s of b.steps || []) {
      for (const r of s.refs || []) {
        if (r.kind && r.kind !== 'literal') out.add(`${r.kind}:${r.name}`);
      }
    }
  }
  return out;
}

/**
 * Normalising a noun for the "did they mean the same thing" question.
 *
 * ⚠️ THE ONE JUDGEMENT IN THIS FILE, AND IT IS DELIBERATELY WEAK. Kind is dropped
 * and case/separators are flattened, so `button:LogToday`, `control:log-today` and
 * `Button:logtoday` collapse together. That is generous ON PURPOSE: it makes the
 * loose score an OPTIMISTIC bound, so a low loose score cannot be dismissed as an
 * artefact of fussy matching. It does NOT do stemming, synonyms or embeddings —
 * `button:LogToday` and `button:MarkDone` stay different, because deciding they
 * are the same is exactly the semantic judgement Kit has always said it will not
 * fake ([[structural conflict detection is free; semantic is not]]).
 */
function loose(noun) {
  return noun.slice(noun.indexOf(':') + 1).toLowerCase().replace(/[^a-z0-9]/g, '');
}

function jaccard(a, b) {
  const inter = [...a].filter((x) => b.has(x));
  const union = new Set([...a, ...b]);
  // A pair of empty corpora is 0-of-0. Reported as null rather than 1.0 or 0.0:
  // "they agree perfectly" and "there was nothing to compare" are different
  // readings and a ratio cannot carry both.
  return union.size === 0 ? null : { inter: inter.length, union: union.size, ratio: inter.length / union.size };
}

function load(app, opts = {}) {
  const p = path.join(opts.dir || BEH_DIR, `${app}.beh`);
  if (!fs.existsSync(p)) return { app, fatal: `no corpus at behaviours/${app}.beh` };
  const behaviours = parse(fs.readFileSync(p, 'utf8'));
  if (!behaviours.length) return { app, fatal: `${app}.beh parsed to zero behaviours` };
  const nouns = nounsOf(behaviours);
  const byLoose = new Map();
  for (const n of nouns) {
    if (!byLoose.has(loose(n))) byLoose.set(loose(n), []);
    byLoose.get(loose(n)).push(n);
  }
  return { app, behaviours: behaviours.length, nouns, byLoose };
}

function compare(a, b) {
  const strict = jaccard(a.nouns, b.nouns);
  const lo = jaccard(new Set(a.byLoose.keys()), new Set(b.byLoose.keys()));

  // The population this whole tool was built to surface: the same word, a
  // different kind. `field:Sleep` vs `input:Sleep` is one thing the app must
  // provide, described twice, and it is the cheapest possible reconciliation —
  // yet Kit treats the two as unrelated nouns needing two bindings.
  const kindMismatch = [];
  for (const [k, av] of a.byLoose) {
    const bv = b.byLoose.get(k);
    if (!bv) continue;
    for (const an of av) for (const bn of bv) if (an !== bn) kindMismatch.push([an, bn]);
  }

  return {
    strict,
    loose: lo,
    kindMismatch,
    // Printed, not just counted. A tool that shows only the disagreement lets a
    // low score be read as "they agreed on the important things and differed at
    // the edges" — which is checkable, so it should be checked rather than
    // assumed. Naming the shared nouns is what makes the number falsifiable.
    shared: [...a.nouns].filter((n) => b.nouns.has(n)).sort(),
    onlyA: [...a.nouns].filter((n) => !b.byLoose.has(loose(n))).sort(),
    onlyB: [...b.nouns].filter((n) => !a.byLoose.has(loose(n))).sort(),
  };
}

function render(loaded, pairs) {
  const L = [];
  L.push('── noun convergence — do independent readings of one brief agree? ──');
  L.push('');
  L.push('  corpus                    beh   nouns');
  for (const c of loaded) {
    L.push(`  ${c.app.padEnd(24)}  ${String(c.behaviours).padEnd(4)}  ${c.nouns.size}`);
  }
  L.push('');

  for (const { a, b, r } of pairs) {
    L.push(`═══ ${a} ↔ ${b} ═══`);
    L.push('');
    const s = r.strict, l = r.loose;
    L.push(`  exact noun agreement   ${s ? `${s.inter}/${s.union}  ${(s.ratio * 100).toFixed(0)}%` : 'nothing to compare'}`);
    L.push(`  ignoring kind + case   ${l ? `${l.inter}/${l.union}  ${(l.ratio * 100).toFixed(0)}%` : 'nothing to compare'}`);
    L.push('');
    // Named first, and never suppressed when empty. "They agreed on nothing" is
    // the single most important thing this tool can report, and a section that
    // silently disappears reports it by absence — which reads as an oversight.
    L.push(r.shared.length ? '  AGREED ON:' : '  AGREED ON: nothing — not one noun in common');
    if (r.shared.length) L.push(`      ${r.shared.join(', ')}`);
    L.push('');
    if (r.kindMismatch.length) {
      L.push('  SAME WORD, DIFFERENT NOUN — two bindings for one thing:');
      for (const [x, y] of r.kindMismatch) L.push(`      ${x}   ≠   ${y}`);
      L.push('');
    }
    if (r.onlyA.length) {
      L.push(`  only in ${a}:`);
      L.push(`      ${r.onlyA.join(', ')}`);
      L.push('');
    }
    if (r.onlyB.length) {
      L.push(`  only in ${b}:`);
      L.push(`      ${r.onlyB.join(', ')}`);
      L.push('');
    }
  }
  return L.join('\n');
}

function main(argv) {
  const args = argv.slice(2);
  const asJson = args.includes('--json');
  const apps = args.filter((a) => !a.startsWith('--'));

  if (apps.length < 2) {
    process.stdout.write([
      'usage: node converge.js <appA> <appB> [<appC> ...] [--json]',
      '',
      '  Measures how far corpora that describe the same product agree on NOUNS,',
      '  which is Kit\'s binding unit. Every pair is compared.',
      '',
      '  Reports a measurement, never a verdict — there is no threshold and it',
      '  never exits 1 on low agreement. What counts as enough is a product call.',
      '',
    ].join('\n') + '\n');
    return 2;
  }

  // Arrow, not a bare `apps.map(load)` — map passes the index as the second
  // argument, which would land in `opts` and only work by accident.
  const loaded = apps.map((a) => load(a));
  const bad = loaded.filter((c) => c.fatal);
  if (bad.length) {
    // Could-not-look, never conflated with a measurement of zero agreement.
    for (const c of bad) process.stderr.write(`converge: ${c.fatal}\n`);
    return 2;
  }

  const pairs = [];
  for (let i = 0; i < loaded.length; i++) {
    for (let j = i + 1; j < loaded.length; j++) {
      pairs.push({ a: loaded[i].app, b: loaded[j].app, r: compare(loaded[i], loaded[j]) });
    }
  }

  if (asJson) {
    process.stdout.write(JSON.stringify({
      corpora: loaded.map((c) => ({ app: c.app, behaviours: c.behaviours, nouns: [...c.nouns].sort() })),
      pairs,
    }, null, 2) + '\n');
  } else {
    process.stdout.write(render(loaded, pairs) + '\n');
  }
  return 0;
}

module.exports = { nounsOf, loose, jaccard, compare, load, render, main };

if (require.main === module) process.exit(main(process.argv));
