#!/usr/bin/env node
// The engine's output, committed — so a port can be SCORED instead of reviewed.
//
//   node conformance.js --record        # write the goldens (never in CI, see below)
//   node conformance.js --check         # exit 1 if the engine no longer reproduces them
//   node conformance.js --check kit     # one corpus
//
// ── Why this file exists ────────────────────────────────────────────────────
//
// James chose C# for the hosted engine on kit#88. The whole safety argument for
// that port is that the Node engine is an EXECUTABLE SPECIFICATION: generation is
// byte-for-byte deterministic, so a C# module is correct exactly when it
// reproduces Node's bytes, and the 181 mutants that prove kit.test.js detects the
// rules it asserts are not discarded by the rewrite.
//
// That argument was uncashed. **No engine output was committed anywhere.**
// `selfhost/run.js` writes generated specs to a temp dir and they never enter the
// repository, so there was nothing for a C# module to be compared against — and
// nothing that would notice if the NODE engine's own output changed either. Eleven
// corpora ran through seven entry points and not one of those outputs was asserted.
// `docs/design/process.md` says the conformance harness is built *before* any
// module is ported; this is that harness.
//
// It is deliberately useful before any C# exists, because a harness whose only
// payoff is in four weeks is a harness nobody keeps green.
//
// ── Sectioned BY MODULE, and that is the load-bearing decision ──────────────
//
// Phase 3 ports the engine module by module. One opaque blob per corpus could not
// score a half-done port: the first C# module would be unverifiable until the last
// one existed, which is the same as not having a harness at all. So each corpus's
// golden has one section per engine stage — `parse`, `resolve`, `generate`, `report` — and a
// C# `Parse` can be driven against the `parse` section on its own, with
// `resolve`/`generate` still in JavaScript.
//
// ⚠️ `parse` is snapshotted BEFORE `resolve` runs, and that is not tidiness.
// `resolve` MUTATES the array `parse` returned — it writes `step.resolved` back
// onto the steps so that a hole filled by another behaviour actually generates
// (kit.js:283, deliberate and load-bearing). Measured on `james-habits-app`:
// serialising the same parse output before and after `resolve` gives 22,701 vs
// 22,728 bytes. Capture it afterwards and the `parse` golden silently contains
// resolve's additions, so a correct C# `Parse` could never match it — the harness
// would score the port wrong rather than failing to score it.
//
// ⚠️ Every `resolve`d behaviour also carries the whole `symbols` Map (kit.js:289).
// `JSON.stringify` turns a Map into `{}`, so left alone each behaviour would carry
// a `"symbols": {}` that asserts nothing while looking like it does. It is dropped
// per behaviour and recorded ONCE at corpus level, in iteration order.
//
// ⚠️ Nothing here is sorted, and that is deliberate. Insertion order is part of
// what the engine deterministically produces — `missing` is rendered into the
// generated `// unbound noun(s): …` comment in order — so sorting the goldens
// would throw away a behaviour the C# port has to reproduce and replace it with
// one it cannot fail. Maps are serialised as ordered `[key, value]` pair arrays:
// JSON-representable, and order-preserving.
//
// ── The trap this file is shaped around ─────────────────────────────────────
//
// 🔴 A golden-file REGENERATOR that runs before its own comparison rewrites the
// fixture to match the code and reports green over a real regression. That is not
// hypothetical here: `ui/src/test/fixtures/generate.js` is kept out of CI for
// exactly this reason, and kit#115 is open about its refusal wording.
//
// The protection is STRUCTURAL, not a flag. `pipeline()` is a pure function and is
// the only thing `kit.test.js` imports; every write lives behind
// `require.main === module`. No test can regenerate a golden, however it is
// edited, because the writer is not reachable from the module's exports.
//
// `--record` additionally refuses when `CI` is set, as a belt — but the belt is
// the weaker half, and if the two ever disagree the structural half is the one to
// trust.
const fs = require('fs');
const path = require('path');

const kit = require('./kit.js');
const bindingsOf = require('./bindings.js');
const projectView = require('./project.js');
const cliRules = require('./cli.js');

// ⚠️ Every flag in KNOWN_FLAGS must appear in this sentence. `kit.test.js`'s
// "a tool must TELL A HUMAN about every flag it accepts" gate reads the two
// against each other, and the failure it exists for is quiet: the usage line is
// printed BY the refusal, so a reader who typo'd a flag is handed a list that
// omits a real one at the exact moment they are trying to find out what is valid.
const USAGE = 'usage: node conformance.js [<corpus-name>] [--check|--record] [--dir <dir>] [--golden <dir>] [--help]';
const KNOWN_FLAGS = ['--check', '--record', '--dir', '--golden', '--help', '-h'];
const VALUE_FLAGS = new Set(['--dir', '--golden']);

// 2 (2026-10-07) added `report`, the project view's engine verdicts, for Phase 4.
// The format version travels IN the golden. A port verified against a golden
// written by a different shape of this file is verified against nothing, and the
// failure would otherwise look like a C# bug.
const FORMAT = 2;

// ── the pure half ───────────────────────────────────────────────────────────

// A Map as ordered pairs. `Object.fromEntries` would also lose a key that is not
// a string, and `symbols` keys are `kind:Name.slot` strings today — so this is
// about order, not about key types.
function pairs(map) {
  return [...map.entries()];
}

// Strip the `symbols` back-reference every resolved behaviour carries (kit.js:289)
// without touching anything else. Named rather than inlined so the test can say
// what it is asserting.
function withoutSymbols(behaviour) {
  const { symbols, ...rest } = behaviour;
  return rest;
}

// What `resolve` did to a behaviour, as a computed structural diff rather than a
// second copy of it.
//
// 🔑 This is a measurement, not a preference. The first version of this file
// recorded `resolve`'s behaviours in full, and then: **36.7% of the golden was a
// near-duplicate of another 35.6%, with 140 of 147 behaviours byte-identical to
// their `parse` counterpart once `filled`/`open` were stripped.** 666 KB of
// artefact to assert about 355 KB of engine output.
//
// ⚠️ The 7 that DID differ are why this is a diff and not a field whitelist.
// `resolve` writes `step.resolved` back onto a step so that a hole filled by
// another behaviour actually generates (kit.js:283) — the mechanism would be
// decorative without it. A hand-picked `{ filled, open, resolved }` list would
// capture that today and silently miss whatever `resolve` starts mutating next,
// which is the failure mode of every shortcut that lists fields instead of
// comparing values. Diffing cannot miss a field it was never told about.
//
// Paths are walked in key-insertion order and NOT sorted, for the same reason
// nothing else here is sorted: the order is itself deterministic output.
function delta(before, after, at = '', out = []) {
  if (before === after) return out;
  const prim = (v) => v === null || typeof v !== 'object';
  if (prim(before) || prim(after)) {
    if (JSON.stringify(before) !== JSON.stringify(after)) out.push({ path: at, from: before === undefined ? null : before, to: after === undefined ? null : after });
    return out;
  }
  if (Array.isArray(before) !== Array.isArray(after)) {
    out.push({ path: at, from: before, to: after });
    return out;
  }
  // Union of keys, `before` first, so a key only `after` has (the mutation case)
  // is reported as an addition rather than vanishing.
  const keys = [...Object.keys(before), ...Object.keys(after).filter((k) => !(k in before))];
  for (const k of keys) {
    delta(before[k], after[k], at ? `${at}.${k}` : k, out);
  }
  return out;
}

