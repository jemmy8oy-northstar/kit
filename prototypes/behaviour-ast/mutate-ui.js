#!/usr/bin/env node
'use strict';
/**
 * Does each rule in the UI have a test that FAILS without it?
 *
 * `mutate.js` asks that question of the ten plain-Node files. It has never
 * asked it of `ui/src`, because its subjects are read with `require` and its
 * suite is `node kit.test.js` — no install, no transform, in-process. The
 * frontend is neither: it needs `npm ci`, a JSX transform and a jsdom
 * environment, which is exactly why `ci.yml` keeps `ui` as a second job.
 *
 * So this is a sibling rather than a flag on `mutate.js`. Putting the two in
 * one file would put a dependency install in front of the prototype gate that
 * deliberately has none, and `node mutate.js` would start failing in the
 * `prototype` job for reasons that have nothing to do with the prototype.
 *
 * Everything about ~1,500 lines of `ui/src` was, until this file, backed only
 * by tests written in the same commit as the code they test. That is the
 * unbacked claim `mutate.js`'s own header exists to catch, on the half of the
 * repo that is now the product.
 *
 *   node prototypes/behaviour-ast/mutate-ui.js
 *
 * Exit 0 = every mutant killed. 1 = something survived (or a mutant is
 * invalid). 2 = could not look — deliberately not 0, the same convention
 * `check.js` and `selfhost/run.js` use.
 */
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const marker = require('./mutation-marker');

const UI = path.join(__dirname, 'ui');
const VITEST = path.join(UI, 'node_modules', '.bin', 'vitest');

// Recovery runs before the install check below: a tree left mutated by a killed
// run must be restorable even from a pod where `npm ci` has never been run.
if (process.argv.includes('--recover')) process.exit(marker.recover());

// ── could not look ───────────────────────────────────────────────────────────
// A mutation harness that silently reports "0 survived" because it never ran
// anything is worse than no harness: it reads identically to a clean run. The
// two ways that happens are an absent install and an absent suite, and both
// exit 2 rather than 0.
if (!fs.existsSync(VITEST)) {
  console.error('cannot look: ui/node_modules/.bin/vitest is missing — run `npm ci` in prototypes/behaviour-ast/ui first');
  process.exit(2);
}

// Before a single original is read from disk — see mutation-marker.js for why a
// stale marker has to refuse the run rather than be cleaned up.
marker.refuseIfStale('mutate-ui.js');

const SUBJECT_FILES = [
  'src/pages/Projects.tsx',
  'src/pages/Project.tsx',
  'src/pages/BehaviourPage.tsx',
  'src/components/Count.tsx',
  'src/components/CoverageBadge.tsx',
  'src/components/Resource.tsx',
  'src/components/WriteResultNote.tsx',
  'src/components/useWrite.ts',
  'src/hooks/useResource.ts',
  'src/api/client.ts',
  // kit#46. Added in the same commit as the component, which is the whole
  // reason it has to be here: 8 tests written beside the code they test prove
  // nothing until something breaks the code and watches them go red.
  'src/components/SignIn.tsx',
  // kit#49. App.tsx carried no rule worth mutating until rule 8 put the router's
  // `basename` in it — one prop whose absence is a page that draws correctly and
  // links out of the application.
  'src/App.tsx',
];
const SUBJECTS = {};
for (const f of SUBJECT_FILES) SUBJECTS[f] = fs.readFileSync(path.join(UI, f), 'utf8');
const restoreAll = () => {
  for (const [f, src] of Object.entries(SUBJECTS)) fs.writeFileSync(path.join(UI, f), src);
};

// Same danger as `mutate.js`, so the same signal in the same place, from the
// same module: for the duration of a run the files on disk are deliberately
// wrong, and anything that stages the tree in that window commits a mutant
// (claude-code-bot#92).
marker.arm({
  tool: 'mutate-ui.js',
  base: path.relative(path.join(__dirname, '..', '..'), UI),
  originals: SUBJECTS,
  warn: [
    'Do not commit, stage, or read prototypes/behaviour-ast/ui/src/** while this',
    'file exists — you will capture a mutant. It is removed when the run ends.',
  ].join('\n'),
  restoreAll,
});

