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
        asks: null, options: [], recommend: null, against: null, cites: [],
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

    // ── James, kit#3 (2026-08-30) ─────────────────────────────────────────
    // "Maybe you should provide a behaviour question sheet for the habits app
    // so that we can pilot kit this way... I can work through it with Gemini"
    //
    // The adjudication count (§5) says HOW MANY behaviours nobody has ruled on.
    // It cannot say what ruling on one would even mean, and "approve or deny?"
    // twelve times is a form nobody fills in honestly — it trains exactly the
    // bulk-approval the count exists to prevent.
    //
    // So the split is: the TOOL finds where a real question exists (an inference
    // nothing displays, a symbol two behaviours disagree about — both computed,
    // §6 and §2), and a HUMAN authors what the question actually asks. Same
    // reasoning as `serves`: an authored claim can be argued with, and a
    // similarity score between a route name and a screen name cannot.
    //
    // `asks` also PROMOTES. Mechanism sets the floor, not the ceiling — a
    // behaviour that looks routine to the tool can still be the one that matters
    // (BEH-HISTORY-3 serves a screen and is still a rename decision), and
    // writing `asks` on it says so.
    if (kw === 'asks') {
      const a = /^"(.*)"$/.exec(rest);
      if (!a) throw new Error(`${at}: asks wants a quoted question, got: ${rest}`);
      cur.asks = a[1];
      continue;
    }

    // An option is a choice AND what changes if it is taken. The consequence is
    // not decoration: a question whose options all change nothing is a question
    // that should never have been asked, and writing the consequence down is
    // what exposes it. Same discipline the decision queue enforces by demanding
    // a default (claude-code-bot#59).
    if (kw === 'option') {
      const o = /^"([^"]*)"\s+"(.*)"$/.exec(rest);
      if (!o) throw new Error(`${at}: option wants "<label>" "<what changes if taken>", got: ${rest}`);
      cur.options.push({ label: o[1], consequence: o[2], at });
      continue;
    }

    if (kw === 'recommend') {
      const r = /^"([^"]*)"\s+"(.*)"$/.exec(rest);
      if (!r) throw new Error(`${at}: recommend wants "<option label>" "<why>", got: ${rest}`);
      cur.recommend = { label: r[1], why: r[2], at };
      continue;
    }

    // `cites` names a behaviour that is EVIDENCE for this question rather than a
    // question of its own. Built because the first real sheet asked the same
    // thing twice at two different tiers: D1 asked which of `days`/`historyDays`
    // wins, while BEH-HISTORY-3 ("the parameter is named historyDays") sat in the
    // review table as a routine tick. Ticking it answers D1 silently, in the
    // section explicitly labelled as the cheap one — and an assistant working
    // top-down would do exactly that and then argue the opposite in D1.
    //
    // So a cited behaviour renders INSIDE the decision and is suppressed from the
    // review list. Deliberately not automatic: two behaviours touching one symbol
    // is not the same as one being the other's evidence, and only an author knows
    // which. The gate below refuses a `cites` that names nothing.
    if (kw === 'cites') {
      if (!/^BEH-[A-Z0-9-]+$/.test(rest)) throw new Error(`${at}: cites wants a behaviour id, got: ${rest}`);
      cur.cites.push({ id: rest, at });
      continue;
    }

    // His claude-code-bot#82 shape, made structural: a pack carries the options,
    // the evidence, my recommendation AND the strongest case against it. A
    // recommendation with no counter-case is advocacy wearing a decision's
    // clothes, and it is the half a reader most needs and I am least inclined
    // to write — so the gate below requires it rather than trusting me.
    if (kw === 'against') {
      const g = /^"(.*)"$/.exec(rest);
      if (!g) throw new Error(`${at}: against wants a quoted counter-case, got: ${rest}`);
      cur.against = g[1];
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

// ─────────────────────────── 7. the question sheet ───────────────────────────
// James, kit#3: "provide a behaviour question sheet for the habits app... I can
// work through it with Gemini".
//
// Two things this must NOT become. It must not become twelve identical "approve
// or deny?" rows, because that is a form, and a form gets bulk-approved. And it
// must not become my prose with a tool's name on it — if I hand-pick which
// behaviours are interesting, the sheet measures my attention, not the corpus.
//
// So consequence is DETECTED, not judged. A question is a DECISION when the
// corpus can prove both answers change something:
//   · nothing documented displays it (§6) — either the doc is missing a screen
//     or the surface should go, and those are opposite edits; or
//   · two behaviours disagree about one symbol (§2) — someone must lose.
// Everything else unreviewed is a REVIEW: cheap, one line, still counted.
//
// The tiers then buy different amounts of a reader's attention, which is the
// whole point of ranking: a DECISION carries options, a recommendation and the
// strongest case against it; a REVIEW carries the citation and stops.

function questions(behaviours, conflicts = []) {
  const byId = new Map(behaviours.map((b) => [b.id, b]));
  const unserved = new Set(surface(behaviours).unserved.map((b) => b.id));
  const out = [];

  // Anything a question cites is that question's evidence, so it must not also
  // appear as an independent row. Collected before either loop because the
  // citing behaviour and the cited one are found by different passes.
  const citedBy = new Map();
  for (const b of behaviours) {
    for (const c of b.cites) if (!citedBy.has(c.id)) citedBy.set(c.id, b.id);
  }
  const evidence = (b) => b.cites.map((c) => {
    const t = byId.get(c.id);
    return {
      id: c.id,
      title: t ? t.title : c.id,
      ref: t ? t.source.ref : null,
      contracts: t ? t.steps.filter((s) => s.kind === 'contract').map((s) => s.text) : [],
    };
  });

  // Conflicts first: they are the only question here where the corpus itself
  // says two authored statements cannot both hold.
  for (const c of conflicts) {
    const sides = [...c.holders, ...c.challengers.map((x) => x.from)];
    // The question can be authored on either side — the collision belongs to
    // the symbol, not to one of the two behaviours that walked into it.
    const owner = sides.map((id) => byId.get(id)).find((b) => b && b.asks);
    out.push({
      kind: 'conflict', tier: 'decision', key: c.key,
      title: `Two behaviours disagree about ${c.key}`,
      // The renderer has a second reader who cannot open the repo, so each side
      // carries its own citation and its own value rather than a bare id.
      sides: sides.map((id) => {
        const b = byId.get(id);
        const ch = c.challengers.find((x) => x.from === id);
        return { id, title: b ? b.title : id, ref: b ? b.source.ref : null, value: ch ? ch.value : c.held };
      }),
      held: c.held, challengers: c.challengers,
      asks: owner ? owner.asks : null,
      options: owner ? owner.options : [],
      recommend: owner ? owner.recommend : null,
      against: owner ? owner.against : null,
      owner: owner ? owner.id : null,
      cites: owner ? evidence(owner) : [],
    });
  }

  for (const b of behaviours) {
    // A defined behaviour is not up for adjudication — a human already wrote it.
    // It can still carry a question (that is how a conflict gets one), and that
    // question is reported above rather than here.
    if (b.source.origin !== 'inferred' || b.review.state !== 'unreviewed') continue;
    // Cited = already on the page, inside the question it is evidence for.
    // Listing it again as its own row is the double-ask this field exists to
    // remove — and the duplicate would land in the section labelled cheap.
    if (citedBy.has(b.id)) continue;
    const detected = unserved.has(b.id);
    out.push({
      kind: detected ? 'unserved' : 'review',
      // `asks` PROMOTES a routine-looking inference to a decision. Detection is
      // the floor: the tool cannot see that a parameter's NAME is wrong, and
      // refusing to let a human say so would make the ranking dumber than both.
      tier: detected || b.asks ? 'decision' : 'review',
      key: b.id, id: b.id, title: b.title,
      source: b.source, serves: b.serves.map((s) => s.id),
      contracts: b.steps.filter((s) => s.kind === 'contract').map((s) => s.text),
      asks: b.asks, options: b.options, recommend: b.recommend, against: b.against,
      cites: evidence(b),
    });
  }

  const rank = { decision: 0, review: 1 };
  out.sort((a, b) => rank[a.tier] - rank[b.tier]);
  return out;
}

// The gate. A sheet that silently ships an incomplete decision is worse than no
// sheet: it looks worked-through. Every failure here is a thing I owe a reader
// and did not write, so it exits 1 alongside the broken-link check.
function questionErrors(qs) {
  const errors = [];
  for (const q of qs) {
    const where = q.kind === 'conflict' ? `conflict ${q.key}` : q.id;
    if (q.tier === 'decision' && !q.asks) {
      errors.push(q.kind === 'conflict'
        ? `${where}: neither side states the question — put an "asks" on one of ${q.sides.map((s) => s.id).join(' or ')}`
        : `${where}: nothing documented displays it, so both answers change something — it needs an "asks"`);
      continue;
    }
    if (!q.asks) {
      // A pack hanging off no question is a stranded opinion.
      if (q.options.length || q.recommend || q.against) {
        errors.push(`${where}: has option/recommend/against but no "asks" to attach them to`);
      }
      continue;
    }
    if (q.options.length < 2) errors.push(`${where}: a question needs at least 2 options, has ${q.options.length}`);
    // Catches the rot case: an option gets relabelled and the recommendation
    // quietly starts pointing at nothing while still reading as a recommendation.
    if (q.recommend && !q.options.some((o) => o.label === q.recommend.label)) {
      errors.push(`${where}: recommends "${q.recommend.label}", which is not one of its options (${q.options.map((o) => o.label).join(', ')})`);
    }
    // A `cites` naming nothing is worse than a broken link in prose: the cited
    // behaviour is SUPPRESSED from the review list, so a typo here deletes a
    // question from the sheet and leaves no trace of it anywhere.
    for (const c of q.cites || []) {
      if (!c.ref) errors.push(`${where}: cites ${c.id}, which is not a behaviour in this corpus — that silently drops it from the sheet`);
    }
    if (q.recommend && !q.against) {
      errors.push(`${where}: recommends without stating the strongest case against — that is advocacy, not a decision pack`);
    }
  }
  return errors;
}

// ─────────────────────────── 8. rendering the sheet ──────────────────────────
// The artefact he actually opens. He said "I can work through it with Gemini",
// so the sheet has a second reader who has never seen this repo, cannot run
// anything, and will confidently fill any gap it is left. That constrains it
// harder than a document for him alone:
//
//   · every question carries its own evidence inline, because the assistant
//     cannot go and look at HabitRoutes.cs:68;
//   · the brief says what the assistant is FOR (pressure-test, not decide) and
//     what it must not do (invent a third option, ratify the recommendation),
//     because an assistant asked to "help decide" agrees;
//   · every answer names the exact corpus line it becomes, so working through
//     the sheet produces an edit rather than a conversation.
//
// The last one is the reason this renders from `questions()` rather than being
// written by hand: a hand-written sheet is a snapshot that stops matching the
// corpus the day after it is written, silently, while still reading as current.
// A test asserts the committed sheet is byte-identical to this output.

function renderSheet(app, qs, opts = {}) {
  const rev = opts.rev || '';
  const decisions = qs.filter((q) => q.tier === 'decision');
  const reviews = qs.filter((q) => q.tier === 'review');
  const L = [];

  L.push(`# Behaviour question sheet — \`${app}\``);
  L.push('');
  L.push(`**${decisions.length} decisions · ${reviews.length} reviews.** Generated by ` +
    `\`node kit.js sheet ${app}${rev ? ` --rev ${rev}` : ''}\`` +
    `${rev ? `, over the app at \`${rev}\`` : ''}. Do not hand-edit — re-run it.`);
  L.push('');
  L.push('## What this is');
  L.push('');
  L.push('Kit read this app\'s `docs/DESIGN.md` and its backend test names and built one list of');
  L.push('behaviours from both. Everything it read out of the **code** is marked unreviewed until a');
  L.push('human rules on it, because an inference that quietly becomes a specification is the failure');
  L.push('this whole thing exists to prevent.');
  L.push('');
  L.push('The two sections below are not the same job and should not take the same effort:');
  L.push('');
  L.push('- **Decisions** — Kit can prove both answers change something: either nothing documented');
  L.push('  displays this surface, or two behaviours contradict each other about one value. Each one');
  L.push('  carries the evidence, the options, my recommendation, and the strongest case against it.');
  L.push('- **Reviews** — the code asserts this, a documented screen needs it, and it looks right.');
  L.push('  One line each. If one is wrong, it becomes a decision.');
  L.push('');
  L.push('## Brief for the assistant (paste this too)');
  L.push('');
  L.push('> You are helping the product owner **pressure-test** these decisions, not make them. For each:');
  L.push('> argue the case *against* the recommendation as strongly as you can; say which option you would');
  L.push('> pick and why in one sentence; and name anything the evidence does not settle. Do not invent a');
  L.push('> third option unless the two on offer genuinely miss the point — and if you do, say which');
  L.push('> evidence made you. Do not agree because the recommendation sounds reasonable; it was written');
  L.push('> by the same system that wrote the question.');
  L.push('');
  L.push('## How an answer comes back');
  L.push('');
  L.push('Each question names the corpus line it becomes. Write the answer under it in any form — the');
  L.push('line is what I will make true in `behaviours/' + app + '.beh`, and re-running this sheet then');
  L.push('drops the question. Nothing here is answered by silence.');
  L.push('');

  L.push('---');
  L.push('');
  L.push(`## Decisions — ${decisions.length}`);
  L.push('');
  if (!decisions.length) L.push('_None. Kit could not prove that any open question has two answers that differ._');

  decisions.forEach((q, i) => {
    const n = i + 1;
    if (q.kind === 'conflict') {
      L.push(`### D${n}. ${q.asks}`);
      L.push('');
      // Strictly what was MEASURED. An earlier draft ended this with "and one of
      // the two has to lose" — which the first real conflict disproved: the two
      // sides of `region:CompletionGrid.days` are reconcilable (caller-supplied
      // DEFAULTING to 30), and the decision underneath was a parameter NAME.
      // Kit detected a symbol collision, which is true; "someone must lose" was
      // a verdict inferred from it, and it was wrong. The authored `asks` says
      // what the collision means — that is the whole division of labour here.
      L.push(`**Why this is a decision:** two behaviours state different values for \`${q.key}\`.`);
      L.push('Both sides were read out of a document rather than out of code, so this is the spec');
      L.push('disagreeing with itself, not the code drifting from it. Kit detects the collision; what it');
      L.push('means is the question above.');
      L.push('');
      L.push('| behaviour | says `' + q.key + '` is | source |');
      L.push('|---|---|---|');
      for (const s of q.sides) {
        L.push(`| \`${s.id}\` ${s.title} | \`${s.value}\` | \`${s.ref || '—'}\` |`);
      }
      L.push('');
    } else {
      L.push(`### D${n}. ${q.asks}`);
      L.push('');
      L.push(`**\`${q.id}\` — ${q.title}**`);
      L.push('');
      if (q.kind === 'unserved') {
        L.push('**Why this is a decision:** you said the API layer is inferred from what the UI needs to');
        L.push('display. This surface exists in the code and **no documented screen displays it**, so either');
        L.push('the design is missing a screen or the surface should go. Those are opposite edits.');
      } else {
        L.push('**Why this is a decision:** it serves a documented screen, so Kit would have filed it as a');
        L.push('routine review — it is here because a human said it is not routine.');
      }
      L.push('');
      L.push(`**Evidence** — read out of \`${q.source.ref}\`:`);
      L.push('');
      for (const c of q.contracts) L.push(`- ${c}`);
      if (q.serves.length) L.push(`- serves: ${q.serves.map((s) => `\`${s}\``).join(', ')}`);
      L.push('');
    }

    // Rendered before the options: it is the half of the evidence that makes the
    // question concrete, and it is here rather than in the review table because
    // ticking it there would answer this question without saying so.
    if ((q.cites || []).length) {
      L.push('**Also on the table here** — these are part of this question, which is why they are not');
      L.push('in the review list below:');
      L.push('');
      for (const c of q.cites) {
        L.push(`- \`${c.id}\` ${c.title} — ${c.contracts.join('; ') || c.title} (\`${c.ref}\`)`);
      }
      L.push('');
    }

    L.push('**Options**');
    L.push('');
    for (const o of q.options) L.push(`- **${o.label}** — ${o.consequence}`);
    L.push('');
    if (q.recommend) {
      L.push(`**I'd pick: ${q.recommend.label}.** ${q.recommend.why}`);
      L.push('');
      L.push(`**The strongest case against that:** ${q.against}`);
      L.push('');
    }
    L.push('**Your answer** — becomes: ' + answerLine(q));
    L.push('');
    L.push('> ');
    L.push('');
  });

  L.push('---');
  L.push('');
  L.push(`## Reviews — ${reviews.length}`);
  L.push('');
  if (!reviews.length) {
    L.push('_None._');
  } else {
    L.push('The code asserts each of these and a documented screen needs it. Tick, or say what is wrong —');
    L.push('a "wrong" here promotes it to a decision on the next run.');
    L.push('');
    L.push('| # | behaviour | what the code asserts | read out of |');
    L.push('|---|---|---|---|');
    reviews.forEach((q, i) => {
      const what = q.contracts.length ? q.contracts.join('; ') : q.title;
      L.push(`| R${i + 1} | \`${q.id}\` ${q.title} | ${what} | \`${q.source.ref}\` |`);
    });
    L.push('');
    L.push('Each becomes `review approved` on that behaviour, or `review denied "<what is actually true>"`.');
    L.push('');
  }

  L.push('---');
  L.push('');
  L.push('## What Kit is NOT asking you here');
  L.push('');
  L.push('Worth stating, because a sheet that omits its own scope reads as complete:');
  L.push('');
  L.push('- **The behaviours this app has documented but not built.** Kit refuses to generate a test for a');
  L.push('  screen that does not exist and names the missing noun instead. That list is a build backlog,');
  L.push('  not a question — it goes to zero on its own as the frontend lands.');
  L.push('- **Anything read out of a document you wrote.** A defined behaviour is not up for adjudication');
  L.push('  here; you already ruled on it by writing it down. It only reappears if it collides with');
  L.push('  another defined behaviour — which is exactly what D1 is.');
  return L.join('\n') + '\n';
}

