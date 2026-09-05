# Gap #8 — does binding glue saturate, or grow 1:1 with the UI?

**Measured 2026-09-03. Reproduce with `node prototypes/behaviour-ast/saturation.js`;
`--check` refuses if this document has drifted from the corpora.**

## The bet being tested

`bindings.json` states Kit's central claim against Gherkin in its own header:
the file "grows with the app's vocabulary rather than with the spec". One
binding per **noun**, not one per step and not one per scenario. If that holds,
glue *saturates*: the tenth behaviour costs less glue than the first, and the
hundredth costs none.

It had never been measured. Four corpora exist; this measures all four.

## Verdict

**No saturation, in any of the four — and the observed decline is _shallower_
than shuffling the same corpus produces.** But the result cannot bear much
weight, and the reason it cannot is the more useful finding: **these apps are
too small for the question to have an answer.** See "Why this cannot settle it".

## Method, and the two things that would otherwise have faked a result

**1. A behaviour with no noun reference is excluded from the curve.** This is
not a detail. `james-habits-app.beh` has 23 behaviours and looks like it
saturates completely after six. It has **8** that touch the UI at all; the other
15 are API, domain and inference behaviours with no UI step, and counting them
drives the marginal to zero for a reason that has nothing to do with bindings.
`language-vocab` is worse: 27 behaviours, **8** noun-bearing.

**2. A shuffled null, because the obvious measurement is worthless without
one.** Accumulating a *fixed* finite noun set must show a declining marginal —
you cannot introduce a noun twice — so "the curve declines" is arithmetic, not
evidence. The null is the same behaviours in a destroyed order, 5,000 shuffles,
seeded. Only a decline steeper than the null says anything about the corpus.

Percentiles are **mid-rank**: `macro-metrics` lands exactly on its own null
median, where a plain `<=` reports 71% and a plain `<` reports 39% for identical
data. Both are defensible readings of the wrong question.

The distinct-noun count is taken a second, differently-shaped way (raw text
scan vs the parsed AST) and disagreement is a hard refusal, because a reader
that silently drops steps reports a *smaller* noun set — which reads as
saturation.

## What it says

| corpus | behaviours | UI behaviours | inert | nouns | nouns / UI behaviour |
|---|---|---|---|---|---|
| james-habits-app | 23 | 8 | 15 | 16 | 2.00 |
| language-vocab | 27 | 8 | 19 | 11 | 1.38 |
| macro-metrics | 10 | 9 | 1 | 18 | 2.00 |
| snip-it | 8 | 7 | 1 | 17 | 2.43 |

Marginal new nouns per UI behaviour, in authoring order:

```
james-habits-app   3,3,2,1,3,4,0,0
language-vocab     5,1,0,0,0,1,4,0
macro-metrics      4,3,1,2,2,1,2,1,2
snip-it            2,6,1,2,4,0,2
```

Back half vs front half, against the null:

| corpus | observed | null median | percentile |
|---|---|---|---|
| james-habits-app | 0.78 | 0.60 | 63% |
| language-vocab | 0.83 | 0.38 | 72% |
| macro-metrics | 0.63 | 0.63 | 59% |
| snip-it | 0.73 | 0.56 | 76% |

**Every one is above 50%.** Authoring order does not saturate faster than
chance; it saturates *slower*. `macro-metrics`' last behaviour still introduced
two new nouns, out of eighteen.

## Cross-app reuse is zero, and partly by construction

The plan's literal question was: build a second `bindings.json` against a
different app and measure the reuse rate.

**0/16, 0/11, 0/18, 0/17. Not one noun is shared by two corpora.**

That number is partly a naming habit rather than a fact about apps:
`bindings.json` records that the macro-metrics nouns were **hand-prefixed**
(`page:MacroHome`, not `page:Home`) to dodge the global namespace, since writing
`page:Home` there emits `page.goto("./")` — snip-it's route, no warning. So the
measure is also taken on what the bindings **point at** — role+name, label,
locator or route — which no prefix can change:

**27 distinct bound targets across all corpora; 0 reached by more than one app.**

Both apps have a "Toggle Theme" button and both would bind it identically. It
does not show up as shared because only snip-it's corpus references it. The
honest summary is that the namespace forces a choice between collision and zero
reuse, and neither branch produces saturation across apps.

## The head-to-head Kit has never run

The claim is against Gherkin, but the fairer comparison is against what a person
actually writes. snip-it has a real hand-written Playwright suite on
`origin/dev`:

| | units of glue | for |
|---|---|---|
| hand-written `e2e/*.spec.ts` | **15 distinct locators** | 6 tests |
| Kit's corpus + bindings | **17 nouns** | 7 UI behaviours |

**2.50 locators per test versus 2.43 nouns per behaviour.** At this scale Kit's
glue is the same size as the glue it replaces. It buys refusal (an unbound noun
is named rather than guessed) and reuse across behaviours *within* an app — it
does not buy less glue.

⚠️ Measure this on the branch the corpus was built from. The first count of that
suite came back as 16 locators, from a checkout sitting on snip-it#29's branch,
which adds 89 lines to that spec.

## Why this cannot settle it — and this is the real finding

The curve has to flatten eventually, because an app has only so many things to
address. So the interesting quantity is the distance to that ceiling, and in
these apps there is almost none:

- **snip-it**: 16 `.tsx` files, ~1,190 lines. Its entire frontend contains **15
  `<button>` elements, 3 headings, 4 links, 7 `aria-label`s**. The corpus binds
  **17 nouns** after 7 behaviours.
- **macro-metrics**: 15 `.tsx` files, ~653 lines, and roughly **12 statically
  named addressable elements** — fewer than the **18 nouns** its corpus already
  references, because the corpus reaches for routes and CSS locators
  (`#presets`) that no accessible name covers.

So "glue grows with the app's vocabulary rather than with the spec" is, here,
**true and vacuous: the two are the same size.** A corpus of 7–9 behaviours has
already named most of what these apps expose. Saturation is not something Kit
achieves; it is something the app imposes, and it would arrive at the same point
for hand-written locators.

⚠️ The addressable counts above are a **lower bound**. macro-metrics uses Radix
`Select`, so its combobox and option semantics come from Radix's ARIA wiring and
are invisible to a source scan; and both apps have dynamically named elements
(snip-it 7 sites, macro-metrics 11) that no static count reaches.

**What would settle it:** an app whose addressable vocabulary is much larger
than any one spec — hundreds of named elements, a corpus grown to 50+ UI
behaviours. None of the four is that app, and the bet cannot be confirmed or
refuted on one that isn't. That is a finding about the pilot set, not about the
notation.

## What guards this

`node prototypes/behaviour-ast/kit.test.js` — **97 tests**, 11 of them here,
including a **control** that the statistic separates a synthetic saturating
corpus from a synthetic 1:1 one. Without that, every other test passes over a
statistic that returns the same number for both shapes and the numbers above are
unfalsifiable.

`node prototypes/behaviour-ast/mutate.js` — **59/59 killed**. Nine mutants cover
this file; three of them turn "no saturation" into "saturation" when the rule is
removed, which is the direction that matters.

Two mutants survived the first run and **both were my own tests passing on the
wrong diagnosis**: the zero-noun refusal was actually being made red by the
too-small-to-halve rule (a corpus with no nouns has no noun-bearing behaviours
either), and the quote-stripping test used the literal `"Ratio: 1.4"`, in which
the noun regex never matched — capital `R`, space after the colon. Both now
assert their own premise.

`--check` compares this document's numbers against a live recomputation and
exits 1 on any difference, because `what-we-can-leverage.md` quoted 19 tests
against a suite that had grown to 72.
