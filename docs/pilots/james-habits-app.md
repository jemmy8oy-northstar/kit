---
tags: [kit, pilot, james-habits-app]
updated: 2026-08-30
status: measured — every number below came out of `node prototypes/behaviour-ast/kit.js james-habits`
inputs: [claude-code-bot#68, james-habits-app@e75de89, design/process.md]
---

# Pilot 1 — `james-habits-app`

> James, #68 (2026-08-30): *"Let's pilot it on the non technical apps for now: habits app, vocab app."*

The corpus is `prototypes/behaviour-ast/behaviours/james-habits-app.beh`. **Nothing in it is invented.**
Every `defined` behaviour cites a line in that repo's own `docs/DESIGN.md`; every `inferred` behaviour
cites a test method or route registration that exists on `origin/dev` at `e75de89`. The `source` line is
the trace, and it is checkable — a reader can go and look, which is the entire point.

## The headline, and it is not flattering

```
behaviours            22        defined by a human    10
generated lines        0        inferred by the model 12
wire contracts        20        NEVER ADJUDICATED     12
ungenerated           26        untraceable            0
unbound nouns         15
generated / total    0/46 = 0%
```

**Kit generates nothing for this app.** That is the honest result of pointing the prototype at a real
target instead of at snip-it, whose corpus was reverse-engineered from an existing Playwright suite and
therefore could not fail. It is worth stating plainly rather than burying, because it is exactly the
"stop condition" `timeline.md` names as the most likely and cheapest failure to discover.

**But 0% has two completely different causes, and the tool currently reports them as one number.**
Separating them is the most useful thing this pilot produced:

| cause | count | what it means | whose gap |
|---|---|---|---|
| **unbound noun** | 15 | `page:Today`, `region:HabitList`, `badge:Streak`… none exist. The frontend on `dev` is still the untouched web-template scaffold — one `Home` page. | **the app's.** Kit is right to refuse |
| **wire contract** | 20 | upsert-on-`(habitId,date)`, 404 + error code, idempotent seeding. Real, tested behaviour that a UI-shaped behaviour tree has no way to assert. | **Kit's** |

The first is Kit working. **Fifteen nouns the spec names and the app does not have is a measurement of
"defined but not built", produced with nobody maintaining a list** — and it will go to zero by itself as
PRs #12 and #13 land. The second is Kit's own limitation and it needs your decision (below).

## What it found that a human had not

### 1. A real contradiction, with no LLM and no embeddings

```
region:CompletionGrid.days: BEH-WINDOW-DEFINED say [30]; BEH-WINDOW-INFERRED says [caller-supplied]
```

`docs/DESIGN.md#mvp-5` promises a **30-day** completion grid. The route that shipped
(`HabitRoutes.cs:GetHabitHistory`, pinned by `History_returns_a_grid_of_the_requested_length`) takes a
caller-supplied window. Two claims about one slot. It fell straight out of the symbol table as a `Map`
collision with both sides named — **this is the differentiated feature, and it is also nearly the cheapest
one**, which is why `process.md` puts it at stage 0 rather than saving it for later.

It is a small contradiction. That is the point: it is exactly the size of thing that survives review
forever, and it was found in the first corpus we ever pointed at a real app.

### 2. Six behaviours the code asserts that no document states

All twelve inferred behaviours are `review unreviewed` — included by default per your #68 call, but not
silently. **Six of them are genuinely absent from `DESIGN.md`:**

| behaviour | what the code asserts | where `DESIGN.md` is silent |
|---|---|---|
| `BEH-ORDER-1` | a new habit is appended last in `sortOrder` | position on create is never stated |
| `BEH-ARCHIVE-1` | an archived habit is still retrievable **by id** | A5 says "archive, don't hard-delete"; retrieval is not covered |
| `BEH-ARCHIVE-2` | `GET /api/habits` takes a flag that includes archived | no such flag anywhere in the doc |
| `BEH-STREAK-3` | duplicate/unordered dates don't inflate the longest streak | A4 defines streaks over clean data only |
| `BEH-ERROR-1` | 404s carry a `ProblemDetails` body with a machine-readable code | no error contract in the doc at all |
| `BEH-SEED-1` | seeding runs **exactly once** and is idempotent | A5 asks for a starter set; idempotence is an added decision |

None of these are wrong. They are **decisions made in code and never written down** — the failure mode Kit
exists for, occurring naturally in an app nobody staged for a demo.

> ⚠️ **Three of the twelve are NOT in that list, and saying so is the point.** `BEH-ENTRY-1`
> (re-logging overwrites) is squarely documented — A2 says *"logging is an idempotent upsert — re-tapping
> toggles/overwrites"*. `BEH-REORDER-1`'s capability is in the MVP scope (only the whole-list endpoint is
> missing from the doc's API list). `BEH-HISTORY-2` is the contradiction in §1, not an omission. A count
> of "everything the corpus inferred" would have read **twelve**, and twelve would have been wrong —
> the number that matters is the one left after checking each against the document.

### 3. Two documented requirements that nothing anywhere enforces

- **`A7` — the client's local date is stored verbatim, no timezone conversion.** An assumption with a
  real failure mode and **no test in the repo names it**.
- **The `<20 second` daily-loop budget** — `DESIGN.md` calls it "the design constraint that outranks any
  feature", and it is enforced by nothing.

Both are recorded as `contract` lines, so they sit in the coverage denominator rather than vanishing.
Dropping them would flatter the number by deleting the two requirements most likely to be missed.

## What this says about the pilot targets you picked

You chose these two apps as the *non-technical* ones, and on the product side that is right — habits and
vocabulary are easy to reason about in plain language. **But the property that makes them hard for Kit is
that their behaviour currently lives below the UI**: 41 backend tests and 37 in `language-vocab`, and
**zero frontend tests in either**. Neither has any epic, feature or BDD artefact — both skipped the SDD
pipeline's spec stages entirely and went from a prose idea issue straight to a design doc and code.

That is a *better* pilot than a staged one, because their defined and inferred behaviours have never once
been reconciled. It just means the first thing the pilot hit was Kit's own notation boundary rather than
the app's.

## The decision this raises — yours, not mine (#58)

**Does Kit assert at the API layer as well as the UI layer?**

Today a noun binds to a Playwright role/name, so `button:SendForExport` becomes
`getByRole('button', {name: 'Send for export'})`. Nothing binds `PUT /api/entries`. On these two apps
that is where 20 of the 46 steps live.

- **If yes:** an `endpoint:` noun binds to method + path, and the generator emits an integration-test
  assertion instead of a Playwright one. Mechanically it is the same shape as the existing generator and
  the symbol table does not change. It roughly doubles what Kit can express on our estate.
- **If no:** Kit is a UI-behaviour tool, the 20 contracts stay contracts, and the pilot's honest answer is
  that it should be measured against apps once they *have* a UI — which for habits is PRs #12/#13.

I lean **yes**, because on a .NET+React estate the interesting invariants (upsert semantics, error codes,
ordering) are backend ones and a tool that cannot see them is describing the shell. **But it widens what
Kit is, so it is an architecture call and yours.** Nothing in this PR depends on the answer: the corpus,
the contradiction and the gap measurement are all true under either option.

## Reproducing this

```
node prototypes/behaviour-ast/kit.js james-habits    # the numbers above
node prototypes/behaviour-ast/kit.test.js            # 27 passed
```

The suite pins the contradiction, so it cannot silently stop being found; it also asserts every pilot
behaviour cites a source, because a corpus that cites nothing looks identical to one that cites
everything right up to the moment someone tries to check it.
