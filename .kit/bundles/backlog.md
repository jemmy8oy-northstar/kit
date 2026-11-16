# Ideas for later: user feedback box, working status, user technical-level profile, Kit reading pod logs, chat transcripts, security and custom agents, external integrations and data, onboarding tour, project integrations and secrets, lite hosting, project spaces, market research
Work-item: backlog
Flag: the ideas from the 2026-11-02 and 11-03 notes already have cards in kit-ui.beh, so nothing new is specced; BEH-TOUR-1 says built though the owner asked to backlog onboarding, and BEH-SPACE-1 is partly built, so the owner should confirm what he wants parked (level: high)
Cards:
- BEH-FEEDBACK-1
- new: Kit shows when it is working
- BEH-CHAT-18
- BEH-EXTDATA-2
- BEH-TRANSCRIPT-1
- BEH-SECAGENT-1
- BEH-AGENTS-1
- BEH-HOOKOUT-1
- BEH-HOOKIN-1
- BEH-EXTDATA-1
- BEH-TOUR-1
- BEH-PROJCONF-1
- BEH-PROJCONF-2
- BEH-LITE-1
- BEH-LITE-2
- BEH-SPACE-1
- BEH-SPECAGENT-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:30Z ("Oo a backlog idea for when we have real users")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 23:12Z ("Kit shows when it is working - backlog if too high of a cost now")
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 00:01Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 13:50Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:54Z (chat logging and a personal transcript volume; the save-as-note question is answered in the chat work item)
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:57Z (security agent; custom agents such as a health or QA agent)
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:59Z (outbound hooks; inbound triggers; logs and log triggers, filed as three features)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 19:28Z (onboarding; "let's backlog this for now")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 20:22Z (project secrets page; migration strategy; lite hosting)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 20:27Z ("Backlog kit": project spaces; whether a global board is still worth having)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-03 10:19Z (market research and competitor analysis)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (lines ~3205 onward: FEAT-CHAT-TRANSCRIPTS, FEAT-SECURITY-AGENT, FEAT-CUSTOM-AGENTS, FEAT-HOOKS-OUT, FEAT-HOOKS-IN, FEAT-EXTERNAL-DATA)
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CHAT-18, BEH-FEEDBACK-1, BEH-TOUR-1, BEH-PROJCONF-1, BEH-PROJCONF-2, BEH-LITE-1, BEH-LITE-2, BEH-SPACE-1, BEH-SPECAGENT-5)
- prototypes/behaviour-ast/behaviours/kit.notes.md (the 2026-11-02 and 11-03 notes listed above)
Context:
Ideas only, not to be specced or built now. Each card above already holds its idea on main, in kit-ui.beh. This run edited no spec. BEH-FEEDBACK-1 holds the feedback idea, and the feedback and working-status cards wait until Kit has users other than the owner (see BEH-EPIC-7). Nothing in this bundle should be picked up until the owner moves it out of backlog.
The working-status note (10-26 23:12Z) is a backlog idea by the owner's own words. Pick it up only if the cost of showing Kit's working state is judged low; otherwise leave it here.
The technical-level profile (11-06 00:01Z, BEH-CHAT-18) is not specified yet and waits for more detail from the owner.
The pod-logs note (11-06 13:50Z) is BEH-EXTDATA-2. The owner said it is wanted later, not now, and unscheduled. A branch kit/work/kit-reads-pod-logs also exists on origin from earlier work; it is not an open ticket, so it is not reused here.
The chat-logging note (18:54Z) asks whether chats are logged. BEH-TRANSCRIPT-1 is the personal transcript volume idea (his own chats only, outside the repo, nothing saved for testers); it needs a volume in the cluster from the owner (claude-code-bot#58).
The security note (18:57Z): BEH-SECAGENT-1 is off by default and spends per audit, so it waits on the spend cap. Custom agents (BEH-AGENTS-1) reuse its switch and ticket filing.
The integrations note (18:59Z) asked for three features to be separated. They are filed as BEH-HOOKOUT-1 (Kit triggers integrations), BEH-HOOKIN-1 (integrations trigger Kit; a public credentialed endpoint, needs his go) and BEH-EXTDATA-1 (Kit reads external sources such as logs). Each needs a platform choice or the owner's decision before it is built.
The onboarding note (2026-11-02 19:28Z) is BEH-TOUR-1. Its contract says a six-step tour is built, but the owner said to backlog onboarding for now; he should confirm which he means.
The 20:22Z note has three ideas, each already a card: BEH-PROJCONF-1 (an integrations and secrets page per project; waits on the vault choice in BEH-USERS-5, not the secrets branches secrets-ui and user-secrets-and-database-hosting on origin, which are not open tickets); BEH-PROJCONF-2 (migrations on users' production databases: his rule to set, preprod only or strong disclosure); BEH-LITE-1 (lite hosting; waits on the platform choice in claude-code-bot#58) and BEH-LITE-2 (a test database and integrations; builds on the two before it). All four are pending.
The 20:27Z note asks for Kit to be organised by project, each project its own space like a git repo. BEH-SPACE-1 holds that and its contract says part is built. The question whether a global board is still worth having is not on a card. It overlaps the open ticket tester-feature-access only on the projects page, so it is not folded into that ticket.
The 2026-11-03 10:19Z note asks for Kit to help with market research and a review of what competitors do, as part of initial analysis. BEH-SPECAGENT-5 holds it; it is pending until he says whether web search may spend, and how much a design session may cost.
Next step toward the goal: none from this bundle; the goal is the open chat and tester work, and these wait.
