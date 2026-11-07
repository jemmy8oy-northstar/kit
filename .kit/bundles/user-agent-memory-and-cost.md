# Per-user agent, memory and cost: a user's work runs on Kit's agent, and test users are not charged
Work-item: user-agent-memory-and-cost
Flag: The 2026-10-23 06:41Z note (test users not charged) may conflict with BEH-USERS-7 (a user pays for compute and AI); apply the kit:conflicts setting and do not pick a reading silently.
Cards:
- BEH-USERS-4
- BEH-USERS-8
- BEH-USERS-9
- BEH-USERS-7
- BEH-USERS-13
- BEH-ORCH-1
- new: test users' compute and AI are paid for by James until tracking and billing are added in a later stage
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:39Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:41Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:42Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 07:07Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- backend/Balenthiran.Kit.Abstractions/Services/IUserMemoryStore.cs
- backend/Balenthiran.Kit.Database/PostgresUserMemoryStore.cs
- backend/Balenthiran.Kit.Services/MemoryUserMemoryStore.cs
- backend/Balenthiran.Kit.DataModels/Models/UserMemory.cs
- backend/Balenthiran.Kit.Abstractions/Services/AgentUsage.cs
Context:
Memory (06:39Z): the answer is already in the code. Repo memory stays in the repo (.kit/memory.md). A user's memory is kept by Kit in its database (IUserMemoryStore, backed by Postgres) and read through GET /api/me/memory. Confirm this against BEH-USERS-8 and BEH-USERS-4 before building a new store.
Cost (06:41Z): James likes the idea of users starting on their own key, but test users will not use it if it costs them. For testing, James pays. Tracking and billing come later, even if charging does not start. This conflicts with BEH-USERS-7 as written, so see the flag.
Sign-up (06:42Z): "I think we skills add it with the sign up feature. So it's ready for first sign up?" is unclear. "skills" is probably "should", and "it" most likely refers to the test-user key or cost setting from 06:41Z. Confirm with James before building the sign-up link.
Independent agent (07:07Z): for the first user, the agent that acts is one Kit runs, not James's own agent. Kit must move away from James's agent eventually, and costs are measured internally first. This is BEH-USERS-9. Keep the test-internally-then-measure-cost step visible in the work, and do not add billing.
