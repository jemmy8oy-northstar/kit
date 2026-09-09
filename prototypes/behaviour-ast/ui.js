#!/usr/bin/env node
'use strict';
//
// ui — the API the Kit UI runs on
// ───────────────────────────────
// `docs/design/ui.md` left two decisions with James, acting 2026-09-08. Both
// dates passed in silence, so **his silence is the decision** (the rule the
// decision queue makes, claude-code-bot#59):
//
//   · decision 1 — **it is a local developer tool.** A deployed one needs a
//     GitHub App token in the cluster, an auth story and a clone layer, all of
//     which are platform-and-secrets work that is never mine.
//   · decision 2 — **it writes the corpus file, and never touches git.** He
//     reviews the change as an ordinary working-tree diff and commits it.
//
// The two decisions are not independent, and the code says so: a write path is
// only reachable when the server is bound to a loopback address (rule 2 below).
//
//   node ui.js [--port 4321] [--host 127.0.0.1] [--repos <dir>]
//
//   GET  /api/projects        every corpus, with enough to render a list
//   GET  /api/projects/<app>  project.js's full projection for one app
//   GET  /api/health          { ok: true } — for a live-check, later
//   POST /api/projects/<app>/behaviours/<id>/steps   { step }
//   POST /api/projects/<app>/behaviours             { id, title, actor, steps }
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
// ── 2. It can write, but only from loopback ─────────────────────────────────
// Decision 2 landed, so the 405-for-every-verb rule this file used to carry is
// gone. What replaces it is narrower and load-bearing: **a write handler is
// unreachable unless `--host` is a loopback address.** Decision 1 said local
// tool; combining "reachable from the network" with "edits files in the working
// tree" is an unauthenticated remote write, and it would arrive not as a
// decision but as someone passing `--host 0.0.0.0` to see the UI from a phone.
// The read surface still binds loopback by default (rule 1); this rule is what
// stops the two defaults being undone one flag at a time.
//
// ── 4. A write carrying a cross-origin `Origin` is refused ──────────────────
// Rule 2 answers "who can reach the socket". It does NOT answer "who caused the
// request", and on loopback those differ: a hostile page open in the developer's
// own browser reaches the socket *as the developer*. CORS does not close this —
// a `text/plain` POST is a simple request, sent with no preflight, and refusing
// the attacker the *response* does not un-write the file. Measured with a probe,
// not reasoned about. So writes refuse any request carrying an `Origin` this
// server would not itself serve; no Origin means a non-browser caller and is
// fine.
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
const writer = require('./writer.js');

const BEH_DIR = path.join(__dirname, 'behaviours');
const DEFAULT_PORT = 4321;
const DEFAULT_HOST = '127.0.0.1';

// A request body big enough to be a mistake or an attack, and far bigger than
// any behaviour anyone will type. Enforced while READING, not after: a limit
// checked on a fully-buffered body is not a limit.
const MAX_BODY = 64 * 1024;

/**
 * Is this host a loopback address?
 *
 * An allowlist of exact spellings, not a pattern. `127.0.0.1` is the one anybody
 * types, but `127.x.y.z` is all loopback, so the range is matched — and nothing
 * else is. A substring or `startsWith` test here would accept `127.0.0.1.evil`
 * and a regexp without anchors would accept anything containing it.
 */