/**
 * The engine's complete observable output for one corpus, as plain JSON.
 *
 * This is the SEAM a C# port has to reproduce. It takes a directory and a corpus
 * name rather than reading any global, so a test can drive it over a fixture.
 */
function pipeline(dir, corpus) {
  const file = `${corpus}.beh`;
  const src = fs.readFileSync(path.join(dir, file), 'utf8');

  const parsed = kit.parse(src, file);
  // Snapshot BEFORE resolve, which mutates `parsed` in place. See the header.
  const parseSection = JSON.parse(JSON.stringify(parsed));

  const { behaviours, symbols, conflicts } = kit.resolve(parsed);

  // One bindings map per corpus, never a merged one — a behaviour generates
  // against its OWN corpus's bindings (kit#66), so reading all and selecting is
  // what the CLI does and what a port must do.
  const byApp = bindingsOf.readAll(dir);
  const bindings = byApp[corpus] || {};

  const generateSection = behaviours.map((b) => {
    const { code, missing, stats } = kit.generate(b, bindings, symbols);
    return { id: b.id, code, missing, stats };
  });

  return {
    format: FORMAT,
    corpus,
    // Deliberately no timestamp and no absolute path. The committed sheet gate
    // (kit.test.js:556) is checkable only because the sheet carries no date; a
    // golden with a wall-clock field differs every day for no reader's benefit,
    // which is the kind of failing check that gets deleted rather than fixed.
    parse: parseSection,
    resolve: {
      symbols: pairs(symbols),
      conflicts,
      // What resolve ADDED to each behaviour, not a second copy of it. See
      // `delta` above for the measurement that chose this shape.
      changed: behaviours.map((b, i) => ({
        id: b.id,
        changes: delta(parseSection[i], withoutSymbols(b)),
      })),
    },
    generate: generateSection,
    // What the project view serves about this corpus, through project.js's own
    // `report` — the one definition of that shape — so the C# server is scored on
    // what the page reads. Its own key because Phase 4 ports it function by
    // function, exactly as Phase 3 ported the stages above.
    report: projectView.report(behaviours, conflicts, bindings),
  };
}

// The pseudo-corpus name the read routes are reported under, beside the corpora.
const ROUTES = 'routes/read';

function routesPath(goldenDir) {
  return path.join(goldenDir, 'routes', 'read.json');
}

/**
 * The READ half of the HTTP surface, as `ui.js`'s own pure `route()` answers it
 * over `dir` with no password and no repos — exactly what hosted Kit serves to a
 * reader. Phase 4's C# server is scored on this, request by request, the way the
 * engine was scored on the corpus goldens.
 *
 * Cross-corpus, which is why it is one file and not a section of each golden:
 * the list route reads every corpus, and every noun's `sharedWith` in a project
 * view does too, so adding ANY corpus moves this file. That is the honest
 * population, and `--record` after a corpus change already regenerates it.
 *
 * Includes the refusals — an unknown project, an undecodable name, an encoded
 * `..`, an unknown route, a method nothing handles — because a port that serves
 * the happy paths and answers the rest with a framework 404 page would pass
 * every request a happy-path oracle makes.
 */
function readRoutes(dir) {
  // Required here, not at the top: ui.js is the server, and the engine stages
  // above need none of it.
  const ui = require('./ui.js');
  const get = [
    '/api/health',
    '/api/session',
    '/api/projects',
    ...corporaIn(dir).sort().map((app) => `/api/projects/${app}`),
    '/api/projects/no-such-app',
    '/api/projects/%E0%A4%A',
    '/api/projects/%2e%2e',
    '/api/no-such-route',
    '/api',
  ];
  const requests = get.map((p) => ({ method: 'GET', path: p, response: ui.route('GET', p, { dir }) }));
  requests.push({ method: 'PUT', path: '/api/projects', response: ui.route('PUT', '/api/projects', { dir }) });

  // The built UI, served out of a committed FIXTURE bundle (`routes/dist`) — the
  // real `ui/dist` is gitignored and absent in CI. Every path a browser sends that
  // is not under /api: the shell for a client-side route, hashed assets, an
  // unhashed root file, an unmapped extension, and each refusal — a missing asset,
  // a missing file, a nested asset path, bad percent-encoding, and `..` both plain
  // and encoded. `raw` is recorded as UTF-8 text (`rawText`), which every fixture
  // file is, so the golden stays readable and language-neutral.
  const dist = path.join(__dirname, 'conformance', 'routes', 'dist');
  const bundlePaths = [
    '/',
    '/projects/snip-it',
    '/assets',
    '/index.html',
    '/assets/index-Ab12Cd.js',
    '/assets/index-Ef34Gh.css',
    '/assets/index-Missing.js',
    '/assets/nested/index-Ab12Cd.js',
    '/favicon.svg',
    '/notes.unknownext',
    '/robots.txt',
    '/%E0%A4%A',
    '/%2e%2e',
    '/a/..%2f..%2fetc',
  ];
  for (const p of bundlePaths) {
    const { raw, ...rest } = ui.route('GET', p, { dir, dist });
    requests.push({ method: 'GET', path: p, dist: true, response: raw === undefined ? rest : { ...rest, rawText: Buffer.from(raw).toString('utf8') } });
  }
  return { format: FORMAT, requests };
}

const HOST = 'routes/host';

function hostPath(goldenDir) {
  return path.join(goldenDir, 'routes', 'host.json');
}

