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

### Where a corpus's bindings live — ANSWERED by James on #66, 2026-09-25

The question this answers was never asked out loud for four weeks. It sat on the board as a note to
myself — *"where a relocated project's bindings live is a real open question, do not build it just
to finish the pair"* — correctly reasoned, with no default, no date and no thread, which is how a
decision-shaped sentence hides in plain sight.

I offered two options and priced option 2 as the costlier. He took it, on a scaling argument I had
not made:

> *"I think lives in a repo not shared in kit. Imagine scaled to 1000 projects and 1000 project
> owners no need to share nouns."*

1. ✅ **A corpus's bindings live BESIDE IT, in `behaviours/<app>.bindings.json`, and belong to that
   corpus alone.** This is the same decision as #52's *"the projects spec should live in the projects
   repo"*: a project owns its vocabulary exactly as it owns its spec, and two projects naming the same
   noun is not a collision — it is two projects, each right about itself.
2. 🔑 **What it replaces, and what that cost.** There was one flat map over every corpus, so the noun
   namespace was GLOBAL. An unprefixed `page:Home` in the macro-metrics corpus inherited snip-it's
   `./` and emitted a test that RAN, against the wrong app, with no unbound-noun warning. Two of the
   three bound corpora hand-prefixed every noun to dodge it, which the file itself called *"not a
   design, it is a habit"*. `node prose-audit.js --demo-collision` prints the old hazard beside the
   refusal that replaced it.
3. ✅ **The migration was lossless, and that was measured before it was claimed.** The flat map's 43
   real nouns split **16 / 13 / 14** across exactly three corpora (`kit-ui`, `macro-metrics`,
   `snip-it`) with **0 claimed by two corpora and 0 orphaned** — so the duplication cost I priced
   option 2 at is zero today. The other 7 of 10 corpora bind nothing and get no file, which is a real
   state rather than an error. Every artefact `kit.js` produces hashes identical to before **except
   one**: the all-corpora ratio moved `43/179` → `43/189`, because a name used by three corpora is now
   three things to bind rather than one. That is the decision showing up in the number, not drift.
4. ⚠️ **`sharedWith` is kept, and its meaning changed under it.** It used to mean *"your bind just
   became their bind too"*. It now means *"these corpora use this NAME and bind it themselves"* —
   still worth knowing while you are naming things, and worth nobody's alarm. So the sentence it feeds
   changed on the CLI and on the screen, and stopped being a `role="alert"`. Retiring the mechanism
   outright would be a larger call than the one he made.
5. ⚠️ **Two side effects, both closed by construction rather than by care.** `--bindings` is gone from
   `ui.js`, `writer.js` and `project.js`: it named one file, which a run spanning several corpora
   cannot use, and `--dir` now isolates both. That is what makes `selfhost/run.js`'s half-applied
   isolation — it copied the corpus and not the bindings, so a self-hosted bind would have written
   into the real repo — impossible to write down again. And `saturation.js --dir <elsewhere>` used to
   read *this* directory's bindings against a foreign corpus, silently collapsing 27 bound targets to
   1; it now reads the directory it was pointed at.

## Stage 5 — Generation: refuse rather than guess

