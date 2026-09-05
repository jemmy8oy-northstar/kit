# The Kit UI — design

_Written 2026-09-05, against James's ask on
[claude-code-bot#89](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/89):_

> A UI where I can manage my projects (including kit) by the spec and then iterating on the
> output. Creating new assertions based on what I see and what the desired behaviour really is.
> … Obviously you will be working in dev so the product that you are testing will be theoretical
> right now. But also think about how kit looks as a kit managed project.

_He asked for documented planning before code on kit (#68) and has not withdrawn that. **This
document is planning. Two decisions in it are his, and both have a stated default I will act on if
he stays silent** — see the queue at the bottom._

---

## Why the UI is not cosmetic

`docs/pilots/kit-self-hosting.md` measured the second half of his ask. Kit can describe itself
completely — all ten behaviours in `behaviours/kit.beh` parse — and can **derive nothing** for
itself. Binding every noun the corpus mentions, as generously as the format allows, moves the
derived count from 0 to 0. The verbs are the wall: Kit's own behaviour needs `runs`, `exits`,
`reports`, `raises`, and the generator's entire vocabulary is `activates, attaches, fills, lands,
opens, sees, shows, state` — all browser verbs, because the emit target is Playwright.

So the two halves of #89 are one thing. **A UI is what makes Kit self-hosting**, because it gives
Kit a browser surface its own generator can address. Kit stops being a special case and becomes the
next app in its own registry.

## The loop he described

Four verbs, in his order:

1. **manage projects by the spec** — see a project's corpus: behaviours, what is covered, what
   conflicts, what was inferred and never reviewed.
2. **iterate on the output** — see the test Kit generates for a behaviour, next to the behaviour,
   and change the behaviour until the test is right.
3. **create new assertions based on what I see** — write a behaviour that did not exist.
4. **… and what the desired behaviour really is** — the corpus is the thing being edited, not a
   report about it.

Steps 3 and 4 are the load-bearing ones and they are the reason this needs a decision from him:
**nothing in Kit writes a `.beh` file today.** Every tool reads. Even the question sheet — the
artefact closest to this loop — ends with a human transcribing their own answer into the corpus by
hand. That is the gap the UI closes, and closing it changes what Kit is.

## What already exists to build on

Kit's exports are already the right shape for a read model; the UI needs no new analysis, only a
projection. From `kit.js`:

| export | returns | the panel it feeds |
|---|---|---|
| `parse` / `resolve` | `{behaviours, symbols, conflicts}` | the corpus list, and the conflict panel |
| `generate(b, bindings, symbols)` | `{code, missing[], stats}` | **the output pane** — his "iterating on the output" |
| `mapping` / `coverage` | `{covered, uncovered, errors[]}` | the coverage badge per behaviour |
| `adjudication` | `{defined, inferred, unreviewed[], approved[], denied[], untraceable[]}` | the review queue |
| `questions` + `renderSheet` | the decision/review packs | **the sheet, made clickable** |

Five corpora exist (`james-habits-app` 23, `language-vocab` 27, `macro-metrics` 10, `snip-it` 8,
`kit` 10) — enough real material that the UI has something to show on day one rather than a fixture.

## Decision 1 — where does it run?

**This is a platform choice, so it is his** (claude-code-bot#58). The two shapes are not variations
on one design; they are different products.

**Option A — a local tool.** `node ui.js` serves `localhost:PORT`, reads corpora from the working
tree, and runs checks against sibling clones in `/data/repos`. No cluster, no ingress, no auth, no
secrets, nothing that costs money, nothing public-facing. It is a developer's instrument, opened
when working on a spec and closed after.

**Option B — a deployed app**, in the cluster alongside the other five. He can open it on his phone.
But it must then read *other people's repositories* server-side, which means a GitHub App token in
the cluster, an auth story so it is not world-writable, and a clone/cache layer. Every one of those
is a secrets-and-platform change that is **never mine** under CLAUDE.md, so option B cannot be
built during his absence even if he prefers it.

**Default if he is silent: A.** It is the only one buildable in the window, and it is not a
throwaway — the read model, the panels and the components are identical under B; only the transport
and the auth differ. **A is the option-invariant half of B.**

## Decision 2 — does the UI write the corpus, and how far does the write go?

His "creating new assertions" only means something if the answer is yes. The question is where it
stops.

**Option A — propose only.** The UI shows the exact corpus edit and the human pastes it. This is
today's sheet with better ergonomics. Safe, and it keeps the human as the writer — but it is also
the thing that already exists, so it may not be worth a UI at all.

**Option B — write the file, never touch git.** The UI edits `behaviours/<app>.beh` on disk. The
human sees the change as a normal working-tree diff, reviews it, and commits it themselves. Kit
gains a writer; git stays the review surface it already is.

**Option C — write and commit, or open a PR.** The full loop, and the point at which Kit is
committing to his repositories on his behalf.

**Default if he is silent: B.** It gives him the loop he described, and it stops exactly where my
own ground rules stop — I do not commit on his behalf without a PR, and neither should a tool I
write. C can be added later without rework; going straight to C cannot be undone quietly.

⚠️ **B has a real cost and it should be stated rather than discovered:** a corpus is currently a
hand-authored document with comments explaining *why* each behaviour exists, and a program that
rewrites it will not preserve that prose unless it is built to. **The writer must be a surgical
edit — insert or replace one behaviour block — not a re-serialisation of the AST.** A round-trip
through `parse()` and back would silently delete every comment in `snip-it.beh`, which is where its
most important caveats live.

## Stack

Follow `web-template`, so this is not a third way of building a frontend: React 19, Vite,
react-router 7. Skip Redux/RTK Query — there is no OpenAPI backend to generate a client from under
option A.

**Identity is not mine to invent.** Use `@jemmy8oy-northstar/design-system` (his call, 2026-08-16:
*"Coral teal looks good"*), whose `Button`, `Card`, `Badge` and `Input` cover most of what the panels
need.

⚠️ **`design-system#11` merged on 2026-09-05, and it does not solve this on its own.** Measured on
`origin/dev` that morning, not assumed:

- `#11` added `"./tokens/*": "./src/tokens/*"` to `exports`, and `src/tokens` to `files`. That
  exposes **seven raw CSS files** — `base.css`, `index.css`, `primitives.css`, `semantic.css` and
  three themes under `themes/`. There is no JS/TS token object; it is CSS custom properties.
- **The package is still published nowhere.** `.github/workflows` on `origin/dev` contains
  `ci.yml` and `check-source-branch.yml` — **no publish workflow** — and `package.json` has no
  `publishConfig`. So `npm i @jemmy8oy-northstar/design-system` cannot work; the only ways in are a
  **git-URL dependency** or vendoring.
- `origin/main..origin/dev` differs by **`package.json` alone**, so the token files are on both
  branches but the `exports` map is dev-only. **A consumer pinning `main` gets the files and not the
  export that names them** — pin `dev`, or pin a commit.

⇒ **The Kit UI takes a git-URL dependency on `dev`.** That is not a workaround to regret: it costs
one line in `package.json`, it consumes the same files a published package would, and it is the only
option that does not re-create the duplication `#11` exists to end — snip-it still vendors 603 lines.
**Publishing the package properly is a packaging change and therefore his** (claude-code-bot#83);
this document does not decide it, and the UI must not be blocked on it.

## Sequence

1. **The read model** — one command emitting the whole projection as JSON. It is needed under every
   option above, so it is the option-invariant half and it can be built before either decision
   lands ([[option-invariant-half]]).
2. The read-only UI over that projection: corpus list → behaviour detail → generated output.
3. The writer, per decision 2.
4. **Kit's own corpus rewritten in browser verbs**, at which point `kit check kit` gates a UI
   Kit generated tests for, and the self-hosting claim is real rather than argued.

## What this document does not decide

Anything about what the UI is *for* beyond his four verbs. In particular: whether it manages
**projects** (many repos, one place) or **one corpus at a time** is left open, because his sentence
says "my projects" and every existing tool in Kit takes a single app. That is a product question,
not an engineering one, and guessing it would shape everything downstream.
