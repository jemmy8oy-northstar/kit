# The Kit UI — read-only

Step 2 of the sequence in [`docs/design/ui.md`](../../../docs/design/ui.md): a
frontend over `ui.js`'s read API. Corpus list → behaviour detail → the test Kit
generates from it.

```sh
node prototypes/behaviour-ast/ui.js --repos /data/repos   # the read API, :4321
npm --prefix prototypes/behaviour-ast/ui install
npm --prefix prototypes/behaviour-ast/ui run dev          # the UI, :5173
```

`--repos` is what makes coverage available; without it every project reports
**not measured**, which is not the same as zero and is not rendered as zero.

## What it deliberately does not do

**It cannot write.** Decision 2 in the design doc — whether the UI edits the
corpus, and how far the edit goes — is James's and is open. `ui.js` returns 405
for every verb but GET, so there is no path from this app to a `.beh` file even
if someone adds a `fetch` in a hurry.

**It sets no `base`.** web-template's template pins `base: '/your-app-name/'`
because it deploys under a sub-path. Decision 1 — local tool or deployed app —
is also open, and a sub-path in a config file would be answering it.

**`ui.js` does not serve this bundle.** In development, Vite proxies `/api` to
the read API. Serving the built assets from `ui.js` is a small change and is
needed under either option in decision 1, but it means joining a path to serve a
file, and `ui.js` has a rule against joining paths that is worth keeping intact
until there is a reason to touch it.

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
