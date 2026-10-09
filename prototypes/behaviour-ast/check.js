#!/usr/bin/env node
//
// kit check — THE GATE
// ────────────────────
// Stage 0's actual deliverable (`docs/timeline.md`): *a behaviour with no test
// naming it fails the build*. `coverage()` has computed that since the first
// commit; nothing has ever called it from an exit code, and kit.js said so in
// its own comment. This is the exit code.
//
//   node check.js <app> --repo <path>              # option C, the default
//   node check.js <app> --repo <path> --via markers # option A
//   node check.js <app> --repo <path> --dir <corpus-dir>
//
// ⚠️ WHY `--dir` EXISTS. James decided on kit#52 that *a spec lives with its
// project*. `ui.js`, `project.js`, `writer.js` and `saturation.js` have all taken
// `--dir` for a while; this gate did not, so a corpus anywhere other than
// `./behaviours` could be served, browsed and WRITTEN through the UI while being
// gateable by nothing. That asymmetry is the whole hazard: the one part of Kit
// that goes red was the one part that could only look in its own repo. The
// default is unchanged, so every existing invocation means what it did.
//
// Exit codes, and the distinction matters more than the gate:
//   0  every behaviour in the corpus has a test naming it, except those marked
//      `pending` (kit#155: spec'd on `dev`, not built yet), which have none
//   1  it looked, and something is wrong — uncovered behaviours, a mapping
//      entry naming a test that does not exist, or a `pending` behaviour that
//      a test already names
//   2  IT COULD NOT LOOK — no corpus, no repo, or zero test files read. Not the
//      same as "it looked and was fine", and never reported as green. A gate
//      that reads nothing and exits 0 is worse than no gate, because it is
//      indistinguishable from a passing one in CI ([[green-over-the-clients-question]]).
//
// ⚠️ WHAT A PASS MEANS. Under BOTH options this proves that someone linked a
// behaviour to a test — a marker written in the test, or a mapping entry naming
// it. It does not prove the test asserts the behaviour. `coverage()` reads whole
// files; `mapping()` reads a human's claim. Stated in `docs/design/tagging.md`
// and repeated in the output, because a gate quoted second-hand loses its
// caveats first.

'use strict';
const fs = require('fs');
const path = require('path');
const { parse, resolve, coverage, mapping, testTitles, expectedTestCount, TEST_FILE_RE, LAYERS } = require('./kit');

const SKIP_DIR = new Set(['node_modules', '.git', 'bin', 'obj', 'dist', 'build', '.next', 'coverage', 'playwright-report', 'test-results']);

function walk(root, rel = '', out = []) {
  for (const e of fs.readdirSync(path.join(root, rel), { withFileTypes: true })) {
    if (e.isDirectory()) {
      if (!SKIP_DIR.has(e.name)) walk(root, path.join(rel, e.name), out);
    } else if (TEST_FILE_RE.test(e.name)) {
      out.push(path.join(rel, e.name));
    }
  }
  return out;
}

function readTests(repo) {
  const files = walk(repo);
  const titles = [];
  const sources = [];
  for (const f of files) {
    const src = fs.readFileSync(path.join(repo, f), 'utf8');
    sources.push(src);
    const got = testTitles(f, src);
    // The pairing walk can drop a test silently; a count cannot. A reader that
    // under-reads makes every number below wrong in the SAFE-LOOKING direction
    // for markers and the alarming one for a mapping, so it is a refusal.
    const want = expectedTestCount(f, src);
    if (want !== null && got.length !== want) {
      // Name the evidence the reader was contradicted by, or the message sends
      // whoever reads it looking for xUnit attributes in a TypeScript file.
      const evidence = f.endsWith('.cs')
        ? `${want} [Fact]/[Theory] attribute(s) exist`
        : `${want} test declaration(s) survive stripping strings and comments`;
      return { fatal: `${f}: read ${got.length} test(s) but ${evidence} — the two counts disagree, so the reader is losing or inventing tests` };
    }
    titles.push(...got);
  }
  return { files, titles, sources };
}

// A Playwright spec — the suite that walks the app in a browser. Everything else
// `TEST_FILE_RE` reads (`*.test.*`, `*Tests.cs`) is a unit test.
const E2E_FILE_RE = /\.spec\.(ts|tsx|js|jsx)$/;

