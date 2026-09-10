#!/usr/bin/env node
'use strict';
//
// writer — the only thing in Kit that changes a corpus
// ────────────────────────────────────────────────────
// `docs/design/ui.md` left decision 2 open — *does the UI write the corpus, and
// does it stop at the working tree or go on to commit?* — with a stated default
// and an acting date of **2026-09-08**. That date passed in silence, so the
// default is now the decision (claude-code-bot#59):
//
//   **Write the file. Never touch git.**
//
// He reviews the change as an ordinary working-tree diff and commits it himself.
// Committing on his behalf can be added later without rework; a bot that commits
// to a spec repo cannot be un-added after the first surprise.
//
//   node writer.js <app> add-step <BEH-ID> "<kind> <rest>"  [--dir <behaviours>]
//   node writer.js <app> add-behaviour <BEH-ID> "<title>"   [--dir <behaviours>]
//   node writer.js <app> review <BEH-ID> "approved"          [--dir <behaviours>]
//   node writer.js <app> review <BEH-ID> "denied <correction>"
//   node writer.js <app> bind <kind:Noun> '<json>'           [--bindings <file>]
//
// ── 1. A surgical edit of one block, never a re-serialisation ────────────────
// A corpus is a hand-authored document whose comments carry its most important
// caveats — `snip-it.beh` opens with nine lines explaining that every step maps
// to a line that exists in a real spec file, and `trial-lend.beh` is only
// interpretable at all because a comment says the app does not exist. A
// round-trip through `parse()` and back would delete every one of them, because
// `parse()` does not keep them. So this file edits an ARRAY OF LINES and splices;
// it never reconstructs a file from an AST. There is no `unparse` here on
// purpose.
//
// ── 2. It refuses to write anything that will not parse ─────────────────────
// The new text is parsed before it reaches the disk, and a failure returns an
// error instead of writing. The alternative is a UI that can corrupt the corpus
// it exists to edit, and the corpus is the source of truth for everything else
// Kit says.
//
// ── 3. It refuses to change any behaviour except the target ─────────────────
// Not a test, a guard: the old and new texts are both parsed and every OTHER
// behaviour is compared. A splice with an off-by-one lands a step in the
// neighbouring behaviour, which parses perfectly and is silently wrong — the
// worst failure this file can have, because the corpus is what everything
// downstream measures against ([[an-uncaught-mutation-is-a-finding]]).
//
// ── 4. It never touches git, and that is checkable, not promised ────────────
// Nothing here requires `child_process` and nothing shells out. `kit.test.js`
// asserts that by reading this file's own source, because "the writer does not
// commit" is a property of the file rather than of any one run — the same way
// `converge.js` proves it has no threshold.

const fs = require('fs');
const path = require('path');
const { parse, parseStep, nounsOf } = require('./kit.js');

const BEH_DIR = path.join(__dirname, 'behaviours');
const BINDINGS_FILE = path.join(__dirname, 'bindings.json');

/** The indentation every corpus uses for a step line under its behaviour header. */
const INDENT = '  ';

/**
 * The line range of one behaviour's block, as indices into `lines`.
 *
 * `end` is the LAST CONTENT line of the block — not the line before the next
 * header. The difference is everything: blank lines and comments sitting between
 * two behaviours belong to the reader, and usually to the behaviour BELOW them
 * (`snip-it.beh`'s comments introduce the block that follows). Appending after
 * them would push a step past a comment that no longer describes it, and
 * appending after a blank line would grow a stray gap on every edit.
 *
 * Returns null when the id is not in the file — never a guess, and never 0.
 */
function block(text, id) {
  const lines = text.split('\n');
  const header = (l) => /^behaviour\s+([A-Z][A-Z0-9-]*)\s+"/.exec(l);

  let start = -1;
  for (let i = 0; i < lines.length; i++) {
    const m = header(lines[i]);
    if (m && m[1] === id) { start = i; break; }
  }
  if (start === -1) return null;

  let end = start;
  for (let i = start + 1; i < lines.length; i++) {
    const line = lines[i];
    if (header(line)) break;
    // A blank or a comment does not extend the block; a content line does. So a
    // trailing run of blanks/comments is left outside it, and an interior one
    // (a comment between two steps) is swallowed back in by the next content
    // line, which is the correct reading in both cases.
    if (line.trim() && !line.trim().startsWith('#')) end = i;
  }
  return { id, start, end };
}

