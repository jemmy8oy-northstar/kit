---
tags: [kit, trial, findings]
updated: 2026-09-26
status: an account of someone else using Kit, not a claim about it
---

# What happens when somebody who has never seen Kit writes a corpus

_2026-09-26. [`starting-a-project-in-kit.md`](starting-a-project-in-kit.md) ends by naming its own
limit: **"I wrote the corpus, the brief and the bindings. That is fatal to any claim about whether
Kit helps someone else think."** Every one of the ten committed corpora, and all three previous
trials, were written by me. This trial exists to attack that sentence rather than repeat it._

## Method, and the confound stated before the findings

A **separate agent with no prior context** was given a checkout and a product brief in ordinary
language, and told to get as far as it could. It was forbidden from reading `docs/trials/` (those are
the answers), from editing any existing file, and from fixing anything it thought was broken —
*"a fix destroys the measurement"*. It authored `pagecount.beh`, 17 behaviours, and reported back.

⚠️ **The confound, which no amount of care removes: a weaker model struggling is not evidence that
Kit is hard.** So the findings below are split, and the split is the honest part:

- **§1–§4 are claims about artefacts** — a number that contradicts another number, a sentence that is
  false, a step that produces nothing. These do not depend on who was reading, and **I reproduced
  every one of them myself with my own controls**, which is why they are stated as facts.
- **§5 is what the author found confusing.** That is model-dependent and is labelled as such. It is
  reported because it is the only evidence available about learnability, not because it is proof.

