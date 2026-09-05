# Kit

> **Documented planning before code** — James, [claude-code-bot#68](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/68):
> *"I think the place to start is planning. No coding yet. Documented planning."* That rule still
> holds: every piece of code here was designed in a document first, and the documents are the
> reading order below.
>
> He then asked for a UI ([claude-code-bot#89](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/89)):
> *"a UI where I can manage my projects (including kit) by the spec and then iterating on the
> output"* — designed in [`docs/design/ui.md`](docs/design/ui.md), built in
> [`prototypes/behaviour-ast/ui/`](prototypes/behaviour-ast/ui/).

**Kit** (short for MakeIt; also ToolKit; also a kitten that helps you code) replaces
`source code → application build` with **`user-defined behaviours → application build`**.

A behaviour corpus is the artefact a human touches. It stays authoritative: unknowns are first-class,
contradictions are detected and adjudicated, nouns bind to the app's vocabulary, and a behaviour with
no test naming it **fails the build**.

## Where to start reading

| | Document | What it answers |
|---|---|---|
| 1 | [`docs/analysis/strategic-position.md`](docs/analysis/strategic-position.md) | Is this worth building, and is it still differentiated? **Read this one first.** |
| 2 | [`docs/design/process.md`](docs/design/process.md) | The loop, with no UI and no product in it |
| 3 | [`docs/timeline.md`](docs/timeline.md) | Rough staging, gated rather than dated |
| 4 | [`docs/analysis/what-we-can-leverage.md`](docs/analysis/what-we-can-leverage.md) | What already exists in our estate, and what's missing |
| 5 | [`docs/research/competitive-landscape.md`](docs/research/competitive-landscape.md) | Lovable, Kiro, Tessl, Spec Kit and the rest |
| 6 | [`docs/research/bdd-prior-art.md`](docs/research/bdd-prior-art.md) | Why BDD never became the default — the graveyard |

## The one thing to know

**"Spec before code" stopped being a differentiator on 17 November 2025**, when AWS Kiro went GA doing
exactly that and GitHub Spec Kit passed 120K stars. What nobody ships is the *second* half: **automated
contradiction detection over an accumulated spec corpus, with a supersede decision recorded so it is
never re-litigated.** That is Kit.

## The prototype

[`prototypes/behaviour-ast/`](prototypes/behaviour-ast/) — the only part of this that runs. It answers one
falsifiable question: *can a behaviour tree generate a runnable test with no hand-written glue?*

```
node prototypes/behaviour-ast/kit.js        # generated tests + measurements
node prototypes/behaviour-ast/kit.test.js   # 139 tests
node prototypes/behaviour-ast/ui.js         # the read API the UI runs on
```

The UI over it: [`prototypes/behaviour-ast/ui/`](prototypes/behaviour-ast/ui/) — read-only, because
whether Kit may *write* a corpus is an open decision at the foot of
[`docs/design/ui.md`](docs/design/ui.md).

Measured against snip-it's real `editor.spec.ts`: **8 behaviours → 28 generated lines, 22 byte-identical
to lines a person actually wrote**, 3 more present but reflowed. 6 wire contracts are **refused and still
counted** — dropping them would flatter the coverage number by deleting the steps that fail.

## Repo setup

This repo was created from `repo-template`. Its original README — CI workflows, the two branch-protection
rulesets, the secrets the Docker build needs — is kept verbatim at
[`docs/repo-setup.md`](docs/repo-setup.md). **The rulesets are not applied yet.**

## Status

Planning. Nothing here is a commitment. The open decisions are at the foot of
[`docs/design/process.md`](docs/design/process.md) and they are James's, not the bot's.
