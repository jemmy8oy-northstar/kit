#!/usr/bin/env node
'use strict';
/**
 * Kit prototype — behaviour tree → resolved tree → executable test.
 *
 * Built to answer ONE falsifiable question from claude-code-bot#68:
 *   can a behaviour tree generate a RUNNABLE test with no hand-written glue?
 *
 * Gherkin/Cucumber answered "no" — every step phrasing needs a step definition,
 * so the glue grows with the SPEC and the spec never constrains the app. The
 * claim under test here is that binding by NOUN instead of by STEP changes that:
 * glue grows with the app's vocabulary, which saturates, and each binding is
 * reused by every behaviour mentioning that noun.
 *
 * Dependency-free on purpose. This is a measuring instrument, not a product.
 */

const STEP_KEYS = new Set(['given', 'when', 'then', 'contract']);

// ─────────────────────────── 1. parse ───────────────────────────
// Line-based on purpose: James's requirement is that a human can write the tree
// by hand, and YAML and JSON both fail that on punctuation alone.

function parse(text, file = '<inline>') {
  const behaviours = [];
  let cur = null;
  const lines = text.split('\n');

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim();
    const at = `${file}:${i + 1}`;
    if (!line || line.startsWith('#')) continue;

    const m = /^behaviour\s+([A-Z][A-Z0-9-]*)\s+"(.*)"$/.exec(line);
    if (m) {
      cur = {
        id: m[1], title: m[2], actor: null, steps: [], unknowns: [], provides: [], serves: [], at,
        // Default `defined`/`approved` so a corpus written before this existed
        // still parses. The asymmetry is deliberate: an INFERENCE has to say so,
        // because the whole risk is an inference passing itself off as a
        // requirement. Silence means a human wrote it.
        source: { origin: 'defined', ref: null }, review: { state: 'approved', note: null },
      };
      behaviours.push(cur);
      continue;
    }
    if (!cur) throw new Error(`${at}: line outside a behaviour: ${line}`);

    const kw = line.split(/\s+/)[0];
    const rest = line.slice(kw.length).trim();

    if (kw === 'actor') { cur.actor = rest; continue; }

    // ── James's #68 decision, 2026-08-30 ──────────────────────────────────
    // "I like this default included but marked unreviewed."
    //
    // Prior art's third fatal failure mode is that the adjudication step is the
    // first thing a team skips, and default-include makes skipping free. The
    // answer is not to block on approval — it is to make the un-adjudicated
    // count VISIBLE, so skipping is a number someone can see rather than an
    // absence nobody can. `source` is what an approve/deny points AT six weeks
    // later; an inference that lives only in a chat log cannot be denied.
    if (kw === 'source') {
      const s = /^(defined|inferred)(?:\s+(.+))?$/.exec(rest);
      if (!s) throw new Error(`${at}: source wants "defined"|"inferred" [ref], got: ${rest}`);
      cur.source = { origin: s[1], ref: s[2] || null };
      // An inference is unreviewed until someone says otherwise. Writing
      // `source inferred` and having it default to approved would reintroduce
      // exactly the silence this mechanism exists to remove.
      if (s[1] === 'inferred' && !cur.reviewExplicit) {
        cur.review = { state: 'unreviewed', note: null };
      }
      continue;
    }

    if (kw === 'review') {
      const r = /^(unreviewed|approved|denied)(?:\s+(.+))?$/.exec(rest);
      if (!r) throw new Error(`${at}: review wants "unreviewed"|"approved"|"denied" [note], got: ${rest}`);
      // A denial without a correction is a hole, not a decision — his #68 point
      // that "on a deny a required indication of what correct behaviour actually
      // looks like should take place". A bare denial deletes a line; a denial
      // with a correction compounds into the corpus.
      if (r[1] === 'denied' && !r[2]) throw new Error(`${at}: a denied behaviour must state the correction`);
      cur.review = { state: r[1], note: r[2] || null };
      cur.reviewExplicit = true;
      continue;
    }

    // ── James's #68 decision, kit#3 2026-08-30 ────────────────────────────
    // "I feel like the api layer is inferred from what needs to be displayed in
    // the ui. From the frontend first development approach. Expose only what is
    // required to display kind of approach."
    //
    // So Kit does NOT grow a second assertion layer for HTTP. An API behaviour
    // exists to make some displayed behaviour possible, and `serves` is where it
    // says which one. The finding is the ABSENCE: an inferred behaviour that
    // serves nothing documented is surface nothing displays.
    if (kw === 'serves') {
      const s = /^([A-Z][A-Z0-9-]*)$/.exec(rest);
      if (!s) throw new Error(`${at}: serves wants a behaviour id, got: ${rest}`);
      cur.serves.push({ id: s[1], at });
      continue;
    }

    if (kw === 'provides') {
      // Where an LLM's INFERENCE is written down. It has to be an explicit,
      // reviewable line: James's approve/deny needs something to point at
      // LATER, and an inference that lives only in a chat message cannot be
      // denied six weeks on.
      const p = /^([a-z]+):([A-Za-z0-9_]+)\.([a-zA-Z_]+)\s*=\s*(.+)$/.exec(rest);
      if (!p) throw new Error(`${at}: provides wants <kind>:<Name>.<slot> = <value>, got: ${rest}`);
      cur.provides.push({
        kind: p[1], name: p[2], slot: p[3],
        value: p[4].split(',').map((s) => s.trim()).filter(Boolean),
        from: cur.id, at,
      });
      continue;
    }

    if (STEP_KEYS.has(kw)) {
      const step = kw === 'contract'
        ? { kind: 'contract', verb: 'contract', text: rest, refs: [], holes: [], at }
        : { kind: kw, ...parseStep(rest, at), at };
      cur.steps.push(step);
      for (const h of step.holes) cur.unknowns.push({ ...h, at });
      continue;
    }

    throw new Error(`${at}: unrecognised keyword "${kw}"`);
  }
  return behaviours;
}