/**
 * The HOST layer, as `ui.js`'s `answer()` — the function `serve()` calls for
 * every request — decides it: the raw request target through WHATWG `new URL`
 * (dot segments, `%2e`, backslashes, absolute and `//` forms), the base-path
 * strip, the preflight, and the CORS headers for each kind of Origin. Recorded
 * under two configurations: a local Kit at the root, and the deployed shape
 * (`/kit` behind `https://balenthiran.co.uk`).
 *
 * What it records is what goes on the wire: status, every header `serve()`
 * writes, and the body. A POST under the prefix records `post` — the path whose
 * body `serve()` would then read — because the writes are not ported yet.
 */
function hostRoutes(dir) {
  const ui = require('./ui.js');
  const dist = path.join(__dirname, 'conformance', 'routes', 'dist');
  const PUBLIC = 'https://balenthiran.co.uk';
  const configs = {
    root: { basePath: '', opts: { dir, dist } },
    deployed: { basePath: '/kit', opts: { dir, dist, publicOrigin: PUBLIC } },
  };
  const get = (url, origin = null) => ({ method: 'GET', url, origin });
  const plan = {
    root: [
      get('/'), get('/api/health'), get('/api/../api/health'), get('/api/projects/%2e%2e'), get('/..%2f'),
      get('/api/health', 'http://localhost:5173'), get('/api/health', PUBLIC),
      { method: 'OPTIONS', url: '/api/projects', origin: 'http://localhost:5173' },
      { method: 'POST', url: '/api/session', origin: null },
    ],
    deployed: [
      // the prefix itself, its look-alikes, and paths it does not own
      get('/kit'), get('/kit/'), get('/kitten'), get('/'), get('/api/health'), get('/%6Bit/api/health'),
      get('/kit/api/health'), get('/kit/api/projects/no-such-app'), get('/kit/projects/snip-it'), get('/kit/assets/index-Ab12Cd.js'),
      // WHATWG normalisation of the target, before the strip
      get('/kit/../api/health'), get('/kit/%2e%2e/api/health'), get('/kit/%2E./kit/api/health'), get('/kit/api/./health'),
      get('/kit/api/%2e/health'), get('/kit\\api\\health'), get('/kit/api/health?x=1'), get('//evil.com/kit/api/health'),
      get('http://other:99/kit/api/health'), get('/kit//api/health'), get('/kit/..'), get('/../kit/api/health'),
      get('/kit/api/projects/%2e%2e'), get('/kit/assets/..%2findex.html'), get('/kit/api/projects/a"b{c}`d<e>^f|g'),
      // (Not `http:/x`, `http:x`, `https:\\x` or `foo:/x`: Node's HTTP parser refuses
      // those with a bare 400 before `serve()` runs, so no answer here is reachable.)
      get('*'), get('//user:pw@host:8080/kit/api/health'), get('//[::1]/kit/api/health'), get('//0x7f.1/kit/api/health'),
      // targets `new URL` cannot parse: before the host layer, each one crashed the process
      get('//x:99999/kit/api/health'), get('//x:abc/kit/api/health'), get('//%/kit'), get('//[/kit'), get('//exa%00mple/kit'),
      get('//@/kit'), get('//1.2.3.4.5/kit'), get('//999.1.1.1/kit'), get('http://[::1/kit'),
      // every kind of Origin, on a read
      ...[PUBLIC, `${PUBLIC}:443`, 'http://balenthiran.co.uk', `${PUBLIC}.evil.com`, 'https://BALENTHIRAN.co.uk',
        `${PUBLIC}:8443`, `${PUBLIC}/`, `https://user@balenthiran.co.uk`, 'https://evil.com#https://balenthiran.co.uk',
        'http://localhost:5173', 'http://LOCALHOST:1', 'http://127.0.0.1:1', 'http://127.1', 'http://0x7f.0.0.1', 'http://127.000.000.001',
        'http://2130706433', 'http://[::1]:3', 'http://[0:0::1]', 'http://localhost.evil.com', 'http://127.0.0.1.evil.com',
        'http://localhost\\@evil.com', 'http://evil.com\\@localhost', 'foo://localhost', 'null', 'not a url', ''].map((o) => get('/kit/api/health', o)),
      // preflights, inside and outside the prefix
      { method: 'OPTIONS', url: '/kit/api/projects', origin: PUBLIC },
      { method: 'OPTIONS', url: '/kit/api/projects', origin: 'https://evil.com' },
      { method: 'OPTIONS', url: '/kit/api/projects', origin: null },
      { method: 'OPTIONS', url: '/api/projects', origin: PUBLIC },
      { method: 'OPTIONS', url: '*', origin: PUBLIC },
      // other methods
      { method: 'PUT', url: '/kit/api/projects', origin: null },
      { method: 'DELETE', url: '/kit/api/health', origin: PUBLIC },
      { method: 'POST', url: '/kit/api/session', origin: PUBLIC },
      { method: 'POST', url: '/api/session', origin: PUBLIC },
    ],
  };

  const requests = [];
  for (const [config, list] of Object.entries(plan)) {
    const { basePath, opts } = configs[config];
    for (const { method, url, origin } of list) {
      const a = ui.answer(method, url, origin, null, opts, basePath);
      let response;
      if (a.post !== undefined) response = { post: a.post };
      else {
        const headers = Object.fromEntries(Object.entries(a.headers).sort(([x], [y]) => (x < y ? -1 : x > y ? 1 : 0)));
        response = { status: a.status, headers };
        if (a.raw !== undefined) response.rawText = Buffer.from(a.raw).toString('utf8');
        else if (a.body !== '') response.body = JSON.parse(a.body);
      }
      requests.push({ config, method, url, origin, response });
    }
  }
  return {
    format: FORMAT,
    configs: Object.fromEntries(Object.entries(configs).map(([k, c]) => [k, { basePath: c.basePath, publicOrigin: c.opts.publicOrigin ?? null }])),
    requests,
  };
}

const AUTH = 'routes/auth';

function authPath(goldenDir) {
  return path.join(goldenDir, 'routes', 'auth.json');
}

/**
 * The write GATES, as scenarios: sign-in, the throttle, session expiry, sign-out,
 * the lock, the loopback rule, and the CSRF Origin check — each request through
 * `answer()` and then `received()`, the two pure functions `serve()` calls. State
 * lives across a scenario's steps, so each runs on a fake clock (`advance`) with
 * tokens minted as `tok-1`, `tok-2`… so a golden can name them.
 *
 * Every write here is refused or fails BEFORE a file is touched (an unknown
 * project, a body that is not an object): the gates are the subject, and the
 * writer is a later oracle.
 */