Kit emits tests, or emits `// UNGENERATED:` and names the unbound noun — plus a
`kit-ungenerated` Playwright annotation, so the refusal reaches the test report and not only a
reader of the file (kit#31, 2026-09-24). It changes no test outcome, deliberately.

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

## How a spec change reaches git — ANSWERED by James on #52, 2026-09-12

He opened #52 with a Gemini transcript proposing a branch-per-spec-change flow, then decided it in his
own words. Three decisions and a sequencing rule:

1. ✅ **A spec change opens a branch and a PR, and the PR starts red.** *"I was thinking the user changes
   the spec which creates a new pr, then in the background you can update the implementation to get the
   tests passing and match the new spec."*
   🔑 **He picked neither option I offered.** Mine were *spec-only* (the `.beh` travels alone, CI green,
   auto-mergeable) and *spec+tests* (red by design, he merges it when the implementation lands). His is
   better than both: the PR is **an opened work item whose acceptance criteria are executable**, and the
   implementation lands *in that same PR*. So the merge is gated on the behaviour existing rather than on
   a policy about auto-merge — and **"auto or manual" stops being a question**, because a PR cannot
   auto-merge while its own generated tests are failing.
   ⇒ Kit emits a *ticket*, not a commit. The hand-off into the existing workflow is the
   `claude-code-bot` label, which is how every other work item here reaches me.
2. ✅ **The corpus lives in the project's repo.** *"I feel like the projects spec should live in the
   projects repo."* This is the *same* decision as (1): spec and implementation can only be one PR if
   they share a repo. Recorded with its full cost in `ui.md`; the load-bearing consequence is that
   **`kit.js`, and therefore the stage 7 gate, cannot yet read a corpus outside its own directory.**
3. 🔴 **SUPERSEDED 2026-09-30 — the GitHub App does reads *and* writes, and commits belong to Kit.**
   On `kit#88` he reversed this in as many words: *"Let's let the commit belong to kit for now, when we
   want to enable more users we can extend to use GitHub oauth but using the gh app speeds up timelines
   for now and we know how this works let's take this approach for now **override the previous
   decision**."* So the operative decision is the **App installation credential** — the `kit-github`
   secret he created, holding an app id, an installation id and a private key.
   🔑 **What he traded, stated plainly because the record should not flatter the choice:** the argument
   that carried the original decision was **commit attribution**, and this gives it up. Every write-back
   continues to commit as `kit <kit@users.noreply.github.com>` rather than as him. He knows — it is the
   thing the sentence above concedes — and he judged shipping sooner worth more than authorship while
   Kit has exactly one user. **OAuth returns when it has more than one**, and that is the condition to
   re-read this on, not a date.
   ⚠️ **The reversal was not noticed by him unprompted, and that matters for how this file is used.**
   He created the `kit-github` secret on 2026-09-27 while describing it as something *"the backend can
   use to read and write to gh"* — i.e. he had already chosen the App route in practice, fifteen days
   and many threads after ruling it out, without connecting the two. It was resolved only because the
   two rulings were put side by side and he was asked which won. ⇒ **A decision recorded here is not
   self-enforcing; the record's value is that it can be held up against a later one.**
   ✅ **What is unaffected.** `kit#47`'s password is still not wasted, for exactly the reason it never
   was: the `auth.js` that PR introduces (⚠️ **not on `dev` yet** — #47 is open at the time of writing)
   mints an opaque session via `sessions().create()`, which takes no argument and knows nothing about
   how identity was proved. **The App credential and the password sit at different layers** — the App is
   how Kit talks to GitHub, the password is how a person is let into Kit — so the App route does not
   remove the need for the second, it only changes what pushes.
   ⚠️ **The secret count still goes up, not down.** The password is one hand-made value; the App route
   needs a private key as well. Gemini's "zero password risk" framing was backwards in this estate and
   still is. What survives intact from the original argument is the part that never counted secrets:
   **GitHub auth dissolves the push-credential question**, because the installation token *is* the push
   credential.

   <details>
   <summary>The decision this replaces, kept verbatim — a superseded ruling is evidence, not clutter</summary>

   > 3. ✅ **The UI authenticates with GitHub.** *"Sounds good"*, to the recommendation that Kit use
   >    GitHub auth — **the web flow, and the user's own token, not an App installation token.**
   >    […] The argument that carries the decision is the one that never counted secrets: **GitHub auth
   >    dissolves the push-credential question** — the user's token *is* the push credential — and it
   >    buys commit attribution, which is genuinely missing today (every write-back commits as
   >    `kit <kit@users.noreply.github.com>`).

   Stated on `kit#52`, 2026-09-12; landed here when `kit#53` merged 2026-09-23; overridden on `kit#88`,
   2026-09-30. It was in force for seven days and was never built, which is the only reason the reversal
   cost nothing but this paragraph.

   </details>
4. 📋 **Sequencing: Kit is validated on Kit before any other repo is onboarded.** *"I think maybe we
   validate using kit before onboarding other repos and adding the specs etc."* This selects the step
   `ui.md`'s own sequence already lists as its last: Kit's corpus in browser verbs, gated by
   `kit check`, so the self-hosting claim is measured rather than argued.
5. ✅ **The hosted engine becomes C#; Node stays.** Decided on `kit#88`, 2026-09-30: *"Let's move to
   csharp, I want it closer to my stack, I want it scalable from the start, I want the csharp, react,
   vite, autogen stack. I also want to move this to a server model."* **Three qualifications are his own
   words and all three narrow the work**, so none of them should be read out of the record:
   - *"maybe we can keep node around for now for local dev as an option"* ⇒ **this is not a migration
     that deletes the Node engine.** The 181 mutants and the suite they hold keep running.
   - *"we also intend to have a Claude plugin… maybe when we get to implementing the plugin we go for an
     mcp approach but let's not worry for now"* ⇒ Node is kept for a **second product**, not out of
     caution. That product is out of scope for now by his own instruction.
   - *"crack on with hosted csharp"* ⇒ the target is **the hosted path only.**
   🔑 **The reason he gave was deployment confidence — *"We know how to deploy dotnet + react"* — and it
   is worth recording that this premise was half false when examined, without that changing the
   decision.** Kit's frontend already *is* the house stack (React 19, Vite 7, TypeScript 5.9,
   `react-router`, plus the design system — which `web-template`'s own frontend does **not** yet
   consume). What was genuinely unaligned was the backend (bare `node:http`), the directory layout, and
   the absence of any image or chart. A wrong premise does not refute a proposal: the skill he is
   pointing at is Docker/OCIR/Helm/ArgoCD, which does not inspect the language in the image, but
   "closer to my stack" and "scalable from the start" are preferences he is entitled to hold directly.
   🔑 **Why Node keeping running is load-bearing rather than sentimental.** Generation is **byte-for-byte
   deterministic** — measured 2026-09-30 across four corpora and three entry points, and **re-measured
   2026-10-03 across seven entry points and all eleven committed corpora: 77 pairs, 77 identical, 0
   differ**, each pair run in two separate processes so shared module state cannot hide an ordering
   difference, with the exit code inside the hashed observation. No timestamps, PIDs or absolute paths
   leak into the output. So the Node engine can serve as the **executable specification** the C#
   implementation is verified against, via committed golden files. That is what stops the port discarding
   the evidence the 181 mutants represent, and it is why the conformance harness is built *before* any
   module is ported.
   ✅ **That harness now exists: `prototypes/behaviour-ast/conformance.js` plus a committed golden per
   corpus**, compared by `kit.test.js` on every CI run. It is sectioned by engine module — `parse`,
   `resolve`, `generate` — so a C# `Parse` can be scored on its own while the other stages are still
   JavaScript. One opaque blob per corpus could not score a half-ported engine, which is the same as
   having no harness for the whole length of the port.
   ✅ **Phase 1 carries BOTH runtimes — a C# server with the Node engine behind one engine interface.**
   This was the last open architecture call inside his C# decision, and it is now settled rather than
   queued. He said *"Can you work on migrating it to csharp next I feel like that's highest value"*
   (`kit#88`, 2026-10-03), which is the event the queued question was waiting for: it was deliberately
   dated into the future on the grounds that it was *"not answerable-and-needed until the port actually
   starts"*. Before that, on 2026-10-02, he was told **"say otherwise and I will hold"** and did not.
   The reason this is cheap to have settled rather than escalated: **Phase 1 is written against one
   engine interface either way**, so what sits behind that interface can be swapped without
   invalidating Phase 1. The Node layer is dropped only once the C# engine reproduces the goldens above
   byte-for-byte — which turns *"drop Node"* into a measurable finish line instead of an intention.

### 🔴 The middle of that loop does not exist yet

Gemini's step 5 — *"CI runs spec-to-test generator & validation"* — has **no implementation**, and the
flow above rests on it. Measured 2026-09-12:

- `ci.yml` runs Kit's **own** suites only. **No job runs a generated test.**
- Generated Playwright specs are written to `os.tmpdir()` by `selfhost/run.js` and **never enter a
  repository**. That harness is a manual gate and must stay one: Kit has no `@playwright/test`
  dependency, so it takes the binary on the command line.

⇒ Closing the gap needs a dependency (**packaging**) *and* a workflow (**platform**) — both James's under
claude-code-bot#83. Until then **a spec-change PR can only carry the `.beh` file itself**, which is the
one thing his loop cannot do without.

⚠️ **This is an absence, so no build reports it** and a green CI badge is not evidence against it. It is
recorded here because that is the only place it can be.

### One mechanical constraint on the write-back target

Write-back should target a **dedicated branch, not `dev`** — and the reason is mechanical, not policy.
`ci.yml` is `cancel-in-progress` keyed on the ref, so a spec edit pushed to `dev` **kills an in-flight
CI run** there; and `writeBack` never pulls first, so from a stale clone the write **strands** rather
than conflicting. Nothing forbids `dev` (kit has no rulesets and `dev` is unprotected — a push would be
accepted), which is exactly why the constraint needs writing down.
