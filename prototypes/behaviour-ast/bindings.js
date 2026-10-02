// bindings.js — THE ONE ANSWER TO "WHERE DO THIS CORPUS'S BINDINGS LIVE?"
//
// ── The rule, as James decided it on kit#66 ─────────────────────────────────
// A corpus's bindings live BESIDE IT, in `<app>.bindings.json`, and belong to
// that corpus alone. His words: *"I think lives in a repo not shared in kit.
// Imagine scaled to 1000 projects and 1000 project owners no need to share
// nouns."* So a project owns its vocabulary the same way it owns its spec
// (kit#52), and two projects naming the same noun is not a collision — it is two
// projects, each right about itself.
//
// This replaces one flat map over every corpus. What that map cost is written in
// its own header, twice: snip-it's `page:Home` was `./` and macro-metrics' was
// `/macro-metrics/`, so an unprefixed `page:Home` in the wrong corpus emitted
// `page.goto('./')` — a test that RUNS, against the wrong app, with no
// unbound-noun warning. Every noun in two of the three corpora was hand-prefixed
// to dodge that, which the file itself called "not a design, it is a habit".
//
// ── Why `--bindings` is gone rather than extended ───────────────────────────
// It pointed at ONE file, which cannot address a run spanning several corpora.
// It existed so a harness could exercise the bind route without writing into the
// repo it was measuring — and `--dir` now does that by itself, because isolating
// the corpus directory isolates the bindings inside it. One flag where there were
// two, and `selfhost/run.js`'s half-applied isolation (it copied the corpus and
// not the bindings) stops being possible to write.
//
// ── The naming ─────────────────────────────────────────────────────────────
// `<app>.bindings.json`, beside `<app>.beh`, following the `<app>.tests.json`
// convention already in `behaviours/`. A corpus with no file binds nothing, which
// is a real state and not an error: 7 of the 10 corpora here bind nothing today.
//
// ⚠️ It requires nothing, and must not. `writer.js` already requires `kit.js`, so
// a resolver living in `writer.js` could not be used by `kit.js`.

const fs = require('fs');
const path = require('path');

const SUFFIX = '.bindings.json';

/** Where corpora live when a caller was told nothing more specific. */
const BEH_DIR = path.join(__dirname, 'behaviours');

/**
 * The resolution rule, in one place: a corpus's bindings sit beside its `.beh`.
 * This is the function James's kit#66 decision lives in — everything else just
 * calls it, which is what claude-code-bot#69 was for.
 */
function fileFor(app, dir = BEH_DIR) {
  return path.join(dir, `${app}${SUFFIX}`);
}

/**
 * Read one corpus's bindings, VERBATIM.
 *
 * ⚠️ It must not filter the `_comment` key. It is prose the writer round-trips on
 * every bind, `writer.js:isComment` is what knows to skip it, and `kit.js` is
 * unbothered because it looks nouns up by name. Filtering here would quietly
 * change what gets written back.
 *
 * A missing file returns `{}` — "this corpus binds nothing yet", which is a state
 * 7 of the 10 corpora are genuinely in, and which the report already renders as
 * `0/N bound`. It is NOT conflated with an unreadable one: a file that exists and
 * will not parse still throws, because that is could-not-look.
 */
function readFor(app, dir = BEH_DIR) {
  const file = fileFor(app, dir);
  if (!fs.existsSync(file)) return {};
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

/**
 * The corpus a behaviour came from. `at` is `<file>:<line>` and is the ONLY
 * per-behaviour provenance the AST carries — deliberately read here rather than
 * adding a field, so the parsed shape (and the UI fixtures pinned to it) do not
 * move for a resolution change.
 *
 * ⚠️ Ids are corpus-scoped and DO collide across corpora — `kit.beh` and
 * `kit-ui.beh` both use `BEH-UI-*` — so a behaviour must never be traced back to
 * its corpus by id.
 */
function corpusOf(behaviour) {
  const at = behaviour && behaviour.at;
  if (typeof at !== 'string') return null;
  const file = at.slice(0, at.lastIndexOf(':'));
  return file.endsWith('.beh') ? file.slice(0, -'.beh'.length) : null;
}

/**
 * Every corpus's bindings in one directory, keyed by app. For a run spanning
 * several corpora: each behaviour is generated against ITS OWN corpus's map, so
 * two corpora using one noun name cannot reach each other — by construction,
 * rather than by a warning nobody reads.
 */
function readAll(dir = BEH_DIR) {
  const out = {};
  if (!fs.existsSync(dir)) return out;
  for (const f of fs.readdirSync(dir)) {
    if (!f.endsWith('.beh')) continue;
    const app = f.slice(0, -'.beh'.length);
    out[app] = readFor(app, dir);
  }
  return out;
}

module.exports = { SUFFIX, BEH_DIR, fileFor, readFor, readAll, corpusOf };
