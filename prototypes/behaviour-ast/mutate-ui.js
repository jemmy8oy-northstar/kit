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

const UI = path.join(__dirname, 'ui');
const VITEST = path.join(UI, 'node_modules', '.bin', 'vitest');

// ── could not look ───────────────────────────────────────────────────────────
// A mutation harness that silently reports "0 survived" because it never ran
// anything is worse than no harness: it reads identically to a clean run. The
// two ways that happens are an absent install and an absent suite, and both
// exit 2 rather than 0.
if (!fs.existsSync(VITEST)) {
  console.error('cannot look: ui/node_modules/.bin/vitest is missing — run `npm ci` in prototypes/behaviour-ast/ui first');
  process.exit(2);
}

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
];
const SUBJECTS = {};
for (const f of SUBJECT_FILES) SUBJECTS[f] = fs.readFileSync(path.join(UI, f), 'utf8');
const restoreAll = () => {
  for (const [f, src] of Object.entries(SUBJECTS)) fs.writeFileSync(path.join(UI, f), src);
};

// Same danger as `mutate.js`, so the same signal in the same place: for the
// duration of a run the files on disk are deliberately wrong, and anything that
// stages the tree in that window commits a mutant (claude-code-bot#92). The
// marker is untracked and at the repo root so `git status` prints it as `??`
// right next to the files you were about to stage.
const MARKER = path.join(__dirname, '..', '..', 'MUTATION-IN-PROGRESS');
const dropMarker = () => { try { fs.unlinkSync(MARKER); } catch { /* already gone */ } };
fs.writeFileSync(MARKER, [
  'mutate-ui.js is running and the working tree is deliberately WRONG.',
  '',
  'Do not commit, stage, or read prototypes/behaviour-ast/ui/src/** while this',
  'file exists — you will capture a mutant. It is removed when the run ends.',
  '',
  `started ${new Date().toISOString()} by pid ${process.pid}`,
  '',
].join('\n'));
process.on('exit', dropMarker);
process.on('SIGINT', () => { restoreAll(); dropMarker(); process.exit(130); });
process.on('SIGTERM', () => { restoreAll(); dropMarker(); process.exit(143); });

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
    return { invalid: why ? why[0] : `vitest exited non-zero with no test-failure line`, out };
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
  ['a write that touched no git reports "Committed.", so the boundary decision 2 draws is invisible',
    "{result.committed ? 'Committed.' : 'Not committed — Kit does not run git.'}",
    "{result.committed ? 'Not committed — Kit does not run git.' : 'Committed.'}",
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
];

let killed = 0;
const survived = [];
const invalid = [];

for (const [name, from, to, file] of MUTANTS) {
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
  restoreAll();

  if (r.invalid) { invalid.push(name); console.log(`  ⚠️  INVALID MUTANT (${r.invalid})  ${name}`); }
  else if (r.failed > 0) { killed++; console.log(`  killed (${r.failed} failing)  ${name}`); }
  else { survived.push(name); console.log(`  SURVIVED             ${name}`); }
}

console.log(`\n${killed}/${MUTANTS.length} killed, ${survived.length} survived, ${invalid.length} invalid`);

// Restoring from an in-memory copy can itself be the thing that is broken, so
// the run does not get to end on trust: the suite must be green again before
// this exits, or the tree is left in a state that looks passing and is not.
const after = run();
if (after.invalid || after.failed > 0) {
  console.error('HARNESS BROKEN: suite is not green after restore');
  process.exit(2);
}
process.exit(survived.length || invalid.length ? 1 : 0);
