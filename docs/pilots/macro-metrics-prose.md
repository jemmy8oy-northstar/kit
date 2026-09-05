# Does the `.beh` notation survive prose?

**Gap #4, `docs/timeline.md` stage 0. Measured 2026-09-03 against
[macro-metrics `docs/user-stories-frontend.md`](https://github.com/jemmy8oy-northstar/macro-metrics/blob/89053e63532166adbf2ce1894226589a498ba4db/docs/user-stories-frontend.md)
@ `89053e6` — 7 stories, 46 acceptance criteria, written for humans before any test existed.**

**Answer: partly, and the missing half is not exotic.** The notation expresses UI
interaction that is **linear, singular, positive and steady-state**. This document —
an ordinary frontend spec, signed off, with an implementation already shipped against
it — is mostly **plural, conditional-on-state, negative and transient**. Two of its 46
acceptance criteria are carried whole.

Per stage 0's stop condition I have **not touched the grammar**. Twelve missing shapes
are named below; adding verbs until the corpus fitted would have turned the experiment
into a tautology.

## Why this corpus and not another

The three existing corpora were all reverse-engineered from working code — snip-it's
off `editor.spec.ts`, habits' and vocab's off `DESIGN.md` plus the routes. That is the
friendliest possible input: a behaviour read out of a passing test is *guaranteed*
expressible, because a test for it already exists. Prose is the case Kit will actually
meet, since James's design starts at natural language.

macro-metrics is also the useful shape: the spec is prose, **and the app is built**, so
an unbindable noun is a real finding rather than an artefact of an empty repo.

## The numbers, with the denominator stated

`node kit.js macro-metrics` prints **29 generated lines of 37 = 78%**. That number is
true and it is theatre: its denominator is the acceptance criteria that happened to fit.
The honest denominator is the document.

| disposition | n | what it means |
|---|---:|---|
| `encoded` | **2** | carried whole, as an assertion |
| `partial` | 7 | carried, with a named part that did not fit |
| `contract` | 5 | carried as prose on a behaviour; generates nothing, by design |
| `refused` | 5 | the notation carried it; **the app** offers no bindable noun |
| `implementation` | 3 | prescribes Recharts / date-fns / 160px — the notation *should* refuse these |
| `inexpressible` | **24** | no shape in the notation at all |

Read it as a ladder rather than a score. Of the 43 criteria that describe behaviour
(46 minus the 3 that prescribe implementation), the notation **fails in whole or in part
on 31**. Only 5 of the 46 fail for the reason the design *wants* — the app lacking an
accessible noun, which is the spec constraining the app.

Every one of the 46 is accounted for in `macro-metrics-prose.ledger.json`, with the
reason and the behaviour that carries it. `node prose-audit.js --source <path>` refuses
(exit 1) if a single one is unaccounted, if the ledger names a behaviour the corpus
lacks, if a behaviour exists that **no criterion asked for**, or if the source document
has drifted from the ledger. That last rule is the one that guards against me: nothing
else in this repo would stop a corpus growing a flattering behaviour and counting it.

## The twelve missing shapes, ranked by how often the document needed one

| shape | n | the criterion that shows it |
|---|---:|---|
| `cardinality` | 12 | *"skeleton versions of all 9 preset ratio cards"*, *"each loaded preset card displays…"* — every noun is a singleton; there is no quantifier |
| `spatial` | 7 | *"side by side"*, *"a 3-column responsive grid"*, *"remains visible (sticky) as the user scrolls"* |
| `attribute` | 7 | *"default is **Max**"*, *"the numerator dropdown **is set to** the preset's metric"* — a noun can be seen or contain text; it cannot have a value or a state |
| `negation` | 6 | *"**No** axis labels are shown"*, *"an error on one card does **not** affect others"*, *"in place of the chart"* |
| `verb` | 4 | *"**Hovering** over a data point"*, *"**scrolls** to the `#compare` section"* — the step vocabulary is closed at eight words and has no pointer or viewport action |
| `scoping` | 4 | the six range buttons have **perfect** accessible names and are unusable anyway: nine cards each render all six, and a noun cannot be scoped to a region |
| `relation` | 4 | *"the metric selected as numerator is excluded from the denominator dropdown"*, *"each narrower than the preset cards"* |
| `transient` | 3 | *"**While** fetching, a loading skeleton is shown"* — `given/when/then` has no *during*. Loading states are the most common thing a frontend spec says |
| `orphaned` | 2 | see below |
| `timing` | 1 | *"the page shell renders **instantly**"* |
| `ordering` | 1 | *"the 9 presets are **ordered by theme**"* |
| `interpolation` | 1 | the chart title is specified as `{Numerator label} / {Denominator label}`; the notation has no template, so the claim degrades to one instance |

`cardinality` is the one to fix first if any are fixed, and not because it is the biggest
number. It is the one that turns a *documentation* limit into a *broken test*: `button:Max`
names nine elements in the real DOM, so a generated locator would fail Playwright's strict
mode. The others produce a corpus that says less than the spec; this one produces a corpus
that cannot run.

## Four findings that are not in the table

**1. `contract` is not a free-standing escape hatch.** It must hang off a behaviour. Story
3's behaviour is itself unexpressible (its trigger — an unnamed preset card — cannot be
addressed), so its two criteria drop out *entirely*, contract and all. The escape hatch is
only available where the notation already half-works.

