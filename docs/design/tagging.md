# How an existing app gets tagged

**Status: open, James's call. Two options survive; one is ruled out by measurement.**
**Default if nothing is decided: option C.**

Run `node prototypes/behaviour-ast/measure-tagging.js` for the numbers. This file
carries the *decision*; the script carries the *evidence*, so that when the corpora
change the numbers move and this document does not quietly go stale — the failure
`docs/analysis/what-we-can-leverage.md` is already an example of.

## Why this had to be asked

`coverage()` is stage 0's whole deliverable — *a behaviour with no test naming it
fails the build* (`docs/timeline.md`, Stage 0). It matches `[BEH-ID]` markers in
test sources. Run it over the three real corpora today and it reports **0 of 58
covered**, because **not one `BEH-` marker exists in any of the three repos**.

snip-it is substantively ~6/8 covered — its corpus was reverse-engineered from
`frontend/e2e/editor.spec.ts` and six behaviours map onto a test title by eye — and
formally 0/8. So the gate as it stands **measures whether a migration has happened,
not whether anything is tested**, and no doc has ever named the migration.

## The three options

| | what it demands | |
|---|---|---|
| **A** | a `[BEH-X]` marker added to each app's tests | viable |
| **B** | nothing — match a behaviour to a test by title | **ruled out, below** |
| **C** | the corpus carries the mapping; the app is never touched | viable, default |

## A is cheaper than it looked

The first draft of this measurement claimed C# tests would each need
`[Fact(DisplayName=…)]` added, because a marker cannot live inside a method
identifier — and two of the three apps are 100% C# method-named.

**That is wrong, and `kit.js:420` is why:** `coverage()` scans the whole test
**source**, not the title. `// [BEH-X]` above the method is enough. The marker
syntax also collides with nothing already written — **zero `[ALLCAPS]` tokens across
all 37 test files**, so `orphanTests` stays signal rather than noise (`[Fact]`,
`[Theory]`, `[InlineData]` are not all-caps and do not match).

Cost is therefore ~58 one-line edits, once.

## B is ruled out, and not for being sloppy

Stated in its favour first, because it earns it: with a good-faith matcher
(camel/snake split, stop words, crude stemming, scored by how much of the
behaviour's vocabulary the title accounts for), at threshold 0.60 on snip-it —
**the only corpus where the right answer is known** — it returns 5 behaviours, all
5 human-confirmed, **zero false positives**.

Three things kill it anyway:

1. **The sixth true pair is matched to the wrong test**, at 0.50 with a 0.17 margin.
   No threshold repairs that: raise the bar and the behaviour reads as uncovered;
   lower it and a test that does not exercise the behaviour is accepted as proof
   that it does. **A false green is worse than the 0/58 we have now.**
2. **The threshold does not transfer.** The same 0.60 that is precise on snip-it
   reports james-habits-app at 2 of 23 — a well-tested app with 41 tests read as
   almost entirely uncovered. Moving the bar 0.60 → 0.40 swings that app by 8 of 23.
   The number the gate emits depends more on a constant nobody can justify than on
   the code under test.
3. **It cannot discriminate where the vocabulary repeats.** On raw top match, one
   habits test title is the best match for **seven** different behaviours; vocab has
   four such collisions. The gate would credit one test with seven behaviours.

The deeper reason, and the one that generalises past this matcher: `serves BEH-X`
is already in the notation *because* a link has to be authored to be arguable
(`kit.js`, §6). A similarity score is a claim nobody made and nobody can dispute.
Option B is that same mistake, one layer down.

## What A and C share, and nobody had written down

`coverage()` reads whole **files**. A marker anywhere in a file marks that behaviour
covered — the largest test file in the estate holds 18 titles. So under either
option the gate proves *someone wrote the id*, not that a test asserts the
behaviour. That is a real limit on what stage 0's exit gate can claim, and it is
worth stating before the gate ships rather than after.

It is not a reason to prefer B: B does not know which test asserts what either, and
unlike A and C it does not know who claimed it.

## One caveat on the numbers themselves

The reader that produces them was wrong once. A fixed six-line lookahead between a
`[Theory]` and its method **silently lost 7 tests** — every theory with five or more
`[InlineData]` rows — under-reading language-vocab by 16%. It was found by counting
`[Fact]`/`[Theory]` a second way and comparing, not by anything the script itself
said, and that cross-check is now a hard refusal inside it.

None of the conclusions above moved when it was fixed; one cell of the threshold
table did. Recorded because the first version of this page would have stated the
wrong numbers with exactly the same confidence.

## What separates A from C

- **C touches no app.** An app adopts Kit without a diff in its own test suite.
- **C's mapping is checkable**: a corpus naming a test that no longer exists is a
  hard failure computable today, so it cannot rot silently. That is the answer to
  the obvious objection that a mapping kept outside the app drifts.
- **C must key on file + title, not title alone** — snip-it has 2 duplicate test
  titles (`GetJobAsync_UnknownId_ReturnsNull`, `Serialize_Deserialize_RoundTrips`),
  which a title-only key cannot address uniquely.
- **A puts the claim next to the test**, where whoever edits the test sees it. That
  is a real advantage and it is why this is a decision rather than a conclusion.

## The gate that runs on this

`node prototypes/behaviour-ast/check.js <app> --repo <path> [--via mapping|markers]`
— **both options are implemented**, so whichever you pick, the gate already runs it.
`mapping` (option C) is the default.

```
0  every behaviour in the corpus has a test naming it
1  it looked, and something is wrong
2  IT COULD NOT LOOK — no corpus, no repo, zero test files, or a reader that
   lost tests. Deliberately not the same as "looked and was fine": a gate that
   reads nothing and exits 0 is indistinguishable in CI from a passing one.
```

**It ships RED.** `behaviours/snip-it.tests.json` maps the six behaviours that
have a test and deliberately omits `BEH-UP-2` and `BEH-EDIT-0`, which have none.
`kit check snip-it` exits 1 at 6/8 today. A gate only ever observed passing has
never been shown to discriminate.

**It found real drift on its first run against a live branch.** Pointed at
snip-it's `feat/no-server-side-storage` (PR #29, open and green), it reports 5/8
and names why: that branch renames `send-for-export submits a cut and surfaces the
job` → `…re-sends the video and surfaces the job`, while `BEH-CUT-1` still claims
the old title. That is the option-C rot argument demonstrated rather than asserted
— and it is `docs/timeline.md`'s stage-0 exit gate (*deleting a test makes CI go
red*) verified by doing it, on a rename nobody planted.

Whether the corpus or the test is what should change there is a human's call; the
gate's job was to make it impossible to miss.

⚠️ **Not yet wired into any CI.** A `.github/workflows/**` change is a platform
change (claude-code-bot#83) — isolated PR, James's review, never a self-merge. Until
that lands, this gate gates nothing, and saying otherwise would be the exact defect
Kit was built to name ([[passing-is-not-gating]]).

Raised on claude-code-bot#84.
