# Trial 2 — build james-habits-app forwards, from James's words only

**Date:** 2026-09-06 · **Thread:** [claude-code-bot#92](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/92)

> James, 2026-09-06 13:02Z: *"As you say the previous candidates were ran in reverse.
> Maybe you could branch off the initial commit or something and give a go building them
> from scratch based off my descriptions and comments."*

[Trial 1](lend-forward-run.md) ran Kit forwards against an app that does not exist and
scored 0/27, correctly. It could not say whether the forward path leads anywhere, because
there was nothing to lead *to*. This trial has the thing trial 1 lacked: **a ground truth.**
`james-habits-app` exists, it shipped, and the corpus already in this repo was
reverse-engineered from it.

---

## Method

**The input is his prose and nothing else.** [`habits-brief.md`](habits-brief.md) is the
opening post of `james-habits-app#1` plus his one product comment on that thread, copied
verbatim by `gh issue view --json body`. Deliberately excluded, and the exclusions are the
experiment:

- every comment I wrote on that thread — already an interpretation of his words
- `docs/DESIGN.md`, which I wrote *from* that issue and then built from
- the shipped source tree, the routes, the e2e suite, `bindings.json`

**The corpus was not written by me.** I built the app, so I know the answer; a corpus I
wrote would measure recall, not the forward path. Two sub-agents were given the brief, the
notation reference, and `trial-lend.beh` as a format example for an unrelated domain — and
an explicit prohibition on reading anything under `james-habits-app/`, the existing habits
corpus, or `bindings.json`. **Neither could see the other's work.** Both reported honouring
the isolation, and both volunteered the moment they wanted to peek instead of peeking.

Two writers rather than one, because a single forward corpus tells you what one reader
produced and nothing about how much of that was the brief and how much was the reader.

| | behaviours | nouns |
|---|---|---|
| `trial-habits-a` (forward) | 10 | 18 |
| `trial-habits-b` (forward) | 13 | 17 |
| `james-habits-app` (reverse-engineered from the shipped app) | 23 | 16 |

---

## Finding 1 — two readings of one brief share 9% of their nouns

`node converge.js trial-habits-a trial-habits-b james-habits-app`

| pair | exact | ignoring kind + case |
|---|---|---|
| A ↔ B | **3/32 — 9%** | 3/31 — 10% |
| A ↔ shipped | 4/30 — 13% | 4/29 — 14% |
| B ↔ shipped | 3/30 — 10% | 3/30 — 10% |

**Normalisation does not rescue it.** The loose score flattens kind, case and separators —
`button:LogToday`, `control:log-today` and `Button:LOG_TODAY` all collapse together — and it
moves the number by one point. So this is not a naming-convention problem that a style guide
or a case-insensitive match would fix. The readings picked genuinely different nouns.

**What all three agreed on:** `page:Today` and `region:HabitList`. The screen, and the list
on it. Nothing else is shared by all three.

**The single most central control in the product got three different names:**

| source | the thing you tick to mark a habit done |
|---|---|
| forward reading A | `checkbox:HabitDone` |
| forward reading B | `checkbox:HabitItem` |
| the shipped app | `checkbox:HabitRow` |

Kit's binding unit is the noun. Three names is three bindings, and **nothing anywhere in Kit
reports that they are the same control.**

### ⚠️ The confound, stated before the conclusion

The three corpora differ in scope (10 / 13 / 23 behaviours), and a noun in one corpus for a
behaviour the other simply did not write inflates the union without being a disagreement
about naming. **So 9% understates naming agreement and should not be quoted as if it were a
naming-agreement figure.** The `checkbox:HabitDone`/`HabitItem`/`HabitRow` row above is
immune to that objection — all three corpora cover the daily loop, so all three had to name
that control — and it is the evidence the conclusion actually rests on. The percentage is
context, not proof.

---

## Finding 2 — the consequence lands on the feature James described, not on binding

The obvious reading of finding 1 is "the forward path can't produce bindings that match the
app someone builds". That reading is **wrong, and [`requires.js`](../../prototypes/behaviour-ast/requires.js)
is why**: the required-surface contract dictates the noun names to the implementer, so the
builder does not have to guess. Divergence between specifier and builder is exactly what that
file closes, and this trial is the first evidence it was load-bearing rather than nice.

The consequence lands somewhere else, and it is on his own stated vision:

> *"feature 12 gets asked 'this contradicts BEH-7, supersede?'"*

Kit's conflict detection is **structural and name-keyed** — two behaviours asserting
different values for the same `noun.slot`. That is the design's cheapest and best property;
it needs no LLM and no embeddings. But it means **a contradiction expressed with a different
noun is invisible.** If BEH-7 says `checkbox:HabitDone` and the corpus grows a BEH-12 saying
`checkbox:HabitItem`, the two can assert opposite things about the same control and Kit
reports no conflict.

Finding 1 measures how often that happens between two readings of the *same brief on the same
day*. A corpus that grows over months, across features, is the same problem with time added.

**This is a product decision and it is his**, so three shapes and no recommendation:

1. **A canonical vocabulary.** The corpus declares its nouns up front; a new behaviour must
   use an existing one or explicitly mint a new one. Cheap, structural, and it makes minting
   a visible act.
2. **A reconciliation step.** New behaviours are diffed against the existing noun set and
   near-misses are surfaced for a human to merge. `converge.js` is most of the machinery.
3. **Accept it.** Say plainly that conflict detection catches same-noun contradictions only.
   Defensible — it is still free and still more than Gherkin offers — as long as it is never
   sold as catching contradictions in general.

---

## Finding 3 — they agreed about the *gaps* far more than about the nouns

The asymmetry is the most encouraging result in this trial. Independently, with no shared
context, both writers named the same missing assertion power:

| gap | A | B |
|---|---|---|
| **Boolean state cannot be asserted** — `sees checkbox:X` proves the control exists, not that it is *checked* | ✅ "the largest gap found" | ✅ |
| **Cross-noun derived rules** — "Exercised, auto ticked by running" relates two nouns; the notation can only assert one noun's visible side effect | ✅ | ✅ |
| **Negation** — no `then does not see` | (not raised) | ✅ |
| **Timing / day boundary** — when does "today" roll over | ✅ | ✅ |
| **Quantitative comparison** — "on track", progress against a goal | ✅ | ✅ |

Both also independently reached for `contract` (prose an assertion cannot honestly own) at
exactly the same point: the one explicit rule in the entire brief, *"Exercised - auto ticked
by running"*. The notation's escape hatch was used by both readers for the same sentence.

**Read together with finding 1: the grammar's holes are objective and its vocabulary is not.**
Two readers agree on what the notation cannot say and disagree on what to call things. For a
tool whose thesis is that the spec must constrain the app, that is the right way round — the
constraint is real, the naming is the soft part — but it means the naming is where the
product work is.

For a habit tracker, "boolean state cannot be asserted" is close to fatal on its own: the
whole product is a done/not-done state, and A's BEH-TODAY-3 ("just ticked") and BEH-TODAY-4
("still ticked when reopened later") end up with **identical `then` clauses** despite
describing different moments.

---

## Finding 4 — two smaller things, both found by doing rather than reasoning

**A wrapped `contract` line does not parse.** The parser is line-based on purpose, and a
`contract` continued onto an indented second line raises `unrecognised keyword "same"`. B hit
this, diagnosed it, and worked around it by collapsing each contract to one physical line. It
is a real limit on a field whose whole purpose is prose too long for an assertion.

**I wrote a false directive into my own brief.** I told both writers to mark their corpora
`# kit:not-a-real-app`, which is *false* of a shipped app. Both did as told, correctly. It was
caught while building the honest exclusion — `# kit:duplicate-corpus <app>`, a third axis
asking "is this app already counted", which is the true reason these two corpora must stay out
of the gap #8 saturation study. Recorded rather than quietly fixed, because a corpus asserting
something untrue about the world is the exact failure this project argues against, and the
instruction came from me.

Verified rather than assumed: `saturation.js --check` exits 0 with both new corpora present —
**gap #8's published numbers are unchanged.**

---

## What this trial did NOT do

**It did not build the app.** The title of his ask was *"give a go building them"*, and this
stops at the specification. That is a deliberate stopping point, not a shortfall of time:
finding 2 says the interesting question is no longer "can the contract be satisfied" — with
`requires.js` it plainly can, by construction — but which of the three reconciliation shapes
Kit should adopt. Building against one of them before he has chosen would be answering his
question with a fait accompli. The branch off the initial commit is the next trial, once
there is a decision to build toward.

**It did not compare behaviour-for-behaviour.** Only nouns are compared, because noun identity
is decidable and behavioural equivalence is not. Deciding that A's BEH-TODAY-3 and B's
BEH-LOG-2 "are the same behaviour" is the semantic judgement Kit has consistently refused to
fake, and faking it here to produce a nicer number would be worse than the gap.