Run on `feat/bindings-live-with-the-corpus` (`da9a1f7`) — deliberately the tree behind the open
[kit#70](https://github.com/jemmy8oy-northstar/kit/pull/70), so that a defect in that decision would
be found before the merge rather than after. None of §1–§4 is caused by it; all four reproduce on
`dev` at `10a1744`.

## The finding, in one line

**Kit's engine did well again, and the three things that failed are all in the part that reports back
to a human**: a question a human explicitly wrote never reaches the question sheet, a step naming a
noun contributes nothing and says nothing, and the sheet asserts how it was built and is wrong.

## 1. 🔴 A question a human writes is silently dropped

The author found a real ambiguity in the brief and wrote it up properly, on `BEH-TOTAL-2`: an `asks`,
two `option`s with trade-offs, a `recommend` and an `against`. That is exactly the artefact the
question sheet exists to carry. Then:

```
$ node kit.js sheet pagecount
**0 decisions · 0 reviews.**
```

The cause, `kit.js:889`:

```js
if (b.source.origin !== 'inferred' || b.review.state !== 'unreviewed') continue;
```

Its comment is candid about the design: *"A defined behaviour is not up for adjudication — a human
already wrote it. It can still carry a question (that is how a conflict gets one), and that question
is reported above rather than here."* The loop above it walks **conflicts**. So a `defined`
behaviour's question surfaces only if that behaviour is also one side of a symbol collision.

🔑 **This is structural for forward authoring, not incidental.** In a corpus written forwards from a
brief there is no code to infer from, so **every behaviour is `defined` by construction** — which
makes the entire Decisions half of the question sheet unreachable for exactly the case
[kit#38](https://github.com/jemmy8oy-northstar/kit/issues/38) is about. Nine of the ten committed
corpora are reverse-engineered, which is why no corpus here had ever shown it.

⚠️ Whether `asks` on a `defined` behaviour *should* reach the sheet is a product call and **his**,
not a defect to patch quietly. Raised as [kit#73](https://github.com/jemmy8oy-northstar/kit/issues/73).

## 2. 🔴 `fills` contributes nothing at all unless the hole is `?fields` on a `form:`

The author's headline. Verified with a probe that varies **one axis** and holds the rest identical —
same actor, same surrounding steps, same noun kinds:

```
behaviour PROBE-FORM   when fills form:ZzForm with ?fields    + provides form:ZzForm.fields = ZzAlpha
behaviour PROBE-FIELD  when fills field:ZzBeta with ?page
```

```
$ node requires.js zzfills
  nouns referenced   3
  The app must provide:
    field:ZzAlpha   (PROBE-FORM)   a form field with an associated <label> — `fills` addresses fields by label only
    page:ZzProbeHome  …
    region:ZzDone     …
```

The documented shape works, and explains itself well. **`field:ZzBeta` appears nowhere** — not as an
obligation, and not even among the 3 `nouns referenced`. A noun written in the corpus is absent from
every number and every list, with no diagnostic.

🔑 **The point is not that the shape is restricted — it is that the restriction is silent.** Kit's
stated premise is *refuse rather than guess*, and this is neither: it is a third outcome the output
has no way to say. In `kit.js`'s report the same step renders as `// UNGENERATED`, which is the
identical marker a merely-unbound noun gets — so "you have not bound this yet" and "this shape can
never be wired up" are indistinguishable to the reader who has to act on them.

This is the same **shape** as [kit#62](https://github.com/jemmy8oy-northstar/kit/pull/62) (the CLI hid
two obligations the UI listed), which fixed `fills`'s resolved *fields*. It did not ask what happens
when the hole is not `?fields` at all.

## 3. 🔴 The question sheet states how it was built, and is wrong

Every sheet prints, unconditionally (`kit.js:986`):

> Kit read this app's `docs/DESIGN.md` and its backend test names and built one list of behaviours
> from both.

`pagecount` has no `docs/DESIGN.md`, no backend and no code of any kind; all 17 behaviours were typed
from a prose brief. The sentence is boilerplate carried over from the reverse-engineered pilots, and
it is the **first thing** a reader of the sheet is told about its provenance.

The sheet is the artefact designed to be handed to someone else, so a false claim about where its
contents came from is worse here than in a log. Stated as a finding rather than fixed: the sheet's
framing text is what he specified on claude-code-bot#68, so rewriting it is not mine to do alone.

## 4. ⚠️ Two adjacent numbers invite an inference that is false — now diagnosed

_⚠️ **Extended later the same day.** This section originally ended *"Not diagnosed further here"*, and
that was deliberate: the first attempt at it asserted an invariant that turned out to be mine rather
than Kit's, so it was left as a measurement instead of a verdict. Everything from **The cause** down
is the diagnosis. **Not one number above it was changed.**_

`kit.js` prints, two lines apart:

```
  nouns bound           0/23   in THIS corpus
  unbound nouns         22     page:Library, region:BookList, …
```

0 bound of 23, then 22 named. Measured across all eleven corpora, the two are computed over different
populations — `referenced` walks every `step.refs`, the list comes from `generate()`'s `missing` — and
neither contains the other:

| corpus | bound/referenced | unbound listed |
|---|---|---|
| `pagecount` | 0/23 | 22 — one fewer (`form:NewBook`) |
| `longlist` | 0/17 | **18 — one MORE than were referenced** |
| `trial-lend` | 0/16 | 16 |

⚠️ **`longlist` is the one that settles it**: 18 unbound against 17 referenced cannot be read as
consistent under any definition where the unbound are a subset of the referenced. My first attempt at
this finding asserted the invariant `referenced − bound = unbound` and was **wrong** — that was my
formula, never Kit's claim. So the defect is not arithmetic; it is that two incomparable measurements
are printed adjacently under one heading, and the reader cannot tell.

### The cause, and it is one sentence in two places

- **`nouns bound X/Y` counts the step-reference population.** `boundNouns()` calls `nounsOf()`, which
  walks `step.refs` and nothing else (`kit.js:1151`).
- **The `unbound` list counts the binding-target population.** It is `generate()`'s `missing`, and
  `kit.js:325` fills that **only from `bind()` calls `emit()` actually reaches** — the comment beside
  it says so: *"bind() is the ONLY thing that records a noun as missing."*

Three mechanisms then separate them, and all three are visible in the corpora:

1. **A noun on a step `emit()` refuses is never a binding target.** `kit.beh` is the extreme case:
   **23 of its 43 referenced nouns** are `command:`, `status:`, `error:`, `slot:`, `host:` — no
   locator will ever bind them, so they sit in the denominator and never in the list.
2. **`fills form:X with ?fields` binds the resolved *fields*, never the form.** So `form:NewIdea`,
   `form:Upload` and `form:NewLoan` are referenced-and-unbound and reported by nothing. That is §2
   above, seen from the other side.
3. **A `field:` named only in a `provides form:X.fields = …` value is a binding target that was never
   referenced.** `field:Idea`, `field:WhyNow`, `field:Hours`, `field:DueDate`. This is what pushes
   `longlist` to 18 against 17.

| corpus | `bound/referenced` | `unbound` listed | needs binding, never listed | listed, not referenced |
|---|---|---|---|---|
| `longlist` | 0/17 | **18** | 1 — `form:NewIdea` | 2 — `field:Idea`, `field:WhyNow` |
| `kit` | 0/43 | 20 | **23** | — |
| `snip-it` | 14/17 | 2 | 1 — `form:Upload` | — |
| `trial-lend` | 0/16 | 16 | 1 — `form:NewLoan` | 1 — `field:DueDate` |
| `trial-habits-b` | 0/17 | 16 | 2 | 1 — `field:Hours` |
| `james-habits-app` | 0/16 | 15 | 1 — `field:HabitValue` | — |
| `macro-metrics` | 13/18 | 5 | — | — |

🔑 **Mechanism 3 is already documented, and that is what settles who owns the fix.** `writer.js:420`
and `:560` state it outright — *"a `field:` named only in a `provides` value is invisible to
`nounsOf()`"* — and `requires.js:219-228` keeps three populations deliberately apart for exactly this
reason. `mutate.js` even carries a mutant for the generator half (*"a field reached through a
`provides` goes back to bypassing `bind()`, so the CLI stops naming it"*). So **`nounsOf()` is not a
bug to patch**: two callers depend on it being precisely the step-reference population. The defect is
in the **report**, which prints a fraction over one population two lines above a list over the other,
under one `── measured ──` heading, with nothing saying so.

### The consequence for a reader, which is not cosmetic

**Two Kit commands print a line called `nouns referenced` for the same corpus and disagree — in 8 of
the 10 committed corpora.**

```
$ node kit.js longlist      | grep 'nouns bound'
  nouns bound           0/17
$ node requires.js longlist | grep 'nouns referenced'
  nouns referenced   18
```

And the instruction the numbers imply is wrong in both directions: binding every noun on the
`unbound` list still leaves refused steps, because mechanisms 1 and 2 put nouns in the denominator
that no binding can satisfy — 23 of them in Kit's own corpus. `0/43` is not "0% of the way through
binding Kit"; only 20 of those 43 are work a binding could ever do.

⚠️ **Which population `nouns bound X/Y` should use is a question, not a patch** — the fraction could
move onto the binding targets, or the report could name all three populations. Raised rather than
answered.

### One thing found while diagnosing it, and fixed

Putting the two CLIs side by side over all ten corpora needed `node kit.js kit`, which turned out to
report on `kit.beh` **and** `kit-ui.beh` merged: `kit.js:1257` filtered corpus files by substring. It
read 26 behaviours and `20/126 = 16%` derived for a corpus whose own answer is 20 and `0/96 = 0%` —
erasing the zero that is the entire reason `kit-ui.beh` exists separately — and it gave one id to two
behaviours, because both corpora legitimately define `BEH-ADJ-1`. Unlike everything else in this
document that is a plain defect with no product question in it, so it is fixed rather than reported.

## 5. What the author found hard (model-dependent — read as testimony, not measurement)

- **Which command produces the useful output.** They reached for `check.js`, the gate, and only
  discovered after reading `check.js:10-12` and its `--repo` requirement that a gate over test files
  is meaningless for an app that does not exist. `requires.js` was the right tool, found via the
  README. For a *new* project — the case kit#38 names — the first-choice command is the wrong one.
- **What the numbers mean.** `0/56 = 0%` was correctly read as the honest state of a spec with no
  implementation rather than as a failure, which is a point in the output's favour.
- **Where the notation ran out.** TIMING and ORDERING again, unprompted and by a different author —
  which makes them **population evidence** rather than one reader's taste, now from four independent
  directions. Also NEGATION (*"no longer in the main list"* — there is no `then does not see`), and
  "collapsed by default", where `sees` proves an element exists and cannot speak to its state. All are
  corpus-language changes and therefore claude-code-bot#58 territory: written down, not built.

## What this says about the loop

The previous trial concluded that *"everything that went wrong was at an edge — no way in, nowhere to
put the corpus, and a report that omits two nouns."* This one moves the edge: the corpus went in fine,
the engine resolved a hole across behaviours, and the refusals were honest. **What failed is
reporting back.** Three of the four findings above are Kit declining to tell a human something it
knows — a question it was handed, a step it cannot ever use, and how it built the page you are reading.

🔑 **And the method finding, which is the durable one: an author who did not write the tool found
three defects in one afternoon that ten corpora and three trials by its author had not.** Not because
the author was better, but because every previous corpus was written by someone who already knew which
shapes worked and therefore never wrote the ones that do not. **A tool cannot be exercised past its
author's habits by its author.** That is worth repeating periodically rather than once.

⚠️ **The limit of this trial, in its turn:** the author was an agent, not James, and `pagecount.beh`
is not committed here — it was written in a throwaway clone and is quoted, not vendored, because a
corpus nobody will maintain is a population change every measurement in this repo would then pay for
(the previous trial measured that toll at three assertions). The commands and their outputs are the
evidence; the corpus was the instrument.