// A step is a verb plus noun references. `?slot` marks a hole. A bare noun with
// no verb (`given transcription:Completed`) is a state precondition.
function parseStep(rest, at) {
  const first = rest.split(/\s+/)[0];
  const verb = /^[a-z]+:[A-Za-z0-9_]+$/.test(first) ? 'state' : first;

  const refs = [];
  const holes = [];
  const re = /\?([a-zA-Z_]+)|([a-z]+):([A-Za-z0-9_]+)|"([^"]*)"/g;
  let m;
  while ((m = re.exec(rest)) !== null) {
    if (m[1] !== undefined) holes.push({ slot: m[1] });
    else if (m[2] !== undefined) refs.push({ kind: m[2], name: m[3] });
    else refs.push({ kind: 'literal', name: m[4] });
  }
  return { verb, text: rest, refs, holes };
}

// ─────────────────────────── 2. resolve ───────────────────────────
// James's actual insight, and the part no BDD tool has: an unknown in one
// behaviour is answered by a DIFFERENT behaviour. "reader user opens post, at a
// glance user can read x y z. These are now inferred as required fields."
//
// It only works if the corpus shares ONE symbol table keyed by noun. That is the
// architectural consequence worth naming: behaviours cannot be isolated trees.
// A per-file AST can never fill a hole from elsewhere, whatever its syntax.

function resolve(behaviours) {
  const symbols = new Map(); // "kind:Name.slot" -> { value, contributors[], conflict[] }

  for (const b of behaviours) {
    for (const p of b.provides) {
      const key = `${p.kind}:${p.name}.${p.slot}`;
      const existing = symbols.get(key);
      if (!existing) {
        symbols.set(key, { value: p.value, contributors: [p.from], at: p.at });
      } else if (sameValue(existing.value, p.value)) {
        existing.contributors.push(p.from);
      } else {
        // Two behaviours asserting different values for the same slot is
        // James's "this conflicts with a previous behaviour, supersede?" — and
        // it needs no embeddings and no LLM. It falls out of the Map.
        (existing.conflict = existing.conflict || []).push({ from: p.from, value: p.value, at: p.at });
      }
    }
  }

  const resolved = behaviours.map((b) => {
    const filled = [];
    const open = [];
    // A hole is written `?fields` on a step that also names the noun it belongs
    // to, so the lookup key comes from the step's own refs.
    for (const step of b.steps) {
      for (const h of step.holes) {
        const owner = step.refs.find((r) => r.kind !== 'literal');
        if (!owner) { open.push({ slot: h.slot, key: `?${h.slot}`, at: step.at }); continue; }
        const key = `${owner.kind}:${owner.name}.${h.slot}`;
        const sym = symbols.get(key);
        if (sym) {
          filled.push({ key, value: sym.value, from: sym.contributors, at: step.at });
          // Hand the resolved value back to the STEP, so a hole filled from
          // another behaviour actually generates. Reporting the fill without
          // using it would make the mechanism decorative.
          (step.resolved = step.resolved || {})[h.slot] = sym.value;
        } else {
          open.push({ key, slot: h.slot, at: step.at });
        }
      }
    }
    return { ...b, filled, open, symbols };
  });

  const conflicts = [];
  for (const [key, sym] of symbols) {
    if (sym.conflict) {
      conflicts.push({ key, held: sym.value, holders: sym.contributors, challengers: sym.conflict });
    }
  }

  return { behaviours: resolved, symbols, conflicts };
}

