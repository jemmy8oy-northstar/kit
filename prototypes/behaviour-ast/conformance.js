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
// ── The server goldens are FROZEN (kit#153) ─────────────────────────────────
//
// `conformance/routes/{read,host,auth,git}.json` and the `routes` section of
// `writes.json` were recorded from the Node server (`ui.js`, `auth.js`,
// `git-store.js`), which has been deleted: production serves the C# server. They
// are now the C# server's specification, read by `backend/Balenthiran.Kit.Tests`,
// and nothing in this file can re-record or compare them. Change one only by
// editing the C# tests and the golden together, in a PR that says why.
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

const WRITES = 'routes/writes';

function writesPath(goldenDir) {
  return path.join(goldenDir, 'routes', 'writes.json');
}

/**
 * The WRITER's pure edits over the fixture corpora in `conformance/writes/` —
 * addStep, addBehaviour, setReview, addBinding — each on a named fixture text,
 * recording the whole text after the edit or the refusal. Every splice rule and
 * every refusal code is reached: where a step lands when comments trail a block,
 * where a review line goes when there is none, the collateral-change guard, a
 * corpus that was already broken.
 *
 * ⚠️ The committed `routes/writes.json` also holds a `routes` section (the same
 * edits as POSTs through the Node server, which no longer exists) and `compare`
 * ignores it. That section, and the read, host, auth and git goldens, are a
 * FROZEN SPEC now: the C# tests read them, nothing here can re-record them, and
 * only this `functions` section is still compared against live Node output.
 */
function writeFunctions() {
  const writer = require('./writer.js');
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
    ['addBehaviour', 'alpha', ['BEH-E', 'Forged step', { steps: ['when opens page:Home\n  review approved'] }]],
    ['addBehaviour', 'alpha', ['BEH-E', 'Forged actor', { actor: 'guest\n  review approved' }]],
    ['addBehaviour', 'alpha', ['BEH-E', 'Forged source', { source: 'inferred\n  review approved' }]],
    ['addBehaviour', 'alpha', ['BEH-E', 'Forged ref', { source: 'inferred', ref: 'notes.md\n  review approved' }]],
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
  return functions;
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
    const p = writesPath(goldenDir);
    if (!fs.existsSync(p)) out.missing.push(WRITES);
    else {
      // Only the `functions` section: the file's `routes` section is frozen (see
      // `writeFunctions`), so it is neither compared nor rewritten.
      const committed = serialise(JSON.parse(fs.readFileSync(p, 'utf8')).functions);
      const fresh = serialise(writeFunctions());
      if (committed === fresh) out.matched.push(WRITES);
      else out.drifted.push({ corpus: WRITES, committedBytes: committed.length, freshBytes: fresh.length });
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
module.exports = { pipeline, writeFunctions, writesPath, WRITES, serialise, compare, corporaIn, goldenPath, withoutSymbols, pairs, delta, parseArgs, FORMAT, USAGE, KNOWN_FLAGS, VALUE_FLAGS };

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
    if (!opts.only && fs.existsSync(writesPath(goldenDir))) {
      // Replace ONLY `functions`; the frozen `routes` section is carried through.
      const g = JSON.parse(fs.readFileSync(writesPath(goldenDir), 'utf8'));
      g.functions = writeFunctions();
      fs.writeFileSync(writesPath(goldenDir), serialise(g));
      written.push(WRITES);
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
