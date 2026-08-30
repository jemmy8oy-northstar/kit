---
tags: [kit, design, process]
updated: 2026-08-29
status: draft — the shape is proposed, the open decisions are listed at the end
inputs: [analysis/strategic-position.md, research/bdd-prior-art.md, prototypes/behaviour-ast]
---

# The Kit process

> James, #68: *"Maybe processes is what we design first."* Agreed, and it's the right instinct for a
> second reason: **every tool in the BDD graveyard died of a process failure, not a syntax failure.**
> So this document describes the loop with no UI, no product and no hosting in it. If the loop is wrong,
> nothing built on top can rescue it.

## The one-line version

```
conversation  →  drafted behaviours  →  resolved corpus  →  bound nouns  →  generated tests  →  code
                      ↑ validate            ↑ holes           ↑ refuse         ↑ coverage gate
                      └──────────────── conflicts adjudicated ──────────────────┘
```

`source code → application build` becomes `behaviours → application build`. **The behaviour corpus is
the artefact the human touches; code is downstream of it.**

---

## Stage 1 — Intent conversation → drafted behaviours

**Input:** a conversation. **Output:** behaviour entries, each attributed and each marked user-stated or
system-inferred.

The model drafts, the human validates. This is deliberate and it is the single most evidence-backed
decision in the design: **twenty years say stakeholders do not author specs**, even when the syntax was
built for them to read (Gherkin, and FitNesse's business-editable wiki tables, both failed the same way).
Draft-and-validate is a cheaper and more honest ask than author.

Two rules that fall out of prior art:

- **An inference is a line in the corpus, not a fact in a chat log.** Approve/deny needs something to
  point at *six weeks later*. An inference that lives only in the conversation cannot be denied once the
  conversation has scrolled. So the model writes it as an explicit line, attributed to the behaviour that
  implied it.
- **On deny, capture the correction.** Your #68 instinct — *"on a deny a required indication of what
  correct behaviour actually looks like should take place"* — is right, and it's the mechanism that makes
  denials compound instead of just deleting a line. A denial with a correction is training data for the
  corpus; a bare denial is a hole.

⚠️ **Open decision:** you proposed inferences are **included by default**. That is right for friction and
wrong for rigour — North's retrospective says the adjudication step is the first thing teams skip, and
default-include makes skipping the path of least resistance. See *Open decisions* below.

## Stage 2 — Resolution: holes are first-class

Behaviours are allowed to gloss. *"User fills out form X"* is a legitimate thing to write, and it creates
unknowns: which fields, which are required. Kit records the hole rather than guessing or blocking.

```
when fills form:Upload with ?fields              # BEH-UP-1 doesn't know
provides form:Upload.fields = VideoOrAudioFile   # BEH-UP-2 does
```

Your example — *"reader opens post, at a glance user can read x y z → these are now inferred as required
fields"* — is exactly this: a hole in one behaviour closed by a different behaviour's `provides`.

> 🔑 **The architectural consequence, and it must be designed in from commit one:** holes resolve across
> documents, therefore **behaviours cannot be isolated trees**. There is one symbol table over the whole
> corpus. A per-file AST can never resolve a hole from elsewhere, and retrofitting that later is a
> rewrite. This came out of building the prototype, not out of theory.

Unknowns surface in an **unknowns view** — the human never has to answer them directly, because most get
filled by writing other behaviours. What remains unfilled at generation time is a refusal, not a guess.

## Stage 3 — Conflict adjudication

Two behaviours asserting different values for the same `noun.slot` is your *"this conflicts with a
previous behaviour, supersede?"*.

**The cheap half is free and should be built first.** It needs no embeddings, no LLM and no latency — it
falls straight out of the symbol table as a `Map` collision, with both sides named. The prototype proved
this. **This is the differentiated feature and it is also nearly the easiest one**, which almost never
happens and is why it should be stage 0.

The expensive half — *semantic* conflict, where two behaviours contradict in meaning without colliding on
a slot — needs a model and is genuinely hard. **It is a later stage.** Note it is the same engine as the
"function vectorisation" idea you parked at the bottom of #68: embed a corpus, find near-duplicates, ask
a human to adjudicate. **Behaviours are the better first corpus than functions** — smaller, higher value
per item, and a wrong answer is a question rather than a bad refactor.

Adjudication outcomes: `supersede` (new wins, old is archived with a pointer), `reject` (new is dropped),
`coexist` (they were not actually in conflict — record why, so the pair is never re-raised).

## Stage 4 — Binding: nouns, not steps

**The most important decision in the design, and the fix for the thing that killed Cucumber.**

Gherkin binds one step definition per *step phrasing*, so glue grows with the spec — unbounded — and a
human maintains the bridge, which means a spec sentence can say anything and the glue makes it pass.

Kit binds by **noun**. `button:SendForExport` has one binding, reused by every behaviour that mentions it:

```json
"button:SendForExport": { "role": "button", "name": "Send for export" },
"link:Download":        { "role": "link",   "name": "Download" }
```

Consequences, all of them the point:

- Glue grows with the **app's vocabulary** (bounded) instead of the **spec** (unbounded).
- The visible UI label lives in **one** place. Rename the button, change one binding — not every scenario
  that mentions it.
- **A noun the app does not have is a build failure, not a passing test.** This is what makes the spec
  *constrain* the app rather than describe it.

## Stage 5 — Generation: refuse rather than guess

Kit emits tests, or emits `// UNGENERATED:` and names the unbound noun.

This is not fastidiousness, it is the answer to the best-evidenced failure mode in the newest research:
LLM-generated specs reach ~94% semantic coverage, and **the residual failure is omission that still
passes** — a test that runs, asserts less than it should, and is green. Automating the glue moves the risk
from "stale step definition" to "silently incomplete spec", which is worse because nothing looks broken.

The prototype refused to emit `page.goto('./editor/')` for a missing route param — a test that would run,
navigate nowhere, pass, and read fine in review. **That refusal is the feature.**

Refusals are **counted, not hidden**. A refused contract still appears in the coverage denominator;
dropping it would flatter the number by deleting the steps that fail.

## Stage 6 — Implementation

The agent builds against the generated tests. This stage is the *least* interesting part of Kit and we
should resist spending design effort on it: coding agents are a commodity fought over by companies with
billions of dollars. Kit's job is to hand one an unambiguous target.

## Stage 7 — The coverage gate

**A behaviour with no test naming its ID fails the build.**

This is the only part of the system that can go red, and therefore the only part that cannot be politely
ignored. It is also the lesson from our own estate: `web-template/docs/testing-strategy.md` is a genuinely
good document, referenced by no issue, no acceptance criterion and no CI step — **so it changed nothing.**

> **A spec no build breaks on is a wish.** Whatever we ship first must be enforced by CI from its first
> commit.

### ⚠️ The gate must check *gating*, not existence — and our own estate proves why

The obvious implementation is "does a test name this behaviour ID?". **That is not enough, and we have a
live counter-example measured on 2026-08-29:**

| repo | `"test": "vitest run"` in `package.json` | run by `ci.yml`? |
|---|---|---|
| `snip-it` | yes | **yes** — `ci.yml:62` |
| `around-the-world` | yes, **byte-identical** | **no** — the frontend job is lint + build + e2e only |

Two repos scaffolded from the same template, with the same test script wired the same way, and **only one
of them actually gates on it.** A check that greps for "is there a test script" calls both safe. ATW is
the app that shipped to real users.

> **So the coverage gate must assert that the test naming the behaviour runs in the CI job that gates the
> merge** — not that the test exists, and not that a script exists. A test suite no workflow invokes is
> not protection; it is dead weight that reads exactly like protection.

---

## What this process is *not*

- **Not "spec before code", and not "tests generated from the spec" either.** Kiro shipped the first at
  GA on **17 November 2025**, and the same release shipped the second — property-based tests extracted
  from the spec, measuring whether the code matches it. **That is stage 5 and stage 7 above.** GitHub Spec
  Kit has 132k stars doing a template version. Writing a spec first is table stakes now, and so is
  generating tests from it; **keeping the corpus honest against itself over time is the part nobody has.**
  ⚠️ So of the seven stages here, **1, 5, 6 and 7 are commodity** — the ones worth our effort are **2
  (holes), 3 (conflict adjudication) and 4 (bind-by-noun)**. Verified 2026-08-30; see
  `analysis/strategic-position.md`.
- **Not a code generator.** Stage 6 is the commodity.
- ~~**Not a UI, yet.**~~ **Superseded by James on #68, 2026-08-30:** *"the site can just start as a page to
  view the defined and inferred behaviours and a location to discuss and add new behaviours/features/
  assertions… for now you can manage it… let's focus on the workflows and the surface."* The surface is in
  scope; the **AI platform behind it is not** — I am the engine for now.

## Decisions — ANSWERED by James on #68, 2026-08-29/30

All four were open when this document was written. He answered all four. Recorded here rather than left
on the thread, because a decision that only exists in a comment is the thing this whole project exists to
stop ([[artefacts-not-states]]).

1. ✅ **Inferences: default-include, but marked unreviewed.** *"I like this default included but marked
   unreviewed."* He took the middle option — the friction of default-include, without the silence.
   **Implemented:** every behaviour carries a `source defined|inferred <ref>` line, and an inferred one
   carries `review unreviewed` until adjudicated. The tool prints a never-adjudicated count, so skipping
   the step is visible rather than free. This is the one part of prior art's three fatal failure modes we
   had no answer for, so it needed to be a real mechanism and not a convention.
2. ✅ **Where it runs: a light web app, not a CLI — and I manage the reasoning.** *"Now we have such a
   strong structure I feel like a lightweight app is much more of a higher cost than cli, maybe we can go
   with light web app… the site can just start as a page to view the defined and inferred behaviours and a
   location to discuss and add new behaviours/features/assertions. And for now you can manage it. Then
   later we can build it out into a system driven by some ai api."*
   ⇒ **The corpus still lives in git** (it must — it has to be diffable and reviewable in a PR, and the
   coverage gate has to run in CI). The web app is a **view and a discussion surface over it**, not a
   second store. No LLM API in the build; I am the engine.
3. ⚠️ **Does the corpus assume our stack? — still open, but now answered in practice.** Piloting on two
   web-template apps means yes for stage 0. Worth revisiting only if Kit ever leaves our estate.
4. ✅ **First target: `james-habits-app` and `language-vocab`.** *"Let's pilot it on the non technical apps
   for now: habits app, vocab app."* Better than my suggestion of ATW or snip-it: both are small enough to
   hold in one head, both are real, and — the part I had not appreciated — **both skipped the SDD pipeline's
   spec stages entirely**, so their defined and inferred behaviours have never once been reconciled. That
   is the exact condition Kit claims to fix, occurring naturally rather than staged.