function sameValue(a, b) {
  return a.length === b.length && a.every((x, i) => x === b[i]);
}

// ─────────────────────────── 3. bind + generate ───────────────────────────
// The claim being measured. A binding says how a NAME in the spec becomes a
// thing on the page. There is one per noun — not one per step, and not one per
// behaviour. That is the entire difference from a Cucumber step definition.

function generate(behaviour, bindings, symbols = new Map()) {
  const body = [];
  const missing = new Set();
  const stats = { generated: 0, contract: 0, ungenerated: 0 };

  const bind = (ref) => {
    if (!ref) return null;
    const b = bindings[`${ref.kind}:${ref.name}`];
    if (!b) missing.add(`${ref.kind}:${ref.name}`);
    return b || null;
  };

  for (const step of behaviour.steps) {
    if (step.kind === 'contract') {
      // Deliberately NOT generated, and deliberately still counted. A wire
      // contract is not something a user can see, so a behaviour tree has no
      // honest way to express it — hiding these would flatter the coverage
      // number by removing exactly the steps that fail.
      body.push(`// CONTRACT (not derivable from a behaviour): ${step.text}`);
      stats.contract++;
      continue;
    }
    const lines = emit(step, bind, bindings, symbols);
    if (lines) { body.push(...lines); stats.generated += lines.length; }
    else { body.push(`// UNGENERATED: ${step.kind} ${step.text}`); stats.ungenerated++; }
  }

  const code = [
    `test(${JSON.stringify(`[${behaviour.id}] ${behaviour.title}`)}, async ({ page }) => {`,
    ...body.map((l) => '  ' + l),
    '});',
  ].join('\n');

  return { code, missing: [...missing], stats };
}

function emit(step, bind, bindings = {}, symbols = new Map()) {
  const nouns = step.refs.filter((r) => r.kind !== 'literal');
  const literal = step.refs.find((r) => r.kind === 'literal');
  const loc = (b) =>
    b.role ? `getByRole(${JSON.stringify(b.role)}, { name: ${JSON.stringify(b.name)}${b.exact ? ', exact: true' : ''} })`
    : b.label ? `getByLabel(${JSON.stringify(b.label)})`
    : b.locator || null;

  switch (step.verb) {
    case 'state': {
      const b = bind(nouns[0]);
      return b && b.state ? [`await ${b.state};`] : null;
    }
    case 'opens': {
      const noun = nouns[0];
      const b = bind(noun);
      if (!b || !b.route) return null;
      // A parameterised route is a HOLE, and the first version of this quietly
      // stripped `:transcriptionId` and emitted `./editor/` — a test that runs,
      // passes nothing, and looks right. Refusing is the point of the design;
      // guessing here would have been the exact defect being argued against.
      const params = b.route.match(/:(\w+)/g) || [];
      let route = b.route;
      for (const p of params) {
        const sym = symbols.get(`${noun.kind}:${noun.name}.${p.slice(1)}`);
        if (!sym) return null;
        route = route.replace(p, sym.value.join(''));
      }
      return [`await page.goto(${JSON.stringify(route)});`];
    }
    case 'activates': {
      const b = bind(nouns[0]);
      const l = b && loc(b);
      return l ? [`await page.${l}.click();`] : null;
    }
    case 'sees': {
      const b = bind(nouns[0]);
      const l = b && loc(b);
      return l ? [`await expect(page.${l}).toBeVisible();`] : null;
    }
    case 'shows': {
      const b = bind(nouns[0]);
      const l = b && loc(b);
      return l && literal ? [`await expect(page.${l}).toContainText(${JSON.stringify(literal.name)});`] : null;
    }
    case 'attaches': {
      const file = bind(nouns.find((n) => n.kind === 'file'));
      const field = bind(nouns.find((n) => n.kind === 'field'));
      if (!file || !field) return null;
      return [`await page.${loc(field)}.setInputFiles(${JSON.stringify(file.fixture)});`];
    }
    case 'lands': {
      const b = bind(nouns[0]);
      return b && b.urlPattern ? [`await expect(page).toHaveURL(new RegExp(${JSON.stringify(b.urlPattern)}));`] : null;
    }
    case 'fills': {
      // The payoff of the whole unknowns mechanism: this step named no field,
      // and the field it generates against came from a DIFFERENT behaviour.
      const fields = step.resolved && step.resolved.fields;
      if (!fields) return null;
      const lines = [];
      for (const f of fields) {
        const fb = bindings[`field:${f}`];
        if (!fb) return null;
        const fixture = Object.entries(bindings).find(([k, v]) => k.startsWith('file:') && v.fixture);
        lines.push(fb.label && fixture
          ? `await page.getByLabel(${JSON.stringify(fb.label)}).setInputFiles(${JSON.stringify(fixture[1].fixture)});`
          : `await page.getByLabel(${JSON.stringify(fb.label)}).fill('');`);
      }
      return lines;
    }
    default:
      return null;
  }
}

