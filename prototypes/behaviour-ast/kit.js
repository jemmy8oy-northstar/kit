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
      // A binding EXISTING is not the same as it carrying what this verb needs,
      // and this branch used to conflate them: a file binding with no `fixture`
      // and a field binding with no locator emitted
      //     await page.null.setInputFiles(undefined);
      // which is counted as GENERATED, contributes to the headline percentage,
      // and throws the moment it runs. That is a false green — strictly worse
      // than the refusal this design is built on — and it survived because every
      // corpus was reverse-engineered from an app that already had both keys
      // (claude-code-bot#92, requires.js). Refuse instead; requires.js says why.
      const l = loc(field);
      if (!l || !file.fixture) return null;
      return [`await page.${l}.setInputFiles(${JSON.stringify(file.fixture)});`];
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
        // Same false green as `attaches`: without a label BOTH branches below
        // emit `getByLabel(undefined)`, which is generated, counted, and unable
        // to run. Note this verb needs a LABEL specifically, not addressability
        // in general — it only ever emits getByLabel — which is why requires.js
        // gives a `field:` noun a different obligation under `fills` than under
        // `attaches`. The requirement belongs to the verb, not to the kind.
        if (!fb.label) return null;
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

// ⚠️ WHAT THIS CAN AND CANNOT PROVE, stated here so the gate cannot quietly
// over-claim it downstream. `testSources` are whole FILES, so a marker anywhere
// in a file marks that behaviour covered — the largest test file in the estate
// holds 18 tests. This proves *someone wrote the id*, not that a test asserts
// the behaviour. True of `mapping()` below as well. `docs/design/tagging.md`.

// ─────────────────────────── 4b. reading an app's tests ───────────────────────────
// One reader, used by the gate and by `measure-tagging.js`. It existed twice for
// about an hour and that is exactly how the two drift apart.
//
// A test TITLE is the string a human would recognise as naming the test. Both
// ecosystems appear in every pilot repo, so a reader handling only one would
// report a repo as untestable when it is merely C#.

