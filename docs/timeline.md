---
tags: [kit, planning, timeline]
updated: 2026-08-29
status: draft — rough, and deliberately gated rather than dated
---

# Rough high-level timeline

> James asked for "a rough high level timeline" (#68). Here it is — but the honest structure is
> **gates, not dates**. Each stage has an exit criterion that can be measured, and a *stop* condition.
> Dates below assume Kit is the single active project (per #49) and that I work it on scheduled wakes
> with you reviewing. **They will move; the gates should not.**

## The shape

| | Stage | Exit gate (measurable) | Rough |
|---|---|---|---|
| **0** | Notation, resolver, coverage gate — run on one real app | CI goes **red** when a behaviour has no test naming its ID, on a real repo | ~3–5 weeks |
| **1** | Structural conflict detection + supersede | A real contradiction is caught, adjudicated, and the decision is recorded and never re-raised | ~2–3 weeks |
| **2** | LLM drafting from conversation (draft-and-validate) | A feature is specced from a chat, validated by you, and built — end to end | ~4–6 weeks |
| **3** | Blueprint UI | You'd rather use the UI than the files | unscoped |
| **4** | Hosted product | someone who is not us uses it | not planned |

**Total to a genuinely useful internal tool (0+1): ~2 months.** Everything past stage 2 is a product
decision, not a schedule.

---

## Stage 0 — make the corpus real and make it break the build

**Weeks 1–5, roughly. The only stage I'd argue for on its own merits today.**

- Notation frozen enough to hand-write (line-based; the prototype's parser is line-based on purpose —
  YAML and JSON both fail hand-authoring on punctuation alone).
- One symbol table across the corpus, holes resolved cross-document. **Designed in from commit one** —
  this cannot be retrofitted onto a per-file AST.
- Noun bindings file; unbound noun ⇒ refuse to generate, name the noun.
- **The coverage gate wired into a real `ci.yml`.** This is the actual deliverable. Everything else is
  scaffolding for it.

**Exit gate:** on a real repo, deleting a test makes CI go red with *"behaviour BEH-X has no test"*.
Verified by doing it, not by reading the workflow.

**Stop condition:** if the notation cannot express a real feature of ATW or snip-it without special
cases, stop and redesign. Do not paper over it with syntax.

> 🔴 **THIS STOP CONDITION HAS FIRED — 2026-09-03.** Fed real prose for the first time
> (macro-metrics' 46 frontend acceptance criteria, written before any test existed), the
> notation carries **2 whole and 24 not at all**, and the twelve missing shapes are ordinary
> — quantifying over a collection, asserting a control's value, asserting an absence, saying
> *"while it is loading"*. The grammar was **not** patched to fit; the shapes are named,
> counted and left for James in `docs/pilots/macro-metrics-prose.md`. Nothing downstream is
> blocked: `kit check` gates on behaviour *ids*, which are carried whether the behaviour
> beneath them is rich or thin.

**Why this first:** it closes the defect your own macro-metrics retro filed (*"it was left to the AI's
discretion whether tests were written"*), and it is useful to us whether or not Kit ever becomes a
product. It is also the cheapest way to find out whether the notation survives contact.

## Stage 1 — the differentiator

**Weeks 6–8, roughly.**

Structural contradiction detection (`noun.slot` collisions), supersede/reject/coexist, and the decision
recorded so the same pair is never raised twice. **The prototype showed this is nearly free once the
symbol table exists** — which is why it comes early despite being the differentiated feature.

**Exit gate:** a genuine contradiction, found on a real corpus, that we did not plant.

**Stop condition:** if six weeks of real use produces no true contradictions, the feature is solving a
problem we don't have at our scale — say so and re-plan rather than manufacturing test cases.

## Stage 2 — draft-and-validate

**Weeks 9–14, roughly. First stage that needs a model at runtime.**

Conversation → drafted behaviours, inferences attributed and adjudicable, denials capturing the
correction. This is where Kit starts being *for* someone rather than *used by* us.

**Exit gate:** one real feature specced from a conversation, validated by you, built from the generated
tests, shipped.

**Stop condition:** if the drafts need more editing than writing the behaviours by hand would have, the
model is not the bottleneck-remover we assumed.

## Stage 3 — the blueprint UI

**Unscoped, and deliberately.**

Your rough/cartoonish components idea from #68 is good — and it is downstream of everything above. Note
it is **blocked on something unrelated**: `design-system` ships `Button, Card, Badge, Input` that no app
installs, while `web-template` bakes in its own four. **A UI-composition layer has nothing to compose
until that is one library.** That is a real dependency, not a nice-to-have.

## Stage 4 — hosted product

**Not planned, and I'd rather it stayed that way for now.** The market read (see
`analysis/strategic-position.md`) is that this lane is contested by companies with billions of dollars.
The question "is Kit a product?" should be answered by *having used it on two real apps*, not by a plan
written before stage 0 exists.

---

## What could invalidate this whole plan

Listed so we notice rather than rationalise:

1. **Tessl ships its core framework.** Their stated goal is exactly stage 1. They have been **~11.5 months**
   in closed beta (since 16 Sep 2025); if that ends with a good product, our differentiator is gone and we
   should say so.
2. **Kiro adds automated conflict detection.** AWS has the spec artefact already; adding contradiction
   detection is a feature for them, not a pivot. ⚠️ **Re-measured 2026-08-30 and this moved while nobody was
   looking:** Kiro's GA release (17 Nov 2025) already ships property-based tests extracted from the spec to
   check the code against it. That is spec↔**code**, not spec↔**spec**, so item 2 has not happened — but the
   distance to it is now one axis, not two.
2a. 🔑 **Nothing in my instrument set would ever have told us any of this.** `sweep`, `pr-health`,
   `live-check` and `verify` all measure our own estate; every competitive claim in this pack was written
   once and silently decayed. Eleven days was enough to make four of them wrong. **Any dateless market
   claim in these docs should be read as expired.** [[a-moat-has-a-shelf-life]]
3. **The notation doesn't survive a real app.** The stage 0 stop condition. Most likely failure, and the
   cheapest to discover — which is why stage 0 is first.
4. **We stop shipping apps.** Kit's whole feedback loop is our own estate. If nothing is being built,
   there is no corpus to keep honest.

## A note on sequencing against #49

This plan assumes Kit is **the** active project. Per your 29 Aug direction: ATW is live and done, snip-it
is paused. **The open snip-it PRs (#22, #23, #29) are the loose end** — three green PRs, one of them a
promotion that has been waiting nine days. Pausing snip-it is your call and I'm not arguing it, but
"paused" should probably mean *those land or get closed*, not *they sit green forever*. One decision from
you clears it either way.
