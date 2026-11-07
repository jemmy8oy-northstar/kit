# Per-user agent, memory and cost: a user's work runs on Kit's agent, and test users are not charged
Work-item: user-agent-memory-and-cost
Flag: The 2026-10-23 06:41Z note (test users not charged) may conflict with BEH-USERS-7 (a user pays for compute and AI); apply the kit:conflicts setting and do not pick a reading silently. The 2026-10-29 08:09Z note's mechanism for a test user's own projects is not specified, so its card is a question.
Cards:
- BEH-USERS-4
- BEH-USERS-8
- BEH-USERS-9
- BEH-USERS-7
- BEH-USERS-13
- BEH-ORCH-1
- BEH-SHARE-1
- BEH-USERS-6
- BEH-PROJCONF-1
- new: test users' compute and AI are paid for by James until tracking and billing are added in a later stage
- new: a test user's own project gets AI from the key James pays for until billing exists (question: is an AI API the mechanism for their own projects?)
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:39Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:41Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:42Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 07:07Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-29 08:09Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 00:58Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/holiday-planning.beh
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
Holiday app (2026-10-29 08:09Z): he asks to onboard the holiday app to Kit with his first test user. The repo is shared, so the bot can pick up its items. BEH-SHARE-1 in holiday-planning.beh already says the project is shared with his first test user and the bot picks up its items. A test user's own projects need another mechanism, which he thinks is an AI API. That is the idea behind BEH-USERS-13. Until he answers, the default is the same key James pays for, as for the other test users (06:41Z).
Database order (2026-10-31 00:58Z): AI support is higher priority than database support for now, and users manage their own deployments. BEH-USERS-6 (databases per user) and BEH-PROJCONF-1 (configure secrets and integrations such as a database or API keys) are the cards. The note puts database work after AI work; it does not remove it.
