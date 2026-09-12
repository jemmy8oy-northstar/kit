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
//   node ui.js [--port 4321] [--host 127.0.0.1] [--repos <dir>] [--bindings <file>]
//              [--git [--git-remote origin] [--git-branch <name>]]
//
//   GET  /api/projects        every corpus, with enough to render a list
//   GET  /api/projects/<app>  project.js's full projection for one app
//   GET  /api/health          { ok: true } — for a live-check, later
//   POST /api/projects/<app>/behaviours/<id>/steps   { step }
//   POST /api/projects/<app>/behaviours/<id>/review  { state, note }
//   POST /api/projects/<app>/behaviours             { id, title, actor, steps }
//   POST /api/projects/<app>/bindings               { noun, binding }
//   GET  everything else      the built UI out of `ui/dist` (rules 5–7)
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
//
// ── 5. The bundle is served by LOOKUP, never by joining ─────────────────────
// This file used to serve no HTML at all, and `ui/README.md` gave the reason:
// serving files means joining a request path to a directory, and rule 3 exists
// to stop exactly that. The reason has not stopped being true — so the same
// answer is used. `/assets/<name>` matches `<name>` against `readdirSync` of
// `ui/dist/assets`; a root path matches against `readdirSync` of `ui/dist`.
// Nothing else under `dist` is reachable and **no request path is ever joined
// to a directory**. The fallback below serves one constant file.
//
// Why it matters that the API alone was not enough: `npm run dev` beside
// `node ui.js` is fine for me and wrong for a tool James opens. Two processes
// and two ports is not "manage my projects by the spec" (claude-code-bot#89).
//
// ── 6. A path that NAMES A FILE and is not there is a 404, never the shell ──
// The SPA fallback is what makes client-side routing work, and it is also how
// "every unmatched path answers 200 with the app shell" becomes a check that
// cannot fail — a status code then proves an app is up when the thing asked
// for does not exist ([[green-over-the-clients-question]]; the portfolio did
// this for the life of the site). So the fallback is deliberately narrow: a
// path with a file extension that is not in the listing is a **404**, and
// `/api/...` never falls back at all — it stays JSON, so a UI whose fetch has
// gone wrong is told so rather than handed HTML that will not parse.
//
// ── 7. A bundle that was never built is reported as that ────────────────────
// Rule 4's shape, one layer out. `ui/dist` is gitignored, so a fresh clone has
// no bundle and the honest answer is neither a 404 (which reads as "wrong URL")
// nor a blank page (which reads as "broken"). It is a 503 naming the command
// that builds it. The API keeps serving throughout: the read model working and
// the bundle being absent are two different states.

const fs = require('fs');
const http = require('http');
const path = require('path');
const proj = require('./project.js');
const writer = require('./writer.js');
const gitStore = require('./git-store.js');
const auth = require('./auth.js');

const BEH_DIR = path.join(__dirname, 'behaviours');
const DIST_DIR = path.join(__dirname, 'ui', 'dist');
const DEFAULT_PORT = 4321;
const DEFAULT_HOST = '127.0.0.1';

// Printed by rule 7's 503 and by `main`, so the sentence a person reads is the
// sentence a person can paste. Relative to the repository root, which is where
// every other command in the README is run from.
const BUILD_CMD = 'npm --prefix prototypes/behaviour-ast/ui ci'
  + ' && npm --prefix prototypes/behaviour-ast/ui run build';

// Only the extensions Vite actually emits, plus the fonts and images a UI grows
// into. An unknown extension gets `application/octet-stream`: the file is in the
// bundle listing or it is not served at all (rule 5), so an unmapped type is a
// download rather than a hole.
const CONTENT_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.txt': 'text/plain; charset=utf-8',
};

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
    // `bindingsFile` passed from the SAME `opts` the write path reads, so the
    // two can never point at different files. They could when `--bindings`
    // first shipped: the write honoured it, this read did not, and a bind
    // succeeded while the page went on showing the refusal.
    return proj.project(app, {
      behDir: opts.dir || BEH_DIR,
      bindingsFile: opts.bindings || null,
      repo: repoFor(app, opts.repos),
    });
  } catch (e) {
    return { fatal: e.message };
  }
}

