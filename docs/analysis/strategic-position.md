---
tags: [kit, analysis, strategy]
updated: 2026-08-29
status: draft
inputs: [research/competitive-landscape.md, research/bdd-prior-art.md, claude-code-bot#68]
---

# Where Kit fits — and the part of my August answer that has expired

## Read this first

1. **Half of the moat I argued for on 16 August has been commoditised since.** I said the differentiator
   was *"the spec is the source, and it can't silently rot"*. **AWS Kiro went GA in March 2026 doing the
   first half of that** — `requirements.md` in EARS notation, `design.md`, `tasks.md`, generated before
   code and gated on human approval. GitHub Spec Kit has 120K+ stars doing a template version. **"Spec
   before code" is now table stakes in the engineer tier, not a wedge.**
2. **The second half is still open, and it is the harder half.** Nobody ships *automated contradiction
   detection over an accumulated spec corpus*. Kiro's conflict check is a human reading a diff. Spec Kit
   is a scaffold with no reasoning over the spec. Tessl has stated exactly this ambition and, ~9 months
   in, still hasn't shipped its core framework. VibeDrift detects drift but infers intent statistically
   from code, so it cannot tell a deliberate new requirement from a bug.
3. **The consumer lane is unwinnable and we should stop looking at it.** Lovable ~$500M ARR, Base44 2M+
   users inside Wix, Bolt $0→$40M in five months, Cursor at a reported $60B acquisition. These are not
   competitors we out-execute; they are weather. (Figures single-sourced — see the ⚠️ flags in the
   research doc.)
4. **The BDD graveyard names the three ways this dies**, and our prototype already answers two of them
   by accident. That is the most encouraging finding in the pack.
5. **Kit pays for itself as our internal process whether or not it is ever a product.** That is the
   staging argument, and it is stronger now than in August because the process half is proven: ATW
   shipped to real users off a front-loaded spec written before any agent started.

---

## 1. What changed since 16 August

My original answer on #68 said the moat candidate was your almost-throwaway line — *"this conflicts with
a previous behaviour, supersede?"* — because every AI builder is good in week 1 and bad in week 12, since
nothing holds the accumulated intent of the app.

**That reasoning survives. The positioning built on it does not.** In the two weeks between, the market
moved into the first half of it:

| | ships today | what it does about conflict |
|---|---|---|
| **AWS Kiro** (GA Mar 2026) | persistent `requirements.md` (EARS) → `design.md` → `tasks.md`, human gate before code | **a human reads it.** No automated contradiction detection found |
| **GitHub Spec Kit** (120K★) | Constitution → Plan → Tasks → Implement, agent-agnostic | none — it is a workflow scaffold, not a reasoning engine |
| **Tessl** | Spec Registry (third-party library specs) in open beta | *stated goal* is drift detect-and-reconcile. Core framework **still closed beta after ~9 months** |
| **VibeDrift** | scans a codebase, flags files deviating from inferred patterns | automated, but **no explicit spec** — cannot distinguish "new requirement" from "bug" |
| **Everything consumer** (Lovable, Bolt, Base44, Replit, v0) | chat log + current code | **nothing.** Lovable's official advice is literally "don't regenerate, you'll lose the parts that worked" |

So the honest read: **the artefact is no longer novel; the *reasoning over the artefact* is.** If Kit's
pitch is "we write a spec first", we are eighteen months late to a fight with AWS and GitHub. If it is
"your spec corpus is checked for contradictions and asks you to adjudicate", nobody has shipped that.

**This should update the plan, not kill it.** Tessl taking nine months and not shipping is the strongest
available evidence that the hard part is genuinely hard — which is what a moat looks like from the inside.

## 2. The three ways this dies, from the BDD graveyard

Twenty years of prior art says a behaviour-spec tool fails in exactly three ways. Two of them our
prototype already avoids, which is worth knowing before we scope anything.

### (a) The glue rots, so the spec becomes descriptive

Every Cucumber-family tool binds a spec sentence to a hand-written step definition. Because a human
maintains that bridge, **a spec sentence can say anything and the glue makes it pass**. The spec stops
constraining the app and starts describing it, and a descriptive spec is a wish. Cucumber's own docs
carry an anti-patterns page for this. SpecFlow — the .NET leader, our stack — went unmaintained for 2.5
years and needed a community fork (Reqnroll, Jan 2024) before Tricentis declared EOL on 31 Dec 2024.

> **We already have an answer, and it is the single best idea in the prototype: bind by *noun*, not by
> step.** Glue then grows with the **app's vocabulary** (bounded — one entry per button, page, form)
> rather than with the **spec** (unbounded — one entry per phrasing). A noun the app doesn't have becomes
> a build failure instead of a passing test. This is the structural fix Gherkin never had.

### (b) Stakeholders never actually author the spec

This is the finding I'd least like to be true and it is the best-evidenced one in the pack. Cucumber's
own 2014 retrospective, current 2026 practitioner writing, and the most literal attempt ever made
(FitNesse's business-editable wiki tables) all converge: **business people do not write or maintain the
spec artefact, even when the syntax was designed for them to read.** Readable-by is not authored-by, and
the gap looks identical twelve years apart.

> **Your design already dodges this and I don't think you noticed.** You wrote: *"some user defined and
> some system inferred… the user can have some form of approve / deny on the inferred but by default they
> get included."* That is **draft-and-validate**, not author. It is the exact shape the evidence says
> works and every failed tool assumed away. Do not let it drift back toward "the user writes behaviours".

### (c) The generated spec is silently incomplete, and the tests pass anyway

The newest research is blunt about where automation moves the risk rather than removing it. *From Law to
Gherkin* (arXiv 2508.20744) generated 60 specs from food-safety legislation: usable as first drafts, but
**omission is the dominant failure mode** — an overlooked constraint yields a test that is green and
wrong. Automating the glue converts "stale step definition" into "silently incomplete spec", which is
worse, because nothing looks broken.

> **The prototype's instinct here was right too:** it emits `// UNGENERATED:` and names the unbound noun
> rather than guessing a locator, and it refused to emit `page.goto('./editor/')` for a missing route
> param — a test that would run, navigate nowhere, pass, and read fine in review. **Refusing to generate
> is a feature and must stay one.** Coverage that is asserted rather than assumed is the whole product.

### (d) — and the one we have *not* answered

North's own retrospective: BDD failed partly because *"the magic isn't in specifying behaviour as
Given-When-Then triples, it is in specifying behaviour as a team"*, and the collaboration step is the
first thing teams skip. **The tool must make that step structurally necessary rather than recommended.**
For a solo builder using Kit, "the team" is you-plus-the-model, so the adjudication moments (approve an
inference, resolve a conflict, fill a hole) *are* the collaboration. **That means those moments cannot be
skippable defaults.** Currently your design says inferences are "included by default", which is right for
friction but wrong for this — worth deciding deliberately rather than by accident.

## 3. What is actually differentiated

Ranked, most defensible first:

1. **Structural conflict detection with a supersede UX.** Two behaviours asserting different values for
   the same `noun.slot` is a contradiction that falls out of a symbol table — no embeddings, no LLM, no
   latency. The prototype proved it is nearly free. **Nobody ships this.**
2. **Cross-document hole resolution.** Your *"unknowns get filled in when other parts are filled out"*.
   The prototype's finding: this forces one symbol table across the whole corpus, so **behaviours cannot
   be isolated trees** — a per-file AST can never resolve a hole from elsewhere. That is an architectural
   constraint to design in from commit one, not a feature to add later.
3. **Noun-binding.** The fix for the 20-year-old glue problem.
4. **Refuse-rather-than-guess generation.** The answer to the omission failure mode.

Items 1 and 2 are yours from the original issue. 3 and 4 came out of building the prototype. **All four
are about keeping the spec honest over time — none is about generating an app faster.** That is the
product, and it is a different product from Lovable, not a cheaper one.

## 4. The uncomfortable questions

Stated plainly rather than buried, because they should shape the plan:

- **Is the market engineers or non-engineers?** Your #68 framing was "Lovable isn't good enough for
  engineers like me". But engineers are exactly who Kiro, Spec Kit, Cursor and Claude Code are already
  fighting over, with far more capital. The non-engineer lane is where the money demonstrably is and
  where our differentiator (rigour) is least valued. **There may be a third answer** — engineers *at
  small orgs shipping real apps*, which is precisely us — but I can't validate demand from here and any
  number I gave you would be invented.
- **Is Kit a product, or our process?** These need different first commits. As a process it is unarguably
  worth building — ATW proved the manual version works. As a product it is a funded-competitor fight.
  **I'd rather build the process well and let the product question be answered by using it.**
- **Does Kit assume our stack?** Everything cheap here (Playwright binding, the web-template CI harness,
  the OpenAPI generator) assumes .NET + React + our template. Generalising costs most of the leverage.
- **Is "no code" even the right frame any more?** The name says no-code; the differentiator is rigour for
  people who *can* code. Those pull in opposite directions on every UI decision we'll make.

## 5. My recommendation

**Build stage 0 as our process, on our own repos, and treat the product question as deliberately
unanswered until it has run on two real apps.**

That is the same conclusion as August, but it now rests on evidence rather than instinct, and one thing
has changed: **do not lead with "spec before code"** — that ship sailed in March. Lead with the corpus
staying honest, because that is what nobody has.