// The line an answer becomes. Kept next to the renderer rather than inlined so
// there is one place that knows the mapping from "he said yes" to corpus text.
function answerLine(q) {
  if (q.kind === 'conflict') {
    // Deliberately does NOT prescribe which side moves. On the first real
    // conflict the answer was neither: both `provides` lines were individually
    // true and had to be reconciled into one statement of the default. A line
    // that named a winner would have described an edit nobody was going to make.
    return `the two \`provides\` lines on ${q.sides.map((s) => `\`${s.id}\``).join(' and ')} reconciled — ` +
      'corrected, merged into one statement, or one of them removed, whichever your answer implies.';
  }
  if (q.kind === 'unserved') {
    return `\`serves BEH-…\` added to \`${q.id}\` (with the screen written into \`DESIGN.md\`), ` +
      `or \`${q.id}\` deleted along with the surface it describes.`;
  }
  return `\`review approved\` on \`${q.id}\`, or \`review denied "<what is actually true>"\`.`;
}

module.exports = {
  parse, parseStep, resolve, generate, coverage, adjudication, surface,
  questions, questionErrors, renderSheet,
};

// ─────────────────────────── cli ───────────────────────────
if (require.main === module) {
  const fs = require('fs');
  const path = require('path');
  const dir = path.join(__dirname, 'behaviours');
  // `node kit.js <name>` scopes the run to one corpus. Needed the moment there
  // was more than one app in here: a measurement averaged over two unrelated
  // corpora tells you about neither.
  // `sheet` renders the artefact for a human instead of the report for me.
  // Separate mode rather than another block of output: the sheet is a document
  // someone opens, and a document with a coverage table stapled to the top is a
  // document nobody finishes.
  const sheetMode = process.argv[2] === 'sheet';
  const argv = process.argv.slice(sheetMode ? 3 : 2);
  // `--rev` records WHICH revision of the app the corpus was read from, which is
  // the only provenance a reader can check. Deliberately no timestamp: the
  // committed sheet is asserted byte-identical to this output, and a wall-clock
  // date would make it differ every day for no reader's benefit — which is the
  // kind of drift that gets a failing check deleted rather than fixed.
  const revArg = argv.findIndex((a) => a === '--rev');
  const rev = revArg >= 0 ? argv[revArg + 1] : '';
  const only = argv.find((a) => !a.startsWith('--') && a !== rev);
  const files = fs.readdirSync(dir).filter((f) => f.endsWith('.beh'))
    .filter((f) => !only || f.includes(only));
  if (!files.length) { console.error(`no corpus matching "${only}" in ${dir}`); process.exit(2); }
  const all = files.flatMap((f) => parse(fs.readFileSync(path.join(dir, f), 'utf8'), f));
  const bindings = JSON.parse(fs.readFileSync(path.join(__dirname, 'bindings.json'), 'utf8'));
  const { behaviours, conflicts, symbols } = resolve(all);

  if (sheetMode) {
    // A sheet built from a corpus that fails its own link check would present
    // broken claims as questions, so the gate runs first and hard.
    const linkErrors = surface(behaviours).errors;
    if (linkErrors.length) {
      console.error('── broken links — refusing to render a sheet over them ──');
      for (const e of linkErrors) console.error(`  ${e}`);
      process.exit(1);
    }
    const qs = questions(behaviours, conflicts);
    const qErrors = questionErrors(qs);
    if (qErrors.length) {
      console.error('── incomplete questions (exit 1) ──');
      console.error('  A half-written decision is worse than a missing one: it looks worked through.');
      for (const e of qErrors) console.error(`  ${e}`);
      process.exit(1);
    }
    const app = (files.length === 1 ? files[0].replace(/\.beh$/, '') : only) || 'all';
    process.stdout.write(renderSheet(app, qs, { rev }));
    return;
  }

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
