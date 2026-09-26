# Trial — is what Kit *generates* honest?

_Run 2026-09-26 against `dev` @ `f805eb6`. Method: two agents with no prior
context, in throwaway clones with `docs/trials/` deleted, forbidden from fixing
anything ("a fix destroys the measurement") and from `mutate*` / `start.js`._

## Why this surface

Every previous trial measured Kit's **inputs**: authoring a corpus
(`someone-else-authors-a-corpus.md`, the habits/lend/longlist forward runs) or
the quality of a **refusal** (`does-kit-refuse-or-guess.md`). All of them ask what
happens when a user gets something wrong.

Nobody had measured what happens when the user gets everything **right** — when
Kit succeeds and prints. That is the output a user sees ~99% of the time, and it
is the product. So: *is what Kit prints true, and can it mislead you?*

## Headline

**Kit's content is accurate; Kit's reporting is not.**

Of 13 concrete selector/route/API claims checked against snip-it's real source,
**13 were TRUE** — every button name, route and payload contract the generator
emitted exists exactly as written. There are no invented facts.

But in three separate places the surrounding report says something the corpus
does not support, and in each case the error is in the direction that makes a
corpus look **better tested than it is**.

## Findings I reproduced myself, with the population measured

Everything in this section was re-measured by the lead session on `dev` @
`f805eb6`, independently of the agents. Probes are named so each is re-runnable.

### 1. 8 of 139 generated tests have an empty body

A behaviour with no `when`/`then` steps still generates a **named test with an
empty body**. It is counted as a test, reads as coverage, and passes
unconditionally.

```
$ node kit.js snip-it
test("[BEH-EDIT-0] The editor addresses one transcription by id", async ({ page }) => {
});
```

Measured across **every** corpus rather than the one the trial landed on:

| corpus | tests | empty | ids |
|---|---|---|---|
| james-habits-app | 23 | 2 | BEH-WINDOW-MVP, BEH-WINDOW-API |
| language-vocab | 27 | 4 | BEH-GRADE-3, BEH-STRENGTH-1, BEH-UNLOCK-2, BEH-SEED-2 |
| macro-metrics | 10 | 1 | BEH-MM-METRICS-1 |
| snip-it | 8 | 1 | BEH-EDIT-0 |
| kit, kit-ui, longlist, trial-habits-a/b, trial-lend | 61 | 0 | — |
| **total** | **139** | **8** | |

**Two distinct causes, not one.** `provides`-only behaviours that exist to supply
a value (`BEH-EDIT-0`, `BEH-WINDOW-MVP`, `BEH-MM-METRICS-1`) and `serves`-only
inferred behaviours derived from backend unit tests, which have no browser steps
by construction (`BEH-GRADE-3`, `BEH-SEED-2`).

This is the one that most directly contradicts Kit's stated premise. Refusing to
generate would be honest; generating something that always passes is the one
outcome the premise exists to prevent.

### 2. 40 of 139 behaviours are flagged `⚠️ UNTRACEABLE` for the condition the README calls normal

`README.md` (lines 69–72) states the rule:

> Silence is asymmetric on purpose. **No `source` means a human wrote it**; no
> `review` on an inference means unreviewed; no `serves` on an inference is the
> finding, and no `serves` on a defined behaviour is normal — a documented
> behaviour is served, it does not serve.

`kit.js` reports that same silence as a warning:

```
⚠️  UNTRACEABLE        8   no source ref: BEH-HOME-1, BEH-EDIT-1, … BEH-EDIT-0
```

| corpus | behaviours | flagged |
|---|---|---|
| kit | 26 | 26 (100%) |
| kit-ui | 6 | 6 (100%) |
| snip-it | 8 | 8 (100%) |
| the other seven | 99 | 0 |
| **total** | **139** | **40 (29%)** |

It fires on **100% of three corpora, including both of Kit's own
self-descriptions** — Kit warns that its own specification is untraceable, on
every line of it. A warning that never varies within a corpus carries no
information, and teaches the reader to skip warnings generally.

Either the README's rule or the report's warning is wrong. They cannot both be
right, and which one changes is a call about what the report *means*.

### 3. A generated test can be green while the app is broken

`BEH-CUT-1` has a step Kit cannot generate. It downgrades that step to a comment
and **still emits the rest of the test**:

