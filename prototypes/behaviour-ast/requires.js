#!/usr/bin/env node
'use strict';

// ─────────────────── the required surface: what the app must provide ───────────────────
//
// James, claude-code-bot#92 (2026-09-06): "Whilst I am away you are kit product
// manager … Progress kit." and "Adding extra spec / notifying assumed spec as you
// find experience that doesn't match intended."
//
// WHY THIS EXISTS. docs/trials/lend-forward-run.md ran Kit forwards for the first
// time — a corpus for an app that does not exist — and scored 0/27. The 0% was
// correct: with no app, there are no locators, so refusing is the honest answer.
// But the run ended there, and finding 4 named why:
//
//     "Today the refusal constrains nothing, because nobody is told what would
//      satisfy it."
//
// A user pointing Kit at a new project gets a list of everything missing. That is
// a true and complete report and it is not a step toward an app. This file is the
// other half: given the same corpus, say WHAT THE APP MUST PROVIDE.
//
// ⚠️ WHAT THIS DELIBERATELY DOES NOT DO. It does not make bindings.json an output,
// it does not write bindings, and it does not change what `kit.js` binds against.
// Inverting stage 4 for real is a product decision about what Kit IS, and that is
// James's (lend-forward-run.md finding 4). This is a READER: it derives the
// contract, prints it, and stops. That keeps the decision his while making it a
// decision with a working demonstration attached instead of an argument.
//
// ─────────────────────────── the one real idea here ───────────────────────────
//
// The requirement is carried by the VERB, not by the noun's kind. `emit()` in
// kit.js switches on `step.verb` and never once looks at `ref.kind` — so `kind` is
// a naming convention with no semantics in the generator. That is why a corpus can
// invent `badge:`, `column:`, `shelf:` freely and nothing complains.
//
// It also means the app's obligations are fully derivable from the corpus alone:
//
//     when opens page:Shelf      ⇒ the app must have a ROUTE that serves it
//     then lands on page:Shelf   ⇒ ... and a URL a regex can recognise
//     then sees region:LoanList  ⇒ an element that is VISIBLE and addressable
//     given loan:Overdue         ⇒ a way to PUT the app in that state (not UI)
//
// ⚠️ AND THE TWO ARE DIFFERENT KEYS, WHICH IS WHERE THE BUGS WERE. `opens` needs
// `route`; `lands` needs `urlPattern`. A page bound with only `route` is counted
// BOUND by boundNouns() (it tests `hasOwnProperty`), reports NO missing noun, and
// still refuses the `lands` step — telling the user their surface is complete when
// it is not. Worse, `attaches` and `fills` do not refuse at all: they emit
// `page.null.setInputFiles(undefined)` and `getByLabel(undefined)`, which are
// counted as GENERATED and cannot run. Both are proved by tests in kit.test.js.
//
// None of this bites any existing corpus, and that is the point: all five were
// reverse-engineered from apps that already shipped, so every binding was written
// with the verb in front of the author. Backwards you always add exactly the key
// you needed. Forwards nobody tells you the key exists.

// ─────────────────────────── 1. the requirement table ───────────────────────────
// Each requirement is a PREDICATE over a binding plus the sentence an implementer
// needs. Predicates, not key lists, because "addressable" is genuinely an OR of
// three shapes and flattening it to keys would lie about two of them.
//
// ⚠️ THIS TABLE MIRRORS `emit()` IN kit.js AND CAN DRIFT OUT OF SYNC SILENTLY —
// which would make it exactly the kind of confident-and-wrong document this whole
// project exists to argue against. So it is not trusted: kit.test.js runs
// every verb through the REAL `generate()` twice, once with a binding that
// satisfies the predicate and once with one that does not, and asserts the
// generator agrees. If someone edits emit(), that test goes red here.

const ADDRESSABLE = {
  id: 'addressable',
  // Mirrors loc() in kit.js: getByRole(role, {name}) | getByLabel(label) | locator
  satisfied: (b) => !!(b && ((b.role && b.name) || b.label || b.locator)),
  surface: 'addressable from a test: a role plus an accessible name (preferred), an associated <label>, or a stable locator',
};

// Deliberately narrower than ADDRESSABLE, and the difference is the thesis of
// this file made concrete: `attaches` reaches its field through loc(), so any of
// the three shapes works, while `fills` only ever emits getByLabel() and so needs
// a label specifically. The SAME `field:` noun therefore owes different things
// depending on the verb it is used with. A table keyed by noun kind could not say
// this at all.
const LABEL = {
  id: 'label',
  satisfied: (b) => !!(b && b.label),
  surface: 'a form field with an associated <label> — `fills` addresses fields by label only, so a role or a bare locator will not do here',
};

const ROUTE = {
  id: 'route',
  satisfied: (b) => !!(b && b.route),
  surface: 'a route that serves this page',
};

const URL_PATTERN = {
  id: 'urlPattern',
  satisfied: (b) => !!(b && b.urlPattern),
  surface: 'a URL that a regex can recognise once navigation has settled — this is NOT the same obligation as the route, and a page reached by both `opens` and `lands` owes both',
};

