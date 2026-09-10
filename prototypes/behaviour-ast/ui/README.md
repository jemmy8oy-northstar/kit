# The Kit UI

The loop James described on
[claude-code-bot#89](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/89), all four
verbs of it: corpus list → behaviour detail → the test Kit generates from it →
**a form that writes a new step or a new behaviour back into the `.beh` file**.

Adding a step re-reads the project, so the regenerated test is what you are
looking at when you decide what to assert next. That is the whole point of the
two panes being on one screen.

```sh
npm --prefix prototypes/behaviour-ast/ui ci                # once
npm --prefix prototypes/behaviour-ast/ui run build         # once, and after any UI change
node prototypes/behaviour-ast/ui.js --repos /data/repos    # http://127.0.0.1:4321
```

**One process, one port, one URL.** `ui.js` serves the API *and* the built
bundle out of `ui/dist`. If you have not built it, the page says so and prints
the command — the API keeps answering meanwhile, because "the bundle is not
built" and "the server is broken" are two states.

`--repos` is what makes coverage available; without it every project reports
**not measured**, which is not the same as zero and is not rendered as zero.

While iterating on the UI *itself*, run Vite instead of rebuilding on every
save — it proxies `/api` to the server above:

```sh
npm --prefix prototypes/behaviour-ast/ui run dev          # :5173, hot reload
```

## What it deliberately does not do

**It does not commit.** Decision 2 landed on its stated default: the write stops
at the working tree. Kit edits `behaviours/<app>.beh` and there is no path from
here to git — `writer.js` has a test asserting it cannot even `require`
`child_process`. Every successful write says which file changed and that nothing
was committed, because a boundary you cannot watch hold is not a guarantee.

**It does not know the grammar.** The step field takes the line exactly as it is
typed into the corpus. A verb dropdown and a noun picker here would be a *second*
definition of what a step is; `writer.js` refuses to hold one, validating instead
by re-parsing the whole file with `kit.js`. One grammar, in one place. A picker
can be added over a working loop later; a second grammar cannot be removed from
one.

**It does not let a machine claim a human wrote something.** A behaviour added
here is `source inferred`, which `parse()` marks `unreviewed`, and the form
offers no way to change that (James, claude-code-bot#68: *"I like this default
included but marked unreviewed"*). Silence in a corpus means a person wrote it,
and a writer must not be able to spend that.

**It sets no `base`.** web-template's template pins `base: '/your-app-name/'`
because it deploys under a sub-path. Decision 1 landed on **local tool**, so
there is no sub-path to pin.

**It does not join a path to serve a file.** This section used to say `ui.js`
did not serve the bundle at all, and gave that as the reason: serving files
means joining a request path to a directory, and `ui.js` has a rule against
that. The reason was right and the conclusion was not — the rule already had an
answer. App names are **looked up** in the corpus listing rather than joined,
and bundled files are now looked up the same way, in `readdirSync` of
`ui/dist/assets`. No request path is ever joined to a directory, and the SPA
fallback serves one constant file.

**It does not answer 200 for something that is not there.** A path that names a
file and is not in the bundle is a 404, and `/api/…` never falls back to the
shell. An unmatched path answering 200 with the app shell is how a status check
stops being able to fail — every unmatched path on balenthiran.co.uk did
exactly that for the life of the site.

## Identity

`@jemmy8oy-northstar/design-system`, on a **git-URL dependency on `dev`**. The
package is published to no registry, and it does not need to be: its
`package.json` has `"prepare": "npm run build"`, and npm runs `prepare` for a git
dependency — so `dist/` (the components, the types and the compiled stylesheet)
is built at install time. Measured, not assumed:

```
npm i github:jemmy8oy-northstar/design-system#dev
→ node_modules/@jemmy8oy-northstar/design-system/dist/{index.js,index.d.ts,design-system.css}
→ exports: Badge, Button, Card, Input, cn
```

`data-theme="casual"` in `index.html` is what switches the colour roles on —
without it every `--color-*` role is unset. "casual" is the coral/teal voice
James picked on 2026-08-16, not a preference of this app's.

Every rule in `src/index.css` is layout; every colour, radius and size is a
design-system token. That is the package's own hard rule for consumers.

## Tests

`npm --prefix prototypes/behaviour-ast/ui test` — Vitest + React Testing
Library, jsdom, no browser.

The fixtures under `src/test/fixtures/` are **real output from `ui.js`**, and
`src/test/fixtures.test.ts` requires `ui.js` and compares against them on every
run. A UI suite over hand-written fixtures proves the fixtures; this is what
stops these drifting from the server they came from. If it fails, the API
changed: regenerate the fixtures, read the diff, and fix the components the diff
implicates — do not edit a fixture to match a component.

The assertions worth keeping are the ones about **two states that look like
one**: unavailable coverage vs zero coverage, a corpus that will not parse vs a
project with no behaviours, a server that is not running vs an empty list, and
"Kit generated nothing" vs an empty test. Each of those was mutated by hand and
watched go red.
