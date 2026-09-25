# Can Kit describe Kit?

_First measured 2026-09-05; re-measured and re-checked 2026-09-16. Every number in a table here
is **written into this file** by `node prototypes/behaviour-ast/self-host.js --record`, and
`--check` regenerates and compares them — exit 1 on a difference, exit 2 if it cannot find the
markers to compare. It runs inside `kit.test.js`, which `ci.yml` runs, so the drift check gates.
**Until 2026-09-16 `--check` only ever compared a JSON file to the corpus and never opened this
document**, which is how the prose said 14 / 42 / 21 for eleven days while the record beside it
said 20 / 60 / 36, under a green check._

James, [claude-code-bot#89](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/89):

> A UI where I can manage my projects (including kit) by the spec and then iterating on the
> output … But also think about how kit looks as a **kit managed project**.

## The answer

**Kit can describe itself completely, and can generate nothing for itself. The gap is not
notation and not bindings — it is that Kit has no UI.**

Which means the two halves of #89 are one thing: the UI is not a nicer front end for Kit,
it is the thing that makes Kit self-hosting at all.

## The numbers

`behaviours/kit.beh` encodes Kit's real behaviours — the stage-0 gate, the exit-2 refusal, the
mapping rot check, the reader's count disagreement, the two generator refusals, the parser's
unrecognised-keyword error, cross-behaviour hole filling, conflict reporting,
default-include-marked-unreviewed, and the read API's four (the write refusal, the loopback
bind, the traversal refusal, and unavailable-is-not-zero).

> ⚠️ **Every number in a table below is written by `node self-host.js --record` and re-checked
> by `--check`. Do not edit one by hand** — the hand-typed copies said 14 / 42 / 21 for eleven
> days while the machine record beside them said 20 / 60 / 36, and `--check` reported green
> throughout because it never opened this file. Numbers in *prose* are dated history, on
> purpose, and are not regenerated.

⚠️ **The corpus has doubled since this was first written, and the number that matters has not
moved once.** Re-measured on 2026-09-16 by replaying today's `self-host.js` over each corpus
generation, which is a stronger statement than three separate runs would be — one measuring
instrument, three inputs:

| corpus | behaviours | steps | `contract` lines | **derived** |
|---|---|---|---|---|
| `ba4165b` 2026-09-05 — the ten this document was written about | 10 | 30 | 12 | **0** |
| `00a0a96` 2026-09-05 — the four `BEH-UI-*` added | 14 | 42 | 21 | **0** |
| `56465e6` 2026-09-09 — the six `BEH-WRITE-*` added | 20 | 60 | 36 | **0** |

(That middle row is why the drift below was drift and not an error: **14 / 42 / 21 was exactly
right for the corpus of 2026-09-05**, and simply stopped being right.) `ui.js` is the UI's
server — it has a network surface but not a *browser* one, so its behaviours are
`runs`/`reports` like every other Kit behaviour, and so are the writer's. That is the finding
surviving a corpus twice the size it was measured on, rather than being an artefact of a small
one. The unlock is a browser, not more description.

<!-- self-host:begin numbers -->
| | |
|---|---|
| behaviours parsed | **20 of 20**, no parse error |
| steps | 60 |
| prose `contract` lines | 36 |
| steps **derived** from a behaviour | **0** |
<!-- self-host:end numbers -->

The notation is not the constraint. `parseStep` takes the first token as the verb and any
`kind:Name` as a noun, so `when runs command:KitCheck` parses exactly as happily as
`when opens page:Home`.

## The discriminating step

A bare "0% generated" would be the same headline `james-habits-app` produced, where the
cause was an app missing nouns and Kit working as designed. So before repeating that
framing, `self-host.js` binds **every noun the corpus mentions**, as generously as the
binding format allows — a route, a role and name, a label, and a `state` setup snippet.

<!-- self-host:begin saturation -->
| | generated | ungenerated |
|---|---|---|
| no bindings | 0 | 60 |
| **every noun bound** | **20** | 40 |
| of which **derived** | **0** | |
<!-- self-host:end saturation -->

Steps *do* generate under full bindings, and reporting that as zero would have been the
convenient number rather than the true one. But every one of them is a `state` step, and a
`state` binding is a setup string a human wrote in `bindings.json`, copied out verbatim.
Nothing is *derived* from a behaviour — which is why the row that carries the claim is the
third one, not the second. A mutation that counts `state` as derived is in `mutate.js`, and
a positive control asserts that binding nouns **does** move the number for a browser-verb
corpus — otherwise "binding changed nothing" would be indistinguishable from a broken tally.

## Why: the verbs

<!-- self-host:begin verbs -->
| verb | steps | in the generator? |
|---|---|---|
| `state` | 20 | yes |
| `runs` | 20 | **no** |
| `reports` | 13 | **no** |
| `exits` | 4 | **no** |
| `raises` | 3 | **no** |
<!-- self-host:end verbs -->

The generator's entire vocabulary is:

<!-- self-host:begin vocabulary -->
`activates, attaches, fills, lands, opens, sees, shows, state`
<!-- self-host:end vocabulary -->

Every one is a browser verb, because the emit target is Playwright. A command-line tool has
none of them, and adding `runs`/`exits` cases would mean deciding what a command-line test
*is* — a second emit target, not a grammar patch.

(That list is read out of `generate()`'s `switch` at runtime, not re-typed here. A hard-coded
copy is how this document would start lying about the code. That was true of the vocabulary
from the first draft, and it is the only number-shaped fact here that never drifted — which is
the whole argument for the markers above.)

## What follows

1. **The UI is the unlock, not a nicety.** Once Kit has a UI, its own behaviours become
   `opens page:Corpus`, `activates button:Check`, `shows region:Coverage` — verbs the
   generator already emits. Kit stops being a special case and becomes the next app in
   its own registry.
2. ✅ **`kit check` already gates Kit, and did within minutes of this being written.** It gates
   on behaviour ids and a mapping, not on generated code, so nothing above stands in its way.
   `ci.yml`'s *"Stage-0 gate — every kit behaviour is named by a test"* runs
   `node prototypes/behaviour-ast/check.js kit --repo .` on every PR, so this is a gate and not
   merely a passing command — and because it is a gate, **every behaviour in `kit.beh` is named
   by a test right now** without this sentence having to quote a number that would rot. The run
   that is red is the one where that stops being true.

   > ⚠️ **This item used to say the opposite, and it was wrong the moment it was written** —
   > *"a separate, smaller piece of work: Kit's test file is named `kit.test.js`, which
   > `TEST_FILE_RE` does not match. Kit currently demands a naming convention of its consumers
   > that it does not follow itself."* `TEST_FILE_RE` is
   > `/\.(spec|test)\.(ts|tsx|js|jsx)$|Tests?\.cs$/` (`TEST_FILE_RE` in `kit.js`), it has matched
   > `kit.test.js` since it landed on 2026-09-03, and the CI step arrived in `e5b1a15` —
   > **three minutes after `ba4165b` committed this paragraph.** Not drift: a claim about the
   > code made without running it, left standing for eleven days beside a `--check` that was
   > green because it only ever compared a JSON file to a corpus.
3. **`kit.beh` is excluded from the binding-saturation study**, by a `# kit:no-ui` directive
   in the corpus itself rather than a filename list inside `saturation.js`. That study asks
   whether the glue binding a spec to a UI saturates; a corpus whose nouns are
   `command:KitCheck` and `status:One` has no answer to give. The exclusion is announced on
   every run — a population you cannot see is one you cannot check.

## What this does not show

`kit.beh` is a sample of Kit, not Kit. Its behaviours were chosen because each is already
tested, so none of them is a behaviour I invented to be expressible — but I chose them, and a
different selection might have found a verb the generator does have. The claim that survives
that caveat is the narrow one: **there exist central Kit behaviours the generator cannot emit,
and the reason is the emit target, not the notation.**

The selection caveat is *weaker* than it was, and worth saying why. The first ten were picked
by me, in one sitting, to make this measurement (2026-09-05). The four `BEH-UI-*` behaviours
were not: they describe a component that was built for its own reasons and documented
afterwards, and they landed on the same two verbs. Neither were the six `BEH-WRITE-*`, which
describe the corpus writer built on 2026-09-09 for its own reasons — and which land on those
same verbs again. Three samples agreeing, two of them chosen by something other than this
measurement, is not proof; it is the kind of evidence a hand-picked sample cannot give itself.

---

# ✅ The prediction, tested (2026-09-10)

Everything above was written on 2026-09-05, before the UI existed, and it ends on a
prediction rather than a result: *"the UI is not a nicer front end for Kit, it is the thing
that makes Kit self-hosting at all."*

The UI shipped — kit#16 → #24 → #26 → #27 → #28 — so the prediction is now checkable, and
`behaviours/kit-ui.beh` is the check.

## The result

| corpus | subject | derived steps | generated tests that PASS |
|---|---|---|---|
| `kit.beh` | Kit the CLI | **0** | — nothing to run |
| `kit-ui.beh` | Kit the browser surface | **20** | **5 of 6** |

**The prediction held.** Same author, same notation, same generator, same bindings file — and
the moment the subject has a browser, the derived count goes from 0 to 20 and the emitted
Playwright runs against the real app. The 0 above was never about the notation.

🔑 **The generated tests were EXECUTED, not inspected.** As far as I can find, this is the
first time Kit's output has been run rather than read or diffed against a hand-written suite:
nothing in this repo wires the generator to a runner, and the head-to-head in
`binding-saturation.md` is a comparison of *glue counts*, not of results. A generated test
nobody ran is a claim, not evidence.