// ─────────────────────────── 4. coverage ───────────────────────────
// The enforcing constraint, and the only part that can go RED. A spec no build
// breaks on is a wish: web-template's testing-strategy.md is genuinely good,
// referenced by nothing, and therefore changed nothing.

function coverage(behaviours, testSources) {
  const named = new Set();
  for (const src of testSources) for (const m of src.matchAll(/\[([A-Z][A-Z0-9-]*)\]/g)) named.add(m[1]);
  return {
    covered: behaviours.filter((b) => named.has(b.id)),
    uncovered: behaviours.filter((b) => !named.has(b.id)),
    orphanTests: [...named].filter((id) => !behaviours.some((b) => b.id === id)),
  };
}

// ─────────────────────────── 5. adjudication ───────────────────────────
// The count that makes skipping visible. Prior art (North's own retrospective)
// says the collaboration/adjudication step is the first thing dropped, and
// James chose default-include — so the ONLY thing standing between that and a
// corpus quietly full of unreviewed machine guesses is this number being
// printed somewhere a person looks.
//
// It deliberately does not gate the build. An inference is not wrong; it is
// unexamined. Failing on it would train people to approve in bulk, which is the
// same silence with an audit trail.

function adjudication(behaviours) {
  const defined = behaviours.filter((b) => b.source.origin === 'defined');
  const inferred = behaviours.filter((b) => b.source.origin === 'inferred');
  return {
    defined: defined.length,
    inferred: inferred.length,
    unreviewed: inferred.filter((b) => b.review.state === 'unreviewed'),
    approved: inferred.filter((b) => b.review.state === 'approved'),
    denied: behaviours.filter((b) => b.review.state === 'denied'),
    // A behaviour with no traceable source is worse than an unreviewed one: it
    // cannot be checked against anything at all.
    untraceable: behaviours.filter((b) => !b.source.ref),
  };
}

// ─────────────────────────── 6. displayed surface ───────────────────────────
// James, kit#3 (2026-08-30): "the api layer is inferred from what needs to be
// displayed in the ui. From the frontend first development approach. Expose only
// what is required to display kind of approach."
//
// That answer closes the "should Kit assert at the API layer too?" question with
// a NO, and it is worth more than a no: it makes a report derivable that was not
// derivable before. If every API behaviour exists to serve a displayed one, then
// an API behaviour serving nothing documented is either a documentation gap or
// surface to delete — and which of the two is a human's call, not a tool's.
//
// The link has to be authored, not guessed. `serves BEH-X` is a claim someone
// made and can be argued with; a similarity score between a route name and a
// screen name is a claim nobody made and nobody can argue with.
//
// Deliberately asymmetric, for the same reason `source` is: silence on a DEFINED
// behaviour is fine (a documented behaviour is served, it does not serve), but
// silence on an INFERRED one is the finding.

function surface(behaviours) {
  const byId = new Map(behaviours.map((b) => [b.id, b]));
  const errors = [];

  for (const b of behaviours) {
    for (const s of b.serves) {
      const target = byId.get(s.id);
      // A dangling link is worse than no link: it reads as adjudicated and is
      // not. Renaming a behaviour must break the build, or the corpus rots into
      // a set of claims about ids that no longer exist.
      if (!target) {
        errors.push(`${s.at}: ${b.id} serves ${s.id}, which is not in the corpus`);
      } else if (target.source.origin !== 'defined') {
        // An inference serving an inference documents nothing. The chain has to
        // terminate at something a human wrote, or "serves" just moves the
        // question one hop and looks answered.
        errors.push(`${s.at}: ${b.id} serves ${s.id}, which is itself inferred — the chain must end at a defined behaviour`);
      }
      if (b.source.origin === 'defined') {
        errors.push(`${s.at}: ${b.id} is defined, so it is served rather than serving — drop the serves line`);
      }
    }
  }

  const inferred = behaviours.filter((b) => b.source.origin === 'inferred');
  return {
    errors,
    served: inferred.filter((b) => b.serves.length),
    unserved: inferred.filter((b) => !b.serves.length),
  };
}