/** Every behaviour id in the file, in order. The only source of "does this exist". */
function ids(text) {
  return [...text.matchAll(/^behaviour\s+([A-Z][A-Z0-9-]*)\s+"/gm)].map((m) => m[1]);
}

/**
 * A comparable projection of one behaviour, for rule 3.
 *
 * Deliberately built from the parsed AST rather than from the raw lines: the
 * question rule 3 asks is "did any OTHER behaviour change MEANING", and a
 * reformatted line that parses identically is not a change of meaning. Comparing
 * raw text here would make rule 3 fire on whitespace and get switched off.
 */
function shape(b) {
  return JSON.stringify({
    id: b.id, title: b.title, actor: b.actor,
    steps: b.steps.map((s) => [s.kind, s.verb, s.noun, s.text].join('|')),
    provides: b.provides.map((p) => `${p.kind}:${p.name}.${p.slot}=${p.value.join(',')}`),
    serves: b.serves.map((s) => s.id),
    source: b.source, review: b.review,
    asks: b.asks, options: b.options.map((o) => `${o.label}|${o.consequence}`),
    recommend: b.recommend && `${b.recommend.label}|${b.recommend.why}`,
    against: b.against, cites: b.cites.map((c) => c.id),
  });
}

/**
 * The one gate every edit goes through.
 *
 * Takes the before and after texts and the id that was meant to change. Returns
 * `{ ok: true, text }` or `{ ok: false, error, reason }` — it never throws, and
 * it never writes. Callers that write do so only on `ok`.
 */
function validate(before, after, id) {
  let oldAst;
  let newAst;
  try {
    oldAst = parse(before, 'before');
  } catch (e) {
    // The corpus was already broken before this edit. Say so, rather than
    // blaming the edit for it — the two need different fixes, and a writer that
    // reports someone else's syntax error as its own sends you to the wrong file.
    return { ok: false, error: 'corpus-already-invalid', reason: `the file did not parse before this edit: ${e.message}` };
  }
  try {
    newAst = parse(after, 'after');
  } catch (e) {
    // Rule 2.
    return { ok: false, error: 'would-not-parse', reason: e.message };
  }

  // Rule 3. Every behaviour that is not the target must be byte-identical in
  // meaning, and the set of ids must not change other than by adding the target.
  // `parse()` returns the behaviour ARRAY, not a wrapper. Written out because
  // the first draft read `.behaviours` off it and threw a TypeError that the
  // caller would have reported as "would not parse" — the same class of mistake
  // ui.js's `summary()` records against itself.
  const oldById = new Map(oldAst.map((b) => [b.id, b]));
  const newById = new Map(newAst.map((b) => [b.id, b]));
  for (const [bid, b] of oldById) {
    if (bid === id) continue;
    const n = newById.get(bid);
    if (!n) return { ok: false, error: 'collateral-change', reason: `${bid} disappeared from the corpus` };
    if (shape(n) !== shape(b)) return { ok: false, error: 'collateral-change', reason: `${bid} changed, and only ${id} was meant to` };
  }
  for (const bid of newById.keys()) {
    if (bid !== id && !oldById.has(bid)) {
      return { ok: false, error: 'collateral-change', reason: `${bid} appeared, and only ${id} was meant to` };
    }
  }

  return { ok: true, text: after };
}

/**
 * Append one step line to an existing behaviour.
 *
 * `line` is the step as it would be typed into the corpus — `then sees
 * button:Save`. It is NOT re-parsed by a second grammar here: rule 2 validates
 * the whole file with `kit.js`'s parser, so there is exactly one definition of
 * what a step is, and this file cannot drift from it.
 */
function addStep(text, id, line) {
  const step = String(line).trim();
  if (!step) return { ok: false, error: 'empty-step', reason: 'a step line cannot be blank' };
  // A step containing a newline would splice two lines in while every count and
  // range here assumes one. Refused rather than silently handled: "add a step"
  // that adds two is the collateral change rule 3 exists to catch, caught one
  // layer earlier where the message can say what happened.
  if (/\n/.test(String(line))) return { ok: false, error: 'multiline-step', reason: 'a step is one line; add them one at a time' };

  const b = block(text, id);
  if (!b) return { ok: false, error: 'no-such-behaviour', reason: `no behaviour ${id} in this corpus`, known: ids(text) };

  const lines = text.split('\n'); // the whole file, comments and blanks included
  lines.splice(b.end + 1, 0, INDENT + step);
  return validate(text, lines.join('\n'), id);
}

/**
 * Append a whole new behaviour to the end of the file.
 *
 * At the END, and not sorted into place. A corpus's order is authored — related
 * behaviours sit together and the comments above them introduce runs of them —
 * so inserting by id would reorder a document a person laid out, which is a
 * collateral change that happens to parse.
 *
 * `source` defaults to `inferred`, which is not cosmetic: this function exists
 * to be called by a UI, so the behaviour it writes came from a machine unless
 * something says otherwise, and `parse()` marks an inference `unreviewed`
 * (James's #68 call: "I like this default included but marked unreviewed").
 * Silence in a corpus means a human wrote it, and a writer must not be able to
 * spend that silence.
 */
function addBehaviour(text, id, title, opts = {}) {
  if (!/^[A-Z][A-Z0-9-]*$/.test(String(id))) {
    return { ok: false, error: 'bad-id', reason: `a behaviour id is uppercase letters, digits and hyphens, got: ${id}` };
  }
  if (ids(text).includes(id)) {
    return { ok: false, error: 'duplicate-id', reason: `${id} is already in this corpus` };
  }
  if (/["\n]/.test(String(title))) {
    // The header grammar is `behaviour ID "title"`, so a quote in the title ends
    // it early and everything after becomes a parse error at best. Refused here
    // because the message can name the character; rule 2 would only say the line
    // is malformed.
    return { ok: false, error: 'bad-title', reason: 'a title cannot contain a double quote or a newline' };
  }

  const steps = Array.isArray(opts.steps) ? opts.steps : [];
  const out = [`behaviour ${id} "${title}"`];
  if (opts.actor) out.push(`${INDENT}actor ${opts.actor}`);
  out.push(`${INDENT}source ${opts.source || 'inferred'}${opts.ref ? ` ${opts.ref}` : ''}`);
  for (const s of steps) out.push(INDENT + String(s).trim());

  // Exactly one blank line between the last content line and the new block, and
  // the file keeps its trailing newline. Computed from the existing text rather
  // than assumed: a corpus that already ends with a blank line and one that does
  // not must both come out the same, or every edit alternates between two
  // spellings and the diff is never just the behaviour.
  const trimmed = text.replace(/\s*$/, '');
  const after = `${trimmed}\n\n${out.join('\n')}\n`;
  return validate(text, after, id);
}

/**
 * Set the review state of an existing behaviour — approve an inference, or deny
 * it with the correction.
 *
 * ── The first function here that CHANGES a line rather than adding one ───────
 * `addStep` and `addBehaviour` are both append-only, and that is why neither had
 * to think about what it was overwriting. Adjudication cannot be an append: a
 * behaviour already carries `review unreviewed`, and adding a second `review`
 * line leaves the file saying two things. `parse()` would take the last one and
 * be right to, but the corpus is a document a person reads, and a document that
 * contradicts itself has already lost the argument the state was recording.
 *
 * ── Why this is executing his decision rather than making one ────────────────
 * The vocabulary is not invented here. `review approved` / `review denied <what
 * is actually true>` is James's #68 call, it is what `parse()` has always
 * accepted, and it is the literal line every generated question sheet ends by
 * telling a human to type (`kit.js`'s `todo()`). Twenty-six inferences across
 * two corpora are sitting at `unreviewed` because typing it means opening the
 * file by hand. This makes the sentence clickable; it does not change what the
 * sentence is.
 *
 * ── 🔴 The newline refusal is a security guard, not tidiness ─────────────────
 * `validate()`'s rule 3 exempts the TARGET behaviour — it must, or no edit could
 * ever change anything. So free text that reaches the target's block is the one
 * place in this file where a caller's string is not policed by the collateral
 * rule. A note of `wrong\n  actor attacker` would splice a second line into the
 * target and rule 3 would wave it through, because the target is the one
 * behaviour it does not compare. `addStep` refuses newlines for the neater
 * reason that its counts assume one line; here the same refusal is load-bearing.
 * (A note that opened a whole new `behaviour` block IS caught — rule 3 sees it
 * appear — which is exactly the trap: the dangerous half is the half that stays
 * inside the target.)
 *
 * State and note are otherwise NOT re-validated here. `kit.js` already refuses a
 * state outside the three, and already refuses a denial with no correction —
 * "a denied behaviour must state the correction", his #68 point that a bare
 * denial deletes a line where a denial with a correction compounds into the
 * corpus. Restating either would be a second definition free to drift from the
 * parser, the mistake `addStep` documents itself for avoiding. The refusal
 * arrives from `validate()` carrying the parser's own sentence, which is the one
 * worth putting on screen.
 */
function setReview(text, id, state, note = null) {
  const st = String(state ?? '').trim();
  const nt = note === null || note === undefined ? '' : String(note).trim();

  if (!st) return { ok: false, error: 'empty-review', reason: 'a review needs a state: unreviewed, approved or denied' };
  // Checked on the RAW arguments, before trimming can hide an interior newline.
  if (/\n/.test(String(state ?? '')) || /\n/.test(String(note ?? ''))) {
    return { ok: false, error: 'multiline-review', reason: 'a review is one line; a state or a note cannot contain a newline' };
  }

  const b = block(text, id);
  if (!b) return { ok: false, error: 'no-such-behaviour', reason: `no behaviour ${id} in this corpus`, known: ids(text) };

  const lines = text.split('\n');
  const line = INDENT + (nt ? `review ${st} ${nt}` : `review ${st}`);

  // The keyword test is the parser's own: `kit.js` reads a line's first
  // whitespace-delimited token and nothing else, so matching on it here cannot
  // disagree with what the file will mean once written.
  let at = -1;
  for (let i = b.start + 1; i <= b.end; i++) {
    if (lines[i].trim().split(/\s+/)[0] === 'review') { at = i; break; }
  }

  if (at !== -1) {
    lines[at] = line;
  } else {
    // No explicit review line — the state was `parse()`'s default. Put the new
    // one directly under `source`, which is where every corpus that writes both
    // already puts it, and where it reads as a comment on the source rather than
    // as a stray line among the steps. Failing that, under `actor`; failing
    // that, the header, which is the only anchor certainly inside the block.
    //
    // Position is cosmetic to the parser and not to the reader, and the reader
    // is who a corpus is for: every one of these files orders its preamble
    // `actor` → `source` → `review` before the first step, so an edit that
    // landed `review` above `actor` would be correct and still look like
    // something went wrong.
    let anchor = b.start;
    for (let i = b.start + 1; i <= b.end; i++) {
      const kw = lines[i].trim().split(/\s+/)[0];
      if (kw === 'actor') anchor = i;
      if (kw === 'source') { anchor = i; break; }
    }
    lines.splice(anchor + 1, 0, line);
  }

  return validate(text, lines.join('\n'), id);
}

// ─────────────────────────── bindings ───────────────────────────
// Everything above edits a `.beh` document. This edits `bindings.json`, and the
// difference in technique is not a style choice: a corpus is a hand-authored
// document whose COMMENTS carry its caveats, so it is spliced line by line and
// never re-serialised (rule 1). JSON has no comments to lose. `bindings.json`
// keeps its prose in real `_comment*` KEYS precisely so a round-trip preserves
// it, so the safe thing here is the opposite of the safe thing there: parse,
// mutate the object, re-stringify.
//
// ── Why a binding is worth a write path at all ──────────────────────────────
// Stage 4 of `docs/design/process.md` — bind by noun, not by step — is the
// design's own answer to what killed Cucumber, and it is the only verb of the
// loop with no way to do it from the browser. Measured on 2026-09-10 across all
// nine corpora: **129 of 172 nouns (75%) have no binding**. The behaviour page
// already shows you which ones, under the note "these are why the steps above
// became comments", and then leaves you to go and hand-edit a JSON file. This
// closes that.
//
// ── 🔴 THE NOUN NAMESPACE IS GLOBAL, and the file itself calls that a habit ──
// `bindings.json` is one flat map over every corpus, and its own `_comment_kit_ui`
// block says:
//
//     "Every noun is prefixed `Kit*` because THE NOUN NAMESPACE IS GLOBAL. An
//      unprefixed page:Home here would inherit snip-it's './' and emit a test
//      that runs against the wrong app with no unbound-noun warning. Still a
//      habit rather than a design."
//
// A habit is enough while binding means opening the file and reading that
// paragraph. It stops being enough the moment binding is a form with a button,
// which is what this function makes it — so the hazard has to become a
// mechanism. `sharedWith()` below is that mechanism.
//
// It REPORTS rather than REFUSES, and that is deliberate. Measured across the
// nine corpora, 7 noun names are used by more than one corpus and **6 of the 7
// are james-habits-app described three ways** (the app plus its two trial
// corpora), where sharing one binding is correct and is the point of binding by
// noun. Only `region:EmptyState` (trial-habits-a and trial-lend) is a genuine
// cross-APP collision. A blanket refusal would break the correct majority to
// stop the minority; naming the other corpora lets the human tell which one they
// are in. Refusing to guess is Kit's rule for the EMITTER, where the alternative
// is a false green — here the alternative is a true fact on screen.

/**
 * Is this string exactly one noun, by the PARSER's definition of a noun?
 *
 * ⚠️ Not a regex here, and the first draft was one. It read
 * `/^[a-z][a-z0-9]*:[A-Za-z][A-Za-z0-9_]*$/`, which disagreed with `kit.js` in
 * both directions — it allowed a digit in the kind, which the parser does not,
 * and required the name to start with a letter, which the parser does not
 * either, so it would have rejected the real, shipped binding `file:talk_mp4`
 * had that name begun with its digit. This is precisely the drift `addStep`'s
 * comment says the writer must not have: **there is one definition of the
 * grammar and it lives in the parser.** Asking `parseStep` costs a function
 * call and cannot disagree with the file it is about to write.
 *
 * The equality at the end is what does the real work: `parseStep` scans for
 * nouns anywhere in a line, so `button:Sa ve` finds `button:Sa` and `xx
 * button:Save` finds `button:Save`. Requiring the ref to spell the WHOLE input
 * back is what makes "contains a noun" into "is a noun".
 */
function isNoun(s) {
  if (typeof s !== 'string') return false;
  const t = s.trim();
  if (!t) return false;
  const { refs, holes } = parseStep(t, 'noun-check');
  if (holes.length !== 0 || refs.length !== 1) return false;
  const [ref] = refs;
  if (ref.kind === 'literal') return false;
  return `${ref.kind}:${ref.name}` === t;
}

/** A `_comment` key is prose for the reader, not a binding. Never treated as one. */
function isComment(key) {
  return key.startsWith('_comment');
}

/**
 * Which OTHER corpora reference this noun name.
 *
 * `corpora` is `{ app: [nounKey, ...] }` — already-extracted names, not parsed
 * behaviours. That shape is the point: **the caller owns the population.**
 * Kit has two legitimate answers to "which nouns does this corpus reference"
 * and they differ — `kit.js`'s `nounsOf()` walks step refs, while
 * `requires.js` also counts a `field:` named only in a `provides` value, which
 * `nounsOf()` cannot see and which is documented there as invisible to every
 * other measurement. Choosing one inside this function would silently pick a
 * population for a caller that had already picked a different one, and the
 * failure mode is the quiet direction: a collision that exists and is not
 * reported. Passing names in also means this is testable with two literals and
 * does no IO.
 *
 * Returns app names, sorted, excluding `self`. Empty means the noun belongs to
 * one corpus alone *in the population it was given*.
 */
function sharedWith(noun, corpora, self) {
  const out = [];
  for (const [app, nouns] of Object.entries(corpora)) {
    if (app === self) continue;
    if (nouns.includes(noun)) out.push(app);
  }
  return out.sort();
}

/**
 * Add one binding. Refuses to overwrite an existing one, and refuses to change
 * any other.
 *
 * Overwriting is refused rather than supported because the two operations have
 * different consequences and only one of them is safe to reach by clicking. A
 * new binding turns a refusal into a generated step — visible, and wrong in a
 * way the generated test shows you. RE-binding silently changes what every
 * behaviour in every corpus that mentions the noun already generates, including
 * ones the person at the form has never opened. That is the collateral change
 * rule 3 exists to catch, one file over, and it deserves the same answer:
 * refuse, and say what is already there.
 *
 * `value` is the binding object — `{role, name}`, `{route}`, `{label}`,
 * `{locator}`, `{state}`, `{fixture}`, `{urlPattern}`. It is NOT validated
 * against what the verbs need here; `requires.js` already computes that per
 * noun and is the one definition of it, the same way rule 2 defers the grammar
 * to `kit.js`'s parser rather than restating it. What IS checked here is that
 * the value is a JSON object that survives a round-trip, because that is a
 * property of this file's write and of nothing else.
 */
function addBinding(text, noun, value, opts = {}) {
  let bindings;
  try {
    bindings = JSON.parse(text);
  } catch (e) {
    return { ok: false, error: 'bindings-already-invalid', reason: `bindings.json did not parse before this edit: ${e.message}` };
  }
  if (bindings === null || typeof bindings !== 'object' || Array.isArray(bindings)) {
    return { ok: false, error: 'bindings-already-invalid', reason: 'bindings.json is not a JSON object' };
  }

  const key = String(noun ?? '').trim();
  if (!isNoun(key)) {
    return { ok: false, error: 'bad-noun', reason: `a noun is <kind>:<Name>, lowercase kind and a capitalised name, got: ${noun}` };
  }
  if (isComment(key)) {
    return { ok: false, error: 'bad-noun', reason: '_comment keys are prose for the reader, not bindings' };
  }
  if (Object.prototype.hasOwnProperty.call(bindings, key)) {
    return {
      ok: false, error: 'already-bound',
      reason: `${key} is already bound to ${JSON.stringify(bindings[key])} — rebinding changes every corpus that mentions it, so it is not a click`,
      current: bindings[key],
    };
  }

  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return { ok: false, error: 'bad-binding', reason: 'a binding is a JSON object, e.g. {"role":"button","name":"Add habit"}' };
  }
  const keys = Object.keys(value);
  if (keys.length === 0) {
    return { ok: false, error: 'bad-binding', reason: 'an empty binding binds nothing — it would satisfy no verb and still count as bound' };
  }

  // A value that does not survive JSON is the one corruption this function can
  // cause that nothing downstream would report: `undefined` and a function both
  // vanish on stringify, leaving a key present with less in it than the caller
  // passed. `bound` is what boundNouns() measures, so a silently-emptied binding
  // reads as progress. Compare the round-trip rather than trusting the input.
  const after = { ...bindings, [key]: value };
  let serialised;
  try {
    serialised = `${JSON.stringify(after, null, 2)}\n`;
  } catch (e) {
    return { ok: false, error: 'bad-binding', reason: `this binding cannot be written as JSON: ${e.message}` };
  }
  const reread = JSON.parse(serialised);
  // ⚠️ Compared against the caller's OWN key list, not against
  // `JSON.stringify(value)`. The first draft did the latter and was inert: for
  // `{role: undefined}` both sides stringify to `{}`, so the check compared the
  // damage to itself and passed. `Object.keys` sees `role` before stringify
  // deletes it, which is the only vantage point from which the loss is visible
  // at all. Caught by running it, not by reading it.
  const survived = Object.keys(reread[key]);
  const lost = keys.filter((k) => !survived.includes(k));
  if (lost.length) {
    return {
      ok: false, error: 'bad-binding',
      reason: `${lost.join(', ')} would not survive being written as JSON — the binding would land with less in it than you gave, and still count as bound`,
    };
  }

  // The rule 3 equivalent. One key appeared, the target; nothing else moved.
  // Written as a comparison rather than trusted from the spread above, for the
  // same reason rule 3 is: the check is cheap and the failure it catches is
  // silent, corpus-wide and only visible in a generated test nobody re-reads.
  for (const k of Object.keys(bindings)) {
    if (JSON.stringify(reread[k]) !== JSON.stringify(bindings[k])) {
      return { ok: false, error: 'collateral-change', reason: `${k} changed, and only ${key} was meant to` };
    }
  }
  for (const k of Object.keys(reread)) {
    if (k !== key && !Object.prototype.hasOwnProperty.call(bindings, k)) {
      return { ok: false, error: 'collateral-change', reason: `${k} appeared, and only ${key} was meant to` };
    }
  }

  const corpora = opts.corpora || {};
  return {
    ok: true,
    text: serialised,
    noun: key,
    // Always present, always an array. A caller that forgets to render it shows
    // nothing rather than crashing, and a caller that renders it gets the empty
    // case for free — the difference between "no other corpus uses this name"
    // and "I did not check" is exactly what this field exists to carry.
    sharedWith: sharedWith(key, corpora, opts.app),
  };
}

/**
 * Every corpus in `dir` as `{ app: [nounKey, ...] }` — the input `sharedWith`
 * wants, read off disk.
 *
 * Uses `kit.js`'s `nounsOf()`, i.e. **the step-reference population**, and that
 * choice is deliberate rather than default: it is the same population
 * `boundNouns()` counts and the same one `generate()`'s `missing` set comes
 * from, so the nouns a bind form offers and the corpora it warns about are
 * measured the same way. The known cost is `requires.js`'s finding — a `field:`
 * named only in a `provides` value is invisible to `nounsOf()`, so a collision
 * on such a field is under-reported here. Under-report rather than disagree
 * with the list on screen; the fuller population is available by passing
 * `requires.js`'s nouns in directly.
 *
 * A corpus that does not parse is SKIPPED, not fatal. This runs to decorate a
 * write to a different file, and one broken corpus elsewhere in the directory
 * must not be able to block binding a noun — but it does mean the answer is
 * incomplete, so it is reported rather than swallowed.
 */
function corpusNouns(dir = BEH_DIR, onSkip = null) {
  const out = {};
  if (!fs.existsSync(dir)) return out;
  for (const file of fs.readdirSync(dir)) {
    if (!file.endsWith('.beh')) continue;
    const app = file.slice(0, -'.beh'.length);
    try {
      const behaviours = parse(fs.readFileSync(path.join(dir, file), 'utf8'), file);
      const names = new Set();
      for (const b of behaviours) for (const n of nounsOf(b)) names.add(n);
      out[app] = [...names].sort();
    } catch (e) {
      if (onSkip) onSkip(app, e);
    }
  }
  return out;
}

/** Resolve an app name to its corpus path. The name is looked up, never joined blindly. */
function corpusPath(app, dir = BEH_DIR) {
  if (!fs.existsSync(dir)) return null;
  const file = `${app}.beh`;
  return fs.readdirSync(dir).includes(file) ? path.join(dir, file) : null;
}

/**
 * Apply an edit to a corpus on disk.
 *
 * `edit` is one of the pure functions above, already applied — this only takes
 * its RESULT, so there is no path by which a failed edit reaches `writeFileSync`.
 */
function commitToDisk(file, result) {
  if (!result.ok) return result;
  fs.writeFileSync(file, result.text);
  return result;
}

function parseArgs(argv) {
  const opts = { dir: BEH_DIR, bindings: BINDINGS_FILE, source: null, actor: null, rest: [] };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--dir') { opts.dir = argv[i + 1]; i++; }
    else if (argv[i] === '--bindings') { opts.bindings = argv[i + 1]; i++; }
    // `--source defined` exists because using this tool on Kit's own corpus
    // found it missing. The HTTP surface takes `source` in the body and the CLI
    // could not say it, so a human at a terminal could only write behaviours
    // marked as a machine's inference. Defaulting to `inferred` is right; being
    // unable to say otherwise is not.
    else if (argv[i] === '--source') { opts.source = argv[i + 1]; i++; }
    else if (argv[i] === '--actor') { opts.actor = argv[i + 1]; i++; }
    else opts.rest.push(argv[i]);
  }
  return opts;
}

function main(argv) {
  const { dir, bindings: bindingsFile, source, actor, rest } = parseArgs(argv);
  const [app, verb, id, arg] = rest;

  if (!app || !verb) {
    console.error('usage: writer.js <app> add-step <BEH-ID> "<step>" | <app> add-behaviour <BEH-ID> "<title>" '
      + '| <app> review <BEH-ID> "approved" | <app> review <BEH-ID> "denied <correction>" '
      + '| <app> bind <kind:Noun> \'{"role":"button","name":"..."}\'');
    return 2;
  }

  // `bind` writes a DIFFERENT file from every other verb — one global
  // bindings.json rather than this app's corpus — so it takes its own path out
  // before the corpus is resolved. `app` is still required and still meaningful:
  // it is what `sharedWith` excludes, i.e. the corpus you are claiming to be in.
  if (verb === 'bind') {
    if (!fs.existsSync(bindingsFile)) {
      console.error(`writer: no bindings file at ${bindingsFile}`);
      return 2;
    }
    let value;
    try {
      value = JSON.parse(String(arg ?? ''));
    } catch (e) {
      console.error(`writer: refused — bad-binding: the value is not JSON (${e.message})`);
      return 1;
    }
    const text = fs.readFileSync(bindingsFile, 'utf8');
    const result = addBinding(text, id, value, { corpora: corpusNouns(dir), app });
    if (!result.ok) {
      console.error(`writer: refused — ${result.error}: ${result.reason}`);
      return 1;
    }
    commitToDisk(bindingsFile, result);
    console.log(`writer: ${path.relative(process.cwd(), bindingsFile)} updated — the change is in your working tree and NOT committed`);
    // Printed on stdout beside the success, not buried in a log: the whole
    // reason this line exists is that the person who just clicked bind is the
    // one who can tell whether sharing it with those corpora is what they meant.
    if (result.sharedWith.length) {
      console.log(`writer: ⚠️  the noun namespace is GLOBAL — ${result.noun} is also referenced by ${result.sharedWith.join(', ')}, `
        + 'which now generate against this binding too');
    }
    return 0;
  }

  const file = corpusPath(app, dir);
  if (!file) {
    console.error(`writer: no corpus named '${app}' in ${dir}`);
    return 2;
  }
  const text = fs.readFileSync(file, 'utf8');

  let result;
  if (verb === 'add-step') result = addStep(text, id, arg);
  else if (verb === 'add-behaviour') result = addBehaviour(text, id, arg, { source, actor });
  else if (verb === 'review') {
    // One argument, split at the first space, so a denial and its correction
    // arrive as the single quoted string a shell makes easy — and so the CLI
    // types the same sentence the corpus stores rather than a flag spelling of
    // it that only exists here.
    const value = String(arg ?? '').trim();
    const sp = value.indexOf(' ');
    result = sp === -1
      ? setReview(text, id, value, null)
      : setReview(text, id, value.slice(0, sp), value.slice(sp + 1));
  } else {
    console.error(`writer: unknown verb '${verb}'`);
    return 2;
  }

  if (!result.ok) {
    console.error(`writer: refused — ${result.error}: ${result.reason}`);
    return 1;
  }

  commitToDisk(file, result);
  // The whole of decision 2, in the line the tool prints. Anyone who expected a
  // commit finds out here rather than three days later when the change is not
  // on the branch.
  console.log(`writer: ${path.relative(process.cwd(), file)} updated — the change is in your working tree and NOT committed`);
  return 0;
}

module.exports = {
  block, ids, addStep, addBehaviour, setReview, validate, shape,
  addBinding, sharedWith, corpusNouns, isComment, isNoun,
  corpusPath, commitToDisk, parseArgs, main, INDENT, BINDINGS_FILE,
};

if (require.main === module) process.exit(main(process.argv.slice(2)));
