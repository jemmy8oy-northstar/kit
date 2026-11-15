# Each user's agent: memory kept by Kit, test users on Kit's key, Kit's agent for the first user
Work-item: user-agent-memory-and-keys
Flag: the 06:42Z note reads "skills add it", and I took "skills" to mean "should", with "it" meaning the user's agent, to be added to sign-up (level: low)
Cards:
- BEH-USERS-4
- BEH-USERS-8
- BEH-USERS-9
- BEH-USERS-13
- new: test users run on Kit's own AI key, with no charge, until tracking and billing are introduced
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:39Z (where user memory is stored)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:41Z (own key, cost to test users)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:42Z (add with sign-up)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 07:07Z (independent agent for the first user)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- backend/Balenthiran.Kit.Services/KitRouter.Memory.cs
- backend/Balenthiran.Kit.Database/PostgresUserMemoryStore.cs
- backend/Balenthiran.Kit.Abstractions/Services/IUserMemoryStore.cs
- backend/Balenthiran.Kit.Tests/UserMemoryTests.cs
Context:
- Where user memory lives (06:39Z): the process keeps repo memory in .kit/memory.md, in the repo. User memory is kept by Kit, not the repo: GET and POST /api/me/memory, stored in Postgres. The note's question is answered by that, so do not build a new store. BEH-USERS-8 already keeps a user's memory to that user.
- Test users and cost (06:41Z): Kit pays for test users' agent work for now. Tracking and billing come in a later stage, even if nothing is charged yet. Do not build billing or metering now. BEH-USERS-13 (a user brings their own subscription as a cheaper tier) stays as written.
- Sign-up (06:42Z): the user's agent and its memory should be created at sign-up, so the first sign-up is ready. The sign-up path is in early-access-sign-in-invite.
- First user (07:07Z): the first user's work is done by an agent Kit runs, not the owner's own agent (BEH-USERS-9). The owner intends to move off his own agent after internal testing and a cost check. Out of scope for this bundle.
