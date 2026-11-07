# Kit supports users other than James
Work-item: kit-supports-other-users
Cards:
- EPIC-KIT-USERS
- BEH-EPIC-7
- FEAT-USER-SIGNUP
- BEH-USERS-10
- FEAT-USER-REPOS
- BEH-USERS-2
- BEH-USERS-3
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
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
The whole note is the epic EPIC-KIT-USERS, which BEH-EPIC-7 names as the first epic. Almost every point already has a card: no spec held centrally in the kit repo (BEH-USERS-2, BEH-USERS-12); projects inferred from the user's GitHub (BEH-USERS-2); a new repo for a new project (BEH-USERS-3); onboarding existing projects, out of scope and a backlog epic (EPIC-ONBOARD); GitHub sign-in (BEH-USERS-10, whose contract records it live since kit#503); a shared dev process with per-user memory (BEH-USERS-4, FEAT-USER-PROCESS); production secrets (FEAT-USER-SECRETS); per-user databases in two tiers (BEH-USERS-6); usage, cost and payment (BEH-USERS-7); and signup (BEH-WELCOME-1).
No new card is needed. The database choice is open: the note's OKE or AKS is not decided, and BEH-USERS-6's contract names Neon as an unreviewed answer. The builder should check the built state of each card rather than assume it. Sign-in persistence from the same user-facing area is its own work item (sign-in-persistence).