// kit#89. What each layer refuses, given the files whose tests name a behaviour.
// A pending behaviour is exempt: it is not built, so it has no evidence to judge.
function layerProblems(behaviours, covered, filesOf) {
  const problems = [];
  const coveredIds = new Set(covered.map((b) => b.id));
  for (const b of behaviours) {
    if (b.pending) continue;
    if (b.layer === 'ui') {
      problems.push(`${b.id}: a ui behaviour cannot be satisfied yet — visual checks are not designed (kit#89). Mark it pending, or give it another layer`);
    } else if (b.layer === 'technical' && coveredIds.has(b.id)) {
      const files = filesOf(b.id);
      if (files.every((f) => E2E_FILE_RE.test(f))) {
        problems.push(`${b.id}: a technical behaviour needs a unit test, but only e2e specs name it (${files.join(', ')})`);
      }
    }
  }
  return problems;
}

const VALUE_FLAGS = new Set(['--repo', '--via', '--dir']);
const DEFAULT_DIR = path.join(__dirname, 'behaviours');

// The one predicate for "this token is a flag, not a value". This file used to
// carry its own — `startsWith('--')` — which made `-h` an app name and let a
// forgotten `--dir` eat the next flag as a corpus path. See `cli.js`.
const { looksLikeAFlag } = require('./cli.js');

// Scans positionally instead of `argv.indexOf(flag) + 1`. The old form asked
// `argv.indexOf(a)` for the index of a VALUE, which is the first index holding
// that string and not necessarily this one — so an app whose name equalled the
// repo path made the app unfindable. With a third value-flag that collision gets
// likelier, and a mis-scanned `--dir` gates the wrong corpus.
//
// 🔑 Every rejection here is exit 2, never a default. A typo'd `--dir` that fell
// back to `./behaviours` would gate Kit's own corpus, report a clean pass, and be
// indistinguishable in CI from having checked the corpus you asked for — the
// exact failure `readTests` already refuses for.
function parseArgs(argv) {
  const opts = { app: null, repo: null, via: 'mapping', dir: DEFAULT_DIR };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (VALUE_FLAGS.has(a)) {
      const v = argv[i + 1];
      if (v === undefined || looksLikeAFlag(v)) return { error: `${a} needs a value` };
      if (a === '--repo') opts.repo = v;
      else if (a === '--via') opts.via = v;
      else opts.dir = v;
      i++;
    } else if (looksLikeAFlag(a)) {
      return { error: `unknown option ${a}` };
    } else if (opts.app === null) {
      opts.app = a;
    } else {
      return { error: `two app names given, "${opts.app}" and "${a}" — this gate checks one corpus` };
    }
  }
  return opts;
}

