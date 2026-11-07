# Kit supports users other than James
Work-item: kit-supports-other-users
Flag: The 17:21Z note moves central spec and user data out of the kit repo, and the user data store is undecided (OKE, AKS or Neon are all open), so this touches how Kit stores work; the owner should choose the store (level: high)
Review: revised — no new owner decision needed: the user memory store is settled and built (BEH-USERS-4), and the app database store already waits on the owner on BEH-USERS-6 with a default, so the rest of the epic can go ahead
Cards:
- EPIC-KIT-USERS
- BEH-EPIC-7
- FEAT-USER-SIGNUP
- BEH-USERS-10
- FEAT-USER-REPOS
- BEH-USERS-2
- BEH-USERS-3
- BEH-USERS-12
- FEAT-USER-PROCESS
- BEH-USERS-4
- FEAT-USER-SECRETS
- FEAT-USER-DATABASES
- BEH-USERS-6
- FEAT-USER-BILLING
- BEH-USERS-7
- EPIC-ONBOARD
- BEH-WELCOME-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 17:21Z (line 127)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z under BEH-USERS-2 ("Yes")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z under BEH-USERS-2 ("Can always change later if need be")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z under BEH-USERS-3 ("Yes", second)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-04 18:07Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-04 18:08Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit.notes.md
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
The whole note is the epic EPIC-KIT-USERS, which BEH-EPIC-7 names as the first epic. Almost every point already has a card: no spec held centrally in the kit repo (BEH-USERS-2, BEH-USERS-12); projects inferred from the user's GitHub (BEH-USERS-2); a new repo for a new project (BEH-USERS-3); onboarding existing projects, out of scope and a backlog epic (EPIC-ONBOARD); GitHub sign-in (BEH-USERS-10, whose contract records it live since kit#503); a shared dev process with per-user memory (BEH-USERS-4, FEAT-USER-PROCESS); production secrets (FEAT-USER-SECRETS); per-user databases in two tiers (BEH-USERS-6); usage, cost and payment (BEH-USERS-7); and signup (BEH-WELCOME-1).
No new card is needed. The database choice is open: the note's OKE or AKS is not decided, and BEH-USERS-6's contract names Neon as an unreviewed answer. That question is already in the corpus: BEH-USERS-6 has a needs-context (users bring their own database first, then Kit-made Neon databases once billing exists, BEH-USERS-7; that plan holds if he says nothing by 2026-11-12), and on 2026-10-31 he put databases below AI support. Do not build BEH-USERS-6 or FEAT-USER-DATABASES until he answers or that date passes. Where user memory is stored is a separate question, and it is settled: his 2026-10-23 06:39Z answer says a database Kit runs, built as the user_memory table behind /api/me/memory (BEH-USERS-4). So this flag does not hold up BEH-USERS-2, BEH-USERS-12 or the other cards. The builder should check the built state of each card rather than assume it. Sign-in persistence from the same user-facing area is its own work item (sign-in-persistence).
Added in triage from three 2026-10-23 06:38Z notes, all bare replies with no question text in the notes file. Their headings put two under BEH-USERS-2 ("Yes", "Can always change later if need be") and one under BEH-USERS-3 ("Yes"). Treat them as confirmation of those cards' current contracts (a project's repo lists projects from GitHub; a new project gets a new repo), and confirm with James before building anything that depends on them. They were filed here, not in early-access-sign-in, because they are about projects, not sign-in.

Added in triage from two 2026-11-04 notes, both about where a project's spec lives:
- 18:07Z: move each project's spec into a hidden .kit/ directory in its own repo, kit's included. This is BEH-USERS-12 (kit-ui.beh near line 1340), which already states it. Build it here as part of this epic; do not add a card.
- 18:08Z: BEH-USERS-2's title says "no spec held centrally", which is the target. Today all spec, including kit's, still lives in kit's own prototypes/behaviour-ast/behaviours, so note that against BEH-USERS-2 (its contract near line 1337 already says so). The card's detection rule (near line 1338) is: a repo is a Kit project when its default branch has a .kit/ directory holding a .beh file, or, until BEH-USERS-12 lands, a .beh file anywhere. Once BEH-USERS-12 lands, detection must find .kit/ and must not drop any project that only has a .beh file today. The owner asked for the card to settle current versus target; the title stays the target and the contract line states today, and that reading should be confirmed by the owner before it is treated as settled.