function authRoutes(dir) {
  const ui = require('./ui.js');
  const auth = require('./auth.js');
  const PUBLIC = 'https://balenthiran.co.uk';
  const W = '/api/projects/snip-it/behaviours';
  const req = (method, p, extra = {}) => ({ method, path: p, origin: null, cookie: null, body: '{}', ...extra });
  const post = (p, extra) => req('POST', p, extra);
  const signIn = (password, extra = {}) => post('/api/session', { body: JSON.stringify(password === undefined ? {} : { password }), ...extra });
  const session = (cookie) => req('GET', '/api/session', { cookie, body: null });

  const scenarios = [
    {
      name: 'local: no password, loopback',
      config: { host: '127.0.0.1', password: null, publicOrigin: null },
      steps: [
        session(null),
        signIn('anything'),
        post('/api/projects/no-such-app/behaviours'),
        post(W, { body: 'null' }),
        post(W, { body: '"a string"' }),
        post(W, { body: '{' }),
        post(W, { body: '' }),
        post(W, { tooLarge: true, body: null }),
        post(W, { origin: 'https://evil.com', body: '{' }),
        post(W, { origin: 'https://evil.com', tooLarge: true, body: null }),
        post(W, { origin: 'http://localhost:5173', body: 'null' }),
        post('/api/nope'),
        post('/api/projects/%E0%A4%A/behaviours'),
        post('/api/projects/no-such-app/behaviours/b1/steps'),
        post('/api/projects/no-such-app/behaviours/b1/review'),
      ],
    },
    {
      name: 'bound wide: no password, not loopback',
      config: { host: '0.0.0.0', password: null, publicOrigin: null },
      steps: [session(null), post(W), signIn('anything')],
    },
    {
      name: 'a whitespace password is no password',
      config: { host: '0.0.0.0', password: ' \t ', publicOrigin: null },
      steps: [session(null), post(W), signIn(' \t ')],
    },
    {
      name: 'locked: sign-in, throttle, expiry, sign-out',
      config: { host: '0.0.0.0', password: 'correct horse', publicOrigin: PUBLIC },
      steps: [
        session(null),
        post(W),
        post(W, { cookie: 'kit_session=forged' }),
        ...Array.from({ length: 5 }, () => signIn('wrong')),
        signIn('correct horse'),
        { advance: auth.COOLDOWN_BASE_MS - 1 },
        signIn('correct horse'),
        { advance: 1 },
        signIn('wrong'),
        { advance: 2 * auth.COOLDOWN_BASE_MS },
        signIn(undefined),
        { advance: 4 * auth.COOLDOWN_BASE_MS },
        signIn(' correct horse'),
        { advance: 8 * auth.COOLDOWN_BASE_MS },
        signIn('correct horse', { origin: 'https://evil.com' }),
        signIn('correct horse', { origin: PUBLIC }),
        session('kit_session=tok-1'),
        session('a=b; kit_session=tok-1; c=d='),
        session('kit_session=tok-1x'),
        post(W, { cookie: 'kit_session=tok-1', body: 'null' }),
        post('/api/projects/no-such-app/behaviours', { cookie: 'kit_session=tok-1' }),
        post(W, { cookie: 'kit_session=tok-1', origin: 'https://evil.com', body: 'null' }),
        post('/api/session/end', { cookie: 'kit_session=tok-1' }),
        session('kit_session=tok-1'),
        post(W, { cookie: 'kit_session=tok-1', body: 'null' }),
        signIn('correct horse'),
        { advance: auth.TTL_MS - 1 },
        session('kit_session=tok-2'),
        { advance: 1 },
        session('kit_session=tok-2'),
        // JSON.parse's semantics, which a port's parser must match: duplicate keys are
        // last-wins, a BOM is not whitespace, trailing text is an error, and depth is
        // not capped at a framework default.
        post('/api/session', { body: '{"password":"wrong","password":"correct horse"}' }),
        post('/api/session', { body: `${String.fromCharCode(0xfeff)}{"password":"correct horse"}` }),
        post('/api/session', { body: '{"password":"correct horse"} x' }),
        post('/api/session', { body: '{"password":123}' }),
        post('/api/session', { body: '["correct horse"]' }),
        post('/api/session', { body: `{"password":${'['.repeat(1000)}${']'.repeat(1000)}}` }),
        // Cookie parsing: a value runs to the end (it may contain `=`), names and
        // values are trimmed, and a later duplicate wins.
        session('kit_session=tok-3='),
        session(' kit_session = tok-3 '),
        session('kit_session=tok-3; kit_session=tok-1'),
        session('kit_session=tok-1; kit_session=tok-3'),
        session('=tok-3; kit_session'),
        post('/api/session/end'),
      ],
    },
  ];

  for (const s of scenarios) {
    let t = 1_700_000_000_000;
    let n = 0;
    const now = () => t;
    const opts = {
      dir,
      host: s.config.host,
      sessions: auth.sessions(now, () => `tok-${++n}`),
      throttle: auth.throttle(now),
    };
    if (s.config.password !== null) opts.password = s.config.password;
    if (s.config.publicOrigin) opts.publicOrigin = s.config.publicOrigin;
    // As `parseArgs` derives it: an https public origin sets the cookie's Secure flag.
    opts.secure = !!(s.config.publicOrigin && /^https:/i.test(s.config.publicOrigin));

    for (const step of s.steps) {
      if (step.advance !== undefined) { t += step.advance; continue; }
      let a = ui.answer(step.method, step.path, step.origin, step.cookie, opts, '');
      if (a.post !== undefined) a = ui.received(a.post, step.tooLarge ? null : Buffer.from(step.body, 'utf8'), step.origin, step.cookie, opts);
      const headers = Object.fromEntries(Object.entries(a.headers).sort(([x], [y]) => (x < y ? -1 : x > y ? 1 : 0)));
      step.response = { status: a.status, headers, body: JSON.parse(a.body) };
    }
  }
  return { format: FORMAT, maxBody: ui.MAX_BODY, scenarios };
}

const WRITES = 'routes/writes';

function writesPath(goldenDir) {
  return path.join(goldenDir, 'routes', 'writes.json');
}

/**
 * The WRITER, at two levels, over the fixture corpora in `conformance/writes/`.
 *
 * `functions`: `writer.js`'s pure edits — addStep, addBehaviour, setReview,
 * addBinding — each on a named fixture text, recording the whole text after the
 * edit or the refusal. Every splice rule and every refusal code is reached:
 * where a step lands when comments trail a block, where a review line goes when
 * there is none, the collateral-change guard, a corpus that was already broken.
 *
 * `routes`: the same edits as POSTs through `answer()` + `received()` against a
 * temporary COPY of the fixtures (git write-back off), recording each response
 * and the file it wrote. A write's `file` names the copy, so it is recorded as
 * `<dir>/<name>`.
 */
