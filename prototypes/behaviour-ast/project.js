#!/usr/bin/env node
'use strict';
//
// project — the read model, as JSON
// ─────────────────────────────────
// One projection of everything Kit knows about one app, in the shape a UI
// consumes. `docs/design/ui.md` calls this the option-invariant half: both open
// decisions there (where the UI runs, whether it writes) need exactly this data
// and neither changes its shape, so it can be built before either lands
// ([[option-invariant-half]]).
//
//   node project.js <app> [--repo <path>] [--pretty]
//
// It adds no analysis. Every field below is an existing kit.js export, renamed
// only where the export's name would be meaningless outside kit.js. If you find
// yourself computing something here, it belongs in kit.js where the suite and
// the mutation harness can see it.
//
// Exit 0 = projected. Exit 2 = could not look — no corpus, or a corpus that
// parsed to nothing. There is no exit 1: this reports, it does not judge. A
// gate that says "0 covered" and a projection that says "coverage unavailable"
// are different statements and only `check.js` is allowed to make the first.

const fs = require('fs');
const path = require('path');
const kit = require('./kit.js');

const BEH_DIR = path.join(__dirname, 'behaviours');

// ⚠️ `available: false` with a reason, never an empty object or a zero. A UI
// that cannot tell "no mapping exists" from "nothing is covered" will render
// the second, and the second is an alarm ([[empty-means-two-things]]).
function unavailable(reason) {
  return { available: false, reason };
}

function readTests(repo) {
  const SKIP = new Set(['node_modules', '.git', 'bin', 'obj', 'dist', 'build', '.next', 'coverage', 'playwright-report', 'test-results']);
  const out = [];
  const walk = (rel) => {
    for (const e of fs.readdirSync(path.join(repo, rel), { withFileTypes: true })) {
      if (e.isDirectory()) { if (!SKIP.has(e.name)) walk(path.join(rel, e.name)); }
      else if (kit.TEST_FILE_RE.test(e.name)) out.push(path.join(rel, e.name));
    }
  };
  walk('');
  const titles = [];
  const sources = [];
  for (const f of out) {
    const src = fs.readFileSync(path.join(repo, f), 'utf8');
    sources.push(src);
    const got = kit.testTitles(f, src);
    const want = kit.expectedTestCount(f, src);
    // The same refusal check.js makes. A projection built on a reader that is
    // losing tests is wrong in the same direction everywhere, and quietly.
    if (want !== null && got.length !== want) {
      return { fatal: `${f}: read ${got.length} test(s) but the independent count says ${want} — the reader is losing or inventing tests` };
    }
    titles.push(...got);
  }
  return { files: out, titles, sources };
}

