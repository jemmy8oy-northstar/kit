---
tags: [kit, trial, findings]
updated: 2026-09-24
status: an account of using Kit, not a claim about it
---

# What happens when you start a project in Kit

_2026-09-24. James, [claude-code-bot#99](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/99):
**"Whilst I do not have access you can use kit! … you can come up with your own projects."** So I
did. The app is [Longlist](longlist-brief.md); the app is not the point. **The point is everything
Kit could not do, measured rather than recalled.**_

Every number below came from a command that ran on `dev` at `ef96206`, with Kit serving on
`127.0.0.1:4321`.

## The finding, in one line

**Binding every noun Kit's command line tells you to bind still leaves a behaviour permanently
refusing, and the two bindings that fix it are named nowhere in that output.** The UI knows them.
The CLI does not.

## 1. You cannot start a project in Kit

Probed rather than read — four requests, all of which wrote nothing:

| request | answer |
|---|---|
| `POST /api/projects` | `404 no-such-route` — *"nothing accepts a POST at /api/projects"* |
| `POST /api/projects/longlist` | `404 no-such-route` |
| `GET /api/projects/longlist` | `404 no-such-project` — *"no corpus named 'longlist'"* |
| `POST /api/projects/longlist/behaviours` | `404 no-such-project` |

The write route that would let you author a first behaviour **refuses because the corpus must
already exist.** So step one of a new project is `cat > behaviours/longlist.beh`, in a shell, on the
machine serving Kit. That is [kit#38](https://github.com/jemmy8oy-northstar/kit/issues/38), now with
a measurement attached instead of an assumption.

## 2. The corpus is in the wrong place, and there is nowhere right

His [kit#52](https://github.com/jemmy8oy-northstar/kit/issues/52) decision is that **a spec lives
with its project**. Longlist has no repo. And only `ui.js` takes `--dir` — `kit.js` and `check.js`
do not:

```
$ grep -n '--dir' kit.js ui.js check.js
ui.js:778:    else if (argv[i] === '--dir') { opts.dir = next; i++; }
```

So a corpus anywhere other than `prototypes/behaviour-ast/behaviours/` can be **browsed** but cannot
be gated by `kit check` — stage 7, the only part of Kit that goes red. Putting `longlist.beh` inside
Kit's own repo is not tidiness; it is the only placement under which Kit can check it at all.

## 3. Writing forwards produced the sub-population nine real corpora never had

Kit's projection sorts a noun into **missing** (no binding) or **insufficient** (bound, and still
not enough). Until today `insufficient` had **zero members in every committed corpus**, because all
nine were reverse-engineered from shipped apps — when you already have the app, you copy the real
locator and it is right first time.

Writing a spec forwards and guessing at bindings produced one immediately. `page:Idea` is bound with
a `route`, and still refuses:

> **`urlPattern`** — *"a URL that a regex can recognise once navigation has settled — this is NOT
> the same obligation as the route, and a page reached by both `opens` and `lands` owes both"*

That is Kit explaining itself well. The finding is not the message, it is that **the branch
producing it had never been exercised by a real corpus**, and one afternoon of forward authoring
exercised it. The `insufficient` branch is no longer resting on a constructed test alone.

## 4. 🔴 The CLI's "unbound nouns" list is incomplete

`node kit.js longlist` reports **16 unbound nouns** and `0/28` generated. Bind all 16 — with the
right *kind* of binding for each verb, which took two attempts — and it reaches **26/28**. Two steps
still refuse. One is `page:Idea` above, correctly explained. The other is not explained anywhere:

```
BEH-CAPTURE-2: when fills form:NewIdea with ?fields     ← still refusing, with form:NewIdea bound
```

`fills` resolves its fields from another behaviour's `provides` — `form:NewIdea.fields = Idea,
WhyNow` — and then looks each one up **directly in `bindings`, bypassing `bind()`**:

```js
const fb = bindings[`field:${f}`];
if (!fb) return null;
```

`bind()` is what records a noun as missing. So `field:Idea` and `field:WhyNow` **never enter the
unbound set**, and the CLI never names them. Adding exactly those two takes the corpus to **28/28**.

| what you bind | generated | refusing |
|---|---|---|
| nothing | 0/28 | 28 |
| every noun the CLI names (16) | 26/28 | 2 |
| **plus `field:Idea`, `field:WhyNow`** | **28/28** | 1 (the `urlPattern` above) |

⚠️ **The UI is right and the CLI is wrong.** The `requires` panel lists **18** nouns — the same 16
plus those two. So this is not a hole in Kit's model; it is a hole in one of its two front ends, and
the one a new user reaches first. A CLI user binds everything they are told to and watches a
behaviour keep refusing with no way to discover why short of reading the generator's source.

🔑 **It is the hole-filling mechanism — Kit's signature feature — that has the blind spot.** A field
that arrived through a `provides` is exactly the field the CLI cannot tell you about.

## 5. Adding one corpus turned three assertions red, in two different suites

Dropping a single `.beh` file into the directory broke:

| assertion | why |
|---|---|
| `binding: the warning fires on the real cross-app collision` | `sharedWith` was pinned to `['trial-lend']`; Longlist uses `region:EmptyState` too |
| `ui` fixtures vs the real read API | `GET /api/projects` lists every corpus, so the committed fixture drifted |
| `marks a trial corpus…` | asserted `toHaveLength(1)` trial; there are now two |

**None of these is a bug, and none of them is noise either.** The first is a *true* collision in a
namespace that is global on purpose — so the fix was to record the new member, not to rename the
noun and hide it. The second is a generated artefact, regenerated. The third was over-specified:
the count of trials was never the claim, so it now asserts one badge **per** trial, which stays red
when the badge is dropped (verified by removing it) without being coupled to how many trials exist.

🔑 **The general shape: the corpus directory is an implicit population for every measurement that
reads it, and onboarding a project is a population change.** Three assertions is the current toll
for adding one file — payable, but nobody had ever paid it, and a consumer onboarding their own
project would hit all three inside Kit's own repo with no idea which were theirs to fix.

## What this says about the loop

Kit's *engine* came out of this well. Hole-filling worked across behaviours; the refusals were
honest; `insufficient` explained itself better than I could have; nothing was guessed. **Everything
that went wrong was at an edge** — no way in, nowhere to put the corpus, and a report that omits two
nouns. Those are the parts nobody had used, because nobody had started a project.

⚠️ **One honest limit on all of the above: I wrote the corpus, the brief and the bindings.** That is
fatal to any claim about whether Kit helps *someone else* think, and it is why `longlist.beh` carries
`# kit:not-a-real-app`. It is not fatal to the question actually asked here — what Kit does when a
project starts — because that question is about the tool, not the author.

## The four shapes the notation still cannot state

Recorded in `longlist.beh` rather than worked around. **TIMING** and **ORDERING** repeat
[trial-lend](../../prototypes/behaviour-ast/behaviours/trial-lend.beh)'s findings unchanged, four
weeks later and by a different route, which makes them population evidence rather than anecdote.
Two are new:

- **NEGATION** — trial-lend could assert a weaker fact instead (an empty state). Here the list is
  not empty after an idea leaves it, so even that escape does not exist. **The workaround does not
  generalise.**
- **REQUIREDNESS** — "declining requires a reason" is the rule that gives the feature its point, and
  the corpus can only say the field is *seen*. It would have to go in a `contract` line, i.e. the
  one rule that justifies the feature is not expressible as a behaviour.

Both are changes to what the corpus language can express, which is
[claude-code-bot#58](https://github.com/jemmy8oy-northstar/claude-code-bot/issues/58) territory —
his, not mine. Written down, not built.
