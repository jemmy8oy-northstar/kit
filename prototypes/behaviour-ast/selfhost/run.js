#!/usr/bin/env node
'use strict';
//
// selfhost/run — run Kit's OWN generated tests against Kit's OWN running UI
// ────────────────────────────────────────────────────────────────────────────
// `docs/pilots/kit-self-hosting.md` claims that `behaviours/kit-ui.beh` derives
// 20 Playwright steps and that **5 of its 6 generated tests pass against the
// running app**. That number was first produced by a scratch harness outside
// this repository, which means nobody but the machine that ran it could check
// it — and this repo's own standard, written three sections above that claim,
// is *"a generated test nobody ran is a claim, not evidence"*. A run nobody can
// repeat is a claim too. This file is that harness, in the repo.
//
//   node selfhost/run.js --playwright <bin>            # measure and report
//   node selfhost/run.js --playwright <bin> --check     # exit 1 if it has drifted
//   node selfhost/run.js --emit-only                   # print the spec, run nothing
//
// Exit 0 = the run happened. Exit 1 = --check and the outcome no longer matches
// `expected.json`. Exit 2 = could not look, which is deliberately not 0.
//
// ⚠️ BRING YOUR OWN PLAYWRIGHT, and that is not laziness. Adding
// `@playwright/test` to this repo's `package.json` is a packaging change, which
// is James's call (claude-code-bot#83) and not something a measurement tool
// gets to decide for the whole repo. So the binary is named on the command line
// or in `PLAYWRIGHT_BIN`, and its absence is `could not look` rather than a
// silent skip.
//
// ⚠️ IT RUNS AGAINST A COPY OF THE CORPUS, always. The behaviours under test
// include adjudicating an inference and adding a step, so the generated tests
// WRITE to the corpus they were generated from. Pointed at `behaviours/`, a
// successful run would edit the very files it is measuring.

const fs = require('fs');
const os = require('os');
const path = require('path');
const http = require('http');
const { spawn, spawnSync } = require('child_process');
const kit = require('../kit.js');

const ROOT = path.join(__dirname, '..');
const CORPUS = path.join(ROOT, 'behaviours');
const SUBJECT = 'kit-ui.beh';
const BINDINGS = path.join(ROOT, 'bindings.json');
const DIST = path.join(ROOT, 'ui', 'dist', 'index.html');
const EXPECTED = path.join(__dirname, 'expected.json');

// Assemble the spec exactly as a consumer would: parse the corpus, resolve it,
// and concatenate `generate()`'s output. Nothing here rewrites, reorders or
// repairs what the generator emitted — the whole point is to execute Kit's
// output rather than a tidied version of it.
function emitSpec(corpusPath = path.join(CORPUS, SUBJECT), bindingsPath = BINDINGS) {
  const bindings = JSON.parse(fs.readFileSync(bindingsPath, 'utf8'));
  const src = fs.readFileSync(corpusPath, 'utf8');
  const { behaviours, symbols } = kit.resolve(kit.parse(src, path.basename(corpusPath)));

  const lines = ["import { expect, test } from '@playwright/test';", ''];
  const stats = { tests: 0, generated: 0, contract: 0, ungenerated: 0, state: 0 };
  for (const b of behaviours) {
    const r = kit.generate(b, bindings, symbols);
    lines.push(r.code, '');
    stats.tests++;
    stats.generated += r.stats.generated;
    stats.contract += r.stats.contract;
    stats.ungenerated += r.stats.ungenerated;
    stats.state += b.steps.filter((s) => s.verb === 'state').length;
  }
  // A `state` step copies a setup string a human wrote in bindings.json, so it
  // is generated but not DERIVED. `self-host.js` draws the same distinction for
  // kit.beh, and the two numbers must mean the same thing to be comparable.
  stats.derived = stats.generated - stats.state;
  return { source: lines.join('\n'), stats };
}

// The refused step, and the line that follows it. This is the finding of the
// original run: the generator refuses honestly and emits a COMMENT, so the test
// runs straight on into an action that depends on the step it declined to
// write. Returned as data rather than asserted here, so the suite can pin it
// without a browser.
function refusals(source) {
  const lines = source.split('\n');
  const out = [];
  for (let i = 0; i < lines.length; i++) {
    const m = /^\s*\/\/ UNGENERATED: (.*)$/.exec(lines[i]);
    if (m) out.push({ step: m[1], next: (lines[i + 1] || '').trim() });
  }
  return out;
}

