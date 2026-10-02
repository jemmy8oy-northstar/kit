# Trial project brief — "Lend"

**This describes an app that does not exist and is not planned.** It is a test
fixture for Kit, created under claude-code-bot#92 (James, 2026-09-06: *"You could
also make up some trial projects to progress it"*).

⚠️ **Do not read this as a proposal for a new northstar project.** cc-bot#49 is
still in force — depth over breadth, reduce over create. Nothing here asks for a
repo, and nothing here should ever get one on my say-so.

## Why a made-up app is the only way to test this

Every corpus Kit has was reverse-engineered from software that already shipped:

| corpus | derived from |
|---|---|
| `snip-it.beh` | its own Playwright suite — so it could not fail |
| `james-habits-app.beh` | `docs/DESIGN.md` + routes on `origin/dev` |
| `language-vocab.beh` | the same, for vocab |
| `macro-metrics.beh` | 46 written acceptance criteria |
| `kit.beh` | Kit's own source |

`bindings.json` says so in its own header: *"Every value below is copied from
snip-it … nothing here is invented."* That is exactly the right discipline for
**measuring** an app and exactly backwards for **starting** one.

So the claim `behaviours → application build` has never been executed. This brief
is the smallest thing that executes it: intent first, no app, no source tree, no
locators to copy.

## The brief, as a user would give it

> I lend things to people constantly — books, tools, a projector — and I lose
> track of them. I want to open one page and see what is currently out and who
> has it. I want to record a new loan in a few seconds. When something comes
> back I mark it returned and it leaves the list. Things that are past their due
> date should be obvious, and I want the worst offenders at the top so I know
> who to chase. If the list is empty I want to be told that clearly rather than
> staring at a blank page.

That is deliberately the register James described on cc-bot#68 — a user talking
about a workflow, glossing over detail, leaving holes an implementer would have
to fill ("a few seconds", "obvious", "worst offenders").

## Chosen to stress the known ceiling

`docs/pilots/macro-metrics-prose.md` named twelve grammar shapes the notation
cannot express. This domain produces four of them **naturally**, without being
contrived to fail:

| shape | where it arises here |
|---|---|
| **ordering** | "worst offenders at the top" |
| **timing** | an item becomes overdue *after* its due date passes |
| **negation** | a returned item must *not* still appear on the shelf |
| **cardinality** | one row per loan; a borrower with three items is three rows |

If the notation handles them, the ceiling finding was pessimistic. If it does
not, the trial says so and the ceiling stands — it is not licence to widen the
grammar to make my own trial look good.

## The holes, left in on purpose

A real Stage-1 conversation ends with unknowns, and the notation makes those
first class (`?field`). Left unresolved here:

- What identifies an item — free text, or a picked thing?
- Is a due date required, or optional with "no date" meaning never overdue?
- Can the same physical item be lent to two people at once? (Assumed no.)
- What happens to returned loans — deleted, or kept as history?

These are exactly the questions Kit's question sheet is meant to surface, so the
trial should be judged partly on whether it surfaces them.
