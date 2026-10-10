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

## Run it

```
npm --prefix prototypes/behaviour-ast/ui ci
npm --prefix prototypes/behaviour-ast/ui run build
dotnet run --project backend/Balenthiran.Kit.WebApi --urls http://127.0.0.1:4321
```

Then open **http://127.0.0.1:4321**. The server is C# (kit#119); it serves the corpora under
`prototypes/behaviour-ast/behaviours` (or `KIT_DIR`) and the UI bundle built above. Settings are
environment variables — `backend/Balenthiran.Kit.WebApi/KitSettings.cs` lists them.

The engine is also a command line:

```
dotnet run --project backend/Balenthiran.Kit.Cli -- check kit --repo . --dir prototypes/behaviour-ast/behaviours
dotnet run --project backend/Balenthiran.Kit.Cli -- report snip-it --dir prototypes/behaviour-ast/behaviours
dotnet run --project backend/Balenthiran.Kit.Cli -- sheet james-habits-app --dir prototypes/behaviour-ast/behaviours
```

⚠️ **By default, Kit writes to your corpus and never commits.** Edits land in the working tree as an
ordinary diff for you to review. With no password set, writes are refused unless it is bound to
loopback, so the default is a local tool. **A deployed Kit with git write-back on commits each edit to
`kit/hosted`**, and the Commit button proposes them to `dev` as one pull request — nothing reaches `dev`
until that pull request is merged. See [`docs/design/ui.md`](docs/design/ui.md) decision 2.

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

⚠️ **The limit of that claim, stated up front: detection is _same-noun_ only.** A contradiction is a
`Map` collision on `kind:Name.slot`, so two behaviours only collide when they spell the noun
identically. Two readings of the same feature that name one control `checkbox:HabitDone` and
`checkbox:HabitItem` produce two keys, and Kit reports **no conflict** — silently, because there is
nothing for it to compare. Measured in [`docs/trials/habits-forward-run.md`](docs/trials/habits-forward-run.md):
two independent readings of the same brief agreed on **3 nouns out of 32**, and named the app's central
control three different ways.

**Kit does not fix this, by decision** — there is no canonical vocabulary and no reconciliation step,
and none is planned. The forward path does not need one: the requires panel emits a required-surface
contract that *dictates* the noun names to whoever implements it, so the builder never guesses. The
gap is real only when two corpora are written independently and then merged, which is not the
workflow Kit is for. Semantic conflict — two behaviours that contradict in meaning without colliding
on a slot — is a separate, later, model-shaped problem; see
[`docs/design/process.md`](docs/design/process.md) stage 3.

## The prototype

[`prototypes/behaviour-ast/`](prototypes/behaviour-ast/) — the only part of this that runs. It answers one
falsifiable question: *can a behaviour tree generate a runnable test with no hand-written glue?*

```
dotnet test ./backend                                   # the engine and server suite
npm --prefix prototypes/behaviour-ast/ui test           # the UI suite
```

The engine was first written in plain Node and ported to C# byte for byte (kit#119). The Node code
is deleted; what it produced is frozen in `prototypes/behaviour-ast/conformance/`, which the C#
tests are still scored against.

The UI over it: [`prototypes/behaviour-ast/ui/`](prototypes/behaviour-ast/ui/) — it reads a corpus,
shows the test Kit generates from each behaviour, and **writes new steps and behaviours back into
the `.beh` file**. By default it never commits, so you review its edits as an ordinary working-tree
diff. Deployed, it commits to `kit/hosted` and proposes the edits as a pull request — both decisions
in [`docs/design/ui.md`](docs/design/ui.md) were later overruled for the deployed case.

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