function resolvePlaywright(argv = [], env = {}) {
  const i = argv.indexOf('--playwright');
  const bin = i >= 0 ? argv[i + 1] : env.PLAYWRIGHT_BIN;
  if (!bin) return null;
  return fs.existsSync(bin) ? bin : null;
}

// Wait for the server's own API to answer, not for the process to exist. A
// spawned node process is "up" long before it is listening, and a Playwright
// run that starts too early fails with connection errors that look exactly like
// a broken locator.
function waitForServer(port, tries = 100) {
  return new Promise((resolve) => {
    let n = 0;
    const poll = () => {
      const req = http.get({ host: '127.0.0.1', port, path: '/api/projects' }, (res) => {
        res.resume();
        if (res.statusCode === 200) return resolve(true);
        again();
      });
      req.on('error', again);
      function again() {
        if (++n >= tries) return resolve(false);
        setTimeout(poll, 100);
      }
    };
    poll();
  });
}

// Two one-line predicates, pulled out of main() so they can be tested and
// mutated. Inline, they sit downstream of a spawned browser, which means the
// only way to exercise them is a full Playwright run — so in practice they
// would never be exercised at all, and a guard nobody can reach is decoration
// ([[an-error-path-that-never-fired-is-untested]]).

// Zero of each means the reporter said nothing this tool understood. Reading
// that as "0 failed" turns an unreadable run into a clean bill of health.
function unreadable(got) {
  return got.passed === 0 && got.failed === 0;
}

function drifted(want, now) {
  return JSON.stringify(want) !== JSON.stringify(now);
}

function parseResults(output) {
  const passed = /(\d+) passed/.exec(output);
  const failed = /(\d+) failed/.exec(output);
  return {
    passed: passed ? Number(passed[1]) : 0,
    failed: failed ? Number(failed[1]) : 0,
    failing: [...output.matchAll(/^\s+\S*specs\/\S+\s+›\s+(.*?)\s*$/gm)].map((m) => m[1].trim()),
  };
}

