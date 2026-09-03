---
tags: [kit, analysis, leverage]
updated: 2026-08-29
status: draft
---

# What Kit can leverage from the existing estate

Survey of `web-template`, `around-the-world`, `snip-it`, `design-system`, `macro-metrics`, `repo-template` (all
in the org) and the running prototype, now vendored here at `prototypes/behaviour-ast/`. Every claim below
is either **verified** (I read the file / ran the command) or **inferred** (labelled explicitly). Paths are
`path:line` where that adds precision.

## Read this first

- The estate already has a **prose spec pipeline** (`docs/specs/sdd-workflow.md` + `.github/ISSUE_TEMPLATE/`) that
  produces genuinely good acceptance criteria (macro-metrics is the best worked example) — but nothing enforces
  that a story's AC ever becomes a test. The estate's own retro says this in writing:
  `macro-metrics/docs/retros/poc/03-template-gaps.md:342-379` (Gap 15) — testing strategy exists, is not
  referenced from any issue body, and 200+ tests were produced "because the AI chose to," not because anything
  required it.
- **CI gating is inconsistent even for the same npm script across sibling repos.** `around-the-world` has a wired
  Vitest suite (10 test files, `npm run test` in `package.json`) that **does not run in CI** — only lint + build +
  e2e do. `snip-it`'s identical setup **does** gate `npm run test` in CI. This is exactly the failure mode Kit's
  coverage check is meant to make structurally impossible.
