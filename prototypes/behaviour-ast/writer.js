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
const { parse } = require('./kit.js');

const BEH_DIR = path.join(__dirname, 'behaviours');

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
  const opts = { dir: BEH_DIR, source: null, actor: null, rest: [] };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--dir') { opts.dir = argv[i + 1]; i++; }
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
  const { dir, source, actor, rest } = parseArgs(argv);
  const [app, verb, id, arg] = rest;

  if (!app || !verb) {
    console.error('usage: writer.js <app> add-step <BEH-ID> "<step>" | <app> add-behaviour <BEH-ID> "<title>"');
    return 2;
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
  else {
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

module.exports = { block, ids, addStep, addBehaviour, validate, shape, corpusPath, commitToDisk, parseArgs, main, INDENT };

if (require.main === module) process.exit(main(process.argv.slice(2)));