function project(app, { repo = null, behDir = BEH_DIR } = {}) {
  const corpusPath = path.join(behDir, `${app}.beh`);
  if (!fs.existsSync(corpusPath)) return { fatal: `no corpus at ${corpusPath}` };

  const src = fs.readFileSync(corpusPath, 'utf8');
  const { behaviours, conflicts, symbols } = kit.resolve(kit.parse(src, `${app}.beh`));
  if (!behaviours.length) return { fatal: `${app}.beh parsed to zero behaviours` };

  const bindings = JSON.parse(fs.readFileSync(path.join(__dirname, 'bindings.json'), 'utf8'));

  // The output pane: one generated test per behaviour, with what it could not
  // bind. This is the half of his loop that is "iterating on the output".
  const generated = behaviours.map((b) => {
    const g = kit.generate(b, bindings, symbols);
    return { id: b.id, code: g.code, missing: g.missing, stats: g.stats };
  });

  let coverage = unavailable('no --repo given, so no test files were read');
  const mapPath = path.join(behDir, `${app}.tests.json`);
  if (repo) {
    if (!fs.existsSync(repo)) {
      coverage = unavailable(`--repo ${repo} does not exist`);
    } else if (!fs.existsSync(mapPath)) {
      coverage = unavailable(`no ${app}.tests.json — this app has no mapping, which is not the same as having no coverage`);
    } else {
      const read = readTests(repo);
      if (read.fatal) {
        coverage = unavailable(read.fatal);
      } else {
        // No `_`-key stripping here on purpose: `mapping()` already skips them
        // (kit.js, "reserved for metadata"). A copy of that rule was written
        // here first, and a mutation proved it was dead code — the rule would
        // then have existed twice, free to drift, with only one of the two
        // tested ([[a-refinement-can-silently-do-nothing]]).
        const map = JSON.parse(fs.readFileSync(mapPath, 'utf8'));
        const m = kit.mapping(behaviours, map, read.titles);
        coverage = {
          available: true,
          via: 'mapping',
          testFiles: read.files.length,
          testsRead: read.titles.length,
          covered: m.covered.map((b) => b.id),
          uncovered: m.uncovered.map((b) => b.id),
          errors: m.errors,
          // ⚠️ Stated in the payload, not only in the docs. A number travels
          // further than the page it was written on, and this one over-claims
          // the moment it is quoted without this sentence.
          proves: 'someone LINKED each covered behaviour to a test. NOT that the test asserts the behaviour.',
        };
      }
    }
  }

  const adj = kit.adjudication(behaviours);
  const surf = kit.surface(behaviours);
  const qs = kit.questions(behaviours, conflicts);

  return {
    app,
    corpus: path.relative(path.join(__dirname, '..', '..'), corpusPath),
    // A corpus can describe an app that does not exist (a trial, cc-bot#92).
    // The UI is a viewer, so it SHOWS these — hiding one would make the list
    // lie about what corpora exist — but it must not present an invented app
    // as indistinguishable from a shipped one. Read from the corpus, same
    // directive saturation.js excludes on; declared in one place.
    notReal: /^#\s*kit:not-a-real-app\b/m.test(src),
    // A corpus can also describe an app that DOES exist and is already listed
    // under another corpus (cc-bot#92's forward trials). `notReal` is false of
    // it and would be a lie; but the list must still not show two entries that
    // both look like the project itself. Names the app it duplicates rather than
    // being a bare boolean, because "which one is the real project" is the only
    // question a reader has on seeing it. `null`, never undefined — absent and
    // not-a-duplicate must read differently.
    duplicateOf: (/^#\s*kit:duplicate-corpus\s+(\S+)/m.exec(src) || [null, null])[1],
    behaviours: behaviours.map((b) => ({
      id: b.id,
      title: b.title,
      actor: b.actor,
      steps: b.steps.map((s) => ({ kind: s.kind, verb: s.verb, text: s.text, refs: s.refs, holes: s.holes })),
      open: b.open.map((h) => h.key),
      filled: b.filled.map((f) => ({ key: f.key, value: f.value })),
      source: b.source,
      review: b.review,
      asks: b.asks,
      at: b.at,
    })),
    conflicts,
    generated,
    coverage,
    adjudication: {
      defined: adj.defined,
      inferred: adj.inferred,
      unreviewed: adj.unreviewed.map((b) => b.id),
      approved: adj.approved.map((b) => b.id),
      denied: adj.denied.map((b) => b.id),
      untraceable: adj.untraceable.map((b) => b.id),
    },
    surface: { errors: surf.errors, served: surf.served.map((b) => b.id), unserved: surf.unserved.map((b) => b.id) },
    questions: qs,
  };
}

function main(argv) {
  const app = argv.find((a) => !a.startsWith('--') && argv[argv.indexOf(a) - 1] !== '--repo' && argv[argv.indexOf(a) - 1] !== '--dir');
  if (!app) {
    console.error('usage: project.js <app> [--repo <path>] [--pretty]');
    return 2;
  }
  const ri = argv.indexOf('--repo');
  const di = argv.indexOf('--dir');
  const out = project(app, {
    repo: ri >= 0 ? argv[ri + 1] : null,
    behDir: di >= 0 ? argv[di + 1] : BEH_DIR,
  });
  if (out.fatal) {
    console.error(`project: ${out.fatal} — could not look`);
    return 2;
  }
  console.log(JSON.stringify(out, null, argv.includes('--pretty') ? 2 : 0));
  return 0;
}

module.exports = { project, main };

if (require.main === module) process.exit(main(process.argv.slice(2)));