- The Kit prototype (`prototypes/behaviour-ast/`) is real and runs clean: `node kit.js` and
  `node kit-test.js` both executed successfully during this survey (**72/72 tests pass, 37/37 mutants killed**,
  78% of steps generated against snip-it's 8 behaviours), and `node compare.js` shows 25/28 generated lines
  exactly or near-exactly match snip-it's actual hand-written Playwright spec on `origin/dev`. It is a prototype
  answering one falsifiable question, not a product — see gaps below for what it deliberately does not do.
  ⚠️ This bullet said **19/19 tests** for two weeks while the suite grew to 72 — Kit's own repo drifting in
  precisely the way Kit exists to catch. Re-measure it when you touch this file; do not copy it forward.
- The deterministic, non-LLM half of Kit's bet is already proven twice in this stack: OpenAPI → RTK Query codegen
  (frontend/backend contract sync) and a Roslyn `IIncrementalGenerator` (interface mirroring) both exist, ship,
  and are documented. Kit's "noun binding → generated test" step is architecturally the same shape as these.
- **Nothing in the estate binds a test to a spec ID.** Not by noun, not by any mechanism. The `[BEH-ID]` /
  `named(...)`-in-test-title pattern the prototype uses does not exist anywhere else surveyed — this is the one
  piece of Kit's core idea with zero precedent in the codebase.

## Summary table

| Asset | Where it lives | Reusable for Kit? | Note |
|---|---|---|---|
| SDD phase-gated spec pipeline | `web-template/docs/specs/sdd-workflow.md` | Partial | Defines *when* a story is written and *what* AC should reference, not machine-checkability |
| User Story issue template | `web-template/.github/ISSUE_TEMPLATE/user-story.md` | Partial | Free-text `- [ ]` checkboxes only, no structured AC format |
| Worked BDD acceptance criteria | `macro-metrics/docs/user-stories-frontend.md` | Yes, as corpus | Close to assertions already (see §2); would need re-authoring into behaviour-tree syntax, not the AC content |
| Testing-strategy doc | `web-template/docs/specs/testing-strategy.md` | No (as enforcement) | Prose only; own README calls it "referenced by nothing" — confirmed true (see §1) |
| Playwright e2e harness | `web-template/frontend`, `around-the-world/frontend`, `snip-it/frontend` | Yes | Kit's prototype emits Playwright directly; matches existing convention exactly |
| Vitest + RTL unit harness | `around-the-world/frontend`, `snip-it/frontend` (NOT `web-template`) | Yes, partially wired | Present + scripted in 2 of 3 repos; gated in CI in only 1 of those 2 |
| xUnit + Moq backend harness | documented in `testing-strategy.md`; not directly audited this survey | Not verified | Backend `dotnet test` runs in CI for all three .NET repos (verified in ci.yml), content not read |
| OpenAPI → RTK Query codegen | `web-template/docs/specs/openapi-codegen.md`, `frontend/openapi-config.cjs` | Yes, as proof-of-pattern | Deterministic generator already in the stack; not itself repurposable for behaviour binding |
| `InterfaceFromConcreteGenerator` (Roslyn) | `dotnet-libraries/src/Northstar.SourceGenerators/InterfaceFromConcreteGenerator.cs` | Yes, as proof-of-pattern | Proves incremental, deterministic C# codegen from attributes is already production-shaped in this org |
| Kit prototype (`kit.js`) | `prototypes/behaviour-ast/` | Yes — it IS the core | parse/resolve/generate/coverage all implemented and tested; see §5 for exact scope |
| ID-bound test coverage check | nowhere else in the estate | No precedent | Confirmed absent everywhere surveyed |

---

## 1. The spec/issue pipeline that already exists in prose

`web-template/docs/specs/sdd-workflow.md` (405 lines, read in full) defines a 7-phase, gated workflow:
Vision → Epic/Feature breakdown → UI/UX design (ASCII mockups + Mermaid) → User Story Definition → UI + Skeleton
Backend → Backend Architecture & DB → Backend Implementation. Each phase has a **hard gate** (line 17: "The next
phase must not begin until all gate conditions are met") enforced only by convention — an AI/developer discipline,
not tooling.

Key mandate for user stories (`sdd-workflow.md:171-199`, Phase 4): stories are written *after* mockups are
signed off, in the format *As [role], I want [action], so that [benefit]*, with **Given/When/Then acceptance
criteria**, and the story quality bar is explicit: "A good story has acceptance criteria specific enough that
there is no ambiguity about whether it is done" (`sdd-workflow.md:199`). This is the human-authored equivalent of
Kit's ambition but has no machine check behind it.

`.github/ISSUE_TEMPLATE/user-story.md` (verified, full text below) is the template every `[3]` story issue is
created from:

```
## Story
> As a **[persona]**, I want to **[action]** so that **[outcome]**.

## Acceptance Criteria
<!-- Reference specific UI states from the signed-off ASCII mockup or API contract -->
- [ ]
- [ ]
- [ ]

## Design Reference / Spec Reference / Dependencies / AI Notes
```

It is unstructured: acceptance criteria are free-text checkbox lines, not a parseable grammar, and nothing ties
a criterion to a test. There is also `.github/ISSUE_TEMPLATE/spec.md` (Phase 1 vision questionnaire) and
`ux-design.md` (Phase 2 design issue: ASCII mockups + Mermaid diagrams, AC = "signed off by developer" — also
unstructured). No `default.md` content was read (out of scope for this question).

`testing-strategy.md` (177 lines, read in full) documents a three-tier pyramid — Vitest+RTL (frontend), xUnit+Moq
(unit), xUnit+Moq in-process integration (no test DB, no containers) — and states directly (`:10`): "Tests are a
spec artefact — a unit test is a more precise, executable version of a spec step." This is the same thesis Kit is
built on. It is not enforced anywhere (see finding below).

**Finding, not inference:** `macro-metrics/docs/retros/poc/03-template-gaps.md:342-379` (Gap 15, "Testing
Strategy Exists But Is Not Surfaced in Issue Templates") is the estate's own written admission of exactly this
gap: "the only CI reference is a generic 'build + test on every PR'... 200+ tests were ultimately produced, but
this relied on the AI choosing to write them — not on ACs requiring them." This corroborates, independently of
the Kit prototype's own README claim, that the prose pipeline is descriptive, not enforced.

## 2. Existing acceptance-criteria writing

Best worked example found: `macro-metrics/docs/user-stories-frontend.md` (254 lines, read in full) — 7 BDD
stories written against signed-off Phase 2 designs. Three real excerpts, verbatim:

> Story 1: "On navigation to `/`, the page shell renders instantly: header, sticky mini-nav, section headings,
> and skeleton versions of all 9 preset ratio cards and all 3 indicator cards" (`user-stories-frontend.md:18`)

> Story 4: "The metric selected as numerator is excluded from the denominator dropdown options, and vice versa"
> (`user-stories-frontend.md:75`)

> Story 7: "A failed preset card shows: `⚠ Could not load data. Retry?` with a **Try again** button. Clicking
> **Try again** on a preset card re-fires that card's fetch only." (`user-stories-frontend.md:133,135`)

**Honest assessment:** these are close to machine-checkable already — each bullet names a concrete DOM state, a
specific string, or a specific query trigger, which is precisely the shape a `getByRole`/`getByText` Playwright
or RTL assertion wants. They are noticeably more precise than the issue template's bare `- [ ]` (they were
authored by an AI working from signed-off ASCII mockups, not filled into the template's blank checkboxes). The
gap to "machine-checkable" is real but narrow: they are prose sentences a human still has to translate into a
DOM query — there is no noun vocabulary, no ID, no binding. `macro-metrics/docs/user-stories-frontend.md:219-240`
even shows the intended translation by hand (a worked `PresetCard.test.tsx` example) — confirming the translation
step is manual today.

## 3. Test harness available to bind to

| Repo | Runner(s) present | npm/dotnet script exists | Runs in CI |
|---|---|---|---|
| `web-template` | Playwright only (`frontend/playwright.config.ts`; `e2e/home.spec.ts`, `e2e/favicon.spec.ts`) | `test:e2e` yes (`frontend/package.json:12`); **no Vitest** anywhere in `package.json` despite `testing-strategy.md` mandating it | e2e: **yes** (`.github/workflows/ci.yml:74-98`, job `e2e` runs `npm run test:e2e`). backend: `dotnet test` **yes** (`ci.yml:43-48`). Frontend unit: **N/A — does not exist** |
| `around-the-world` | Playwright + Vitest+RTL+jsdom (`frontend/playwright.config.ts`; 10 files under `src/**/__tests__/*.test.{ts,tsx}`, `e2e/app.spec.ts`, `e2e/favicon.spec.ts`) | `test` = `vitest run` yes (`frontend/package.json:13`), `test:e2e` yes (`:12`) | e2e: **yes** (`.github/workflows/ci.yml:74-101`, runs WebKit not Chromium — deliberate, see comment at `:95-98`). backend: `dotnet test` yes. **Vitest unit suite: NOT run in CI** — the `frontend` job (`ci.yml:50-72`) is Install → Lint → Build only, no `npm run test` step anywhere in the file. Confirmed by reading the full 110-line workflow. |
| `snip-it` | Playwright + Vitest (verified `frontend/e2e/editor.spec.ts` + `favicon.spec.ts`; `test` script present) | `test` = `vitest run`, `test:e2e` = `playwright test` (`frontend/package.json`) | e2e: yes (`ci.yml:64-96`). backend: `dotnet test` yes. **Vitest unit suite: runs in CI** — `ci.yml:61-62` `Unit tests` step runs `npm run test` inside the `frontend` job, after lint+build. This is the one repo of the three where the wired script is actually gated. |

**This is the precise distinction the task asked to be careful about:** `around-the-world` and `snip-it` have
byte-identical `package.json` test scripts, but only one of them is actually a gate. Grepping `package.json` or
even `git log` for "tests exist" would have reported both as equally protected; only reading the CI YAML shows
they are not. This is the exact class of defect Kit's "no test names the ID → build fails" check is meant to
make impossible — except generalised from "test exists" to "test exists *and is wired into the gate that runs on
merge*," which is a layer up from what the prototype currently checks (see gaps, §6).

Backend xUnit content was not read in this survey (out of scope of the CI-gating question); only confirmed that
`dotnet test` executes in CI for all three .NET repos.

## 4. Deterministic generators we already have

**OpenAPI → RTK Query codegen** (`web-template/docs/specs/openapi-codegen.md`, 149 lines, read in full):
- Input: `backend/SolutionName.WebApi/openapi.json`, a committed schema emitted at **build time** (not runtime) by
  `Microsoft.Extensions.ApiDescription.Server`, which runs the app in-process via `GetDocument.Insider` and
  skips DB startup while doing so (`openapi-codegen.md:36-42`).
- Config: `frontend/openapi-config.cjs` (quoted in full at `:21-31`): `schemaFile`, `apiFile` (`emptyApi.ts`),
  `outputFile` (`generatedApi.ts`), `hooks: true`.
- Output: typed RTK Query hooks (`src/api/generatedApi.ts`) — never hand-edited (`:129`).
- Hard constraint that keeps the schema honest: every route handler must return a concrete `TypedResults` type,
  never `IResult` (`:44-61`) — otherwise OpenAPI records no schema and the hook types as `unknown`. This is
  directly analogous to Kit's "refuse rather than guess" design principle: a badly-typed handler produces no
  usable output rather than a wrong one.
- This exact pattern (schema in, typed artefact out, committed and diffable) is reused verbatim across
  `web-template`, `around-the-world`, `macro-metrics`, `snip-it` — all scaffolded from the same template.

**`InterfaceFromConcreteGenerator`** — a Roslyn `IIncrementalGenerator`, present identically in
`dotnet-libraries/src/Northstar.SourceGenerators/InterfaceFromConcreteGenerator.cs` (527 lines; source-of-truth
package) and copied into `web-template/backend/SolutionName.SourceGenerators/` and
`around-the-world/backend/AroundTheWorld.SourceGenerators/` (confirmed present by filename match, contents not
diffed).
- Input: a class marked `[GenerateInterface]` (`:23-54`).
- Output: an interface `I{ClassName}` mirroring the class's public surface, written to a **separate generated
  file** (never mutates the source class) in the same namespace (`:12-21`).
- Two diagnostics exist for cases it refuses to handle: nested classes (`NestedTypeUnsupported`, `:68-74`) and
  static classes (`StaticTypeUnsupported`, `:76-80`) — again the "refuse and name the reason" pattern rather than
  emitting something wrong.
- This is direct proof that deterministic, non-LLM, attribute-driven C# codegen is already production-shaped
  in this org's toolchain — the same category of mechanism Kit would need for a "generate the C# contract from
  a behaviour's `provides` lines" extension, if that is ever wanted (not attempted by the prototype).

`snip-it`, `macro-metrics`, `design-system`, `repo-template` were not searched for additional generators beyond
the `ISourceGenerator|IIncrementalGenerator` grep already covering every local org clone for those
interfaces — no further matches were found.

## 5. The prototype

`prototypes/behaviour-ast/` — 5 files: `kit.js` (342 lines, core), `kit-test.js` (182 lines, own
test suite), `compare.js` (73 lines, checks generated output against real hand-written code), `bindings.json`
(one noun→locator binding file), `behaviours/snip-it.beh` (8 behaviours re-expressing snip-it's real e2e suite),
`README.md`.

**What `kit.js` implements** (all four stages, verified by reading the source):
1. **parse** (`kit.js:24-92`) — line-based, not YAML/JSON, because James's requirement is that a human can write
   the tree by hand. Recognises `behaviour ID "title"`, `actor`, step keywords (`given`/`when`/`then`/`contract`),
   `provides <kind>:<Name>.<slot> = <value>`, and `?slot` holes. Unrecognised keywords and steps outside a
   behaviour **throw**, they are not silently dropped (`:40,71`).
2. **resolve** (`kit.js:103-160`) — builds one `Map`-based symbol table (`kind:Name.slot` → value) across the
   *whole corpus*, not per file. A hole in one behaviour can be filled by a `provides` line in a different
   behaviour (this is demonstrated on real material: `BEH-UP-1`'s `?fields` hole is filled by `BEH-UP-2`'s
   `provides form:Upload.fields = VideoOrAudioFile`, `behaviours/snip-it.beh:49,74`). Conflicting `provides` for
   the same slot are recorded as structural conflicts with both sides named (`:148-155`) — no LLM/embeddings
   involved, "falls straight out of the symbol table."
3. **generate** (`kit.js:167-277`) — emits Playwright test bodies per behaviour. Six verb handlers implemented:
   `state`, `opens` (with route-param substitution from resolved symbols), `activates`, `sees`, `shows`,
   `attaches`, `lands`, `fills`. An unbound noun or an unfilled route param is **refused** (emits
   `// UNGENERATED: ...` and names the missing noun) rather than guessed — this is the load-bearing design claim,
   and it is unit-tested specifically (`kit-test.js:110-131`, including a regression test for a real bug: the
   first version silently truncated a parameterised route). `contract` steps (wire-level assertions like "POST
   /api/cuts is sent exactly once") are deliberately never generated — kept and counted separately, not hidden,
   so the coverage number isn't flattered by omitting the steps that fail (`kit.js:180-186`).
4. **coverage** (`kit.js:284-292`) — a behaviour is "covered" only if some test source string contains
   `[ITS-ID]`; behaviours with no matching test are `uncovered`, and test IDs matching no behaviour are
   `orphanTests`. This is the only part of the pipeline meant to fail a build.

**Real output from this survey** (all three scripts executed successfully; not simulated):
- `node kit.js`: 8 behaviours, 14 noun bindings, 28 generated lines, 6 wire contracts, 2 ungenerated (refused)
  steps, 2 unbound nouns (`region:WordTrack`, `region:CutStatus` — deliberately absent from `bindings.json`
  because the real app gives those regions no accessible name, per the binding file's own comment,
  `bindings.json:8-12`), 28/36 = 78% of steps generated.
- `node kit-test.js`: **19 passed, 0 failed.** Covers parse (5 tests), resolve/cross-behaviour symbol table
  (4 tests), generate/refusals (8 tests — the load-bearing half, each refusal paired with a positive control per
  the file's own stated design principle at `kit-test.js:11-13`), coverage (2 tests).
- `node compare.js`: of 28 generated `await` lines, 22 are byte-identical to a line in snip-it's real
  `frontend/e2e/editor.spec.ts` on `origin/dev`, 3 more match after whitespace/constant-expansion normalisation
  (25/28 total), and 3 are genuinely absent from the hand-written suite (the two `setInputFiles` lines and the
  `toHaveURL` regex check — printed explicitly, not buried, at the end of the run).

**What it does NOT do** (verified by absence, not claimed by the README alone):
- No parser for existing prose acceptance criteria (macro-metrics-style BDD bullets) into the `.beh` grammar —
  the one behaviour file that exists (`snip-it.beh`) was hand-authored by reverse-engineering an existing
  Playwright spec, not derived from an issue's AC.
- No LLM inference step at all — `provides` lines are 100% hand-written in `snip-it.beh`; the README explicitly
  frames the *design* for where an LLM inference would be written down (`README.md:52-56`) but no code path
  exercises it.
- No build-breaking mechanism — `coverage()` returns data; nothing in this prototype wires it into an exit code,
  a CI step, or a git hook. "Fails the build" is aspirational at the level of this code.
- Only one binding file (`bindings.json`, 14 nouns) against one behaviour corpus (8 behaviours, all from one
  spec file). No test of noun-binding reuse across multiple unrelated apps/repos.
- Only Playwright is a generation target. No Vitest/RTL emission, no backend (xUnit) emission — despite the
  estate having wired harnesses for both (see §3).
- No handling of contradictions beyond same-slot value mismatch — no semantic/near-duplicate detection (the
  README calls this "not free" explicitly, `README.md:58-63`).
- No persistence/corpus format beyond flat `.beh` files in one directory — no notion of multiple projects, no
  versioning, no way to mark an inference "denied" (the mechanism the README says the format was designed to
  support is not yet implemented, only made structurally possible).

---

## The gaps

1. **No spec-to-test binding mechanism exists anywhere in the estate outside the prototype.** Every repo
   surveyed writes acceptance criteria as free prose in a GitHub issue, then a human or the AI manually
   translates it into a test with no traceable link back to the AC line. Kit's noun-binding idea has zero
   precedent to build on beyond its own prototype — everything else is prose.

2. **"Tests are a spec artefact" is a sentence, not a rule, and the estate has already independently discovered
   and documented this as a gap** (macro-metrics Gap 15) without connecting it to a fix — the retro proposes
   adding a checkbox to the issue template ("`npm test` passes... before the PR is raised"), which is exactly
   the kind of unenforced prose Kit exists to replace with a build-time check.

3. **CI gating is not uniform even within scaffolds of the same template** — `around-the-world`'s wired Vitest
   suite is dead weight from a gating perspective, `snip-it`'s identical setup is real. If Kit's coverage check
   is meant to be the enforcement layer, it needs to check not just "a test exists naming this ID" but "that
   test's CI job actually runs on the branch this PR targets" — the prototype currently checks neither of those;
   it checks whether any string in a supplied test-source list contains the ID.

4. **The behaviour-tree grammar (`.beh` format) has never been exercised against prose input.** The one corpus
   file that exists was reverse-engineered from working Playwright code, not from a GitHub issue's Given/When/Then
   acceptance criteria — the actual expected input in production. Whether a human (or LLM) can reliably turn a
   macro-metrics-style AC bullet into `.beh` syntax with the right verb/noun/hole split is untested.

5. **No LLM inference loop exists.** The entire "unknowns get filled in when other parts are filled out, approve/
   deny by default-include" mechanism described in the README is a design intent backed by a hand-authored
   `provides` line, not by any code that infers one. This is the part of Kit's stated value proposition (over
   Gherkin) that has the least engineering behind it today.

6. **No build-failure wiring.** `coverage()` computes the right data structure but nothing calls it from a CI
   step, git hook, or exit code anywhere in this estate. "Fails the build when a behaviour has no test naming
   its ID" is currently true only in the sense that the function *could* be called that way — it has never been.

7. **Backend (xUnit) generation is entirely unaddressed** — the prototype only emits Playwright. Given the
   estate's own testing pyramid has three tiers (Vitest/RTL, xUnit unit, xUnit integration) and this survey did
   not even audit the backend test content, a behaviour tree that can only ever produce e2e/UI assertions covers
   at most one of three tiers documented in `testing-strategy.md`.

8. **Cross-repo noun-binding reuse is unproven.** The entire "glue grows with the app's vocabulary, which
   saturates" claim (the prototype's core bet against Gherkin) rests on one binding file for one app. It has
   never been run against a second, unrelated app's vocabulary to see whether bindings genuinely reuse or
   whether every new page/component still needs its own binding entries at roughly 1:1 with new UI, which would
   undercut the saturation claim.