function writeRoutes() {
  const ui = require('./ui.js');
  const writer = require('./writer.js');
  const os = require('os');
  const FIX = path.join(__dirname, 'conformance', 'writes');
  const read = (f) => fs.readFileSync(path.join(FIX, f), 'utf8');
  const texts = {
    alpha: read('alpha.beh'),
    broken: read('broken.beh'),
    'alpha-unterminated': read('alpha.beh').replace(/\s*$/, ''),
    bindings: read('alpha.bindings.json'),
    'bindings-empty': '{}',
    'bindings-array': '[]',
    'bindings-bad': '{',
    // Not a `.beh`, so it stays out of every corpus population: an INDENTED header
    // the parser treats as a new behaviour and the splice does not, which is the one
    // way an edit lands in a NEIGHBOUR and rule 3's "changed" branch must catch it.
    indented: read('indented.txt'),
  };
  const corpora = writer.corpusNouns(FIX);

  const calls = [
    ['addStep', 'alpha', ['BEH-A', 'then sees region:Done']],
    ['addStep', 'alpha', ['BEH-C', '  then sees region:Settings  ']],
    ['addStep', 'alpha', ['BEH-D', 'then sees region:Info']],
    ['addStep', 'alpha', ['BEH-A', '   ']],
    ['addStep', 'alpha', ['BEH-A', 'then sees a:B\nthen sees c:D']],
    ['addStep', 'alpha', ['BEH-Q', 'then sees a:B']],
    ['addStep', 'alpha', ['BEH-A', 'flies away']],
    ['addStep', 'alpha', ['BEH-A', 'behaviour BEH-NEW "sneaky"']],
    ['addStep', 'broken', ['BEH-Z', 'then sees a:B']],
    ['addStep', 'indented', ['BEH-F', 'then sees region:F']],
    ['addBehaviour', 'alpha', ['BEH-E', 'Plain', {}]],
    ['addBehaviour', 'alpha', ['BEH-E', 'Full', { actor: 'guest', steps: ['when opens page:Home', '  then sees region:Saved '], source: 'defined', ref: 'notes.md#e' }]],
    ['addBehaviour', 'alpha-unterminated', ['BEH-E', 'Plain', {}]],
    ['addBehaviour', 'alpha', ['beh-e', 'Lower', {}]],
    ['addBehaviour', 'alpha', ['BEH E', 'Space', {}]],
    ['addBehaviour', 'alpha', ['BEH-A', 'Duplicate', {}]],
    ['addBehaviour', 'alpha', ['BEH-E', 'Say "hi"', {}]],
    ['addBehaviour', 'alpha', ['BEH-E', 'Two\nlines', {}]],
    ['addBehaviour', 'alpha', ['BEH-E', 'Bad step', { steps: ['flies away'] }]],
    ['addBehaviour', 'alpha', ['BEH-E', 'Bad source', { source: 'sideways' }]],
    ['setReview', 'alpha', ['BEH-A', 'approved', null]],
    ['setReview', 'alpha', ['BEH-A', 'denied', 'the guest does it']],
    ['setReview', 'alpha', ['BEH-B', 'approved', null]],
    ['setReview', 'alpha', ['BEH-C', ' approved ', '  ']],
    ['setReview', 'alpha', ['BEH-D', 'unreviewed', null]],
    ['setReview', 'alpha', ['BEH-A', '', null]],
    ['setReview', 'alpha', ['BEH-A', 'approved\n', null]],
    ['setReview', 'alpha', ['BEH-A', 'denied', 'x\ny']],
    ['setReview', 'alpha', ['BEH-Q', 'approved', null]],
    ['setReview', 'alpha', ['BEH-A', 'denied', null]],
    ['setReview', 'alpha', ['BEH-A', 'maybe', null]],
    ['addBinding', 'bindings', ['page:Home', { route: '/' }]],
    ['addBinding', 'bindings', ['  page:Settings ', { route: '/settings' }]],
    ['addBinding', 'bindings', ['region:Saved', { 2: 'b', 1: 'a', z: 'c', nested: { 10: 1, 9: 2 } }]],
    ['addBinding', 'bindings', ['region:Done', { name: `Spar${String.fromCharCode(0xe9)} ${String.fromCharCode(0x2028)} "q" \\ </` }]],
    ['addBinding', 'bindings', ['button:Save', { role: 'link' }]],
    ['addBinding', 'bindings', ['Save', { role: 'link' }]],
    ['addBinding', 'bindings', ['button:Sa ve', { role: 'link' }]],
    ['addBinding', 'bindings', ['_comment', { role: 'link' }]],
    ['addBinding', 'bindings', ['page:Home', null]],
    ['addBinding', 'bindings', ['page:Home', ['/']]],
    ['addBinding', 'bindings', ['page:Home', {}]],
    ['addBinding', 'bindings', ['page:Home', '/']],
    ['addBinding', 'bindings-empty', ['page:Home', { route: '/', n: 1.5, big: 1e21, t: true, z: null }]],
    ['addBinding', 'bindings-array', ['page:Home', { route: '/' }]],
    ['addBinding', 'bindings-bad', ['page:Home', { route: '/' }]],
  ];

  const functions = calls.map(([fn, input, args]) => {
    const text = texts[input];
    const r = fn === 'addStep' ? writer.addStep(text, ...args)
      : fn === 'addBehaviour' ? writer.addBehaviour(text, ...args)
        : fn === 'setReview' ? writer.setReview(text, ...args)
          : writer.addBinding(text, args[0], args[1], { corpora, app: 'alpha' });
    return { fn, input, args, result: r };
  });

  // ── the routes, on a copy ─────────────────────────────────────────────────
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'kit-writes-'));
  try {
    for (const f of fs.readdirSync(FIX)) fs.copyFileSync(path.join(FIX, f), path.join(tmp, f));
    const opts = { dir: tmp, host: '127.0.0.1' };
    const B = '/api/projects/alpha/behaviours';
    const steps = [
      [`${B}/BEH-A/steps`, { step: 'then sees region:Done' }],
      [`${B}/BEH-B/review`, { state: 'approved' }],
      [`${B}/BEH-A/review`, { state: 'denied', note: 'the guest does it' }],
      [B, { id: 'BEH-E', title: 'New', actor: 'guest', steps: ['when opens page:Home'], source: 'defined', ref: 'x.md' }],
      [B, { id: 'BEH-E', title: 'Duplicate' }],
      [`${B}/BEH-Q/steps`, { step: 'then sees a:B' }],
      [`${B}/%E0/steps`, { step: 'then sees a:B' }],
      [`${B}/BEH%2DA/steps`, { step: 'then sees a:C' }],
      [B, { title: 'No id' }],
      [B, { id: 'BEH-F' }],
      [B, { id: 'BEH-F', title: 'T', steps: 'when opens page:Home' }],
      [B, { id: 'BEH-F', title: 'T', steps: ['when opens page:Home', 5] }],
      [B, { id: 'BEH-F', title: 'T', actor: null, source: null, ref: null, steps: null }],
      [B, ['BEH-G']],
      [`${B}/BEH-A/steps`, { step: 5 }],
      [`${B}/BEH-A/steps`, {}],
      [`${B}/BEH-A/review`, { note: 'x' }],
      [`${B}/BEH-A/review`, { state: 'approved', note: 7 }],
      ['/api/projects/alpha/bindings', { noun: 'page:Home', binding: { route: '/' } }],
      ['/api/projects/alpha/bindings', { noun: 'button:Save', binding: { role: 'link' } }],
      ['/api/projects/alpha/bindings', { binding: { role: 'link' } }],
      ['/api/projects/alpha/bindings', { noun: 'region:Saved', binding: {} }],
      ['/api/projects/beta/bindings', { noun: 'page:Home', binding: { route: '/b' } }],
      // Sent as RAW TEXT, because a JS object literal would already have reordered
      // it: JSON.parse moves integer-like keys first, ascending, at every depth, and
      // the bindings file is written in that order.
      ['/api/projects/alpha/bindings', '{"noun":"region:Done","binding":{"b":1,"2":"x","1":"y","in":{"z":0,"10":1,"9":2}}}'],
      ['/api/projects/broken/behaviours/BEH-Z/steps', { step: 'then sees a:B' }],
      // A corpus that starts with a byte-order mark keeps it through an edit.
      ['/api/projects/delta/behaviours/BEH-Y/steps', { step: 'then sees region:Delta' }],
      ['/api/projects/%E0/bindings', { noun: 'page:Home', binding: { route: '/' } }],
      ['/api/projects/gamma/bindings', { noun: 'page:Home', binding: { route: '/' } }],
    ];
    const relTmp = path.relative(path.join(__dirname, '..', '..'), tmp).split(path.sep).join('/');
    const routes = steps.map(([p, body]) => {
      const a = ui.received(p, Buffer.from(typeof body === 'string' ? body : JSON.stringify(body), 'utf8'), null, null, opts);
      const response = { status: a.status, body: JSON.parse(a.body) };
      let written = null;
      if (typeof response.body.file === 'string' && response.body.file.startsWith(`${relTmp}/`)) {
        const name = response.body.file.slice(relTmp.length + 1);
        response.body.file = `<dir>/${name}`;
        written = { name, text: fs.readFileSync(path.join(tmp, name), 'utf8') };
      }
      return { path: p, body, response, written };
    });
    return { format: FORMAT, functions, routes };
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

const GIT = 'routes/git';

function gitPath(goldenDir) {
  return path.join(goldenDir, 'routes', 'git.json');
}

/**
 * GIT WRITE-BACK (`git-store.js`), end to end: a write through `received()` with
 * `opts.git` switched on, each in a fresh fixture — a REAL bare remote and a REAL
 * clone holding `alpha.beh` and its bindings, as `kit.test.js`'s `gitFixture()`.
 * Every outcome `gitOutcome()` can describe is reached: pushed, unchanged, no work
 * tree, detached HEAD, a push the remote refuses, a remote that does not exist, an
 * explicit branch, a bind, and a tree dirty with someone else's staged file.
 *
 * Recorded per scenario: the response, and what the REMOTE then holds — the last
 * commit's subject, author and files on the pushed branch, or null — plus whether
 * the clone's HEAD moved. The remote is the half that matters: a commit that never
 * left the pod is the state this feature exists to report honestly.
 *
 * Three things differ per run and are masked, identically on both sides: the
 * commit hash (`<sha>`), the fixture's absolute root (`<tmp>`), and the corpus
 * path relative to the Kit repo (`<dir>/`).
 */
function gitRoutes() {
  const ui = require('./ui.js');
  const os = require('os');
  const { spawnSync } = require('child_process');
  const FIX = path.join(__dirname, 'conformance', 'writes');
  const REPO = path.join(__dirname, '..', '..');
  const sh = (args, cwd) => {
    const r = spawnSync('git', args, { cwd, encoding: 'utf8' });
    if (r.status !== 0) throw new Error(`fixture: git ${args.join(' ')} — ${r.stderr}`);
    return r.stdout;
  };
  const B = '/api/projects/alpha/behaviours';
  const step = [`${B}/BEH-A/steps`, { step: 'then sees region:Done' }];
  const scenarios = [
    ['pushed', step, {}],
    ['unchanged', [`${B}/BEH-A/review`, { state: 'unreviewed' }], {}],
    ['bind', ['/api/projects/alpha/bindings', { noun: 'page:Home', binding: { route: '/' } }], {}],
    ['new-behaviour', [B, { id: 'BEH-E', title: 'New' }], { name: 'Someone', email: 'someone@example.com' }],
    ['explicit-branch', step, { branch: 'kit-edits' }],
    ['no-such-remote', step, { remote: 'nope' }],
    ['remote-gone', step, {}, 'remote-gone'],
    ['detached', step, {}, 'detached'],
    ['not-a-work-tree', step, {}, 'not-a-work-tree'],
    ['dirty-tree', step, {}, 'dirty-tree'],
  ];

  return {
    format: FORMAT,
    scenarios: scenarios.map(([name, [p, body], git, setup]) => {
      const root = fs.mkdtempSync(path.join(os.tmpdir(), 'kit-git-'));
      try {
        const bare = path.join(root, 'bare.git');
        const clone = path.join(root, 'clone');
        sh(['init', '-q', '--bare', '-b', 'main', bare]);
        sh(['clone', '-q', bare, clone]);
        sh(['-C', clone, 'config', 'user.name', 'fixture']);
        sh(['-C', clone, 'config', 'user.email', 'fixture@example.com']);
        const dir = path.join(clone, 'behaviours');
        fs.mkdirSync(dir);
        for (const f of ['alpha.beh', 'alpha.bindings.json']) fs.copyFileSync(path.join(FIX, f), path.join(dir, f));
        sh(['-C', clone, 'add', '-A']);
        sh(['-C', clone, 'commit', '-q', '-m', 'initial']);
        sh(['-C', clone, 'push', '-q', 'origin', 'main']);

        let served = dir;
        if (setup === 'remote-gone') fs.renameSync(bare, `${bare}.gone`);
        if (setup === 'detached') sh(['-C', clone, 'checkout', '-q', '--detach', 'HEAD']);
        if (setup === 'dirty-tree') {
          fs.writeFileSync(path.join(dir, 'unrelated.txt'), 'not part of this edit\n');
          sh(['-C', clone, 'add', '--', 'behaviours/unrelated.txt']);
        }
        if (setup === 'not-a-work-tree') {
          served = path.join(root, 'loose');
          fs.mkdirSync(served);
          for (const f of ['alpha.beh', 'alpha.bindings.json']) fs.copyFileSync(path.join(FIX, f), path.join(served, f));
        }

        const before = sh(['-C', clone, 'rev-parse', 'HEAD']).trim();
        const opts = { dir: served, host: '127.0.0.1', git: { enabled: true, ...git } };
        const a = ui.received(p, Buffer.from(JSON.stringify(body), 'utf8'), null, null, opts);
        const response = { status: a.status, body: maskGit(JSON.parse(a.body), root, path.relative(REPO, served)) };

        const branch = git.branch || 'main';
        const remote = !fs.existsSync(bare) || spawnSync('git', ['-C', bare, 'rev-parse', '--verify', '-q', `refs/heads/${branch}`]).status !== 0
          ? null
          : {
            branch,
            subject: sh(['-C', bare, 'log', '-1', '--format=%s', branch]).trim(),
            author: sh(['-C', bare, 'log', '-1', '--format=%an <%ae>', branch]).trim(),
            files: sh(['-C', bare, 'show', '--name-only', '--format=', branch]).trim().split('\n'),
          };
        const headMoved = sh(['-C', clone, 'rev-parse', 'HEAD']).trim() !== before;
        return { name, path: p, body, git, setup: setup || null, response, remote, headMoved };
      } finally {
        fs.rmSync(root, { recursive: true, force: true });
      }
    }),
  };
}

/**
 * The three per-run values out of a git write's answer: the commit hash, the
 * fixture root, the corpus path. The C# test masks with the same three rules.
 */
function maskGit(body, root, relDir) {
  const sha = typeof body.commit === 'string' && /^[0-9a-f]{10}$/.test(body.commit) ? body.commit : null;
  const rel = relDir.split(path.sep).join('/');
  const mask = (s) => {
    let t = s;
    if (sha) t = t.split(sha).join('<sha>');
    return t.split(root).join('<tmp>');
  };
  const out = {};
  for (const [k, v] of Object.entries(body)) {
    if (k === 'file' && typeof v === 'string' && v.startsWith(`${rel}/`)) out[k] = `<dir>/${v.slice(rel.length + 1)}`;
    else out[k] = typeof v === 'string' ? mask(v) : v;
  }
  return out;
}

// The serialised form, which is what is actually compared. One definition, so
// `--record` and `--check` cannot disagree about formatting — the failure mode
// where a check is permanently red because the writer indents differently.
function serialise(result) {
  return `${JSON.stringify(result, null, 2)}\n`;
}

function goldenPath(goldenDir, corpus) {
  return path.join(goldenDir, `${corpus}.json`);
}

/** Corpus names in the behaviours directory, in readdir order. */
function corporaIn(dir) {
  return fs.readdirSync(dir).filter((f) => f.endsWith('.beh')).map((f) => f.replace(/\.beh$/, ''));
}

/**
 * Compare committed goldens against what the engine produces now.
 *
 * Returns { drifted[], missing[], extra[], matched[] } and NEVER writes. A
 * missing golden is reported separately from a drifted one because they are
 * different statements: one is "could not look", the other is "looked and it
 * has changed".
 */
function compare(dir, goldenDir, only) {
  const corpora = only ? [only] : corporaIn(dir);
  const out = { drifted: [], missing: [], extra: [], matched: [] };

  for (const corpus of corpora) {
    const p = goldenPath(goldenDir, corpus);
    const fresh = serialise(pipeline(dir, corpus));
    if (!fs.existsSync(p)) { out.missing.push(corpus); continue; }
    const committed = fs.readFileSync(p, 'utf8');
    if (committed === fresh) out.matched.push(corpus);
    else out.drifted.push({ corpus, committedBytes: committed.length, freshBytes: fresh.length });
  }

  // A golden with no corpus is the state nobody looks for: a corpus is deleted or
  // renamed, the golden stays, and the suite keeps proving the engine reproduces
  // output for something that no longer exists. Only reported on a full run,
  // because a single-corpus run has nothing to say about the others.
  // The read routes span every corpus, so they are only compared on a full run —
  // and through the same three outcomes as a corpus, so nothing downstream needs
  // a fourth state to report them.
  if (!only) {
    for (const [name, p, fresh] of [[ROUTES, routesPath(goldenDir), serialise(readRoutes(dir))], [HOST, hostPath(goldenDir), serialise(hostRoutes(dir))], [AUTH, authPath(goldenDir), serialise(authRoutes(dir))], [WRITES, writesPath(goldenDir), serialise(writeRoutes())], [GIT, gitPath(goldenDir), serialise(gitRoutes())]]) {
      if (!fs.existsSync(p)) { out.missing.push(name); continue; }
      const committed = fs.readFileSync(p, 'utf8');
      if (committed === fresh) out.matched.push(name);
      else out.drifted.push({ corpus: name, committedBytes: committed.length, freshBytes: fresh.length });
    }
  }

  if (!only && fs.existsSync(goldenDir)) {
    const named = new Set(corpora);
    for (const f of fs.readdirSync(goldenDir)) {
      if (!f.endsWith('.json')) continue;
      const corpus = f.replace(/\.json$/, '');
      if (!named.has(corpus)) out.extra.push(corpus);
    }
  }

  return out;
}

// ⚠️ `parseArgs` and `VALUE_FLAGS` are exported DELIBERATELY, and not for a
// caller — nothing requires this module. Exporting the parser is what puts this
// tool inside `kit.test.js`'s two parser populations: the forgotten-value gate and
// the positional-collision gate. This file reads `argv[i + 1]`, which is the
// property that CARRIES the defect those gates exist for, so the suite's second
// derivation would fail if the parser stayed private — correctly, because an
// unexported parser is an invisible opt-out [[your-written-exemption-is-a-work-item]].
//
// 🔴 `main` is deliberately NOT exported, and that absence is the structural
// protection in the header. `self-host.js`, `saturation.js` and `prose-audit.js`
// all export `main` so a test can drive their refusal — but every one of those is
// a tool whose write is the point. This one's write REBUILDS THE FIXTURE ITS OWN
// CHECK COMPARES AGAINST, so handing a test the ability to call it would recreate
// exactly the failure the file is shaped around. `main` is reachable only from
// `require.main === module`, so `require('./conformance.js').main` is `undefined`
// and no test can regenerate a golden however it is edited. The CI env-var guard
// is the belt; this is the braces, and it is the half to trust.
module.exports = { pipeline, readRoutes, routesPath, ROUTES, hostRoutes, hostPath, HOST, authRoutes, authPath, AUTH, writeRoutes, writesPath, WRITES, gitRoutes, gitPath, GIT, serialise, compare, corporaIn, goldenPath, withoutSymbols, pairs, delta, parseArgs, FORMAT, USAGE, KNOWN_FLAGS, VALUE_FLAGS };

// ── the CLI, which is the only thing that can write ─────────────────────────

function parseArgs(argv) {
  // An unknown flag is a refusal, never a silent drop (cli.js). A VALUE that
  // itself looks like a flag means the value was forgotten — `--dir --check`
  // must not point a gate at a path nobody named.
  const bad = cliRules.unknownFlag(argv, KNOWN_FLAGS);
  if (bad) return { error: `unknown option ${bad}` };

  const opts = { mode: null, dir: null, golden: null, only: null, help: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--help' || a === '-h') { opts.help = true; continue; }
    if (a === '--check' || a === '--record') {
      if (opts.mode && opts.mode !== a) return { error: '--check and --record ask for opposite things' };
      opts.mode = a;
      continue;
    }
    if (VALUE_FLAGS.has(a)) {
      const v = argv[i + 1];
      if (v === undefined || cliRules.looksLikeAFlag(v)) return { error: `${a} needs a value` };
      // Both branches compare the flag by name rather than one being an `else`.
      // Not style: `kit.test.js`'s "a guard cannot ADVERTISE a flag its own tool
      // no longer implements" gate reads the flags the CODE compares against, and
      // an `else` means `--golden` is advertised in KNOWN_FLAGS and compared
      // nowhere — indistinguishable from the flag having been removed.
      if (a === '--dir') opts.dir = v;
      else if (a === '--golden') opts.golden = v;
      i++;
      continue;
    }
    if (opts.only === null) { opts.only = a; continue; }
    return { error: `two corpus names given, "${opts.only}" and "${a}" — this reports on one` };
  }
  return opts;
}

function main(argv) {
  const opts = parseArgs(argv);
  if (opts.error) {
    process.stderr.write(`cannot look: ${opts.error}\n${USAGE}\n`);
    return 2;
  }
  // Help is answered before anything is read, so it works in a directory whose
  // corpus does not parse. Exit 0: asking for help is not an error.
  if (opts.help) { process.stdout.write(`${USAGE}\n`); return 0; }

  const dir = opts.dir || path.join(__dirname, 'behaviours');
  const goldenDir = opts.golden || path.join(__dirname, 'conformance');

  if (!fs.existsSync(dir)) {
    process.stderr.write(`cannot look: no behaviours directory at ${dir}\n`);
    return 2;
  }
  if (opts.only && !fs.existsSync(path.join(dir, `${opts.only}.beh`))) {
    process.stderr.write(`cannot look: no corpus "${opts.only}" in ${dir}\n`);
    return 2;
  }

  const mode = opts.mode || '--check';

  if (mode === '--record') {
    // The belt. The structural protection is that this branch is unreachable
    // from the module's exports — but a human running it in a CI shell would get
    // the fixture-generator failure, so say no out loud.
    if (process.env.CI) {
      process.stderr.write('cannot look: --record rewrites the goldens and CI is set.\n'
        + 'A regenerator that runs before its own comparison reports green over a real regression.\n');
      return 2;
    }
    fs.mkdirSync(goldenDir, { recursive: true });
    const written = [];
    for (const corpus of (opts.only ? [opts.only] : corporaIn(dir))) {
      fs.writeFileSync(goldenPath(goldenDir, corpus), serialise(pipeline(dir, corpus)));
      written.push(corpus);
    }
    if (!opts.only) {
      fs.mkdirSync(path.dirname(routesPath(goldenDir)), { recursive: true });
      fs.writeFileSync(routesPath(goldenDir), serialise(readRoutes(dir)));
      written.push(ROUTES);
      fs.writeFileSync(hostPath(goldenDir), serialise(hostRoutes(dir)));
      written.push(HOST);
      fs.writeFileSync(authPath(goldenDir), serialise(authRoutes(dir)));
      written.push(AUTH);
      fs.writeFileSync(writesPath(goldenDir), serialise(writeRoutes()));
      written.push(WRITES);
      fs.writeFileSync(gitPath(goldenDir), serialise(gitRoutes()));
      written.push(GIT);
    }
    process.stdout.write(`conformance --record: wrote ${written.length} golden(s) to ${path.relative(process.cwd(), goldenDir)}\n`);
    return 0;
  }

  const r = compare(dir, goldenDir, opts.only);

  // Three-valued, like check.js and kit.js: a refusal is never conflated with a
  // pass. "No golden on disk" is COULD NOT LOOK — reporting it as drift would
  // tell a first-time reader their engine is broken.
  if (r.missing.length) {
    process.stderr.write(`cannot look: no golden for ${r.missing.join(', ')} — run \`node conformance.js --record\`\n`);
    return 2;
  }
  if (r.extra.length) {
    process.stderr.write(`conformance --check: golden(s) with no corpus: ${r.extra.join(', ')}\n`
      + '  a renamed or deleted corpus leaves its golden behind, and the suite then proves\n'
      + '  the engine reproduces output for something that no longer exists.\n');
    return 1;
  }
  if (r.drifted.length) {
    process.stderr.write('conformance --check: the engine no longer reproduces its committed output.\n');
    for (const d of r.drifted) {
      process.stderr.write(`  ${d.corpus}: committed ${d.committedBytes} bytes, now ${d.freshBytes}\n`);
    }
    process.stderr.write('If the change was intended, `node conformance.js --record` and commit the diff —\n'
      + 'the diff IS the review, and it is the only place the behaviour change is visible.\n');
    return 1;
  }

  process.stdout.write(`conformance --check: ${r.matched.length} corpus/corpora still reproduce their committed output byte-for-byte.\n`);
  return 0;
}

if (require.main === module) process.exit(main(process.argv.slice(2)));
