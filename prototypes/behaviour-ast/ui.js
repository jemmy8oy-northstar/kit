#!/usr/bin/env node
'use strict';
//
// ui — the read API the Kit UI runs on
// ────────────────────────────────────
// `docs/design/ui.md` leaves two decisions with James (where the UI runs, and
// whether it writes the corpus), acting 2026-09-08. This file is the half that
// is true under EVERY option of both ([[option-invariant-half]]):
//
//   · decision 1, local vs deployed — the transport is the same server either
//     way; only where it is started and what fronts it differ;
//   · decision 2, propose vs write — every option needs exactly this read
//     surface first, and none of them changes its shape.
//
//   node ui.js [--port 4321] [--host 127.0.0.1] [--repos <dir>]
//
//   GET /api/projects        every corpus, with enough to render a list
//   GET /api/projects/<app>  project.js's full projection for one app
//   GET /api/health          { ok: true } — for a live-check, later
//
// Adds no analysis. Like project.js, if you find yourself computing something
// here it belongs in kit.js, where the suite and the mutation harness can see
// it. This file's own job is the four rules below, and each has a test and a
// mutant.
//
// ── 1. It listens on the loopback interface, and that is a decision ──────────
// A default of 0.0.0.0 would publish every corpus in the working tree, and the
// absolute paths of every repo beside it, to anything that can reach the host.
// This is a developer's instrument; `--host` exists for the deployed option,
// where something else is doing the authenticating.
//
// ── 2. It cannot write. Not "does not" — cannot ──────────────────────────────
// Decision 2 is open, so the honest state of the code is that no verb other
// than GET reaches a handler at all. A doc saying "the UI does not write yet"
// and a server that returns 405 for every write are different assurances, and
// only the second survives someone adding a fetch() in a hurry.
//
// ── 3. An app name is matched against the corpus listing, never joined ───────
// `/api/projects/../../etc/passwd` must be a 404. The name is looked UP in the
// set of corpora that exist; it is never used to build a path.
//
// ── 4. Unavailable is never zero ────────────────────────────────────────────
// project.js's rule, carried through the list endpoint. A UI that cannot tell
// "no mapping exists" from "nothing is covered" renders the second, and the
// second is an alarm ([[empty-means-two-things]]).

const fs = require('fs');
const http = require('http');
const path = require('path');
const proj = require('./project.js');

const BEH_DIR = path.join(__dirname, 'behaviours');
const DEFAULT_PORT = 4321;
const DEFAULT_HOST = '127.0.0.1';

/** Every corpus in behaviours/, by app name. The only source of valid names. */
function corpora(dir = BEH_DIR) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir)
    .filter((f) => f.endsWith('.beh'))
    .map((f) => f.slice(0, -'.beh'.length))
    .sort();
}

/**
 * Where an app's repository is, for the coverage read.
 *
 * Returns null rather than a guess when it is not there. A wrong path and a
 * missing one produce the same "0 covered" if you let them, and rule 4 exists
 * to stop exactly that.
 */
function repoFor(app, reposDir) {
  if (!reposDir) return null;
  const candidate = path.join(reposDir, app);
  return fs.existsSync(candidate) ? candidate : null;
}

/**
 * One row of the list endpoint. Deliberately NOT the full projection: the list
 * is rendered before anything is selected, and projecting five corpora to render
 * five list rows would read every test file in every repo to answer a question
 * the list does not ask.
 */
function summary(app, opts) {
  // ⚠️ `project()` signals could-not-look by RETURNING `{ fatal }`, it does not
  // throw. Handled explicitly: an earlier draft here relied on the TypeError
  // that reading `.behaviours` off that object happens to raise, which passed
  // the test for a reason unrelated to the rule it was testing.
  const p = projectOf(app, opts);
  if (p.fatal) {
    // A corpus that will not parse is a real state of the world and the list
    // must still render the others. It is reported AS an error, not as an app
    // with zero behaviours.
    return { app, error: p.fatal };
  }

  const cov = p.coverage;
  return {
    app,
    corpus: p.corpus,
    notReal: p.notReal,
    // Carried for the same reason as `notReal` and killed by the same mutant: a
    // marker the API drops never reaches the UI, so the list would show a trial
    // corpus and the project it was written against as two equal projects.
    duplicateOf: p.duplicateOf,
    behaviours: p.behaviours.length,
    conflicts: p.conflicts.length,
    // Rule 4. `covered` is null — not 0 — when there was nothing to read.
    coverage: cov.available
      ? { available: true, covered: cov.covered.length, uncovered: cov.uncovered.length }
      : { available: false, covered: null, uncovered: null, reason: cov.reason },
    unreviewed: p.adjudication.unreviewed.length,
  };
}

/** project.js names its corpus directory `behDir`; this is the only place that spelling leaks. */
function projectOf(app, opts) {
  try {
    return proj.project(app, { behDir: opts.dir || BEH_DIR, repo: repoFor(app, opts.repos) });
  } catch (e) {
    return { fatal: e.message };
  }
}