```js
test("[BEH-CUT-1] Sending for export submits a cut and surfaces the finished job", …) => {
  …
  test.info().annotations.push({ type: "kit-ungenerated", description: "then shows region:CutStatus \"is complete\"" });
  // UNGENERATED: then shows region:CutStatus "is complete"
  await expect(page.getByRole("link", { name: "Download" })).toBeVisible();
});
```

The dropped assertion is the only one covering the status display. Verified in
snip-it's real source — `frontend/src/features/transcript-editor/components/TranscriptEditorPage.tsx:186-187`:

```jsx
Cut job <code>{cutJob.id}</code> is {describeJobStatus(cutJob.status)}.{' '}
{cutJob.downloadUrl && <a href={cutJob.downloadUrl}>Download</a>}
```

The `Download` link is governed by `cutJob.downloadUrl`; the status text by
`describeJobStatus(cutJob.status)`. **They are independent fields.** So
`describeJobStatus` can be completely broken and this test still passes — and it
passes having *named* the behaviour whose status display it no longer checks.

The `kit-ungenerated` annotation is queryable by a reporter, which is better than
nothing. The sibling `CONTRACT (not derivable from a behaviour)` markers — six of
them in snip-it alone, including the cut-request payload shape — are bare
comments that no reporter can see.

### 4. `node kit.js <corpus>` does not print a runnable file

The output references `test`, `expect` and `mockApi` and emits no `import` for
any of them (`grep -cE '^import ' → 0`). So the obvious user move,
`node kit.js snip-it > my.spec.ts`, produces a file that cannot run. The trailing
adjudication/measurement prose goes to the same stream, so it cannot be piped
either.

⚠️ **Correction to the agents' framing, which overstated this.** Kit *does* have a
spec assembler and it *does* emit the import — `selfhost/run.js:65` builds
`lines` starting with `import { expect, test } from '@playwright/test';` and
appends each `kit.generate()` fragment. The gap is not a missing import; it is
that the assembler lives inside the self-host harness and **no user-facing
command exposes it**. `kit.js` prints fragments by design; nothing turns them
into a file.

⚠️ A comment at `selfhost/run.js:235` says *"The GENERATED spec's own first line
is `import … from '@playwright/test'`, and Kit is right to emit it"* — but the
generator does not emit it; line 65 of that same file does. The prose describes
an arrangement the code does not have.

## Reported by the agents, NOT yet reproduced by me

Kept separate on purpose. These are credible and specific, but no control of mine
has confirmed them, so they are claims rather than facts:

- Three tools give three different total-noun counts for the same corpus and run
  (17 / 17 / 16); `form:Upload` is dropped from `requires.js`'s population
  without explanation, and `kit.js`'s "14/17 bound" does not reconcile with its
  own two-item unbound list. (Related open question: `kit#76`.)
- `measure-tagging.js` reports 66 C# test titles for snip-it where a direct
  `[Fact]`/`[Theory]` grep finds 65 — off by one. The non-C# half of the same
  claim (68) reproduces exactly.
- `compare.js`'s final summary pools three unrelated corpora, making snip-it's
  ~89% line fidelity read as ~30%. (This is `kit#78`, already open and his.)
- `check.js` does not gate on generation gaps: it flagged two behaviours on
  traceability while staying silent on the two that actually carry
  `UNGENERATED`/`CONTRACT` holes.

## Actionability, as scored by the blind agents

Of 14 scorable outputs: **8 ACTIONABLE, 5 PARTIAL, 1 OPAQUE**. The generated
tests, `sheet`, `requires.js` and `measure-tagging.js`'s option tables were
usable without reading source. The adjudication block, the `displayed surface`
0/0 block and the pooled `compare.js` summary needed manual reconstruction.

Truth scoring on the report surface: **9 TRUE, 1 FALSE, 1 UNVERIFIABLE** of 11
claims checked.

## What the method cost, and what it found

Two agents, ~15 minutes wall clock, ~210k tokens. Four reproduced defects, three
of them invisible to all ten corpora, three trials and every gate in CI — because
every one of those measures whether Kit *runs*, and none asks whether what it
prints is true.

⚠️ **One agent's headline claim was wrong in its framing** (finding 4) and was
caught only because the lead session re-derived it from source. The split — the
agent reports, the author reproduces — is doing real work and must not be
dropped.

## Environment limits on this run, which are not Kit findings

`check.js`, `bindings.js <corpus>`, `project.js <corpus>` and
`requires.js --check` were refused by the sandbox's own permission layer before
executing. That is a limit on this trial's coverage, not evidence about Kit.