/**
 * The names of the files directly inside a directory — the only source of
 * servable names (rule 5), exactly as `corpora()` is the only source of valid
 * app names.
 *
 * Directories are filtered out, so `/assets/<a-subdirectory>` cannot resolve to
 * something `fs.readFileSync` then fails on with an EISDIR the caller reads as a
 * 500. A missing directory is an empty list, not a throw: rule 7 reports the
 * absent bundle once, at the top, rather than every helper having an opinion.
 */
function filesIn(dir) {
  try {
    return fs.readdirSync(dir, { withFileTypes: true })
      .filter((e) => e.isFile())
      .map((e) => e.name);
  } catch {
    return [];
  }
}

/** The declared type for a file name, or a download. See CONTENT_TYPES. */
function contentTypeFor(name) {
  return CONTENT_TYPES[path.extname(name).toLowerCase()] || 'application/octet-stream';
}

/**
 * Rules 5–7: the built UI, served out of `ui/dist`.
 *
 * Returns the same shape the JSON routes return, plus `raw` — the bytes to send
 * — so `serve()` has one send path and cannot serve a file as JSON by omission.
 * `readFileSync` and not a stream on purpose: this is a local tool serving a
 * bundle of a few hundred KB to one browser, and a stream here would buy nothing
 * but a second error path to get wrong.
 */
