# Trial 1 — pointing Kit forwards, at an app that does not exist

**Date:** 2026-09-06 · **Corpus:** `prototypes/behaviour-ast/behaviours/trial-lend.beh`
· **Brief:** `docs/trials/lend-brief.md` · **Raised by:** claude-code-bot#92

> James, cc-bot#92: *"You could also make up some trial projects to progress it."*
> and *"Adding extra spec / notifying assumed spec as you find experience that
> doesn't match intended."*

## Why this run existed

Every corpus Kit had was reverse-engineered from software that already shipped —
snip-it's from its own Playwright suite, habits' and vocab's from design docs and
routes, macro-metrics' from 46 written acceptance criteria, Kit's own from Kit.
`bindings.json` states the discipline in its header: *"Every value below is
copied from snip-it … nothing here is invented."*

That is the right way to **measure** an app and the wrong way to **start** one.
So the product's central claim — `behaviours → application build` — had never
once been executed. This is the smallest run that executes it.

## The result

```
── measured ──                    trial-lend            snip-it (control)
  behaviours                      8                     8
  nouns bound                     0/16                  14/17
  generated lines                 0                     28
  ungenerated                     27                    2
  unbound nouns                   15                    2
  generated / total               0/27 = 0%             28/36 = 78%
```

**0%, and the 0% is correct.** Kit refused all 27 steps and named all 15 missing
nouns rather than inventing a locator. Given that no app exists, refusing is the
only honest answer, and it is the behaviour the design asks for.

But *"Kit works correctly"* and *"Kit is usable forwards"* are different claims,
and only the first is supported. What a user gets from running Kit on a new
project today is a list of everything that is missing. That is a true and
complete report, and it is not yet a step toward an app.

## Finding 1 — the reported binding count was global, not per-corpus 🔴 FIXED

The run printed **`noun bindings 27`** for an app binding *zero* nouns. So did
snip-it. So does every corpus, because the number was
`Object.keys(bindings).length` — the size of the shared `bindings.json` file,
printed inside a per-app `── measured ──` block next to per-app numbers.

It is also the number carrying Kit's headline claim against Cucumber
(*"Cucumber would need one step definition per step phrasing"*), so the
comparison was being made against a constant.

**Fixed** — the report now says `nouns bound 0/16` and `14/17`. Three tests and
two mutants, one of which reinstates the exact original defect.

⚠️ **`14/17` alongside `2 unbound nouns` is not a contradiction.** 17 − 14 = 3;
the third noun is `form:Upload`, resolved from *within the corpus* by another
behaviour's `provides` line rather than by `bindings.json`. The two numbers count
different things and both are right.

🔑 **This was only visible from the forward direction.** Backwards, every corpus
had most of its nouns bound, so a plausible number was always printed. It took a
corpus binding *nothing* to make a constant look like a constant.

## Finding 2 — a trial corpus silently changed a published measurement 🔴 FIXED

Dropping `trial-lend.beh` into `behaviours/` turned `saturation.js --check`
**red**. Correctly: gap #8's recorded findings are a study over the four *real*
apps, and the corpus directory is the study's population. Cross-app noun reuse
computed over an app I invented measures my own naming habits, not two teams
independently converging.

`saturation.js` already had the right pattern — *"the exclusion is DECLARED BY
THE CORPUS, not by a filename list here"* — but only one axis: `# kit:no-ui`,
which asks whether a corpus describes a UI. This trial is **full** of UI nouns,
so that directive would never have caught it. The missing axis is *existence*.

**Fixed** — `# kit:not-a-real-app`, announced on every run like its sibling. Four
tests (including a CONTROL, and one asserting the two directives are independent
rather than one rule spelled twice) and three mutants.

⚠️ **Nothing about gap #8's published numbers changed** — `saturation.js` indexes
`bindings[noun]` per corpus and never used the global count, so Finding 1 does
not touch it. Verified rather than assumed: the four per-app figures are
unchanged at 0/16, 0/11, 0/18, 0/17.

## Finding 3 — the ceiling is real, and it is the four shapes 🔴 OPEN, HIS CALL

The brief was written to stress four of the twelve grammar shapes
`docs/pilots/macro-metrics-prose.md` named as missing. All four arose naturally,
none contrived:

| shape | the brief's words | what the notation could say |
|---|---|---|
| **ordering** | "worst offenders at the top" | nothing — a test can see the list, not its order |
| **timing** | overdue once the due date passes | only `given loan:Overdue`, a fixture *state* |
| **cardinality** | one row per loan | nothing |
| **negation** | returned items "leave the list" | only a weaker, different assertion |

