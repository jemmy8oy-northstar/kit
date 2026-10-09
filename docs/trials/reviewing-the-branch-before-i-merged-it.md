# Trial — reviewing the branch before I merged it

_Run 2026-10-02, while `kit#106`'s CI was still running, against that branch @
`2ee8e2e` (blinded to `5a04f46`). Method: two agents with no prior context, in
throwaway clones blinded with `git rm -r docs/trials && git commit`, forbidden
from fixing anything and from running `mutate*` / `start.js`. The blinded
clone's suite was verified at **414 passed / 0 failed before either agent was
handed it**. Lead session reproduced every artefact claim independently._

## The one change from the 263rd's trial, and it is the whole point

That trial pointed blind agents at work **already merged**, four hours old. The
findings were real and the fixes were follow-up PRs. This one pinned the clones
to the **branch SHA while CI ran**, so a finding is a commit on the branch
rather than a new ticket — and the one finding that mattered most would have
been impossible to act on after the fact, because it was a false claim in the
PR body itself.

Two further corrections to the method, both learned the hard way:

- **Blind with `git rm`, not `rm`.** The 263rd used a plain `rm`, which left ten
  *tracked* files missing, took a test ENOENT in both clones, and an agent
  reported my own blinding step as "a pre-existing, unrelated test failure" —
  correct symptom, wrong cause, and the cause was me.
- **Verify the blinded clone is green before handing it over.** One command. It
  turns "the environment is broken" from a finding the agent has to diagnose
  into a precondition it can rely on.

## The claim handed to agent A, verbatim

> Every command-line tool in `prototypes/behaviour-ast/` that parses arguments
> refuses a flag whose VALUE was forgotten. If you write `--dir --actor human`,
> the tool refuses and names `--dir`, rather than silently taking `--actor` as
> the directory and dropping `human`. This holds for all five tools that export a
> parser, it is enforced by the test `cli: an app name that EQUALS a flag value
> is still the app name`, and that test's population is derived from what each
> tool exports so no tool can quietly opt out.

Agent B was given one question instead: *when a tool refuses a bad command line,
does the message tell the operator what was actually wrong — and is the MESSAGE
held by a test, or only the exit code?*

## Headline

**Every clause of the claim was true. The sentence as a whole was not.**

The five named tools do refuse. The population *is* derived. Nothing in the
mechanism was broken. But the derivation was from **what each tool exports**,
and that is a property of a file's *shape* rather than of whether it can have
the defect — so three tools that parse inline in `main()` and export nothing
were outside the population **by construction, with every assertion green**.

Both agents found them independently. `kit#103`'s own body had said *ten* tools;
the gate held five, and I was one sentence away from telling James it was closed.

| what was wrong | severity |
|---|---|
| `self-host.js --findings --record` wrote a file literally named `--record`, **exit 0** | the tool that writes |
| `prose-audit.js --source --demo-collision` ran the demo, **exit 0**, never looked at `--source` | read-only |
| `saturation.js --dir --check`, `self-host.js --corpus --writeup x` | refused, blamed the wrong thing |
| `selfhost/run.js` has **no flag guard at all** — any typo accepted at exit 0 | kit#107 |
| `compare.js /no/such/repo` answers with a raw Node stack trace | kit#107 |

`self-host.js` is the one that earns the trial. Its findings JSON went to
`--record` while the **prose was rewritten from the fresh numbers**, so the two
halves disagreed — which is precisely the drift that branch's own comment says
the design exists to prevent ("for eleven days it kept a WRONG one and `--check`
never opened the file"). A forgotten keystroke re-created the failure the tool
was built to kill.

## What the trial found about me, which is the durable part

**1. An exemption's reason must be true OF THE PROPERTY BEING TESTED.** Twice in
one session, and that makes it a pattern rather than an anecdote:

- `kit#105` deferred `ui.js` because *"`main` starts a server, so its parser is
  not reachable by a unit test"*. True of the success path; a refusal never
  reaches it, `parseArgs` was already exported, and a test 3,400 lines earlier
  already drove `ui.main`. The bug was behind the exemption.
- `kit.test.js` exempts `selfhost/run.js` because *"it drives Playwright, and
  kit deliberately has no `@playwright/test` dependency"*. True of a full run,
  irrelevant to a flag guard — `kit#101` had already established that a guard
  reads `argv` and nothing else.

Both reasons named the **expensive** thing the tool does. Both tests needed only
the **cheap** thing.

**2. A deferral pinned in a test checks that the reason EXISTS, never that it is
TRUE.** `kit#105` moved `ui.js`'s exemption out of prose into a `DEFERRED` map
with assertions that it was still an entry point, not also driven, and carried a
reason over 20 characters. That felt like the rigorous version. Nothing in it
read the sentence as a claim about the world, and the sentence was false. State a
deferral's reason as a **measurement** and take the measurement.

**3. An exit code is a discriminator only if no other branch can produce it.**
Found by my own red control coming back green, then confirmed by agent B as a
systemic pattern: `quiet()` discards stderr, so ~15+ assertions read only
`=== 2` across tools whose `main` has 3–8 independent exit-2 branches. In
`ui.js`'s case a `main` that ignored `opts.error` entirely left the suite green,
because `parseArgs` returns `{error}` **and nothing else** — so `dir` was
`undefined`, no corpora were found, and it exited 2 for an unrelated reason.
Same shape as `kit#101`'s `mutate-ui.js --zznotaflag`, which already exited 2
from its missing-install branch.

**4. Proving beats inferring, and it was nearly free.** The PR body claimed
`git-store.js` "would have pushed a branch called `--git-remote`". Rather than
leave that an inference from two verified facts, `git push origin
"HEAD:--git-remote"` into a throwaway local **bare** repo created exactly that
branch. Inverting the probe to a local remote made proving it cost nothing. In
the same pass the body's claim that `--dir --repos /x` "served" a wrong corpus
turned out to be false — it exits 2 blaming a missing one — and was corrected
before the merge.

## What held up

Agent A tried `=value` syntax, repeated flags, a lone `-`, `--`, negative
numbers, booleans and flags-after-`--` against the five exported parsers and
broke none of them. Agent B confirmed that `kit.js`'s spawned message-asserting
tests, the cross-tool unknown-flag scan, the sandboxed harness-pair guards and
`ui.js`'s own forgotten-value test all hold their messages properly. The
mechanism was sound; only its population was wrong.

## One thing checked and deliberately not reported

`mutate-ui.js` also reads `process.argv[i + 1]` for `--only` and is exempt as
`SANDBOXED`, so a third instance looked likely. It already refuses `--only`
followed by a flag, with a sentence, for the reason given in its own comment.
Measured before writing it up.

## Agent hygiene

Agent A reported one claim it had reached by **reading** rather than running
(`prose-audit.js`). It was right, and it was still re-run here before being
acted on — a report's confidence is not evidence. Neither agent ran a forbidden
tool. Both disclosed what they could not check: no Playwright binary, and
`git` denied by sandbox permissions in one case.