**2. `provides` records a collection; nothing asserts it.** The 11 comparable metrics are
the one collection in the document the notation can carry — `provides
combobox:Numerator.options = UK House Prices, …` puts all 11 in the symbol table. Nothing
generates a test that the dropdown offers them. Recording is not checking, and the
distinction is invisible in the output.

**3. The noun namespace is global across every corpus, and that is now measurable.**
`node prose-audit.js --demo-collision`:

```
   A macro-metrics corpus that writes page:Home emits:

   test("[BEH-DEMO] a macro-metrics behaviour that says page:Home", async ({ page }) => {
     await page.goto("./");
   });
```

snip-it's `page:Home` is `./`; macro-metrics' home is `/macro-metrics/`. No unbound-noun
warning, no conflict, and the test **runs** — against the wrong app. The macro-metrics
nouns in `bindings.json` are hand-prefixed to dodge this, which is a habit, not a design.
(`button:ToggleTheme` is a near-miss the other way: both apps label it *"Toggle Theme"*, so
one shared binding is correct today by coincidence.)

**4. One acceptance criterion is not one assertion.** AC line 93 carries seven sub-bullets
under a single checkbox. The 46 above therefore *undercounts* the real assertion load, and
every fraction in this document is correspondingly generous to the notation.

## Two things that worked, and one the encoding found

**Conditionals are not a gap.** *"if both are set, the chart re-fetches"* splits cleanly
into two behaviours with different `given` lines. No grammar change needed; this was on my
list of expected failures and is not one.

**Error and empty states encode well.** *"Select two metrics above to see a custom
comparison"*, *"Now select a denominator"*, *"⚠ Could not load data."* + a **Try again**
button — all carried, all generating. Multi-step picker interaction generates correctly
too, including selecting from a Radix combobox.

**Encoding the prose surfaced two real spec↔app divergences**, neither of which any test,
lint or review has ever reported:

1. AC line 22 says the mini-nav *"contains three anchor links"*. `StickyNav.tsx` renders
   three `<button onClick={scrollIntoView}>`. A link and a button differ for keyboard,
   middle-click and sharing. Encoded as `link:` on purpose, so the generator reports the
   unbound noun instead of the corpus absorbing the difference — which is exactly the
   Gherkin failure mode Kit exists to avoid.
2. AC line 133 says a failed preset card shows *"⚠ Could not load data. Retry?"*.
   `PresetCard.tsx:39-40` renders *"⚠ Could not load data."* and a separate *"Try again"*
   button; the word *"Retry?"* is nowhere in the component.

## An input to gap #8, not an answer to it

Noun references per distinct noun, across all four corpora:

| corpus | behaviours | noun refs | distinct nouns | refs/noun |
|---|---:|---:|---:|---:|
| language-vocab | 27 | 25 | 11 | 2.27 |
| macro-metrics | 10 | 35 | 18 | 1.94 |
| snip-it | 8 | 31 | 17 | 1.82 |
| james-habits-app | 23 | 26 | 16 | 1.63 |

Kit's central bet is that binding glue **saturates** rather than growing 1:1 with the
spec. If it did, this ratio would climb with corpus size. It does not track size at all:
27 behaviours give 2.27, 23 give 1.63, 8 give 1.82. **That is a signal, not a result**: the
habits and vocab corpora are mostly `provides` and inference lines with few steps, so
their reference counts are low for an unrelated reason. Gap #8 is the item that measures
this properly, against one app grown deliberately. Recorded here so that item starts from
a number rather than from an intuition.

## What this does and does not change

It does **not** invalidate stage 0. `kit check` gates on behaviour *ids*, and an id is
carried whether the behaviour beneath it is rich or thin.

It does mean that **"natural language in, tests out" is, on this document, "natural
language in, two tests and a ledger of what did not fit, out"** — and that the honest
product is arguably the ledger. A tool that tells you *which 24 of your 46 acceptance
criteria nobody can test* is a different and possibly better proposition than one that
generates the two it can.

**The fork is James's**, and nothing waits on it:

- **Extend the notation** with the shapes above. `cardinality` and `attribute` together
  fully unblock **7** criteria and partly help **10** more (computed from the ledger, not
  estimated). Each is a general primitive, not a special case, so none of them trips stage
  0's stop condition. It is a bigger grammar and a harder thing for a human to write by
  hand, which was his original requirement.
- **Accept the ceiling** and scope Kit to the behaviours it carries, making the ledger —
  what the notation *cannot* say about your spec — a first-class output rather than an
  apology.

**Default, and what happens if he says nothing: the second.** No grammar change; the
twelve shapes stay documented limits; gap #8 proceeds. Nothing in the plan is blocked
either way, so this is not in the decision queue.

---

*Reproduce: `node prose-audit.js --source <macro-metrics>/docs/user-stories-frontend.md`
· `node kit.js macro-metrics` · `node prose-audit.js --demo-collision`.
86 tests, 50/50 mutants — `node kit.test.js`, `node mutate.js`.*