/**
 * The router, as a pure function of method and path.
 *
 * Separated from the server on purpose so the rules above are testable without
 * a socket — but NOT instead of testing the socket. `kit.test.js` drives a real
 * listening server for the same routes, because a handler that returns the right
 * object and a server that delivers it are different claims
 * ([[test-the-delivery-not-just-the-value]]).
 */
function route(method, pathname, opts = {}) {
  const json = (status, body) => ({ status, contentType: 'application/json', body });

  // Rule 2. Checked before anything is parsed, so there is no path at all from
  // a write verb to a handler.
  if (method !== 'GET') {
    return json(405, {
      error: 'read-only',
      reason: 'Whether the UI writes the corpus is an open decision (docs/design/ui.md, decision 2). '
        + 'Until it is made, this server has no write path.',
      allow: 'GET',
    });
  }

  if (pathname === '/api/health') {
    return json(200, { ok: true });
  }

  if (pathname === '/api/projects') {
    return json(200, { projects: corpora(opts.dir).map((app) => summary(app, opts)) });
  }

  const match = /^\/api\/projects\/([^/]+)$/.exec(pathname);
  if (match) {
    // Rule 3. decodeURIComponent first, so an encoded traversal is compared as
    // the string it decodes to rather than sneaking past as %2e%2e.
    let app;
    try {
      app = decodeURIComponent(match[1]);
    } catch {
      return json(400, { error: 'bad-request', reason: 'the app name is not valid percent-encoding' });
    }

    const known = corpora(opts.dir);
    if (!known.includes(app)) {
      // The name is never joined to a path — it is looked up in this list, so
      // there is nothing to traverse with.
      return json(404, { error: 'no-such-project', reason: `no corpus named '${app}'`, known });
    }

    const p = projectOf(app, opts);

    // A reason, never a stack. The stack names paths inside the pod, and this
    // server is a candidate for the deployed option in decision 1.
    return p.fatal
      ? json(500, { error: 'projection-failed', reason: p.fatal })
      : json(200, p);
  }

  return json(404, { error: 'no-such-route', reason: `nothing is served at ${pathname}` });
}

function serve(opts = {}) {
  // `?? ` and not `||`: port 0 is a REQUEST for an ephemeral port, and `||`
  // silently turns it into 4321 — which the suite met as two tests fighting
  // over one port rather than as the bug it is.
  const port = opts.port ?? DEFAULT_PORT;
  const host = opts.host ?? DEFAULT_HOST;

  const server = http.createServer((req, res) => {
    // `new URL` needs a base; the host header is untrusted input and is only
    // ever used to satisfy the parser, never read back out.
    const { pathname } = new URL(req.url, 'http://localhost');
    const result = route(req.method, pathname, opts);
    res.writeHead(result.status, {
      'content-type': result.contentType,
      // A read API for local tooling. No credentials are involved, and a
      // permissive CORS header on a loopback-bound read-only server buys a
      // Vite dev server on another port at no cost worth naming.
      'access-control-allow-origin': '*',
    });
    res.end(JSON.stringify(result.body));
  });

  return new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(port, host, () => resolve(server));
  });
}

function parseArgs(argv) {
  const opts = { dir: BEH_DIR, repos: null, port: DEFAULT_PORT, host: DEFAULT_HOST };
  for (let i = 0; i < argv.length; i++) {
    const next = argv[i + 1];
    if (argv[i] === '--port') { opts.port = Number(next); i++; }
    else if (argv[i] === '--host') { opts.host = next; i++; }
    else if (argv[i] === '--repos') { opts.repos = next; i++; }
    else if (argv[i] === '--dir') { opts.dir = next; i++; }
  }
  return opts;
}

async function main(argv) {
  const opts = parseArgs(argv);

  if (!Number.isInteger(opts.port) || opts.port < 1 || opts.port > 65535) {
    console.error('ui: --port must be an integer between 1 and 65535');
    return 2;
  }

  const apps = corpora(opts.dir);
  if (apps.length === 0) {
    // Exit 2, "could not look" — project.js's convention. A UI serving an empty
    // list looks identical to a Kit with nothing to say.
    console.error(`ui: no .beh corpora in ${opts.dir} — nothing to serve`);
    return 2;
  }

  try {
    await serve(opts);
  } catch (e) {
    console.error(`ui: could not listen on ${opts.host}:${opts.port} — ${e.message}`);
    return 2;
  }

  console.log(`kit ui  http://${opts.host}:${opts.port}`);
  console.log(`  ${apps.length} corpora: ${apps.join(', ')}`);
  console.log(`  repos: ${opts.repos || '(none — coverage will report unavailable, not zero)'}`);
  console.log('  read-only: decision 2 in docs/design/ui.md is open');
  return 0;
}

module.exports = { route, serve, corpora, repoFor, summary, parseArgs, main };

if (require.main === module) {
  main(process.argv.slice(2)).then((code) => {
    // Only exit on failure — on success the server is holding the loop open.
    if (code !== 0) process.exit(code);
  });
}