const STATE = {
  id: 'state',
  satisfied: (b) => !!(b && b.state),
  surface: 'a way to put the app into this state before the test acts — a seed, a fixture, or a route mock. This is a precondition, not a thing on screen',
};

const FIXTURE = {
  id: 'fixture',
  satisfied: (b) => !!(b && b.fixture),
  surface: 'an upload fixture: a file name and a mime type',
};

// What each verb demands, and of which noun on the step. `pick` selects the ref a
// requirement lands on, because `attaches` puts two different obligations on two
// different nouns in one line.
const VERBS = {
  state: [{ req: STATE, pick: (nouns) => nouns.slice(0, 1) }],
  opens: [{ req: ROUTE, pick: (nouns) => nouns.slice(0, 1) }],
  lands: [{ req: URL_PATTERN, pick: (nouns) => nouns.slice(0, 1) }],
  activates: [{ req: ADDRESSABLE, pick: (nouns) => nouns.slice(0, 1) }],
  sees: [{ req: ADDRESSABLE, pick: (nouns) => nouns.slice(0, 1) }],
  shows: [{ req: ADDRESSABLE, pick: (nouns) => nouns.slice(0, 1) }],
  attaches: [
    { req: FIXTURE, pick: (nouns) => nouns.filter((n) => n.kind === 'file') },
    { req: ADDRESSABLE, pick: (nouns) => nouns.filter((n) => n.kind === 'field') },
  ],
  // `fills` is the odd one out and is handled in requirementsOf(): the noun on the
  // step is the FORM, but the obligation falls on fields named by a `provides`
  // line in a different behaviour. See the comment there.
  fills: [],
};

const VERB_LIST = Object.keys(VERBS);

// ─────────────────────────── 2. deriving the contract ───────────────────────────

function key(ref) {
  return `${ref.kind}:${ref.name}`;
}

// Every obligation a single step places on the app, as {nounKey, kind, name, req}.
function requirementsOf(step) {
  const out = [];
  const nouns = (step.refs || []).filter((r) => r.kind !== 'literal');

  // `fills form:X with ?fields` generates one getByLabel().fill() per field name,
  // and those names come from `provides form:X.fields = A, B, C` — which may sit
  // in a DIFFERENT behaviour. So the required fields are only visible after
  // resolve() has run, and a field named ONLY in a provides value is a required
  // surface that appears nowhere as a noun reference. Nothing else in Kit counts
  // those: boundNouns() walks step refs, so `field:DueDate` in trial-lend is
  // invisible to every measurement while being something the app must have.
  if (step.verb === 'fills') {
    const fields = (step.resolved && step.resolved.fields) || [];
    for (const f of fields) {
      out.push({ nounKey: `field:${f}`, kind: 'field', name: f, req: LABEL, verb: 'fills', at: step.at });
    }
    return out;
  }

  const rules = VERBS[step.verb];
  if (!rules) return out; // an unknown verb is emit()'s `default: return null`
  for (const rule of rules) {
    for (const ref of rule.pick(nouns)) {
      out.push({ nounKey: key(ref), kind: ref.kind, name: ref.name, req: rule.req, verb: step.verb, at: step.at });
    }
  }
  return out;
}

// The whole contract for a corpus: one entry per noun, carrying every obligation
// placed on it and whether the current bindings meet each one.
//
// Merged PER NOUN rather than per step on purpose — a noun used by four behaviours
// is one thing the implementer builds once, and the interesting case (a page owing
// both `route` and `urlPattern`) is only visible once the uses are merged.
function requirements(behaviours, bindings = {}) {
  const byNoun = new Map();

  for (const b of behaviours) {
    for (const step of b.steps || []) {
      for (const r of requirementsOf(step)) {
        if (!byNoun.has(r.nounKey)) {
          byNoun.set(r.nounKey, {
            noun: r.nounKey, kind: r.kind, name: r.name,
            needs: new Map(), usedBy: new Set(),
          });
        }
        const entry = byNoun.get(r.nounKey);
        entry.usedBy.add(b.id);
        if (!entry.needs.has(r.req.id)) {
          entry.needs.set(r.req.id, { id: r.req.id, surface: r.req.surface, verbs: new Set(), req: r.req });
        }
        entry.needs.get(r.req.id).verbs.add(r.verb);
      }
    }
  }

  const nouns = [...byNoun.values()].map((e) => {
    const binding = Object.prototype.hasOwnProperty.call(bindings, e.noun) ? bindings[e.noun] : null;
    const needs = [...e.needs.values()].map((n) => ({
      id: n.id,
      surface: n.surface,
      verbs: [...n.verbs].sort(),
      met: n.req.satisfied(binding),
    }));
    return {
      noun: e.noun, kind: e.kind, name: e.name,
      usedBy: [...e.usedBy].sort(),
      bound: binding !== null,
      needs,
      // The distinction the existing report cannot make. `bound` is what
      // boundNouns() measures (the key is present). `satisfied` is what actually
      // decides whether a step generates.
      satisfied: needs.every((n) => n.met),
    };
  }).sort((a, b) => a.noun.localeCompare(b.noun));

  return {
    nouns,
    // Three populations, deliberately kept apart, because collapsing them is the
    // defect this file was written to expose:
    //   missing     — no binding at all. Today's "unbound noun".
    //   insufficient— a binding EXISTS and does not carry what the verb needs.
    //                 Reported by nothing before this file.
    //   satisfied   — generatable.
    missing: nouns.filter((n) => !n.bound),
    insufficient: nouns.filter((n) => n.bound && !n.satisfied),
    satisfied: nouns.filter((n) => n.satisfied),
  };
}