async function main(argv = []) {
  const spec = emitSpec();

  if (argv.includes('--emit-only')) {
    console.log(spec.source);
    console.error(`\n${spec.stats.tests} test(s), ${spec.stats.derived} derived step(s), ${spec.stats.ungenerated} refused`);
    return 0;
  }

  const bin = resolvePlaywright(argv, process.env);
  if (!bin) {
    console.error('selfhost: no Playwright binary — pass --playwright <bin> or set PLAYWRIGHT_BIN.');
    console.error('  This repo deliberately does not depend on @playwright/test: adding it is a');
    console.error('  packaging change and therefore James\'s call (claude-code-bot#83).');
    return 2;
  }
  if (!fs.existsSync(DIST)) {
    console.error(`selfhost: no built bundle at ${path.relative(process.cwd(), DIST)} — could not look.`);
    console.error('  Build it first: npm --prefix prototypes/behaviour-ast/ui ci && npm --prefix prototypes/behaviour-ast/ui run build');
    return 2;
  }

  const pi = argv.indexOf('--port');
  const port = pi >= 0 ? Number(argv[pi + 1]) : 4407;
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    console.error('selfhost: --port must be an integer between 1 and 65535');
    return 2;
  }

  // The copy the tests are allowed to write to.
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'kit-selfhost-'));
  const corpus = path.join(tmp, 'behaviours');
  fs.mkdirSync(corpus);
  for (const f of fs.readdirSync(CORPUS)) fs.copyFileSync(path.join(CORPUS, f), path.join(corpus, f));
  const specs = path.join(tmp, 'specs');
  fs.mkdirSync(specs);
  fs.writeFileSync(path.join(specs, 'kit-ui.spec.ts'), spec.source);

  // The GENERATED spec's own first line is `import … from '@playwright/test'`,
  // and Kit is right to emit it — but it has to resolve from wherever the spec
  // sits, and a fresh temp directory has no `node_modules` above it. The
  // scratch version of this harness only ever worked because its specs happened
  // to sit beside a `node_modules` symlink borrowed from another repository,
  // which is precisely why its result was unrepeatable. Derive the same link
  // from the binary the caller named, so the two cannot disagree.
  const nm = path.resolve(bin, '..', '..');
  if (path.basename(nm) === 'node_modules') {
    fs.symlinkSync(nm, path.join(tmp, 'node_modules'), 'dir');
  } else {
    console.error(`selfhost: ${bin} is not inside a node_modules/.bin — the generated spec's own`);
    console.error("  `import { test } from '@playwright/test'` has nothing to resolve against.");
    fs.rmSync(tmp, { recursive: true, force: true });
    return 2;
  }

  const server = spawn(process.execPath, [path.join(ROOT, 'ui.js'), '--port', String(port), '--dir', corpus], { stdio: 'ignore' });
  let code = 0;
  try {
    if (!(await waitForServer(port))) {
      console.error(`selfhost: nothing answered /api/projects on ${port} — could not look`);
      return 2;
    }
    console.log(`\n── Kit's own generated tests, against Kit's own UI on :${port} ──\n`);
    console.log(`  ${spec.stats.tests} test(s) generated from behaviours/${SUBJECT}`);
    console.log(`  ${spec.stats.derived} derived step(s), ${spec.stats.contract} prose contract(s), ${spec.stats.ungenerated} refused\n`);

    // --browsers exists because a browser install is not always where Playwright
    // looks by default, and the alternative is asking the caller to export
    // PLAYWRIGHT_BROWSERS_PATH before running — which is exactly the kind of
    // undocumented ambient state that made the first version of this harness
    // unrepeatable.
    const bi = argv.indexOf('--browsers');
    const env = { ...process.env, KIT_SELFHOST_SPECS: specs, KIT_SELFHOST_BASE_URL: `http://127.0.0.1:${port}` };
    if (bi >= 0) env.PLAYWRIGHT_BROWSERS_PATH = argv[bi + 1];

    const r = spawnSync(bin, ['test', '--config', path.join(__dirname, 'playwright.config.js')], {
      cwd: tmp, encoding: 'utf8', env,
    });
    const output = (r.stdout || '') + (r.stderr || '');
    process.stdout.write(output);
    if (r.error || (r.status === null)) {
      console.error(`selfhost: could not run ${bin} — could not look`);
      return 2;
    }

    const got = parseResults(output);
    if (unreadable(got)) {
      console.error('selfhost: could not read a pass/fail tally out of the run — could not look');
      return 2;
    }
    console.log(`\n  ${got.passed} passed, ${got.failed} failed`);

    if (argv.includes('--check')) {
      const want = JSON.parse(fs.readFileSync(EXPECTED, 'utf8'));
      delete want._;
      const now = { tests: spec.stats.tests, derived: spec.stats.derived, ungenerated: spec.stats.ungenerated, passed: got.passed, failed: got.failed, failing: got.failing };
      if (drifted(want, now)) {
        console.error('\nselfhost --check: the run no longer says what the write-up claims.');
        console.error(`  recorded: ${JSON.stringify(want)}`);
        console.error(`  now:      ${JSON.stringify(now)}`);
        code = 1;
      } else {
        console.log('\nselfhost --check: the run still matches docs/pilots/kit-self-hosting.md.');
      }
    }
    if (argv.includes('--record')) {
      fs.writeFileSync(EXPECTED, JSON.stringify({
        _: [
          'Written by `node selfhost/run.js --playwright <bin> --record`. The table in',
          'docs/pilots/kit-self-hosting.md quotes these numbers; --check re-runs the tests',
          'and exits 1 on any difference. Re-record ONLY after deciding the write-up is',
          'wrong — a re-record with no edit to the prose is the drift, not the fix.',
        ],
        tests: spec.stats.tests, derived: spec.stats.derived, ungenerated: spec.stats.ungenerated,
        passed: got.passed, failed: got.failed, failing: got.failing,
      }, null, 2) + '\n');
      console.log(`\nrecorded ${path.relative(process.cwd(), EXPECTED)}`);
    }
  } finally {
    server.kill();
    if (!argv.includes('--keep')) fs.rmSync(tmp, { recursive: true, force: true });
    else console.log(`\n  kept ${tmp}`);
  }
  return code;
}

module.exports = { emitSpec, refusals, resolvePlaywright, parseResults, unreadable, drifted, main, SUBJECT, EXPECTED };

if (require.main === module) main(process.argv.slice(2)).then((c) => process.exit(c));