const ANSI = /\[[0-9;]*m/g;

/**
 * Run the suite and say HOW it failed, not just whether.
 *
 * The distinction matters and is the one thing this runner does that
 * `mutate.js`'s does not need to. A `.tsx` mutant can break the esbuild
 * transform or the type-free parse instead of a test, and vitest exits
 * non-zero either way. Counting that as a kill would be counting a mutant the
 * suite never saw: it died at collection, one step before the assertions I am
 * trying to measure. So a run reports one of:
 *
 *   { failed: n }      n tests failed — a real kill, the suite noticed
 *   { failed: 0 }      green — the mutant SURVIVED
 *   { invalid: why }   the suite never ran the mutant (transform/collect error)
 *
 * `invalid` is not a kill and not a survivor. It means the mutant itself is
 * badly written and must be rewritten, and it is loud because an invalid
 * mutant counted as a kill inflates the score with a test that does not exist.
 */
const run = () => {
  let out;
  try {
    out = execFileSync(VITEST, ['run', '--reporter=basic'], {
      cwd: UI, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'],
      env: { ...process.env, CI: 'true', FORCE_COLOR: '0' },
    });
    // vitest exits 0 only when the whole suite is green.
    return { failed: 0, out };
  } catch (e) {
    out = (String(e.stdout || '') + String(e.stderr || '')).replace(ANSI, '');
    const m = /Tests\s+(\d+) failed/.exec(out);
    if (m) return { failed: Number(m[1]), out };
    // No "Tests N failed" line at all: vitest never got as far as reporting on
    // tests. Either it could not transform the mutated file, or it collected no
    // files. Both are "the mutant never reached the suite".
    const why = /Unhandled Error|Failed to load|Transform failed|No test files found|error TS\d+/.exec(out);
    if (why) return { invalid: why[0], out };
    // 🔴 THE UNMATCHED BRANCH IS A GENERIC FALLBACK, AND IT FIRED THREE TIMES
    // WITHOUT EVER SAYING WHY (kit#39). It means no cause downstream identified
    // itself, so it is the one branch that must carry the raw evidence rather
    // than a sentence describing the absence of evidence. `status` and `signal`
    // are what name the layer: a child the OOM killer took reports
    // `signal: 'SIGKILL', status: null` and is indistinguishable, in the old
    // message, from a dozen unrelated failures. The output tail is included
    // because the run that needs it has already ended by the time anyone reads.
    const how = e.signal ? `killed by ${e.signal}` : `exit ${e.status}`;
    const tail = out.trim().split('\n').slice(-3).join(' ⏎ ').slice(0, 300);
    return { invalid: `vitest ${how}, no test-failure line — ${tail || '(no output)'}`, out };
  }
};

// ── mutants ──────────────────────────────────────────────────────────────────
// Each entry: [name, from, to, file].
//
// `name` states the WRONG BEHAVIOUR A USER WOULD SEE, not the line being
// edited. That phrasing is the design rule, not a style: a survivor is only
// actionable if it names the damage that ships unnoticed. A mutant whose name
// is "removes the filter" tells you nothing about whether you care.
//
// ⚠️ Mutate the damage, not the guard. Deleting a `data-testid` makes every
// test that queries it go red, which looks like a kill and proves nothing
// except that the element was findable. Those are excluded on purpose.
const MUTANTS = [
  // ── unavailable is never zero ──────────────────────────────────────────────
  // ui.js's rule 4, carried onto the screen. "Nobody looked" and "nothing is
  // covered" are different sentences and the second is a lie about the app.
  ['an unmeasured project renders "0/0 covered", so "nobody looked" reads as "nothing is tested"',
    'if (!coverage.available) {', 'if (false) {', 'src/components/CoverageBadge.tsx'],
  ['a project reporting no coverage field at all is rendered as a count instead of "not measured"',
    'if (!coverage) {', 'if (false) {', 'src/components/CoverageBadge.tsx'],
  ['a partly-covered project gets the green badge, so the warning colour never appears',
    "tone={covered === total ? 'success' : 'warning'}", "tone={'success'}", 'src/components/CoverageBadge.tsx'],
  ['the badge stops counting the uncovered, so every project reads as fully covered',
    'const total = covered + count(coverage.uncovered)', 'const total = covered', 'src/components/CoverageBadge.tsx'],
  ['the detail endpoint\'s id array is concatenated instead of counted — "BEH-HOME-1BEH-EDIT-1 covered"',
    'return Array.isArray(value) ? value.length : value', 'return value as number', 'src/components/CoverageBadge.tsx'],
  ['a count of one is pluralised — "1 conflicts", from the tool whose pitch is that it noticed',
    '{n} {n === 1 ? one : (many ?? `${one}s`)}', '{n} {many ?? `${one}s`}', 'src/components/Count.tsx'],

  // ── the reload that must not blank the page ────────────────────────────────
  // The bug a real browser found and the mocked suite could not: every write
  // ends in reload(), and a reload that returns to `loading` unmounts the form
  // -- destroying the note saying which file was written and that Kit did not
  // commit it. Decision 2's guarantee is only a guarantee if he can watch it hold.
  ['a write\'s reload blanks the page, unmounting the form and destroying the note saying what was written',
    "setResource((prev) => (isReload && prev.state === 'ready' ? prev : { state: 'loading' }))",
    "setResource({ state: 'loading' })", 'src/hooks/useResource.ts'],
  ['a NAVIGATION is treated as a reload, so the previous project\'s behaviours sit under the new project\'s heading',
    'const isReload = key !== null && key === lastIdentity.current', 'const isReload = true', 'src/hooks/useResource.ts'],
  ['navigating away mid-request writes the old app\'s data into the new app\'s page',
    '      live = false', '', 'src/hooks/useResource.ts'],
  ['a request that resolves after you navigate away still overwrites the page',
    'if (live) setResource({ state: \'ready\', value })', "setResource({ state: 'ready', value })", 'src/hooks/useResource.ts'],
  ['a failed load renders as an empty page rather than the reason it failed',
    "setResource({ state: 'error', message })", "setResource({ state: 'ready', value: [] as T })", 'src/hooks/useResource.ts'],

  // ── a failed write must never look like a successful one ───────────────────
  ['a refused write is shown as success, so he believes a corpus edit landed that did not',
    "setWrite({ state: 'refused', message })", "setWrite({ state: 'wrote', result: {} as AnyWriteResult })", 'src/components/useWrite.ts'],
  ['a refused write silently returns the form to idle, with no sentence saying why the corpus refused it',
    "setWrite({ state: 'refused', message })", "setWrite({ state: 'idle' })", 'src/components/useWrite.ts'],
  ['the project is not re-read after a write, so his new step appears beside the test that predates it',
    '      onWrote()', '', 'src/components/useWrite.ts'],
  ['onWrote runs even when the write threw, re-reading the corpus as if the edit had landed',
    '      setWrite({ state: \'wrote\', result })\n      onWrote()',
    "      setWrite({ state: 'wrote', result })", 'src/components/useWrite.ts'],

  // ── the refusal sentence IS the feature ────────────────────────────────────
  // writer.js's four refusals are statements about the request ("a step is one
  // line", "that id is already taken"). Each tells him how to fix what he typed,
  // so a page that renders them as a status code throws the guidance away.
  ['a writer.js refusal reaches the screen as "Conflict" instead of the sentence saying why',
    '      reason = parsed.reason ?? parsed.error ?? reason', '', 'src/api/client.ts'],
  ['a read error loses the server\'s reason and shows only the status text',
    '      reason = body.reason ?? body.error ?? reason', '', 'src/api/client.ts'],
  ['an unreachable write API throws a bare network error instead of "Start it with `node ui.js`"',
    '      `Could not reach the Kit write API at ${path}. Start it with \\`node ui.js\\`.`,',
    '      `Failed to fetch`,', 'src/api/client.ts'],
  ['an empty note is omitted from the review request, so "he typed nothing" and "the field was not on the form" become the same request',
    '    { state, note },', '    { state },', 'src/api/client.ts'],
  ['an app whose name needs escaping breaks its own detail route',
    'return get<ProjectDetail>(`/api/projects/${encodeURIComponent(app)}`)',
    'return get<ProjectDetail>(`/api/projects/${app}`)', 'src/api/client.ts'],
  ['the bind request drops the app, so the server cannot say which OTHER corpora the binding now generates against',
    'return post<BindResult>(`/api/projects/${encodeURIComponent(app)}/bindings`, { noun, binding })',
    'return post<BindResult>(`/api/projects/bindings`, { noun, binding })', 'src/api/client.ts'],

  // ── decision 2's boundary, which is only a guarantee if he can watch it hold ─
  // ⚠️ RE-ANCHORED (kit#49). This pointed at a flat ternary,
  // `{result.committed ? 'Committed.' : 'Not committed — …'}`, which kit#45
  // replaced with a nested one when it added the commit-and-push rendering. The
  // anchor therefore stopped matching the moment that merged, and the rule went
  // UNMEASURED — which is exactly the failure `mutate.js`'s header is about, and
  // the reason an unmatched anchor is reported rather than skipped. Inverting the
  // condition says the same thing as swapping the branches did, and is anchored
  // on one short line instead of on the shape of the whole expression.
  ['a write that touched no git reports "Committed.", so the boundary decision 2 draws is invisible',
    '        {result.committed\n', '        {!result.committed\n',
    'src/components/WriteResultNote.tsx'],
  ['a bind that changed what OTHER corpora generate says nothing — the global namespace goes silent again',
    '{bind && bind.sharedWith.length > 0 && (', '{false && (', 'src/components/WriteResultNote.tsx'],
  ['"a corpus could not be parsed" is dropped, so "could not look" is shown as "nothing else uses it"',
    '{bind && bind.unreadableCorpora.length > 0 && (', '{false && (', 'src/components/WriteResultNote.tsx'],

  // ── the three states are deliberately not two ──────────────────────────────
  ['a resource still in flight renders as though it had arrived',
    "if (resource.state === 'loading') {", 'if (false) {', 'src/components/Resource.tsx'],
  ['a failed read renders as an empty page instead of the error the component exists to stop pages swallowing',
    "if (resource.state === 'error') {", 'if (false) {', 'src/components/Resource.tsx'],

  // ── the project list: what is real, what is a trial, what would not parse ──
  ['a corpus that will not parse is listed as an ordinary openable project showing zero behaviours',
    'if (project.error) {', 'if (false) {', 'src/pages/Projects.tsx'],
  ['an invented trial corpus is presented as one of his real projects',
    '{project.notReal ? (', '{false ? (', 'src/pages/Projects.tsx'],
  ['a second corpus for an app already listed loses the badge saying which app it duplicates',
    '{project.duplicateOf ? (', '{false ? (', 'src/pages/Projects.tsx'],

  // ── adjudication: an inference is not a decision until a human makes it ────
  ['every behaviour is shown as human-authored, so machine inferences are indistinguishable from what he wrote',
    "{behaviour.source.origin === 'inferred' && <Badge tone=\"warning\">inferred</Badge>}",
    '{false && <Badge tone="warning">inferred</Badge>}', 'src/pages/Project.tsx'],
  ['the unreviewed flag is inverted, so the queue points at exactly the behaviours already adjudicated',
    'unreviewed={unreviewed.has(b.id)}', 'unreviewed={!unreviewed.has(b.id)}', 'src/pages/Project.tsx'],
  ['a bare denial is submittable, and a denial with no correction is the one thing parse() must refuse',
    "disabled={saving || note.trim() === ''}", 'disabled={saving}', 'src/pages/BehaviourPage.tsx'],
  ['approving a behaviour sends whatever is sitting in the correction box as the note',
    "state === 'denied' ? note.trim() : null", 'note.trim()', 'src/pages/BehaviourPage.tsx'],
  ['a recorded correction from a past denial is hidden from the next adjudicator',
    '{behaviour.review.note && (', '{false && (', 'src/pages/BehaviourPage.tsx'],
  ['a new behaviour can be submitted with a blank id',
    "disabled={write.state === 'saving' || id.trim() === '' || title.trim() === ''}",
    "disabled={write.state === 'saving' || title.trim() === ''}", 'src/pages/Project.tsx'],
  // ⚠️ REMOVED, and the removal is the finding: a mutant reading
  //   '...(actor.trim() ? { actor: actor.trim() } : {}),' -> 'actor: actor.trim(),'
  // survived the first run under the name "a blank Actor field writes a literal
  // empty `actor ""` line into the corpus". **That damage cannot happen.**
  // `writer.js:229` is `if (opts.actor) out.push(...)`, and '' is falsy, so the
  // server drops the key regardless of what the client sends. The UI's guard is
  // defence-in-depth over a server guard and there is no observable difference.
  //
  // A survivor is a question, not a verdict, and the answer here was "the mutant
  // is wrong" rather than "a test is missing". Writing a test to kill it would
  // have pinned a defect that does not exist and made the score look better for
  // it [[an-equivalent-mutant-reports-survived]].

  // ── stage 4, the slice kit#32 shipped ─────────────────────────────────────
  ['the bind panel asks him to bind every unbound noun in the project, not the ones this behaviour names',
    '      .filter((n) => n.usedBy.includes(behaviourId))', '', 'src/pages/BehaviourPage.tsx'],
  ['a noun no step here names sorts to the FRONT, putting the least relevant requirement first',
    'return i === -1 ? Number.MAX_SAFE_INTEGER : i', 'return i === -1 ? -1 : i', 'src/pages/BehaviourPage.tsx'],
  ['"Kit generated nothing for this behaviour" and an empty generated test become the same screen',
    'if (!generated) {', 'if (false) {', 'src/pages/BehaviourPage.tsx'],

  // ── the sign-in gate (kit#46) ─────────────────────────────────────────────
  // The server-side lock is mutated in `mutate.js`. What only these can reach
  // is which of three states the PAGE decides it is in — and the two failures
  // that matter are both "shows the app when it should not", because a gate
  // that wrongly opens is the one nobody reports.
  ['a locked Kit renders the app anyway, so the gate is decoration',
    '  if (state.required && !state.signedIn) {', '  if (false) {', 'src/components/SignIn.tsx'],
  ['a LOCAL Kit is shown a password field, inventing a step that does not exist',
    '  if (state.required && !state.signedIn) {', '  if (!state.signedIn) {', 'src/components/SignIn.tsx'],
  ['the app renders for one frame before the server has answered, firing reads it cannot make',
    '  if (!state) {\n    return <p role="status">Checking…</p>\n  }', '  if (false) {\n    return <p role="status">Checking…</p>\n  }',
    'src/components/SignIn.tsx'],
  // A server we could not ask must not fall through to the app: the app then
  // makes read calls that also fail, and an unreachable server presents as an
  // empty corpus [[empty-means-two-things]].
  ['a server that could not be asked falls through to the app instead of saying so',
    '  if (error && !state) {', '  if (false) {', 'src/components/SignIn.tsx'],
  ['a wrong password is swallowed, so the form just clears and nothing explains why',
    '        setError(err instanceof ApiError ? err.message : \'Could not sign in\')', '        setError(null)',
    'src/components/SignIn.tsx'],
  ['an empty password is submittable, spending one of the throttle\'s five attempts on a stray tap',
    '        <button type="submit" disabled={busy || password.length === 0}>', '        <button type="submit" disabled={busy}>',
    'src/components/SignIn.tsx'],

  // ── the path Kit is served under (kit#49, rule 8) ───────────────────────────
  // Both of these describe the same outcome and it is the quietest one in the
  // estate: Kit deployed at `/kit` shares its host with four other apps behind an
  // ingress that does not rewrite, and **every unmatched path there answers 200
  // with the portfolio's SPA**. So neither failure 404s. The first loads a page
  // that cannot fetch anything; the second gives you a header link that silently
  // leaves Kit. Nothing on either side logs a thing
  // ([[green-over-the-clients-question]]).
  ['every fetch goes to the host root, where a sibling app answers 200 and the page loads nothing',
    "  return import.meta.env.BASE_URL.replace(/\\/$/, '') + path",
    '  return path',
    'src/api/client.ts'],
  // Not `basename={undefined}`: dropping the prop is what a careless edit
  // actually does, and it is the spelling that must go red.
  ['the router loses its basename, so every link in the header navigates out of Kit entirely',
    "      <Router basename={import.meta.env.BASE_URL.replace(/\\/$/, '') || '/'}>", '      <Router>',
    'src/App.tsx'],
  // 🔴 The defect a real browser found and the whole server-side suite could not.
  // react-router matches a basename by `startsWith`, so `/kit/` never matches the
  // location `/kit` — the URL he types — and the router renders NOTHING while the
  // shell, both hashed assets and every API call return 200. This mutant is the
  // bug, put back deliberately, so the test that caught it can never be deleted
  // without something going red.
  ['the basename keeps its trailing slash, so /kit renders a white page and every request still 200s',
    "      <Router basename={import.meta.env.BASE_URL.replace(/\\/$/, '') || '/'}>",
    '      <Router basename={import.meta.env.BASE_URL}>',
    'src/App.tsx'],
];

// ── --only filter ────────────────────────────────────────────────────────────
// Proves a small slice (e.g. one new component's mutants) without paying for
// the full ~70-minute run. Filters a DERIVED list only: MUTANTS itself,
// SUBJECT_FILES, SUBJECTS and restoreAll() are untouched, so restore still
// covers every subject file even when this run only mutates one of them.
const onlyArgIdx = process.argv.indexOf('--only');
const only = onlyArgIdx !== -1 ? process.argv[onlyArgIdx + 1] : null;
// `--only` with nothing after it, or followed by another flag, would otherwise
// leave `only` falsy and run the FULL suite while the operator believes they
// asked for a slice — the same "absent and empty read identically" failure the
// zero-match check below refuses ([[empty-means-two-things]]).
if (onlyArgIdx !== -1 && (only === undefined || only.startsWith('--'))) {
  console.error('cannot look: --only needs a substring to match, e.g. `--only SignIn.tsx`');
  process.exit(2);
}
const RUN_MUTANTS = only
  ? MUTANTS.filter(([name, , , file]) => name.includes(only) || file.includes(only))
  : MUTANTS;

// A filter that matches nothing is a typo, not an empty pass. Reading a 0/0
// run as success is exactly the "silently reports 0 survived because it never
// ran anything" failure this harness exists to catch (see the block above the
// vitest-presence check), so a bad --only exits 2 (could not look) rather
// than 0.
if (only && RUN_MUTANTS.length === 0) {
  console.error(`cannot look: --only ${JSON.stringify(only)} matched no mutants`);
  process.exit(2);
}

let killed = 0;
const survived = [];
const invalid = [];

for (const [name, from, to, file] of RUN_MUTANTS) {
  const original = SUBJECTS[file];
  // An anchor that stopped matching is a SURVIVOR, not a skip. A refactor that
  // moves the line silently stops the rule from being measured, and a skip
  // reports identically to a kill — the failure mode that makes a mutation
  // score worth nothing.
  if (original === undefined) { console.log(`  ⚠️  UNKNOWN SUBJECT ${file}  ${name}`); survived.push(name); continue; }
  if (!original.includes(from)) { console.log(`  ⚠️  ANCHOR MISSING  ${name}`); survived.push(name); continue; }
  // A `from` that appears twice mutates only the first and the mutant means
  // something other than what its name says — also a survivor, not a skip.
  if (original.split(from).length > 2) { console.log(`  ⚠️  ANCHOR AMBIGUOUS  ${name}`); survived.push(name); continue; }

  fs.writeFileSync(path.join(UI, file), original.replace(from, to));
  const r = run();

  // ── a kill is confirmed, a survival is not ───────────────────────────────
  // 🔴 THIS EXISTS BECAUSE THE HARNESS PRODUCED A FALSE KILL, and only running
  // it twice caught it. The first full run reported `killed (1 failing)` for
  // "every behaviour is shown as human-authored"; the second reported SURVIVED,
  // and five manual runs — on the second run's tree AND on a checkout of the
  // first run's exact tree and test population — agreed with the second. The
  // rule was genuinely untested and one run said it was covered.
  //
  // The asymmetry is deliberate and is the whole argument for the cost. A false
  // SURVIVOR wastes a few minutes: you investigate and find nothing. A false
  // KILL is invisible and says a rule is backed when nothing backs it — which
  // is the single thing this harness exists to detect, failing silently. So an
  // apparent kill is re-run, and only a kill that reproduces is counted.
  //
  // A disagreement is reported rather than resolved by a third run. Two runs
  // differing means the suite is not deterministic under mutation, and that is
  // a finding about the suite, not a number to average away.
  let confirm = null;
  if (!r.invalid && r.failed > 0) confirm = run();
  restoreAll();

  if (r.invalid) { invalid.push(name); console.log(`  ⚠️  INVALID MUTANT (${r.invalid})  ${name}`); }
  else if (r.failed > 0 && confirm.invalid) {
    invalid.push(name);
    console.log(`  ⚠️  NON-DETERMINISTIC (killed, then ${confirm.invalid})  ${name}`);
  } else if (r.failed > 0 && confirm.failed === 0) {
    // Counted as a SURVIVOR, not discarded: the evidence that it is killed did
    // not reproduce, so the rule is unbacked until something proves otherwise.
    survived.push(name);
    console.log(`  ⚠️  FALSE KILL (${r.failed} failing, then green)  ${name}`);
  } else if (r.failed > 0) { killed++; console.log(`  killed (${r.failed} failing)  ${name}`); }
  else { survived.push(name); console.log(`  SURVIVED             ${name}`); }
}

// A filtered run must never be readable as a full score: printing only
// "${killed}/${RUN_MUTANTS.length}" would look identical to a genuine
// suite-wide result whenever the filtered count happens to read cleanly.
// State the filter and the true MUTANTS.length alongside it so a filtered
// pass can never be mistaken for "the suite is proven".
if (only) {
  console.log(
    `\n${killed}/${RUN_MUTANTS.length} killed, ${survived.length} survived, ${invalid.length} invalid` +
      ` — FILTERED RUN (--only ${JSON.stringify(only)}) over ${RUN_MUTANTS.length} of ${MUTANTS.length} mutants` +
      ' total; this is NOT a score for the suite.',
  );
} else {
  console.log(`\n${killed}/${MUTANTS.length} killed, ${survived.length} survived, ${invalid.length} invalid`);
}

// Restoring from an in-memory copy can itself be the thing that is broken, so
// the run does not get to end on trust: the suite must be green again before
// this exits, or the tree is left in a state that looks passing and is not.
const after = run();
if (after.invalid || after.failed > 0) {
  console.error('HARNESS BROKEN: suite is not green after restore');
  process.exit(2);
}
process.exit(survived.length || invalid.length ? 1 : 0);
