---
tags: [kit, trial, ui, findings]
updated: 2026-09-26
status: an account of using Kit, not a claim about it
---

# What Kit looks like in a browser

_2026-09-26. A standing review finding says **nobody has run Kit, James included**. Three trials
already exist and **not one of them opened the UI**: [someone-else-authors-a-corpus](someone-else-authors-a-corpus.md)
drove authoring, [does-kit-refuse-or-guess](does-kit-refuse-or-guess.md) drove refusing, and
[is-what-kit-generates-honest](is-what-kit-generates-honest.md) drove the report. So this one drives
the only surface James would actually touch: the browser._

Everything below ran on `dev` at `3c1a295`, served by `node start.js`, driven with Chromium 1228
(`playwright-core` 1.61.1) at **390x844 and 1280x800**. Two people looked: me, and a **second agent
given no context about Kit, working in a throwaway clone, forbidden from fixing anything** — the
split that found six defects on 2026-09-26 that ten corpora and three trials had missed.

## The finding, in one line

**The UI works, it is genuinely good, and it is legible on a phone — and the screen `kit-ui.beh`
promises as its second behaviour is one that 8 of Kit's 10 projects never show.**

## 1. It works, and that is the news

`node start.js` installed, built and served in about 30 seconds, exactly as the README says. Then:

| measured | result |
|---|---|
| `page.on('pageerror')` across a whole session | **fired zero times** |
| `page.on('console')` errors on any real project or the home page | **none** (the only two were a deliberate `/projects/does-not-exist-xyz`, handled in-page) |
| failed requests | **none** |
| legible at 390x844 | **yes** — single-column cards, no overlap, no clipping, badges wrap |

Worth stating plainly, because every previous trial measured a way Kit falls short.

⚠️ **This is not the first time the UI has been opened, and an earlier draft of this page said it
was.** `1e1de7f` and `5ad0492` both captured real screenshots of it for the docs — including its
**write** surface (`docs/screenshots/ui/write-new-behaviour.png`, `write-add-step.png`), taken while
correcting three places that still called the UI read-only. So the UI has been *rendered* before.
What no trial had done is **drive it as a user with a task and then check whether what it says is
true** — which is the only claim this page makes. The project list, on a phone:

![Kit's project list at 390x844](https://raw.githubusercontent.com/jemmy8oy-northstar/kit/87faf2bad178e8f1bc174ab96851fbd8fb5cb94e/docs/screenshots/ui/trial-projects-390.png)

Opening `james-habits-app` shows the tool at its best — a real contradiction, both sides cited to
anchors in **that app's own** `DESIGN.md` (`#A2`, `#mvp-3`; there is no `docs/DESIGN.md` in kit),
two options naming real files and lines, a recommendation, and an explicit counter-argument to its
own recommendation:

![The question sheet at 390x844](https://raw.githubusercontent.com/jemmy8oy-northstar/kit/87faf2bad178e8f1bc174ab96851fbd8fb5cb94e/docs/screenshots/ui/trial-question-sheet-390.png)

The blind agent independently drove the **write** loop, which I deliberately did not (my server
pointed at a real working tree): author a behaviour, approve an inferred one, add a step, bind a
noun. It reports all four landing in the `.beh` file with **nothing committed**, verified with `git diff`
rather than by believing the banner, and a malformed step **refused** rather than swallowed
(`after:133: unrecognised keyword "this"`). ⚠️ **That is its evidence, not mine** — see §5.

## 2. Eight of ten projects never see a question sheet

`kit-ui.beh`'s `BEH-SHEET-1` is *"A project shows the question sheet Kit built for it."* Asked of
every corpus through Kit's own read API:

| corpus | behaviours | defined | inferred | questions | sheet |
|---|---|---|---|---|---|
| james-habits-app | 23 | 12 | 11 | 11 | 3 decisions + 8 reviews |
| language-vocab | 27 | 12 | 15 | 15 | 3 decisions + 12 reviews |
| kit | 20 | 20 | 0 | **0** | — |
| kit-ui | 6 | 6 | 0 | **0** | — |
| longlist | 8 | 8 | 0 | **0** | — |
| macro-metrics | 10 | 10 | 0 | **0** | — |
| snip-it | 8 | 8 | 0 | **0** | — |
| trial-habits-a | 10 | 10 | 0 | **0** | — |
| trial-habits-b | 13 | 13 | 0 | **0** | — |
| trial-lend | 8 | 8 | 0 | **0** | — |

**The empty set is exactly the zero-inferred set**, and the UI omits the section entirely rather
than showing an empty one — no heading, no "nothing to decide". Pinned from both sides: the API's
`questions.length` and the rendered `Question sheet` heading agree on all ten.

**Counted a second way, because the probe behind that table lied twice before it worked** (§6). The
independent route needs no probe at all — it reads the raw corpus instead of Kit's API, so anyone
can re-run it:

```
grep -c '^behaviour '   prototypes/behaviour-ast/behaviours/*.beh
grep -c 'source inferred' prototypes/behaviour-ast/behaviours/*.beh
```

**Every cell above matches**, all ten corpora, both columns (`defined` = the difference). And it
exposes a structural fact the table only implies: **`questions` equals `inferred` exactly — 11 and
11, 15 and 15.** The sheet is not *related* to the inferred count, it *is* the inferred count, which
is why a corpus with none loses the whole section rather than part of it.

![A project with no sheet, at 390x844](https://raw.githubusercontent.com/jemmy8oy-northstar/kit/87faf2bad178e8f1bc174ab96851fbd8fb5cb94e/docs/screenshots/ui/trial-no-sheet-390.png)

This is the **measured consequence** of [kit#73](https://github.com/jemmy8oy-northstar/kit/issues/73)(a),
and it is stronger than that issue states: #73 says a forward corpus loses the sheet's *Decisions*
half, and what is actually lost is *the whole sheet*. A forward corpus is `defined` by construction,
so **every new project Kit is used on starts in the 8, not the 2.**

⚠️ **Nothing here is a fix, deliberately.** `kit#73` is dated 2026-10-08 and is James's call.

## 3. A generated assertion can be satisfied by the behaviour that generated it

Kit emits `getByRole("heading", { name: "Question sheet" })` with no `exact: true`. Playwright
matches that as a **substring**, so it is satisfied by any heading containing those words —
including a behaviour card showing the behaviour's own title. Measured on `kit-ui`, whose project
page has no sheet at all:

```
kit-ui             loose=1  exact=0
    matched: "BEH-SHEET-1 A project shows the question sheet Kit built for it"
james-habits-app   loose=1  exact=1
    matched: "Question sheet"
```

The assertion passes on a page with no question sheet, because the page lists the behaviour whose
title says "question sheet". Across all ten corpora:

**18 generated heading assertions — 4 with `exact: true`, 14 (78%) substring-matched.** One of them
(`BEH-SHEET-1`, counted twice because `kit.js kit` merges `kit.beh` and `kit-ui.beh`) asserts a name
already contained in its own title.

Today this costs nothing: `BEH-SHEET-1`'s generated test navigates to `/projects/james-habits-app`,
one of the only two projects that has a sheet, so it passes for the right reason. The defect is the
**class** — a corpus whose behaviour title quotes its own assertion grades itself. It belongs with
[kit#86](https://github.com/jemmy8oy-northstar/kit/issues/86)'s theme (green meaning less than it
looks), and it is **not fixed here**: emitting `exact: true` changes what every generated test means.

## 4. Coverage is unavailable on all ten projects out of the box

Following the README exactly — `node start.js`, no flags — every project reads **`not measured`**.
Adding `--repos /data/repos`, pointed at a directory that really does hold the clones:

| result | corpora | reason Kit gives |
|---|---|---|
| coverage reported | **2** — `kit` (20/20), `snip-it` (6 covered, 2 uncovered) | — |
| no mapping file | 3 | *"no `<app>.tests.json` — this app has no mapping, which is not the same as having no coverage"* |
| no such repo | 5 | *"`--repo /data/repos/<app>` does not exist"* |

**Kit's reasons are honest and distinct**, and the middle one is the product making exactly the
distinction between "measured zero" and "could not look". That is the right behaviour, recorded here
as praise, not as a defect. Whether `start.js` should default `--repos` is adjacent to
[kit#71](https://github.com/jemmy8oy-northstar/kit/issues/71) and is **not** answered here.

## 5. What the blind agent found that I did not

Reproduced by me, from the raw corpus:

- **A binding's requirements are corpus-wide, and the UI never names the behaviour driving them.**
  Binding `page:Shelf` to `{"route": "/shelf"}` reported the step **generated** *and* the noun
  *"bound and still refuses"*, demanding a `urlPattern` "wanted by `lands`" — a verb absent from the
  behaviour on screen. ✅ **Reproduced:** in `trial-lend.beh`, `page:Shelf` takes `opens` six times
  and `lands` once, at **line 72, inside `BEH-LEND-2`** (declared line 66). The logic is right; the
  screen just never says which other behaviour is asking.
- **A detected decision has nowhere to click.** Every decision ends *"Kit has no syntax for recording
  a resolution to this… that is James's call."* Corroborated by my own render — it is on the
  screenshot in §1. Honest, and still a loop that terminates.

**Recorded but NOT reproduced** — treat as claims, not findings, until someone controls them:

- the generated-test `<pre>` scrolls horizontally with no visible affordance (`scrollWidth` 760 vs
  `clientWidth` 538), worse at 390px;
- one navigation to a nonexistent project logged **two** identical console 404 lines for **one**
  observed request (agent flagged it undiagnosed);
- the end-to-end write loop landing in the corpus — I never drove writes, so I am taking that on
  its evidence, not mine.

## 6. Two probes lied to me before one worked

Worth recording because both failures were **flattering**. Asking the read API how many questions
each corpus carries returned `0 of 10` and then `SAME SET? YES` — both from guessed field names:
the list route returns `{projects:[{app}]}` not `name`, the sheet is **`questions`**, a flat array
of `{kind,tier}`, not `sheet`, and `source` is `{origin,ref}`, so `source === 'defined'` is never
true. What caught it was `james-habits-app` reading 0 when I had **just watched its sheet render**.
The working version takes `defined`/`inferred` from Kit's own `adjudication` block rather than
re-deriving them, and prints a warning when any column is constant across the population.

The same trap then caught a real one: the API↔UI check reported a disagreement on `kit-ui`, which
was **my selector substring-matching** — and chasing *which heading matched* is what turned a bug in
my probe into §3.
