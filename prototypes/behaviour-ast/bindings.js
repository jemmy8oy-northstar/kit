// bindings.js — THE ONE ANSWER TO "WHERE DO THIS CORPUS'S BINDINGS LIVE?"
//
// Nothing here is new behaviour. Every caller resolved that question for itself
// before this file existed, and they did not all resolve it the same way:
//
//   writer.js, project.js, ui.js   took `--bindings`, defaulting to __dirname
//   kit.js, requires.js,           hardcoded path.join(__dirname,'bindings.json')
//   compare.js, prose-audit.js,    with no flag and no way to point them anywhere
//   saturation.js                  — even the three that already took `--dir`
//
// ── Why that split is a defect and not a tidiness complaint ─────────────────
// It has already cost us once. `project.js:74` carries the post-mortem: `ui.js`
// gained `--bindings` so a harness could exercise the bind route without writing
// into the repo it was measuring, the WRITE honoured it and the READ did not, and
// a bind reported success, changed the file on disk, and the page re-read the
// *other* file and showed the same refusal. Every test passed throughout.
//
// That was fixed in ONE place. Five callers kept the shape of the bug, which is
// what makes it worth a module rather than a sixth careful patch: a rule applied
// by hand at N call sites is not a rule, it is N chances to differ.
//
// ── What the split makes LATENT, measured 2026-09-25 (kit#66) ───────────────
// Both of these are latent today and neither is a live bug — say so, because the
// first framing of them overclaimed and a probe is the only reason we know:
//
//   `saturation.js --dir <elsewhere>` reads the corpora it was pointed at and the
//   bindings of THIS directory. Against a byte-identical copy that is invisible —
//   a copy shares noun NAMES, so Kit's own bindings still resolve them and both
//   runs print the same 27 bound targets. Against a genuinely foreign corpus
//   (every `kind:Name` uniformly renamed, grammar, population and per-corpus noun
//   counts held identical) the same command prints **1**. It does not say it could
//   not look; it prints the collapse as a finding.
//
//   `selfhost/run.js:207` copies the corpus into a tmpdir described in its own
//   comment as "the copy the tests are allowed to write to", and does not copy
//   bindings.json or pass `--bindings` to the ui.js it spawns. A self-hosted bind
//   would therefore write to the real repo file. Not reachable today — kit-ui.beh
//   is entirely `opens`/`sees` — so the isolation is half-applied, not broken.
//
// Both bite the moment a corpus genuinely relocates, which is exactly what James
// decided on kit#66: bindings live WITH the corpus, per-project namespaces, on the
// argument that a thousand projects with a thousand owners have no need to share
// nouns. Executing that means changing where the answer comes from. This file is
// so that it changes in one function instead of eleven.
//
// ⚠️ It deliberately takes NO `dir` argument yet. The resolution rule here is
// today's rule exactly, unchanged, so this lands provably behaviour-neutral; a
// parameter nothing reads would be a rule nobody enforces.
//
// ⚠️ It lives in its own module and requires nothing, because it cannot live in
// writer.js — writer.js already requires kit.js, and kit.js needs this.

const fs = require('fs');
const path = require('path');

/**
 * Where a caller that was given nothing more specific looks. This is the file
 * beside the prototype, which is where every corpus's bindings live today.
 */
const BINDINGS_FILE = path.join(__dirname, 'bindings.json');

/**
 * The resolution rule, in one place. `file` is whatever the caller was told on
 * the command line (`--bindings`), or null/undefined if it was told nothing.
 *
 * Null-defaulting rather than a default parameter value, so that a caller
 * threading an explicit `null` through — which `ui.js` does, to guarantee its
 * read and its write cannot be configured apart — gets the same answer as a
 * caller that passed nothing at all.
 */
function resolve(file) {
  return file || BINDINGS_FILE;
}

/**
 * Read and parse a bindings file, VERBATIM.
 *
 * ⚠️ It must not filter the `_comment*` keys, and there are three of them
 * (`_comment`, `_comment_macro_metrics`, `_comment_kit_ui`). They are prose the
 * writer round-trips on every bind, `writer.js:isComment` is what knows to skip
 * them, and `kit.js` is unbothered because it looks nouns up by name. Filtering
 * here would quietly change what gets written back.
 */
function read(file) {
  return JSON.parse(fs.readFileSync(resolve(file), 'utf8'));
}

module.exports = { BINDINGS_FILE, resolve, read };
