---
tags: [kit, trial, findings, refusals]
updated: 2026-09-26
status: an account of Kit's refusals under provocation, not a claim about its design
---

# Does Kit refuse, or does it guess?

_2026-09-26. The previous four trials all asked **"can someone get a corpus written?"** — they measured
the happy path and reported the defects they tripped over on the way. This one inverts it: the target
is Kit's own stated premise, **"refuse rather than guess"**, and the only question asked of each
refusal is **"could you act on this message without reading the source?"**_

_Why the premise and not a feature: a premise is the thing every other claim rests on, and nothing in
the repo measures it. `kit.test.js` asserts individual refusals exist; no artefact anywhere asks what
fraction of provoked mistakes produce one._

## Method

A **separate agent with no prior context** was given a checkout with `docs/trials/` deleted (those are
the answers), told to approach each tool as a user would — `--help` first, then the error, and only
then the source — and **forbidden from fixing anything**: *a fix destroys the measurement*. It was
kept off `node start.js` so it stayed on the CLI surface. It was asked for a **ratio**, not a list of
failures, because a premise that holds 9 times in 10 is a different product from one that holds 4
times in 10 and a failure list cannot tell you which you have.

Run on `dev` at `4101be6`. ⚠️ **That tree contains neither [kit#72](https://github.com/jemmy8oy-northstar/kit/pull/72)
nor [kit#77](https://github.com/jemmy8oy-northstar/kit/pull/77)**, both open at the time — which turns
out to be the most useful thing about the run, and is dealt with in §2.

**The split, stated before the findings.** §1–§3 are claims about artefacts — an exit code, a byte
count, a message that names the wrong thing. I re-measured every one against real trees with my own
probes, which is why they are stated as facts. §4 is what the author found confusing; that is
model-dependent and labelled as such.

## The ratio

51 provoked mistakes — missing arguments, unknown flags, flags with no value, nonexistent corpora,
ambiguous names, malformed and empty fixtures, wrong-type values, wrong working directory.

| verdict | count | % |
|---|---|---|
| ACTIONABLE — the message alone said what to do | 31 | 61% |
| PARTIAL — fix guessable, not stated | 2 | 4% |
| OPAQUE — had to read source, or it named the wrong thing | 5 | 10% |
| **SILENT GUESS — it did not refuse at all** | **13** | **25%** |

**The premise holds about 6 times in 10, and a full quarter of accidental mistakes produce no refusal
whatsoever.** The 25% is not scattered noise: it has one structural cause (§1).

## §1 — Two populations, not one toolkit

Kit has sixteen runnable entry points, and they fall into two groups that are **invoked identically**
and behave oppositely:

- `check.js`, `kit.js`, `converge.js`, `project.js`, `requires.js`, `writer.js` have a real CLI layer:
  a known-flag list, a usage string, a refusal that names the offending token.
- `measure-tagging.js`, `prose-audit.js`, `saturation.js`, `self-host.js`, `mutate.js`, `compare.js`
  have **none at all**. They are "run the demo" scripts that happen to be spelled `node <tool>
  [flags]`, exactly like the ones that validate.

So the same accidental keystroke gets **three different treatments depending on which tool you
reached for** — a hard refusal, a silent no-op, or an uncaught crash. The inconsistency is the finding;
each individual message is defensible on its own.

⚠️ `kit.test.js` already carries mutants for this rule at the tools that *have* it (*"an unknown flag
is ignored again, so a typo silently gates the default corpus"*). **A guard cannot regress where it
never existed**, so the mutation suite was green over the whole second population.

## §2 — Six of the thirteen were already fixed, in a PR nobody had merged

This is why running on a tree *behind* the open PRs was worth more than running on the newest one.
kit#72 exists to make an unknown flag a refusal at every entry point, and its header names a population
of seven tools. The blind author, told nothing, independently re-found **six of those seven**
(`writer.js`, the seventh, already refused). In case terms that is **9 of the 13 silent guesses closed
by #72** and a 10th by #77, leaving three. Measured, same command on both trees:

| command | `dev` | `dev` + #72 + #77 |
|---|---|---|
| `converge.js snip-it macro-metrics --bogus-flag` | SILENT (1026B) | REFUSED |
| `project.js snip-it --bogus-flag` | SILENT (18677B) | REFUSED |
| `requires.js snip-it --bogus-flag` | SILENT (585B) | REFUSED |
| `prose-audit.js --bogus-flag` | SILENT (852B) | REFUSED |
| `saturation.js --bogus-flag` | SILENT (3108B) | REFUSED |
| `self-host.js --bogus-flag` | SILENT (1494B) | REFUSED |

`prose-audit.js`, `saturation.js` and `self-host.js` swallowed `--help` on `dev` too, and refuse it on
the merged tree — those are the other three of the nine.

kit#77 closes the seventh and worst-by-correctness case: `node kit.js trial` substring-matched three
unrelated corpora — a habit tracker and a lending app — into **one coherent 31-behaviour report with
no warning that more than one file matched**. Not a crash and not an ignored flag: a wrong answer that
looks right.

🔑 **The lesson is about the review queue, not the code.** These defects were found, fixed, and then
sat unmerged long enough for an independent author to rediscover them from scratch. A fix waiting for
a click is, to a user, indistinguishable from no fix.

⚠️ **My own first version of the table above was wrong, and wrong in the flattering direction.** I ran
`converge.js --bogus-flag` with no positional argument, which refuses for the *missing argument*, and
recorded that as the flag being caught. The trial's shape — valid positionals first, **then** the typo
— is the one that exposes the bug. I varied the flag and forgot to vary the context it sits in.

## §3 — Four entry points outside that PR's population, and one is a different kind of bug

kit#72 names its population deliberately: the tools that answer a question about a *named artefact*.
These four are outside it and still drop flags:

| entry point | `--bogus-flag` | `--help` |
|---|---|---|
| `measure-tagging.js` | silent, runs the full report | silent, runs the full report |
| `compare.js` | **raw stack trace** | same |
| `mutate.js` | opaque exit | **runs the whole mutation suite** |
| `mutate-ui.js` | opaque exit | **runs the whole UI mutation suite** |

`compare.js` is a third shape again — not a silent drop but **no CLI layer whatsoever**: `argv[2]` goes
straight into `git -C`, so any stray token produces `fatal: cannot change to '--help'` followed by a
Node stack trace from `compare.js:25`.

🔴 **`mutate.js` and `mutate-ui.js` are not "the wrong artefact" — they rewrite your working tree.**
For every other tool here the cost of a swallowed flag is a misleading number. For these two it is an
unrequested, multi-minute, file-rewriting job. I did this to myself while measuring: `node
mutate-ui.js --help`, fired as a one-off probe, was **still running 20 minutes later**, had left a live
mutant in `prototypes/behaviour-ast/ui/src/pages/Project.tsx`, and **survived the `SIGTERM`** its
caller's timeout sent.

⚠️ **And recovering it exposed a second hazard worth writing down: `--recover` *consumes*
`MUTATION-IN-PROGRESS`.** The marker is not a flag, it is the restore data. I called `--recover` while
a run was still live; it correctly restored the file mutated so far and deleted the marker, and the
still-running process then mutated a *different* file with nothing left to restore it from. The
marker's own text is right that `git checkout --` is the wrong reflex — but once the marker is gone,
checkout is the only option left, and it is only safe if you can first prove `HEAD` is not itself
carrying a committed mutant. Suite green again afterwards (87/87).

## §4 — What genuinely works, and what confused the author

Not a complaint list; the ratio is 61% actionable and the strong dimension is worth naming.

**Consistently good: every tool that takes a corpus or app name refuses a bad one cleanly**, names the
exact path or name it looked for, and none crash. `check.js`, `kit.js`, `converge.js`, `requires.js`,
`project.js` and `writer.js` each phrase it slightly differently but all six are actionable with no
source-reading at all. Kit's "could not look" vocabulary shows up here and does real work.

**Model-dependent, reported as such:** the author's main confusion was that the two populations in §1
are invisible from outside — nothing in a tool's name, location or invocation says whether it has a CLI
layer, so `--help` is a coin flip. It also could not tell library from entry point: `git-store.js`
exits 0 silently with no output because it has no `require.main` guard, which is correct behaviour that
reads identically to a tool ignoring you.

## What this trial does not show

It provoked mistakes a *user* makes by accident. It says nothing about malformed corpora written by a
machine, nothing about the write paths (`ui.js` was never served), and nothing about whether the 31
actionable messages are actionable to a **human** rather than to a model that can read the source if it
gets stuck. The last one is the confound that no version of this method removes.