// ⚠️ THIS REGEX WAS THE WHOLE JS READER AND IT WAS WRONG IN BOTH DIRECTIONS.
// It is kept only as the *second* count (see jsDeclarationCount below), because
// it is wrong in a differently-shaped way from the scanner that replaced it,
// which is the only thing that makes a cross-check worth having.
//
//  · It UNDER-read every parameterised test. `it.each([...])('%s', fn)` puts the
//    title after the table, so the `\s*\(` here never reaches a quote and the
//    test vanished silently. Measured across 28 real JS test files in four
//    repos: 5 files disagreed with a line count, and `.each` explained all five
//    exactly. Under option C that surfaces as "mapping entry names a test that
//    does not exist" — a FALSE RED pointing at the corpus instead of the reader.
//  · It OVER-read any file containing a test-shaped string literal. Kit's own
//    suite is the suite of a test GENERATOR, so it is full of them: 97 real
//    declarations, 108 matches, 11 phantoms out of fixtures. That is why Kit
//    could not be pointed at itself without lying about itself.
//
// Both were invisible because `expectedTestCount()` returned null for JS: the
// C# path has refused on a count disagreement since the vocab 16% under-read,
// and the JS path had no second count at all ([[count-it-a-second-way]]).
const JS_TITLE = /\b(?:test|it)(?:\.(?:only|skip|fixme))?\s*\(\s*(['"`])((?:\\.|(?!\1)[^\\])*)\1/g;

// A test declaration begins a statement. Leading whitespace (a `describe` block)
// and an `await`/`return` in front of it are the only things that legitimately
// precede one; a fixture string never does, because it is preceded by the quote
// that opens it.
// The trailing class is `[(`] and not `\(`: `it.each` may take its table as a
// tagged template, where no paren follows the name at all.
const JS_DECL = /^[ \t]*(?:await\s+|return\s+)?(?:test|it)(?:\.(?:only|skip|fixme|concurrent|each))?\s*[(`]/gm;
const JS_QUOTED = /^\s*(['"`])((?:\\.|(?!\1)[^\\])*)\1/;
const CS_ATTR = /\[(?:Fact|Theory)[^\]]*\]/;
const CS_DISPLAY = /DisplayName\s*=\s*"((?:\\.|[^"\\])*)"/;
const CS_METHOD = /^\s*(?:public|internal)\s+(?:async\s+)?[\w<>,\[\]?\s]+?\s(\w+)\s*\(/;
// Between `[Theory]` and its method sit its `[InlineData]` rows, blank lines and
// comments — an unbounded number of them.
const CS_SKIP = /^\s*(?:\[|\/\/|\/\*|\*|$)/;

const TEST_FILE_RE = /\.(spec|test)\.(ts|tsx|js|jsx)$|Tests?\.cs$/;

// Walk forward from `i` over one balanced bracket group, or one template
// literal, treating quoted text as opaque. Needed because `it.each([...])` puts
// arbitrary data — including brackets and parens inside strings — between the
// name and the title. Returns the index just past the group, or -1 if it never
// closes (a truncated file), which the caller must treat as "stop reading".
function skipGroup(src, i) {
  while (i < src.length && /\s/.test(src[i])) i++;
  const open = src[i];
  if (open === '`') return skipTemplate(src, i);
  if (open !== '(' && open !== '[') return -1;
  const close = open === '(' ? ')' : ']';
  let depth = 0;
  for (; i < src.length; i++) {
    const c = src[i];
    if (c === '"' || c === "'") { i = skipQuoted(src, i); if (i < 0) return -1; continue; }
    if (c === '`') { i = skipTemplate(src, i) - 1; if (i < 0) return -1; continue; }
    if (c === '/' && src[i + 1] === '/') { i = src.indexOf('\n', i); if (i < 0) return src.length; continue; }
    if (c === '/' && src[i + 1] === '*') { const e = src.indexOf('*/', i + 2); if (e < 0) return -1; i = e + 1; continue; }
    if (c === open || (open === '(' && c === '[') || (open === '[' && c === '(')) depth++;
    else if (c === close || (open === '(' && c === ']') || (open === '[' && c === ')')) { depth--; if (depth === 0) return i + 1; }
  }
  return -1;
}

// `i` is at the opening quote; returns the index OF the closing quote.
function skipQuoted(src, i) {
  const q = src[i];
  for (let j = i + 1; j < src.length; j++) {
    if (src[j] === '\\') { j++; continue; }
    if (src[j] === q) return j;
    if (q !== '`' && src[j] === '\n') return -1; // unterminated on its line
  }
  return -1;
}

// `i` is at the backtick; returns the index just past the closing one. `${}`
// interpolations are skipped as balanced groups so a brace inside them cannot
// end the literal early.
function skipTemplate(src, i) {
  for (let j = i + 1; j < src.length; j++) {
    if (src[j] === '\\') { j++; continue; }
    if (src[j] === '$' && src[j + 1] === '{') {
      let depth = 0;
      for (; j < src.length; j++) {
        if (src[j] === '{') depth++;
        else if (src[j] === '}') { depth--; if (depth === 0) break; }
      }
      continue;
    }
    if (src[j] === '`') return j + 1;
  }
  return -1;
}

function testTitles(file, src) {
  const out = [];
  if (!file.endsWith('.cs')) {
    // Scan DECLARATIONS, not quote-shaped text. A declaration begins a
    // statement; a fixture string never does. See the note on JS_TITLE.
    let m;
    JS_DECL.lastIndex = 0;
    while ((m = JS_DECL.exec(src))) {
      const line = src.slice(0, m.index).split('\n').length;
      const isEach = m[0].includes('.each');
      // For `.each`, the title sits after the table: skip the table group (or
      // tagged template), then the title is the next call's first argument.
      let at = m.index + m[0].length - 1; // the '(' or '`' the regex ended on
      if (isEach) {
        const past = skipGroup(src, at);
        if (past < 0) break;
        let k = past;
        while (k < src.length && /\s/.test(src[k])) k++;
        if (src[k] !== '(') continue; // `.each` table with no call after it
        at = k;
      }
      const q = JS_QUOTED.exec(src.slice(at + 1));
      if (!q) continue; // a computed title — counted by jsDeclarationCount, so a
                        // disagreement will refuse rather than silently drop it
      out.push({ file, line, raw: q[2], style: isEach ? 'each' : 'title' });
    }
    return out;
  }
  // xUnit: the name is the method, unless a DisplayName overrides it. Walk line
  // by line so the [Fact] and its method stay associated.
  const lines = src.split('\n');
  for (let i = 0; i < lines.length; i++) {
    if (!CS_ATTR.test(lines[i])) continue;
    const display = lines[i].match(CS_DISPLAY);
    for (let j = i + 1; j < lines.length; j++) {
      if (CS_SKIP.test(lines[j])) continue;
      const m = lines[j].match(CS_METHOD);
      if (m) out.push({ file, line: j + 1, raw: display ? display[1] : m[1], style: display ? 'DisplayName' : 'method' });
      break; // matched or not, the first non-attribute line settles it
    }
  }
  return out;
}

// A second, differently-shaped count of the same thing — because the C# walk
// PAIRS an attribute with a method and a pairing can drop one silently, while a
// count cannot. This is not hypothetical: a fixed six-line lookahead here lost 7
// tests across the pilot repos (language-vocab under-read by 16%) and nothing
// said so.
//
// ⚠️ It used to return null for JS, on the reasoning that the JS reader "counts
// occurrences directly and has no pairing step to lose anything in". That was
// true of the regex and false of the problem: the regex lost every `it.each`
// test and invented one per test-shaped string literal, both silently, for
// exactly as long as this function declined to look. An unmeasured half is not
// a safe half.
//
// The two JS mechanisms must not be the same idea twice, or agreement proves
// nothing. `testTitles` uses POSITION (a declaration starts a statement);
// `jsDeclarationCount` uses LEXICAL STRUCTURE (strip strings and comments, then
// count call heads anywhere). A test written mid-line after a semicolon is
// invisible to the first and visible to the second, so they disagree and the
// caller refuses — which is the outcome we want, rather than a quiet undercount.
function expectedTestCount(file, src) {
  if (!file.endsWith('.cs')) return jsDeclarationCount(src);
  return (src.match(/\[(?:Fact|Theory)\b/g) || []).length;
}

// ⚠️ The lookbehind is load-bearing, not defensive. `\b` matches immediately
// after a dot, so a plain `\b(test|it)\s*\(` counts every `SOME_RE.test(x)` in
// the file as a test declaration. Kit's own suite has 16 of them and the count
// came back 113 against 97 real tests — a refusal on a file that was fine.
const JS_CALL_HEAD = /(?<![.\w$])(?:test|it)(?:\.(?:only|skip|fixme|concurrent|each))?\s*[([`]/g;

function jsDeclarationCount(src) {
  let out = '';
  for (let i = 0; i < src.length; i++) {
    const c = src[i];
    if (c === '"' || c === "'") {
      const e = skipQuoted(src, i);
      if (e < 0) { out += ' '; continue; }
      out += ' '; i = e; continue;
    }
    if (c === '`') {
      const e = skipTemplate(src, i);
      if (e < 0) { out += ' '; continue; }
      // Keep the backtick, blank the body: `it.each` + a tagged template table
      // is a declaration, and JS_CALL_HEAD needs the backtick to see it.
      out += '`'; i = e - 1; continue;
    }
    if (c === '/' && src[i + 1] === '/') { const e = src.indexOf('\n', i); if (e < 0) break; out += ' '; i = e - 1; continue; }
    if (c === '/' && src[i + 1] === '*') { const e = src.indexOf('*/', i + 2); if (e < 0) break; out += ' '; i = e + 1; continue; }
    out += c;
  }
  return (out.match(JS_CALL_HEAD) || []).length;
}

// ─────────────────────────── 4c. the mapping (option C) ───────────────────────────
// `docs/design/tagging.md`: the corpus carries behaviour → test, so an app adopts
// Kit without a diff in its own test suite. James's call between this and markers
// (option A); this is the default and the gate supports both.
//
// The obvious objection is that a mapping kept outside the app rots. The answer
// is that it rots LOUDLY: every entry names a file and a title that must exist,
// so a renamed or deleted test is a hard failure here rather than a silent
// downgrade to "covered". That checkability is the whole argument for option C,
// so it is a refusal, not a warning.
//
// Keyed on file + title, not title alone: snip-it has two duplicate test titles
// and a title-only key cannot address them.

function mapping(behaviours, map, titles) {
  const index = new Map();
  for (const t of titles) {
    const k = `${t.file}\u0000${t.raw}`;
    index.set(k, (index.get(k) || 0) + 1);
  }
  const files = new Set(titles.map((t) => t.file));
  const ids = new Set(behaviours.map((b) => b.id));
  const errors = [];
  const linked = new Map();

  for (const [id, entries] of Object.entries(map)) {
    if (id.startsWith('_')) continue; // reserved for metadata
    if (!ids.has(id)) { errors.push(`${id}: mapped to a test, but no such behaviour in the corpus`); continue; }
    for (const e of entries) {
      if (!files.has(e.file)) { errors.push(`${id}: names ${e.file}, which is not a test file in this app`); continue; }
      const n = index.get(`${e.file}\u0000${e.title}`) || 0;
      // Zero and two are different failures and deserve different words: one is
      // a test that moved, the other is a key that cannot address what it names.
      if (n === 0) { errors.push(`${id}: ${e.file} has no test titled "${e.title}" — renamed or deleted?`); continue; }
      if (n > 1) { errors.push(`${id}: "${e.title}" appears ${n}× in ${e.file} — file+title cannot address it uniquely`); continue; }
      linked.set(id, [...(linked.get(id) || []), e]);
    }
  }
  return {
    covered: behaviours.filter((b) => linked.has(b.id)),
    uncovered: behaviours.filter((b) => !linked.has(b.id)),
    errors,
    linked,
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

// The nouns a behaviour names. Lives here, in the module that owns the AST,
// because two copies of this rule are two things free to drift — saturation.js
// imports it rather than re-deriving it.
function nounsOf(behaviour) {
  const out = [];
  for (const step of behaviour.steps || []) {
    for (const ref of step.refs || []) {
      if (ref.kind === 'literal') continue;
      out.push(`${ref.kind}:${ref.name}`);
    }
  }
  return out;
}

// How many of THIS corpus's nouns bindings.json actually binds. bindings.json is
// a global file shared by every app, so counting its keys — which is what the
// report used to do — answered the same number for every corpus, including one
// that binds none of them (claude-code-bot#92).
function boundNouns(behaviours, bindings) {
  const referenced = new Set();
  for (const b of behaviours) for (const n of nounsOf(b)) referenced.add(n);
  const bound = [...referenced].filter((n) => Object.prototype.hasOwnProperty.call(bindings, n));
  return { referenced, bound: bound.length };
}

module.exports = {
  parse, parseStep, resolve, generate, coverage, adjudication, surface,
  questions, questionErrors, renderSheet, nounsOf, boundNouns,
  testTitles, expectedTestCount, jsDeclarationCount, mapping, TEST_FILE_RE,
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

  const { referenced, bound: boundCount } = boundNouns(behaviours, bindings);
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
  console.log(`  nouns bound           ${boundCount}/${referenced.size}   in THIS corpus (Cucumber would need one step definition per step phrasing, i.e. ${steps})`);
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