function main(argv) {
  const opts = parseArgs(argv);
  if (opts.error) {
    console.error(`cannot look: ${opts.error}`);
    console.error('usage: node check.js <app> --repo <path-to-app-repo> [--via mapping|markers] [--dir <corpus-dir>]');
    return 2;
  }
  const { app, repo, via, dir } = opts;

  if (!app || !repo) {
    console.error('usage: node check.js <app> --repo <path-to-app-repo> [--via mapping|markers] [--dir <corpus-dir>]');
    return 2;
  }
  if (via !== 'mapping' && via !== 'markers') {
    console.error(`--via must be "mapping" (option C, default) or "markers" (option A), got "${via}"`);
    return 2;
  }
  if (!fs.existsSync(repo)) { console.error(`cannot look: no such repo ${repo}`); return 2; }

  const behPath = path.join(dir, `${app}.beh`);
  if (!fs.existsSync(behPath)) { console.error(`cannot look: no corpus ${behPath}`); return 2; }
  const { behaviours } = resolve(parse(fs.readFileSync(behPath, 'utf8'), `${app}.beh`));
  if (!behaviours.length) { console.error(`cannot look: ${app}.beh resolved to 0 behaviours`); return 2; }

  const read = readTests(repo);
  if (read.fatal) { console.error(`cannot look: ${read.fatal}`); return 2; }
  if (!read.files.length) { console.error(`cannot look: 0 test files under ${repo}`); return 2; }

  let result, errors = [];
  if (via === 'markers') {
    result = coverage(behaviours, read.sources);
    // An id written in a test that no behaviour claims is a rot signal in the
    // other direction: the corpus lost a behaviour the tests still name.
    errors = result.orphanTests.map((id) => `[${id}] is named by a test but is not a behaviour in this corpus`);
  } else {
    // Same `dir`, deliberately. A `--dir` that moved the corpus but left the
    // mapping behind would half-relocate a project: the gate would read one
    // repo's behaviours against another repo's claims about its tests, and both
    // files exist, so nothing would say so.
    const mapPath = path.join(dir, `${app}.tests.json`);
    if (!fs.existsSync(mapPath)) { console.error(`cannot look: no mapping ${mapPath} (--via mapping)`); return 2; }
    result = mapping(behaviours, JSON.parse(fs.readFileSync(mapPath, 'utf8')), read.titles);
    errors = result.errors;
  }

  // kit#155: a `pending` behaviour is on `dev` ahead of its code, so having no
  // test yet is the expected state and not a failure. A pending behaviour that
  // DOES have a test is the opposite slip — the branch that built it forgot to
  // delete the marker — and it fails, because otherwise it would sit in the
  // pending list as unbuilt work forever.
  const notBuilt = result.uncovered.filter((b) => b.pending);
  const uncovered = result.uncovered.filter((b) => !b.pending);
  for (const b of result.covered.filter((x) => x.pending)) {
    errors.push(`${b.id}: a test names this behaviour, but it is still marked pending — delete the \`pending\` line`);
  }
  const built = behaviours.length - notBuilt.length;

  // kit#89: the layer decides which suite's evidence counts. `ux` is unchanged —
  // any test, as before layers existed. A `technical` behaviour is an
  // implementation detail, which a browser walking the app cannot prove, so a
  // Playwright spec alone does not satisfy it. `ui` has no evidence at all yet.
  const filesOf = (id) => (via === 'markers'
    ? read.files.filter((f, i) => read.sources[i].includes(`[${id}]`))
    : (result.linked.get(id) || []).map((e) => e.file));
  errors.push(...layerProblems(behaviours, result.covered, filesOf));
  const uiRefused = new Set(behaviours.filter((b) => b.layer === 'ui' && !b.pending).map((b) => b.id));
  const layers = LAYERS.map((l) => [l, behaviours.filter((b) => b.layer === l).length]).filter(([, n]) => n);

  console.log(`── kit check: ${app} (via ${via}) ──`);
  // Name the corpus that was read, not just the app. Once two directories can
  // answer to one app name, "kit check: snip-it ✅" no longer says which one
  // went green, and the reader of a CI log has no way to find out.
  console.log(`   ${behaviours.length} behaviour(s) read from ${behPath}`);
  console.log(`   ${read.files.length} test file(s), ${read.titles.length} test(s) read from ${repo}`);
  console.log(`   ${built - uncovered.length}/${built} behaviour(s) have a test naming them`);
  if (notBuilt.length) console.log(`   ${notBuilt.length} pending behaviour(s): spec'd, not built — not counted above`);
  // Only once a corpus uses a layer, so every all-ux corpus prints as before.
  if (layers.some(([l]) => l !== 'ux')) console.log(`   layers: ${layers.map(([l, n]) => `${l} ${n}`).join(', ')}`);
  console.log('');

  for (const e of errors) console.log(`   ✗ ${e}`);
  // A `ui` behaviour already has its own line above; "no test names it" would
  // send the reader to write a test that cannot satisfy it.
  const untested = uncovered.filter((b) => !uiRefused.has(b.id));
  for (const b of untested) console.log(`   ✗ ${b.id}: no test names this behaviour — "${b.title}"`);
  for (const b of notBuilt) console.log(`   ◌ ${b.id}: pending — spec'd, not built — "${b.title}"`);

  if (!errors.length && !uncovered.length) {
    console.log(notBuilt.length ? '   ✅ every built behaviour is named by a test.' : '   ✅ every behaviour is named by a test.');
    console.log('   ⚠️  this proves someone LINKED each behaviour to a test, not that the test');
    console.log('      asserts it. See docs/design/tagging.md before quoting this as coverage.');
    return 0;
  }
  console.log(`\n   ${errors.length + untested.length} problem(s). This is what "fails the build" means.`);
  return 1;
}

if (require.main === module) process.exit(main(process.argv.slice(2)));
// `VALUE_FLAGS` is exported so the gate on the flag predicate can read the tool's
// OWN set rather than keep a copy of it — a copy would be a fourth list free to
// drift, which is the defect the three gates above this one exist for.
module.exports = { main, readTests, walk, parseArgs, layerProblems, VALUE_FLAGS, DEFAULT_DIR };