🔑 **The cross-behaviour hole-filling earned itself here.** Only `BEH-PAIR-1` names the app
and behaviour it opens (`provides page:KitBehaviour.app`/`.id`). `BEH-ADJ-1`, `BEH-ADJ-2`
and `BEH-STEP-1` say only `when opens page:KitBehaviour` — and all three emitted the full
concrete route, resolved from a *different behaviour's* provides. That is the mechanism the
design has always claimed, and this is the first time it produced running code.

## 🔴 The one failure is the interesting half

`BEH-ADJ-2` says the corpus fills the correction field before clicking Deny. The generator
**correctly refuses** to derive that step — its `fills` verb works off the `?fields`
unknowns mechanism, not a literal — and emits:

```
test.info().annotations.push({ type: "kit-ungenerated", description: "when fills field:KitCorrection with \"…\"" });
// UNGENERATED: when fills field:KitCorrection with "…"
await page.getByRole("button", { name: "Deny" }).click();
```

**The test still runs on regardless.** Deny is disabled precisely because the fill never
happened, so it fails on a click timeout — a failure that reads like an application bug and is
nothing of the kind. The cause is the line above it.

Originally the refusal was *only* the comment, which no test runner will ever show you: honest
at the point of generation and lost at the point of execution, so Kit knew the test was
incomplete and emitted an artefact that did not. **Resolved 2026-09-24 (kit#31):** the emitter
now also pushes a Playwright annotation, so the refusal reaches the report.

🔑 **It deliberately changes no test outcome.** 74 of the 125 tests generated across the nine
committed corpora carry at least one refusal, so `throw` or `test.fixme()` would have
re-coloured most of every consumer's suite to say something the suite already knew. The
annotation is emitted **above** the comment so that the comment stays directly above the action
that depends on it — that adjacency is the finding, and `kit.test.js` pins it.

⚠️ **The confound, stated plainly:** the same author wrote the app, this corpus and its
bindings, in one sitting. That is fatal to any *independence* claim, which is why
`kit-ui.beh` declares `# kit:self-authored` and `saturation.js` excludes it from the
binding-saturation study. It is **not** fatal to the question asked here — whether the
generator can emit a runnable test for a browser surface — because that question is about the
generator, and the answer is checkable by running it. Whether two authors would choose the
same nouns is kit#23, still open.

## What changed in the registry

Kit is now **two apps in its own registry**: `kit` (the CLI, derives 0, and that 0 is the
evidence — do not "fix" it) and `kit-ui` (the browser surface, derives 20). Kit stopped being
a special case in the tool it is.

## How to re-run it — because the first run could not be

⚠️ **The 5-of-6 above was first produced by a harness outside this repository**, in a scratch
directory with a `node_modules` symlink borrowed from another project. Every number in it was
true and **not one of them was checkable by anyone else** — which fails this document's own
standard, three sections up: *a generated test nobody ran is a claim, not evidence*. A run
nobody can repeat is a claim too.

The harness is now `prototypes/behaviour-ast/selfhost/run.js`. It copies the corpus to a temp
directory (the generated tests **write** to the corpus they came from), starts `ui.js` itself,
waits for `/api/projects` rather than for the process, and runs the emitted spec:

```
npm --prefix prototypes/behaviour-ast/ui ci && npm --prefix prototypes/behaviour-ast/ui run build
node prototypes/behaviour-ast/selfhost/run.js --playwright <bin> [--browsers <dir>] --check
```

`--check` re-runs the tests and exits 1 if the outcome has drifted from
`selfhost/expected.json`; exit 2 is **could not look**, which is deliberately not 0.

✅ **Re-measured 2026-09-12 on `dev`: 5 passed, 1 failed, exit 0 — the table above is still what
the run says.** Worth dating, because `--check` is manual: between the run that recorded these
numbers and this one, nothing had executed them, so their shelf life was unmeasured rather than
long ([[a-capability-claim-has-a-shelf-life]]).

🔴 **`--browsers` is not optional in practice, and forgetting it used to produce a LIE.** Playwright
reports a browser it cannot launch as an ordinary test failure, once per test, so a machine whose
browsers sit somewhere else emits a complete, well-formed, entirely red report. Run here without
the flag, `--check` printed `0 passed, 6 failed` and concluded *"the run no longer says what the
write-up claims"* — blaming this document for a fault in the environment, and pointing the reader
at `--record`, which would have overwritten `expected.json` with zeroes and destroyed the evidence.
A launch failure is now **exit 2**, and `selfhost/fixtures/no-browser.txt` is that run's verbatim
output, kept as the fixture three tests drive from.

**Kit does not depend on `@playwright/test`, on purpose** — adding it is a packaging change and
therefore James's call ([claude-code-bot#83](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/83)).
So the binary is named on the command line, and its absence refuses rather than skipping. That
also means **`--check` is a manual gate, not a CI one.** The half that does not need a browser —
6 tests, 20 derived steps, 1 refusal, and the refused step sitting directly above the click that
depends on it — is asserted in `kit.test.js`, which CI runs. Before this, the `kit-ui` table was
gated by nothing at all while the `kit.beh` table above it was gated by `self-host.js --check`.