// Why did THIS step refuse? Used by the report so a refusal names its own cause
// instead of leaving the user to diff two files by eye.
function reasonsFor(step, bindings = {}) {
  const out = [];
  for (const r of requirementsOf(step)) {
    const binding = Object.prototype.hasOwnProperty.call(bindings, r.nounKey) ? bindings[r.nounKey] : null;
    if (r.req.satisfied(binding)) continue;
    out.push({
      noun: r.nounKey,
      need: r.req.id,
      surface: r.req.surface,
      why: binding === null ? 'no binding' : `binding exists but has no \`${r.req.id}\``,
    });
  }
  return out;
}

// ─────────────────────────── 3. the report ───────────────────────────

function render(app, report) {
  const L = [];
  const { nouns, missing, insufficient, satisfied } = report;

  L.push(`── required surface — ${app} ──`);
  L.push('');
  L.push('What an app must provide for this corpus to generate. Derived from the');
  L.push('verbs each noun is used with; nothing here is invented.');
  L.push('');
  L.push(`  nouns referenced   ${nouns.length}`);
  L.push(`  satisfied          ${satisfied.length}`);
  L.push(`  no binding         ${missing.length}`);
  L.push(`  binding too thin   ${insufficient.length}`);
  L.push('');

  if (insufficient.length) {
    // First, because it is the population nothing else reports and the one that
    // reads as healthy everywhere else in Kit.
    L.push('⚠️  BOUND BUT NOT ENOUGH — these count as bound today and still refuse:');
    L.push('');
    for (const n of insufficient) {
      L.push(`  ${n.noun}`);
      for (const need of n.needs.filter((x) => !x.met)) {
        L.push(`      needs \`${need.id}\` — used with ${need.verbs.map((v) => `\`${v}\``).join(', ')}`);
        L.push(`      ${need.surface}`);
      }
    }
    L.push('');
  }

  if (missing.length) {
    L.push('The app must provide:');
    L.push('');
    for (const n of missing) {
      L.push(`  ${n.noun}   (${n.usedBy.join(', ')})`);
      for (const need of n.needs) {
        L.push(`      ${need.surface}`);
      }
      L.push('');
    }
  }

  if (!missing.length && !insufficient.length) {
    L.push('Every noun this corpus references is satisfied by the current bindings.');
    L.push('');
  }

  return L.join('\n');
}

// ─────────────────────────── 4. CLI ───────────────────────────
// kit.js is required lazily, inside main(), so this module stays dependency-free
// and kit.js can require IT for refusal diagnostics without a require cycle.

function main(argv) {
  const fs = require('fs');
  const path = require('path');
  const { parse, resolve } = require('./kit');

  const args = argv.slice(2);
  const asJson = args.includes('--json');
  const asCheck = args.includes('--check');
  const app = args.find((a) => !a.startsWith('--'));

  if (!app) {
    process.stdout.write([
      'usage: node requires.js <app> [--json] [--check]',
      '',
      '  Prints what an application must provide for <app>\'s corpus to generate.',
      '',
      '  --json    machine-readable, for a UI or a generator',
      '  --check   exit 1 if any referenced noun is unsatisfied. Unlike the bound',
      '            count, this fails on a binding that exists but is too thin.',
      '',
    ].join('\n') + '\n');
    return 2;
  }

  const corpus = path.join(__dirname, 'behaviours', `${app}.beh`);
  if (!fs.existsSync(corpus)) {
    process.stderr.write(`requires: no corpus at ${corpus} — could not look\n`);
    return 2; // never conflated with a pass, same three-valued rule as check.js
  }

  const behaviours = parse(fs.readFileSync(corpus, 'utf8'));
  resolve(behaviours); // fills each step's `resolved`, which `fills` needs
  const bindings = JSON.parse(fs.readFileSync(path.join(__dirname, 'bindings.json'), 'utf8'));
  const report = requirements(behaviours, bindings);

  if (asJson) {
    process.stdout.write(JSON.stringify(report, null, 2) + '\n');
  } else {
    process.stdout.write(render(app, report) + '\n');
  }

  if (asCheck) {
    const bad = report.missing.length + report.insufficient.length;
    if (bad) {
      process.stderr.write(`\nrequires: ${bad} noun(s) unsatisfied — the app does not yet provide this surface\n`);
      return 1;
    }
  }
  return 0;
}

module.exports = {
  requirements, requirementsOf, reasonsFor, render,
  VERBS, VERB_LIST, ADDRESSABLE, LABEL, ROUTE, URL_PATTERN, STATE, FIXTURE,
};

if (require.main === module) process.exit(main(process.argv));