Negation is the sharpest, because it is the one I *could* half-write.
`BEH-RETURN-2` claims *"a returned loan is no longer on the shelf"* and the
notation forced it to assert `then sees region:EmptyState` — true only when the
returned loan was the *last* one. A reader skimming the corpus sees a behaviour
whose title matches the requirement and whose body does not.

🔑 **That is precisely the Gherkin failure mode Kit exists to prevent** — the
spec absorbing the divergence instead of exposing it. `bindings.json`'s own
header names it: *"Binding these … would let the corpus absorb the divergence."*
Here the *grammar* did the absorbing rather than a binding.

**Timing has a second edge:** `given loan:Overdue` pushes the domain rule
("overdue means the due date has passed") into `bindings.json` as a fixture
state, where no reviewer reads it as a rule. The corpus looks complete and the
rule lives somewhere nobody adjudicates.

⚠️ **I did not extend the grammar.** The stage-0 stop condition fired on
2026-09-03 and the fork — *extend the notation* vs *accept the ceiling* — is
James's, defaulting to "accept the ceiling". Widening it to make my own trial
score better would be marking my own homework. This trial is evidence for that
decision, not a licence to pre-empt it.

## Finding 4 — the forward gap is structural, and it is stage 4 🟠 OPEN

Of the seven process stages, `docs/design/process.md` says **2, 3 and 4** are
ours. Stage 4 is *binding: nouns, not steps* — and binding is defined as mapping
a noun to a locator in an app that exists. Running forwards, stage 4 has nothing
to bind against, and stages 5–7 depend on it. So the forward path is not blocked
by a missing feature; it is blocked by the direction of one of the three stages
we chose as differentiating.

**The shape of an answer** (design only — not built, and not mine to choose):
today `bindings.json` is an **input** hand-copied from the app. Forwards, the
corpus could emit it as an **output** — a required-surface contract saying *"this
app must have a page at some route, a button whose accessible name is …"* — which
the implementation then satisfies and `check.js` verifies. That inverts the
dependency without new grammar, and it is close to something `bindings.json`
already gestures at: *"That refusal is the spec constraining the app, which is
the property Gherkin never had."* Today the refusal constrains nothing, because
nobody is told what would satisfy it.

That is a **product decision about what Kit is**, so it is James's. Logged here,
not built.

## Finding 5 — the UI would have shown an invented app as a project 🔴 FIXED

The same contamination, a third time and in a third population: the UI's fixture
test turned red because `ui.js`'s project list is checked against its captured
output, and the trial corpus joined the list.

**Here the population was right and the presentation was wrong.** The UI is a
viewer — hiding a corpus would make the list lie about what Kit reads — so the
trial *should* appear. But an app scoring 0% because it is unbuilt and an app
scoring 0% because it does not exist mean opposite things, and the row said
neither.

**Fixed** — `notReal` is carried from the corpus through `project.js` and the
list endpoint, and the row renders a neutral `trial — no app` badge. `notReal`
is always a boolean, never absent, so `!p.notReal` cannot be true for two
different reasons.

🔑 **And a mutant survived on the way, which is the more useful half.** Dropping
`notReal: p.notReal` from `ui.js` killed nothing, because `mutate.js` runs
`kit.test.js` alone — so **any rule whose only assertion lives in the UI's vitest
suite is unmutated**. The rule is now asserted in the Node suite too, where the
harness can see it. That boundary is worth remembering beyond this PR: the two
suites are gated together in CI but only one is mutation-tested.

## What this says about the trial method

Worth keeping: four of the five findings came from *friction*, not from analysis.
The global count and all three population problems were invisible from the
backwards direction and surfaced within minutes of running forwards. A fifth
pilot app would not have found any of them.

The pattern underneath them is one thing said three times: **the corpus directory
is an implicit population for every measurement that reads it** — the saturation
study, the UI's project list, and the fixture capture. Adding one file changed
all three, and only two of them were wrong to change. "Which populations does
this corpus belong to" had no answer anywhere before this run.

Worth watching: an invented corpus can be written to succeed. This one was
written to a brief fixed *before* the corpus, and deliberately over a domain
that stresses known-missing shapes — but that discipline is mine to keep, and
nothing enforces it. `# kit:not-a-real-app` keeps invented corpora out of the
measurements that matter, which is the structural half of the guard.