function bundle(pathname, distDir = DIST_DIR) {
  const html = (status, body, cache) => ({
    status,
    contentType: 'text/html; charset=utf-8',
    cacheControl: cache,
    raw: body,
  });

  const index = path.join(distDir, 'index.html');

  // Rule 7, checked first: with no bundle every answer below would be a 404,
  // and "you typed the wrong URL" is the wrong sentence for "nobody has built
  // it yet". The API is unaffected — this branch is only reached for non-/api
  // paths.
  if (!fs.existsSync(index)) {
    return html(503, '<!doctype html><html><head><meta charset="utf-8">'
      + '<title>Kit UI — not built</title></head><body>'
      + '<h1>The Kit UI has not been built</h1>'
      // Relative to cwd, like the write response's `file`. Not cosmetic: this
      // page is screenshotted into public PRs, and an absolute path names the
      // filesystem of whatever host is running it. It also reads better — the
      // path a person can act on is the one relative to where they typed the
      // command.
      + `<p>The API is running and answering. The bundle is not in <code>${path.relative(process.cwd(), distDir) || distDir}</code>,`
      + ' which is gitignored, so a fresh clone has to build it once:</p>'
      + `<pre>${BUILD_CMD}</pre>`
      + '<p>Then reload this page. While iterating on the UI itself, run'
      + ' <code>npm --prefix prototypes/behaviour-ast/ui run dev</code> instead —'
      + ' it proxies <code>/api</code> here and reloads on save.</p>'
      + '</body></html>', 'no-store');
  }

  // Decoded once and then only ever compared, never joined — the same order as
  // rule 3, so an encoded traversal is matched as the string it decodes to.
  let p;
  try {
    p = decodeURIComponent(pathname);
  } catch {
    return { status: 400, contentType: 'application/json', body: { error: 'bad-request', reason: 'the path is not valid percent-encoding' } };
  }

  // Belt to rule 5's braces. The lookup below is what makes traversal
  // impossible — a name either is in the listing or is not served — but a
  // reader should not have to reconstruct that argument to believe it, and a
  // future edit that reaches for `path.join` finds this refusal already here.
  if (p.split('/').includes('..')) {
    return { status: 404, contentType: 'application/json', body: { error: 'no-such-file', reason: 'a path segment of `..` names nothing in the bundle' } };
  }

  const send = (dir, name, cache) => ({
    status: 200,
    contentType: contentTypeFor(name),
    cacheControl: cache,
    raw: fs.readFileSync(path.join(dir, name)),
  });

  const asset = /^\/assets\/([^/]+)$/.exec(p);
  if (asset) {
    // Vite content-hashes every name in here, so a name that resolves can be
    // cached forever and a name that does not is gone for good — a 404, never
    // the shell (rule 6). A stale index.html pointing at a deleted hash is the
    // one failure this pair has to make legible.
    return filesIn(path.join(distDir, 'assets')).includes(asset[1])
      ? send(path.join(distDir, 'assets'), asset[1], 'public, max-age=31536000, immutable')
      : { status: 404, contentType: 'application/json', body: { error: 'no-such-asset', reason: `no bundled asset named '${asset[1]}' — the page asking for it was built against a different bundle` } };
  }

  const root = /^\/([^/]+)$/.exec(p);
  if (root && filesIn(distDir).includes(root[1])) {
    // favicon.svg and friends. Unhashed, so never cached: rebuilding the bundle
    // has to be able to change them.
    return send(distDir, root[1], 'no-store');
  }

  // Rule 6. Anything that names a file and got this far is not in the bundle.
  if (path.extname(p)) {
    return { status: 404, contentType: 'application/json', body: { error: 'no-such-file', reason: `nothing named ${p} is in the bundle` } };
  }

  // The shell. `no-store` because it is the only unhashed file that names the
  // hashed ones: a cached copy survives a rebuild and asks for assets that were
  // deleted, which presents as a blank page with a console nobody is reading.
  return html(200, fs.readFileSync(index), 'no-store');
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
function route(method, pathname, opts = {}, body = null, origin = null, cookie = null) {
  const json = (status, b) => ({ status, contentType: 'application/json', body: b });

  if (method === 'POST') return write(pathname, opts, body, json, origin, cookie);

  if (method !== 'GET') {
    return json(405, {
      error: 'method-not-allowed',
      reason: `this server serves GET and POST; ${method} reaches no handler`,
      allow: 'GET, POST',
    });
  }

  // Rule 6's second half. Everything under /api answers JSON to the end,
  // including its 404 — falling back to the shell here would hand a broken
  // fetch a page of HTML and report it as a parse error three layers away
  // ([[the-message-names-the-layer]]).
  if (!/^\/api(\/|$)/.test(pathname)) {
    return bundle(pathname, opts.dist ?? DIST_DIR);
  }

  if (pathname === '/api/health') {
    return json(200, { ok: true });
  }

  // ── who am I? (kit#46) ────────────────────────────────────────────────────
  // The page needs this BEFORE it renders anything, because the two states it
  // has to tell apart are "you must sign in" and "there is no sign-in here" —
  // and a local Kit is permanently the second one. Inferring it from a 401 on
  // the first write would mean the user discovers the lock by losing an edit.
  //
  // Deliberately readable without a session: it reveals only whether a lock
  // exists, which anyone can determine anyway by attempting one write, and
  // hiding it would make the page unable to draw itself.
  if (pathname === '/api/session') {
    return json(200, {
      required: auth.enabled(opts),
      signedIn: !auth.enabled(opts) || auth.signedIn(opts.sessions, cookie),
    });
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
function write(pathname, opts, body, json, origin = null, cookie = null) {
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
  //
  // 🔴 Deployed, loopback is no longer the whole answer: the page Kit serves is
  // at `https://<public origin>`, so that origin must be accepted too or every
  // write from the browser he actually uses is refused. `--public-origin` names
  // it EXACTLY — scheme, host and port compared as a whole string, not a
  // hostname suffix. A suffix test is how `balenthiran.co.uk.evil.com` passes
  // for `balenthiran.co.uk`, and it is the classic way this check is written
  // wrong.
  if (origin !== null && origin !== undefined) {
    if (!originAllowed(origin, opts)) {
      return json(403, {
        error: 'cross-origin-write',
        reason: `a write carrying Origin '${origin}' came from a page this server does not serve. `
          + 'Kit\'s UI is a local developer tool; a page on another origin editing your working '
          + 'tree is CSRF, not a feature.',
      });
    }
  }

  // ── sign in / sign out (kit#46) ──────────────────────────────────────────
  // Below the Origin check and above the lock, which is the only correct place
  // for it. Above the Origin check it would let any page on the internet run a
  // password-guessing loop through someone's browser; below the lock it would
  // be a key locked inside the box it opens.
  if (pathname === '/api/session' || pathname === '/api/session/end') {
    return session(pathname, opts, body, json, cookie);
  }

  // ── the lock (kit#46) ────────────────────────────────────────────────────
  // Rule 2 used to be the only thing here, and it asks the wrong question: it
  // tests `opts.host`, the address this process was STARTED on, so it is a
  // startup switch rather than a check on the caller. That is why a deployed
  // Kit is read-only today.
  //
  // A configured password replaces it with a question about the caller. The
  // two branches are exclusive on purpose:
  //
  //   password set   → the session decides, and the bind address is irrelevant.
  //                    This is the deployment.
  //   password unset → rule 2 exactly as it was, untouched. This is his laptop,
  //                    and it must not change because nobody asked it to.
  //
  // ⚠️ Note what is NOT here: there is no branch that allows a write because
  // the host is loopback WHILE a password is set. A Kit with a password in
  // front of it is a Kit whose writes are locked, including through a proxy
  // that makes the caller look local — which is the exact hole kit#44 measured
  // in the old gate.
  if (auth.enabled(opts)) {
    if (!auth.signedIn(opts.sessions, cookie)) {
      return json(401, {
        error: 'not-signed-in',
        reason: 'this Kit is password-protected and this request carries no valid session. '
          + 'POST the password to /api/session first.',
      });
    }
  } else if (!isLoopback(opts.host ?? DEFAULT_HOST)) {
    return json(403, {
      error: 'not-loopback',
      reason: `writes are served only to loopback; this server is bound to ${opts.host}. `
        + 'docs/design/ui.md decision 1: Kit\'s UI is a local developer tool, and an '
        + 'unauthenticated write API on a routable interface is not that.',
    });
  }

  // ── the one write that is not scoped to this corpus ──────────────────────
  // `/api/projects/<app>/bindings` writes `bindings.json`, which is ONE FLAT
  // MAP over every corpus — so unlike the three routes below, the file this
  // touches is not the app's own.
  //
  // It is still routed under the app, and that is not an inconsistency. The app
  // is what `sharedWith` excludes: "which OTHER corpora reference this noun"
  // has no answer without knowing which one you are in. Routing it at
  // `/api/bindings` would have to take the app in the body to say the same
  // thing, and would read as if the write were global in a way the three below
  // are not — which is true of the FILE and false of the request.
  const bm = /^\/api\/projects\/([^/]+)\/bindings$/.exec(pathname);
  if (bm) return postBinding(bm, body, opts, json);

  // Three writes, one shape: `/behaviours` creates, `/behaviours/<id>/steps`
  // appends, `/behaviours/<id>/review` adjudicates. The verb is the last
  // segment rather than a field in the body, so the route a request took is
  // visible in a log and in the contract fixture — a body field would make all
  // three the same line in both.
  const m = /^\/api\/projects\/([^/]+)\/behaviours(?:\/([^/]+)\/(steps|review))?$/.exec(pathname);
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

  let result;
  if (m[3] === 'review') {
    // His #68 mechanism, finally reachable from the browser it was designed
    // for: an inference is included but marked unreviewed, and the count of
    // un-adjudicated ones is meant to be VISIBLE so that skipping the step is a
    // number someone can see. It has been visible and un-actable since the UI
    // existed — 26 inferences across two corpora sit at `unreviewed` because
    // approving one meant opening the file by hand.
    result = writer.setReview(text, id, body.state, body.note);
  } else if (m[2]) {
    result = writer.addStep(text, id, body.step);
  } else {
    result = writer.addBehaviour(text, id, body.title, { actor: body.actor, steps: body.steps, source: body.source, ref: body.ref });
  }

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
  const what = m[3] === 'review' ? `adjudicate ${id}` : m[2] ? `add a step to ${id}` : `add ${id}`;
  return json(200, {
    ok: true,
    app,
    behaviour: id,
    file: path.relative(process.cwd(), file),
    ...gitOutcome(file, opts, what, app),
  });
}

/**
 * What git did with this edit, as fields a caller can act on.
 *
 * One helper for both write paths so they cannot drift into describing the same
 * outcome two different ways — the corpus write and the bindings write are
 * equally lost if a push fails, and a caller should not have to learn two
 * vocabularies to find that out.
 *
 * 🔴 `ok: true` still means the edit is ON DISK, which it always is by the time
 * we get here. `pushed` is the field that says whether it reached anywhere that
 * survives the pod restarting, and `warning` exists so a UI does not have to
 * infer trouble from the absence of something.
 */
function gitOutcome(file, opts, summary, app) {
  const g = gitStore.writeBack(file, { ...(opts.git || {}), summary, app });

  // Off is the local default and decision 2 unchanged: say exactly what the
  // response has always said, so nothing that reads this today breaks.
  if (!(opts.git && opts.git.enabled)) {
    return {
      committed: false,
      note: 'written to the working tree. Kit does not run git — review the diff and commit it yourself.',
    };
  }

  if (g.pushed) {
    return {
      committed: true,
      pushed: true,
      commit: g.commit,
      branch: g.branch,
      note: `committed as ${g.commit} and pushed to ${g.branch}.`,
    };
  }

  // Everything else is switched-on-but-not-published. Benign (nothing changed)
  // or serious (the push was rejected), and the caller is told which by the
  // reason git itself gave, never by a summary of it.
  return {
    committed: g.committed,
    pushed: false,
    commit: g.commit,
    branch: g.branch,
    note: g.reason,
    warning: g.committed
      ? `this edit is committed locally but did NOT reach ${(opts.git && opts.git.remote) || 'origin'}: ${g.reason}`
      : undefined,
  };
}

/**
 * `POST /api/projects/<app>/bindings` — bind one noun.
 *
 * Split out rather than folded into the chain above because it is the one write
 * whose target file is not derived from the app, and inlining it would put a
 * second `readFileSync` of a different file inside a function whose next three
 * branches all share one. Every gate it needs — loopback, Origin, method — has
 * already run in the caller; this is only what is different.
 *
 * Body: `{ "noun": "button:AddHabit", "binding": { "role": "button", "name": "Add habit" } }`
 */
function postBinding(match, body, opts, json) {
  let app;
  try {
    app = decodeURIComponent(match[1]);
  } catch {
    return json(400, { error: 'bad-request', reason: 'the app name is not valid percent-encoding' });
  }
  const dir = opts.dir || BEH_DIR;
  // The app must EXIST even though its corpus is not what gets written. The
  // name is looked up in the directory listing, never joined — rule 3 — and an
  // unknown app here means `sharedWith` would silently compare against nothing
  // and report no collisions, which is the quiet wrong answer rather than a
  // loud one.
  if (!writer.corpusPath(app, dir)) {
    return json(404, { error: 'no-such-project', reason: `no corpus named '${app}'`, known: corpora(dir) });
  }

  if (!body || typeof body !== 'object') {
    return json(400, { error: 'bad-request', reason: 'the body must be a JSON object' });
  }

  const file = opts.bindings || writer.BINDINGS_FILE;
  if (!fs.existsSync(file)) {
    return json(500, { error: 'no-bindings-file', reason: `there is no bindings file at ${path.relative(process.cwd(), file)}` });
  }

  const skipped = [];
  const result = writer.addBinding(fs.readFileSync(file, 'utf8'), body.noun, body.binding, {
    corpora: writer.corpusNouns(dir, (a) => skipped.push(a)),
    app,
  });
  if (!result.ok) {
    return json(409, { error: result.error, reason: result.reason, current: result.current });
  }

  writer.commitToDisk(file, result);
  return json(200, {
    ok: true,
    app,
    noun: result.noun,
    file: path.relative(process.cwd(), file),
    ...gitOutcome(file, opts, `bind ${result.noun}`, app),
    // 🔴 The namespace fact, in the response rather than only in a log. The
    // person who just clicked bind is the only one who can tell whether
    // sharing this noun with those corpora is what they meant, and this is the
    // moment they are looking.
    sharedWith: result.sharedWith,
    // A corpus that would not parse was skipped, so `sharedWith` is an
    // INCOMPLETE answer and says so. Silence here would turn "could not look"
    // into "nothing collides" — the two readings this codebase keeps apart
    // everywhere else ([[empty-means-two-things]]).
    unreadableCorpora: skipped,
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
/**
 * May a page at `origin` write to this Kit?
 *
 * Loopback is always allowed — that is the local tool, unchanged. A deployment
 * additionally names its own public origin with `--public-origin`.
 *
 * 🔴 The comparison is on the WHOLE normalised origin, never a substring.
 * `endsWith('balenthiran.co.uk')` would accept `https://balenthiran.co.uk.evil.com`
 * and `includes` would accept `https://evil.com/?x=balenthiran.co.uk`. Parsing
 * both sides and comparing `origin` to `origin` is the only form of this check
 * that does not have a famous bypass.
 */
function originAllowed(origin, opts = {}) {
  let url;
  try {
    url = new URL(origin);
  } catch {
    // An Origin that is not a URL is not a browser Kit serves. Refused rather
    // than ignored: the alternative treats a malformed header as "no origin",
    // which is the branch that ALLOWS the write.
    return false;
  }
  if (isLoopback(url.hostname)) return true;

  const allowed = opts.publicOrigin;
  if (!allowed) return false;
  let want;
  try {
    want = new URL(allowed);
  } catch {
    return false;
  }
  // `url.origin` is the scheme+host+port triple with the default port removed,
  // so `https://x` and `https://x:443` compare equal, and `http://x` does not
  // match `https://x` — a downgrade to plain HTTP is a different origin and is
  // supposed to fail here.
  return url.origin === want.origin;
}

/**
 * `POST /api/session` — sign in. `POST /api/session/end` — sign out.
 *
 * Both answer 404 when no password is configured, rather than 400 or 200. A
 * local Kit genuinely has no such route, and saying so keeps one truth in one
 * place: the page asks `/api/session` whether a lock exists and gets `required:
 * false`; anything that skips that step and posts a password anyway is told the
 * endpoint is not there, which is exactly what it is.
 */
function session(pathname, opts, body, json, cookie) {
  if (!auth.enabled(opts)) {
    return json(404, {
      error: 'no-such-route',
      reason: 'this Kit has no password configured, so there is nothing to sign in to',
    });
  }

  if (pathname === '/api/session/end') {
    const token = auth.parseCookies(cookie)[auth.COOKIE];
    if (opts.sessions) opts.sessions.destroy(token);
    // The cookie is cleared even if the token was already unknown. Signing out
    // twice, or after a restart wiped the store, must still leave the browser
    // without a cookie — otherwise the page believes it is signed in and every
    // write 401s with no way for the user to reach the sign-in form again.
    return { status: 200, contentType: 'application/json', body: { ok: true, signedIn: false }, setCookie: auth.clearCookieHeader(opts) };
  }

  // A throttle is only useful if it is shared across requests, so it lives on
  // opts beside the session store. Missing one means no throttling rather than
  // no sign-in: a server that cannot be signed into is worse than one that can
  // be guessed at, and `serve()` always provides it.
  const wait = opts.throttle ? opts.throttle.retryAfterMs() : 0;
  if (wait > 0) {
    return json(429, {
      error: 'too-many-attempts',
      reason: `too many failed sign-ins; try again in ${Math.ceil(wait / 1000)}s`,
      retryAfterSeconds: Math.ceil(wait / 1000),
    });
  }

  const given = body && typeof body === 'object' ? body.password : null;
  if (!auth.secretsMatch(typeof given === 'string' ? given : '', opts.password)) {
    if (opts.throttle) opts.throttle.fail();
    // One message for a missing password and for a wrong one. Telling them
    // apart tells a guesser which half of the request they got right.
    return json(401, { error: 'bad-password', reason: 'that is not the password' });
  }

  if (opts.throttle) opts.throttle.succeed();
  const token = opts.sessions.create();
  return {
    status: 200,
    contentType: 'application/json',
    // 🔴 The token is NOT in the body. It goes out only as an HttpOnly cookie,
    // so page script can never read it — returning it here as well would undo
    // that in one line and hand any XSS a durable credential.
    body: { ok: true, signedIn: true },
    setCookie: auth.cookieHeader(token, opts),
  };
}

function cors(origin, opts = {}) {
  if (!origin) return {};
  if (!originAllowed(origin, opts)) return {};
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

  // One session store and one throttle per SERVER, created here rather than per
  // request for the obvious reason, and attached to opts rather than closed over
  // so that `route`/`write` stay pure functions of their arguments — which is
  // what lets the suite test the lock without a socket. A caller that supplies
  // its own (the tests do) keeps it.
  if (auth.enabled(opts)) {
    if (!opts.sessions) opts.sessions = auth.sessions();
    if (!opts.throttle) opts.throttle = auth.throttle();
  }

  const server = http.createServer((req, res) => {
    // `new URL` needs a base; the host header is untrusted input and is only
    // ever used to satisfy the parser, never read back out.
    const { pathname } = new URL(req.url, 'http://localhost');

    const send = (result) => {
      const headers = { 'content-type': result.contentType, ...cors(req.headers.origin, opts) };
      if (result.cacheControl) headers['cache-control'] = result.cacheControl;
      // Set only by the sign-in and sign-out routes. Checked for presence
      // rather than truthiness so an empty string could never be sent as a
      // header — though `clearCookieHeader` never returns one.
      if (result.setCookie) headers['set-cookie'] = result.setCookie;
      res.writeHead(result.status, headers);
      // `raw` is set only by `bundle()`, and its presence is what distinguishes
      // bytes from a payload. Checked with `!== undefined` rather than for
      // truthiness: an empty file is a legitimate asset, and `||` would serve it
      // as the string "undefined" wearing its content-type.
      res.end(result.raw !== undefined ? result.raw : JSON.stringify(result.body));
    };

    // A preflight is answered by the same allowlist that answers the request, so
    // the two can never disagree — an ACAO that permits an origin a preflight
    // refuses is a bug that only shows up in a browser.
    if (req.method === 'OPTIONS') {
      res.writeHead(204, cors(req.headers.origin, opts));
      return res.end();
    }

    if (req.method !== 'POST') return send(route(req.method, pathname, opts, null, null, req.headers.cookie ?? null));

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
      send(route('POST', pathname, opts, body, req.headers.origin ?? null, req.headers.cookie ?? null));
    });
  });

  return new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(port, host, () => resolve(server));
  });
}

function parseArgs(argv, env = process.env) {
  const opts = { dir: BEH_DIR, bindings: null, repos: null, port: DEFAULT_PORT, host: DEFAULT_HOST };

  // ── the password (kit#46) ────────────────────────────────────────────────
  // From the ENVIRONMENT and deliberately not from a flag. A flag is visible in
  // `ps`, in a shell history and in the pod's own command line, and Kubernetes
  // delivers a secret as an env var anyway (`secretKeyRef`), which is what the
  // rest of the estate already does. Unset means no lock, which is his laptop.
  if (typeof env.KIT_PASSWORD === 'string') opts.password = env.KIT_PASSWORD;
  // The origin the browser will actually be on, e.g. https://balenthiran.co.uk.
  // Only meaningful deployed; locally the loopback rule covers it.
  if (env.KIT_PUBLIC_ORIGIN) opts.publicOrigin = env.KIT_PUBLIC_ORIGIN;
  // Set the cookie's `Secure` flag. Derived from the public origin rather than
  // configured separately, because the two can only ever disagree by mistake:
  // an https deployment wants Secure, and a plain-http localhost cannot use it.
  opts.secure = !!(opts.publicOrigin && /^https:/i.test(opts.publicOrigin));

  for (let i = 0; i < argv.length; i++) {
    const next = argv[i + 1];
    if (argv[i] === '--port') { opts.port = Number(next); i++; }
    else if (argv[i] === '--host') { opts.host = next; i++; }
    else if (argv[i] === '--repos') { opts.repos = next; i++; }
    else if (argv[i] === '--dir') { opts.dir = next; i++; }
    // `--bindings` for the same reason `selfhost/run.js` copies the corpus
    // before running Kit's own generated tests: the bind route WRITES, so a
    // demo or a harness pointed at the repo's own bindings.json dirties the
    // working tree of the thing it is measuring. Null means the real file,
    // which is the right default for the tool he actually opens.
    else if (argv[i] === '--bindings') { opts.bindings = next; i++; }
    // ── git write-back (kit#43) ──────────────────────────────────────────────
    // OFF unless asked for. Locally `docs/design/ui.md` decision 2 still holds:
    // the edit lands in the working tree and the author reviews the diff. This
    // flag is for the deployment James chose over a database on kit#41, where
    // there is no working tree anyone will ever look at.
    // Overrides KIT_PUBLIC_ORIGIN, so a flag beats the environment — the usual
    // precedence, and the one that lets a test drive this without mutating
    // process.env underneath every other test in the file.
    else if (argv[i] === '--public-origin') {
      opts.publicOrigin = next;
      opts.secure = /^https:/i.test(next || '');
      i++;
    }
    else if (argv[i] === '--git') { opts.git = { ...(opts.git || {}), enabled: true }; }
    else if (argv[i] === '--git-remote') { opts.git = { ...(opts.git || {}), remote: next }; i++; }
    else if (argv[i] === '--git-branch') { opts.git = { ...(opts.git || {}), branch: next }; i++; }
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
  const gitOn = !!(opts.git && opts.git.enabled);
  // Three states, not two, since kit#46 — and the operator has to be able to
  // tell which one they are in from this line alone, because the other way to
  // find out is to lose a write.
  if (auth.enabled(opts)) {
    console.log(`  writes: LOCKED — sign in with the password in KIT_PASSWORD${gitOn ? '; edits are committed and pushed' : '; edits are NEVER committed'}`);
    console.log(`  origin: ${opts.publicOrigin || '(none set — only a loopback page may write; set KIT_PUBLIC_ORIGIN when deployed)'}`);
  } else {
    console.log(isLoopback(opts.host)
      ? `  writes: ON — edits land in the working tree${gitOn ? ' and are committed and pushed' : ' and are NEVER committed'}`
      : `  writes: OFF — ${opts.host} is not loopback (docs/design/ui.md decision 1)`);
    // Said loudly, because this is the configuration that looks deployed and
    // is not: bound to the world, no password, so every write is refused and
    // the page will appear broken rather than protected.
    if (!isLoopback(opts.host)) {
      console.log('    set KIT_PASSWORD to accept writes from a non-loopback address (kit#44)');
    }
  }
  if (gitOn) {
    // Printed whether or not the tree is a repo, because "--git was accepted
    // and is doing nothing" is exactly the state an operator needs told at
    // startup rather than discovering from an edit that vanished on restart.
    const tree = gitStore.workTreeFor(path.join(opts.dir || BEH_DIR, '.'));
    console.log(tree
      ? `  git: ON — committing to ${opts.git.branch || gitStore.currentBranch(tree) || '(detached HEAD — writes will refuse)'} on ${opts.git.remote || 'origin'}`
      : `  git: ON but ${opts.dir || BEH_DIR} IS NOT IN A GIT WORK TREE — every write will report that it was not committed`);
  }
  // Said at startup and not only by the 503, because the person who needs to
  // read it is looking at this terminal, not at the browser tab they have not
  // opened yet.
  console.log(fs.existsSync(path.join(opts.dist ?? DIST_DIR, 'index.html'))
    ? '  ui: serving the built bundle — open the URL above, nothing else to run'
    : `  ui: NOT BUILT — the API answers, the page will not. Build it once:\n      ${BUILD_CMD}`);
  return 0;
}

module.exports = {
  route, write, serve, cors, isLoopback, originAllowed, session, corpora, repoFor, summary, parseArgs, main,
  bundle, filesIn, contentTypeFor, MAX_BODY, BUILD_CMD, DIST_DIR,
};

if (require.main === module) {
  main(process.argv.slice(2)).then((code) => {
    // Only exit on failure — on success the server is holding the loop open.
    if (code !== 0) process.exit(code);
  });
}