module.exports = { parse, parseStep, resolve, generate, coverage, adjudication, surface };

// ─────────────────────────── cli ───────────────────────────
if (require.main === module) {
  const fs = require('fs');
  const path = require('path');
  const dir = path.join(__dirname, 'behaviours');
  // `node kit.js <name>` scopes the run to one corpus. Needed the moment there
  // was more than one app in here: a measurement averaged over two unrelated
  // corpora tells you about neither.
  const only = process.argv[2];
  const files = fs.readdirSync(dir).filter((f) => f.endsWith('.beh'))
    .filter((f) => !only || f.includes(only));
  if (!files.length) { console.error(`no corpus matching "${only}" in ${dir}`); process.exit(2); }
  const all = files.flatMap((f) => parse(fs.readFileSync(path.join(dir, f), 'utf8'), f));
  const bindings = JSON.parse(fs.readFileSync(path.join(__dirname, 'bindings.json'), 'utf8'));
  const { behaviours, conflicts, symbols } = resolve(all);

  const nounCount = Object.keys(bindings).filter((k) => !k.startsWith('_')).length;
  const totals = { generated: 0, contract: 0, ungenerated: 0 };
  const unbound = new Set();

  for (const b of behaviours) {
    const { code, missing, stats } = generate(b, bindings, symbols);
    for (const k of Object.keys(totals)) totals[k] += stats[k];
    missing.forEach((m) => unbound.add(m));
    console.log(code);
    if (missing.length) console.log(`// unbound noun(s): ${missing.join(', ')}`);
    if (b.open.length) console.log(`// OPEN unknown(s): ${b.open.map((u) => u.key).join(', ')}`);
    if (b.filled.length) {
      console.log(`// filled from elsewhere: ${b.filled.map((u) => `${u.key} = [${u.value}] via ${u.from.join('+')}`).join(', ')}`);
    }
    console.log('');
  }

  if (conflicts.length) {
    console.log('── structural conflicts (the "supersede?" case, no LLM involved) ──');
    for (const c of conflicts) {
      console.log(`  ${c.key}: ${c.holders.join('+')} say [${c.held}]; ` +
        c.challengers.map((x) => `${x.from} says [${x.value}]`).join('; '));
    }
    console.log('');
  }

  const adj = adjudication(behaviours);
  console.log('── adjudication (James, #68: "default included but marked unreviewed") ──');
  console.log(`  defined by a human    ${adj.defined}`);
  console.log(`  inferred by the model ${adj.inferred}`);
  console.log(`  NEVER ADJUDICATED     ${adj.unreviewed.length}   ${adj.unreviewed.map((b) => b.id).join(', ')}`);
  if (adj.denied.length) console.log(`  denied w/ correction  ${adj.denied.length}`);
  if (adj.untraceable.length) {
    console.log(`  ⚠️  UNTRACEABLE        ${adj.untraceable.length}   no source ref: ${adj.untraceable.map((b) => b.id).join(', ')}`);
  }
  console.log('');

  const surf = surface(behaviours);
  console.log('── displayed surface (James, kit#3: "expose only what is required to display") ──');
  console.log(`  serves a documented behaviour   ${surf.served.length}`);
  console.log(`  NOTHING DOCUMENTED DISPLAYS IT  ${surf.unserved.length}   ${surf.unserved.map((b) => b.id).join(', ') || '—'}`);
  if (surf.unserved.length) {
    console.log('  ⇒ each is a documentation gap or surface to delete. Kit will not guess which.');
  }
  console.log('');

  const steps = totals.generated + totals.contract + totals.ungenerated;
  console.log('── measured ──');
  console.log(`  behaviours            ${behaviours.length}`);
  console.log(`  noun bindings         ${nounCount}   (Cucumber would need one step definition per step phrasing)`);
  console.log(`  generated lines       ${totals.generated}`);
  console.log(`  wire contracts        ${totals.contract}   not expressible as a behaviour — something else must own these`);
  console.log(`  ungenerated           ${totals.ungenerated}   refused rather than guessed`);
  console.log(`  unbound nouns         ${unbound.size}   ${[...unbound].join(', ')}`);
  console.log(`  generated / total     ${totals.generated}/${steps} = ${Math.round((totals.generated / steps) * 100)}%`);

  // The one thing here that GATES. Everything else above is a number a person
  // reads; a broken `serves` link is a corpus that lies about itself, and a
  // report nobody has to act on is the failure mode this whole prototype is
  // arguing against.
  if (surf.errors.length) {
    console.log('');
    console.log('── broken links (exit 1) ──');
    for (const e of surf.errors) console.log(`  ${e}`);
    process.exit(1);
  }
}
