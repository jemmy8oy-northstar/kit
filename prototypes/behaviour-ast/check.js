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
//   0  every behaviour in the corpus has a test naming it
//   1  it looked, and something is wrong — uncovered behaviours, or a mapping
//      entry naming a test that does not exist
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
const { parse, resolve, coverage, mapping, testTitles, expectedTestCount, TEST_FILE_RE } = require('./kit');

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

const VALUE_FLAGS = new Set(['--repo', '--via', '--dir']);
const DEFAULT_DIR = path.join(__dirname, 'behaviours');

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
      if (v === undefined || v.startsWith('--')) return { error: `${a} needs a value` };
      if (a === '--repo') opts.repo = v;
      else if (a === '--via') opts.via = v;
      else opts.dir = v;
      i++;
    } else if (a.startsWith('--')) {
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

  console.log(`── kit check: ${app} (via ${via}) ──`);
  // Name the corpus that was read, not just the app. Once two directories can
  // answer to one app name, "kit check: snip-it ✅" no longer says which one
  // went green, and the reader of a CI log has no way to find out.
  console.log(`   ${behaviours.length} behaviour(s) read from ${behPath}`);
  console.log(`   ${read.files.length} test file(s), ${read.titles.length} test(s) read from ${repo}`);
  console.log(`   ${result.covered.length}/${behaviours.length} behaviour(s) have a test naming them\n`);

  for (const e of errors) console.log(`   ✗ ${e}`);
  for (const b of result.uncovered) console.log(`   ✗ ${b.id}: no test names this behaviour — "${b.title}"`);

  if (!errors.length && !result.uncovered.length) {
    console.log('   ✅ every behaviour is named by a test.');
    console.log('   ⚠️  this proves someone LINKED each behaviour to a test, not that the test');
    console.log('      asserts it. See docs/design/tagging.md before quoting this as coverage.');
    return 0;
  }
  console.log(`\n   ${errors.length + result.uncovered.length} problem(s). This is what "fails the build" means.`);
  return 1;
}

if (require.main === module) process.exit(main(process.argv.slice(2)));
module.exports = { main, readTests, walk, parseArgs, DEFAULT_DIR };
