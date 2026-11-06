# Notes on kit-ui

Left in Kit (kit#118). Fold each one into the spec or the code, and delete it in the same pull request.

## 2026-10-16 11:45Z · BEH-KIND-1

This is good this is a useful filter addition I was originally hoping for the filter in the project view but I am going to accept this as well

## 2026-10-16 11:53Z · BEH-NOTE-10

Going to approve, do you think we could auto archive them or soemthing and then have an option to show archived. Maybe in the future we will have some expiry where old notes get auto deleted but don’t really need that yet.

## 2026-10-20 07:13Z · BEH-STAGE-7

I think we need some unstated column rather than leaving it off the board so that they can be found and staged

## 2026-10-26 22:50Z · BEH-USERS-3

I added client id and client secret idk whether this was for this ticket feel free to move it if it wasn’t. Also when you finish work but you are waiting for secrets or soemthing this needs to go into blocked not done

## 2026-10-26 23:00Z · BEH-USERS-6

Just thinking about this idk whether this is the best way to do it. Dbs are a lot of admin to manage right. Maybe better to outsource this at the start. Just as lovable does

## 2026-10-27 21:16Z · BEH-STAGE-11

What does this mean

## 2026-10-27 21:16Z · BEH-STAGE-9

What does this mean

## 2026-10-28 23:53Z · BEH-STAGE-11

Placed by how much of it is written? Do you mean spec wise? It’s placed on the board by what stage it’s at. How much of ux beh ui impl or

## 2026-10-28 23:53Z · BEH-USERS-5

This is not about Kubernetes secrets. The future direction is we use some kind of vault

## 2026-10-29 08:04Z · BEH-USERS-6

Do you have any other suggestions here?

## 2026-10-31 01:00Z · BEH-USERS-5

Oh I haven’t heard of openbao or infiacal. I was thinking oci or hashicorp

## 2026-10-31 01:01Z · BEH-GATE-H2

What does this mean?

## 2026-10-31 01:07Z · BEH-USERS-6

For all of these would it be better to let the user integrate their own account or spin up instances for the user? Which would be better for our pricing model as well? I guess we could price the user for what they use. Do you think neon would be good for us?

## 2026-10-31 16:45Z · BEH-USERS-9

Do you have an estimation of cost for how much a feature will take to execute or anything like that using anthropic api with opus 5.5? I am imagining that it will be quite expensive.

## 2026-11-03 08:23Z · BEH-SPLASH-1

Merge it when it ready

## 2026-11-03 08:57Z · BEH-SPLASH-1

Merged

## 2026-11-03 09:17Z · BEH-SPLASH-1

Merged to main

## 2026-11-03 10:58Z · BEH-SPLASH-1

This is complete right? Also why are you not cracking on with the kit backlog. Got to work towards being ready for users

## 2026-11-03 13:36Z

The owner wants one chat for the whole of kit, not three. kit, kit-ui and kit-hosted are all related and should share a single chat. Today each is a separate project with its own chat, so he has to talk to three places about one thing. Please group them so kit's chat covers all three.

## 2026-11-03 13:49Z

The owner wants a button next to Approve and Skip in the chat's floating widget that reposts the current item at the bottom of the chat, so he doesn't have to scroll up to find it. Label: "Show again" (alternatives he may prefer: "Bring to bottom" or "Recap"). Motivation: when an item ends up far up the chat he wants it posted again at the bottom.

## 2026-11-03 13:50Z

When the chat agent asks a yes/no or multiple-choice question, it proposes the answers as tappable options. He can tap one, or type a free-form response instead.

## 2026-11-03 13:54Z

The owner likes the current flow for creating notes from the chat. When a note is created from the chat, a bubble in the chat shows the deterministic action that was taken, with an undo button. He wants this kept and made concrete in the spec: after a note is created from the chat, the chat shows a bubble that states exactly what happened, and that bubble has an undo button that reverses it.

## 2026-11-03 13:55Z

The owner wants notes to carry a tag or label showing where they came from (for example, the chat, the floating note button, or the agent's own background work). The tag shows on the Notes page. It does not need to show in the chat for now.

## 2026-11-03 23:30Z · BEH-SPECAGENT-2

<!-- from: chat -->

The API key is added and in use for chat. Spend cap: the user can use as much money as they have credited in their account, and can also set a money allowance per day, week or month for their Kit agent to spend, drawn from that credit.

## 2026-11-03 23:32Z

<!-- from: chat -->

Three changes to the chat and action cards, from the owner's review:

1. The chat can show an answer that was already posted to an item, on request, so he can see what he wrote. It can also show a note's attributes (its text, its answer, its tag) the same way, without him having to open the note.

2. When an answer is added to a note in an action card, the card shows the text that was added, so he can see it straight away.

3. Remove the "next up in Kit" section. The next item comes up in the chat anyway, so it is not needed.

## 2026-11-03 23:35Z

<!-- from: chat -->

In the chat and on action cards, any item ID written in the text, such as BEH-USERS-5, is a link. Clicking it opens that item, so the owner can follow a reference like "It builds on a user's stored secrets (BEH-USERS-5)" without searching for it.

## 2026-11-03 23:37Z

<!-- from: chat -->

Vault decision for BEH-USERS-5 (user production secrets): the owner chose OCI Vault for now. Kit writes secrets to OCI Vault and the cluster reads them; Kit can write a value but never read it back. Migration to another vault (e.g. HashiCorp Vault) can be done later if needed, so keep the storage choice behind a seam. This unblocks BEH-USERS-5 and the work that waits on it, including BEH-PROJCONF-1 (integrations page) and the secrets part of BEH-USERS-9. Nothing else was decided here.

## 2026-11-03 23:39Z

<!-- from: chat -->

Owner wants BEH-USERS-5 (production secrets per user) deprioritised, so that the agent Kit runs for users (BEH-USERS-9) gets built first. He feels getting users the coding agent matters more than secrets right now. Please lower BEH-USERS-5's priority and reorder the schedule so BEH-USERS-9 comes ahead of it. The vault decision (OCI Vault for now) still stands and should stay recorded against BEH-USERS-5.

## 2026-11-03 23:40Z

<!-- from: chat -->

Owner wants the chat to know what Kit can and cannot do (BEH-CHAT-7), so it can explain this when asked and act within it. Kit can: approve, deny (with what is true instead) or answer an item in hand; undo the last thing done to an item; show an item again; save a note for the coding agent; put a technical question to the coding agent; and read whole spec cards. Kit cannot itself reorder, pause, schedule or change the priority of items, or edit the spec. When the owner asks for any of those, Kit leaves a note and the coding agent makes the change.

## 2026-11-03 23:44Z

<!-- from: chat -->

Spec and tests must stay coupled going forward, not only when a test is written. The aim is that the spec is always enforced in the project, and that we can deterministically verify its tests still enforce it.

When a behaviour has a test that enforces it, record a hash of the source code that test exercises against that behaviour on the spec. If that source later changes, flag the behaviour and its test link as possibly broken, so it can be re-checked quickly. The hash must be deterministic: the same code always gives the same hash, and the spec records which hash each link was last verified against.

He sees the flagged links. The coding agent computes and records the hashes and does the re-check.

Related: FEAT-HEALTH and BEH-HEALTH-1 count whether a test names a behaviour. They do not detect when a test has stopped enforcing it after the code changes, so this is an addition to that feature.

## 2026-11-04 11:00Z

<!-- from: chat -->

Chat input: on a phone, Enter adds a new line and the send button sends. On a desktop, Enter sends and Shift+Enter adds a new line.

## 2026-11-04 18:07Z

<!-- from: chat -->

Migrate the spec out of the kit repo into each project's repo, under a hidden .kit/ directory. Each project's spec lives in .kit/ in its own repo, the same way for every project, including kit: kit's own spec moves out of the kit repo into kit's .kit/. Consistent with BEH-USERS-2 (no spec held centrally).

## 2026-11-04 18:08Z

<!-- from: chat -->

Under BEH-USERS-2 ("Kit lists a user's projects from their own GitHub, with no spec held centrally"): the owner says all spec currently lives centrally in the kit repo, not in each project's repo. So the card's "no spec held centrally" describes the target, not today's state. Please note this against BEH-USERS-2. The card also detects a project by a .beh file on the default branch, so once the migration to .kit/ lands, detection needs to find .kit/ too. Settle which the card should say, current or target, and what detection checks.

## 2026-11-06 00:01Z

<!-- from: chat -->

Backlog idea: Kit keeps a rough profile of each user's technical level, tracking signs such as whether they hand-edit the repo and how technical their prompts are. The profile is meant to set the tone of Kit's replies. Not yet specified; to be backlogged for more detail later.

## 2026-11-06 13:50Z

<!-- from: chat -->

Kit should be able to read the internal logs of the backend and service pods that Kit has run for a project. The user should not have to wire these up, since they come from pods Kit itself runs. This is wanted later, not now: keep it in the backlog, unscheduled.