function isLoopback(host) {
  if (host === 'localhost' || host === '::1' || host === '[::1]') return true;
  return /^127\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/.test(String(host))
    && String(host).split('.').slice(1).every((n) => Number(n) >= 0 && Number(n) <= 255);
}

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
function route(method, pathname, opts = {}, body = null, origin = null) {
  const json = (status, b) => ({ status, contentType: 'application/json', body: b });

  if (method === 'POST') return write(pathname, opts, body, json, origin);

  if (method !== 'GET') {
    return json(405, {
      error: 'method-not-allowed',
      reason: `this server serves GET and POST; ${method} reaches no handler`,
      allow: 'GET, POST',
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

/**
 * The write half. Everything decision 2 turned on lives here.
 *
 * Separated from `route` for the same reason the router is separated from the
 * server: the rules below are the ones worth being able to state and test one at
 * a time. Note the ORDER — the loopback refusal comes before the body is looked
 * at, so a remote caller cannot even learn whether an app or a behaviour exists.
 */
function write(pathname, opts, body, json, origin = null) {
  // Rule 4, and it is checked FIRST because it is the only one that defends
  // against a caller who is not the developer.
  //
  // The CORS rule below (`cors()`) was written believing it closed the
  // "hostile page open in the developer's browser" vector. It does not, and a
  // probe proved it rather than a reading of it: a POST with
  // `content-type: text/plain` is a CORS **simple request**, so the browser
  // sends it with NO preflight and only withholds the *response*. Withholding
  // the response does not un-write the file — the corpus was already edited.
  // `<form method=POST enctype="text/plain">` does the same with no JS at all.
  //
  // So the check that actually holds is the Origin header, which browsers
  // attach to every cross-origin POST and which page script cannot forge. A
  // request with no Origin at all is a non-browser caller — curl, the CLI, the
  // suite — and is allowed; that is the normal case, not a hole.
  if (origin !== null && origin !== undefined) {
    let host = null;
    try { host = new URL(origin).hostname; } catch { host = null; }
    if (!host || !isLoopback(host)) {
      return json(403, {
        error: 'cross-origin-write',
        reason: `a write carrying Origin '${origin}' came from a page this server does not serve. `
          + 'Kit\'s UI is a local developer tool; a page on another origin editing your working '
          + 'tree is CSRF, not a feature.',
      });
    }
  }

  // Rule 2. Decision 1 said local tool; a write path reachable from the network
  // is the deployed option arriving through a flag rather than through him.
  if (!isLoopback(opts.host ?? DEFAULT_HOST)) {
    return json(403, {
      error: 'not-loopback',
      reason: `writes are served only to loopback; this server is bound to ${opts.host}. `
        + 'docs/design/ui.md decision 1: Kit\'s UI is a local developer tool, and an '
        + 'unauthenticated write API on a routable interface is not that.',
    });
  }

  const m = /^\/api\/projects\/([^/]+)\/behaviours(?:\/([^/]+)\/steps)?$/.exec(pathname);
  if (!m) return json(404, { error: 'no-such-route', reason: `nothing accepts a POST at ${pathname}` });

  let app;
  try {
    app = decodeURIComponent(m[1]);
  } catch {
    return json(400, { error: 'bad-request', reason: 'the app name is not valid percent-encoding' });
  }

  // Rule 3, unchanged for writes: the name is looked UP, never joined. `corpusPath`
  // resolves it against the directory listing for exactly this reason, so a
  // traversal has nothing to traverse with.
  const file = writer.corpusPath(app, opts.dir || BEH_DIR);
  if (!file) {
    return json(404, { error: 'no-such-project', reason: `no corpus named '${app}'`, known: corpora(opts.dir) });
  }

  if (!body || typeof body !== 'object') {
    return json(400, { error: 'bad-request', reason: 'the body must be a JSON object' });
  }

  const text = fs.readFileSync(file, 'utf8');
  const id = m[2] ? decodeURIComponent(m[2]) : body.id;
  const result = m[2]
    ? writer.addStep(text, id, body.step)
    : writer.addBehaviour(text, id, body.title, { actor: body.actor, steps: body.steps, source: body.source, ref: body.ref });

  if (!result.ok) {
    // 409, not 500. Every refusal in writer.js is a statement about the request
    // — the behaviour is not there, the step will not parse, the edit would
    // change a neighbour. A 500 would say the server broke, and send whoever
    // reads it looking in the wrong place ([[the-message-names-the-layer]]).
    return json(409, { error: result.error, reason: result.reason, known: result.known });
  }

  writer.commitToDisk(file, result);

  // The response says what was NOT done, every time. Decision 2's whole content
  // is the second half of this sentence, and a caller that assumes a commit
  // finds out here rather than when the branch turns out to be empty.
  return json(200, {
    ok: true,
    app,
    behaviour: id,
    file: path.relative(process.cwd(), file),
    committed: false,
    note: 'written to the working tree. Kit does not run git — review the diff and commit it yourself.',
  });
}

/**
 * CORS headers, as a function of the request's Origin.
 *
 * The old rule here was `access-control-allow-origin: *`, and it was defensible
 * while the server could not write: it bought a Vite dev server on another port
 * and gave away nothing but a read of local corpora. It is not defensible now.
 *
 * ⚠️ **This function does NOT stop a cross-origin write, and an earlier version
 * of this comment claimed it did.** Withholding the CORS header stops the
 * attacking page READING the reply; it does not stop the request arriving, and
 * for a write the request IS the damage. A `content-type: text/plain` POST is a
 * CORS simple request that never triggers a preflight at all. The rule that
 * actually defends the write is the Origin check at the top of `write()`; this
 * one governs who may read a *reply*, which is all it ever did.
 *
 * A loopback origin is reflected, and every other origin gets no CORS header.
 * Reflected rather than `*` because `*` cannot be combined with the preflight a
 * JSON write triggers, and an allowlist naming ports breaks the moment Vite
 * picks a different one.
 */
function cors(origin) {
  if (!origin) return {};
  let host;
  try {
    host = new URL(origin).hostname;
  } catch {
    return {};
  }
  if (!isLoopback(host)) return {};
  return {
    'access-control-allow-origin': origin,
    'access-control-allow-methods': 'GET, POST, OPTIONS',
    'access-control-allow-headers': 'content-type',
    // Two different origins get two different answers, and a cache that forgets
    // that serves one of them the other's headers.
    vary: 'Origin',
  };
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

    const send = (result) => {
      res.writeHead(result.status, { 'content-type': result.contentType, ...cors(req.headers.origin) });
      res.end(JSON.stringify(result.body));
    };

    // A preflight is answered by the same allowlist that answers the request, so
    // the two can never disagree — an ACAO that permits an origin a preflight
    // refuses is a bug that only shows up in a browser.
    if (req.method === 'OPTIONS') {
      res.writeHead(204, cors(req.headers.origin));
      return res.end();
    }

    if (req.method !== 'POST') return send(route(req.method, pathname, opts));

    let size = 0;
    const chunks = [];
    req.on('data', (c) => {
      size += c.length;
      // Enforced while reading: past the limit nothing more is BUFFERED, so a
      // large body cannot grow this process's memory. The remaining bytes are
      // still drained rather than the socket destroyed, because destroying the
      // request aborts the response with it and the caller gets a hang-up
      // instead of the sentence explaining what happened.
      if (size > MAX_BODY) { chunks.length = 0; return; }
      chunks.push(c);
    });
    req.on('end', () => {
      if (size > MAX_BODY) return send({ status: 413, contentType: 'application/json', body: { error: 'too-large', reason: `a request body over ${MAX_BODY} bytes is not a behaviour` } });
      let body;
      try {
        body = JSON.parse(Buffer.concat(chunks).toString('utf8') || 'null');
      } catch (e) {
        return send({ status: 400, contentType: 'application/json', body: { error: 'bad-json', reason: e.message } });
      }
      send(route('POST', pathname, opts, body, req.headers.origin ?? null));
    });
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
  console.log(isLoopback(opts.host)
    ? '  writes: ON — edits land in the working tree and are NEVER committed'
    : `  writes: OFF — ${opts.host} is not loopback (docs/design/ui.md decision 1)`);
  return 0;
}

module.exports = { route, write, serve, cors, isLoopback, corpora, repoFor, summary, parseArgs, main, MAX_BODY };

if (require.main === module) {
  main(process.argv.slice(2)).then((code) => {
    // Only exit on failure — on success the server is holding the loop open.
    if (code !== 0) process.exit(code);
  });
}
