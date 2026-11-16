# Ideas for later: user feedback box, working status, user technical-level profile, Kit reading pod logs, chat transcripts, security and custom agents, external integrations and data, onboarding tour, project integrations and secrets, lite hosting, project spaces, market research, hosted coding agent and token-credit pricing, reading the queue from CLAUDE.md
Work-item: backlog
Flag: the ideas from the 2026-11-02 and 11-03 notes already have cards in kit-ui.beh, so nothing new is specced; BEH-TOUR-1 says built though the owner asked to backlog onboarding, and BEH-SPACE-1 is partly built; the 11-04 pricing notes and the 11-03 queue question are for the owner to answer, not build; the 2026-11-07 notes are ideas for later and are not built; the .beh rename touches how Kit stores files; the 2026-11-12 and 11-14 GitHub-minutes notes ask a paid-plan question and a look at minimising minutes, for the owner, not built (level: high)
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
- new: Hosted coding agent and token-credit pricing
- new: Agent reads the queue from its instructions
- new: Pipeline runs as C# async instead of a node script
- new: Rename the spec file extension from .beh (owner leans to .kit)
- new: Design the spec structure per project; one file per project is not structured
- BEH-ORCH-12
- new: Compare a paid plan and self-hosting with the free Actions minutes
- new: Promotion pipeline that deploys from main, apart from the dev gate
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
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-03 11:17Z (question: does reading the queue need to be built into CLAUDE.md)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-04 00:52Z (hosting the user's coding agent; volume or clone per job; base subscription with monthly and bought credits; refunds; the note is cut short, read the source for the rest)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-04 00:57Z (the same pricing idea, restated in full; a duplicate of 00:52Z, filed once here)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 02:11Z (pipeline as a node script; C# async suggested)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 21:13Z (rename the .beh extension; owner leans to .kit)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 21:14Z (design the spec structure properly; one file per project is not structured)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-12 08:08Z (minutes used up; is a paid plan worth it, is self-hosting viable, back to the free tier?)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-12 08:37Z (use the pipeline for deployment to cut costs; a promotion pipeline that deploys from main)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-14 08:27Z (minimising credit usage while kit is private; paying for GitHub later)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (lines ~3205 onward: FEAT-CHAT-TRANSCRIPTS, FEAT-SECURITY-AGENT, FEAT-CUSTOM-AGENTS, FEAT-HOOKS-OUT, FEAT-HOOKS-IN, FEAT-EXTERNAL-DATA)
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CHAT-18, BEH-FEEDBACK-1, BEH-TOUR-1, BEH-PROJCONF-1, BEH-PROJCONF-2, BEH-LITE-1, BEH-LITE-2, BEH-SPACE-1, BEH-SPECAGENT-5)
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-ORCH-12, the private-project minutes card on main)
- prototypes/behaviour-ast/behaviours/kit.notes.md (the 2026-11-02, 11-03, 11-04, 11-12 and 11-14 notes listed above)
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
The 2026-11-03 11:17Z note asks whether reading the queue must be built into CLAUDE.md. It is a question for the owner, not work: the card line is new and unspecced, and it waits for his answer.
The 2026-11-04 00:52Z and 00:57Z notes are the same pricing idea (the second restates the first in full). They ask how a hosted coding agent would run (a runtime, a checkout of the repo, the dev process as instructions, Kit's commands as tools, per-user memory), whether it needs its own volume or a temporary clone per job, and how to price it: a base subscription with monthly credits that expire, a premium on token cost so most of the fee funds the volume and pod, bought credits that do not expire, and a refund path. It is design thinking, not a decision, and no card holds it yet; the owner should decide before any card is written.
Next step toward the goal: none from this bundle; the goal is the open chat and tester work, and these wait.
2026-11-16: three more ideas for later from the 2026-11-07 notes. Not built and not specced; the owner may want them scheduled.
2026-11-16: three GitHub-minutes notes, filed here rather than under record-session-cost, because they are about Actions minutes, not Claude session cost. The 2026-11-12 08:08Z note asks whether a paid plan (said to be 50% more) would still run out, what other options exist, whether self-hosting is viable, and whether to go back to the free tier. That is a question for the owner, and its answer needs research; nothing is built. The 08:37Z note asks to use the pipeline for deployment to cut costs, with a promotion pipeline that deploys from main so promotions are deliberate. BEH-ORCH-12 on main already covers part of it: a private project's tests run on the runner pool, not as Actions, and deploys build only from main. A separate promotion step is on no card, so it is a new card for later. The 2026-11-14 08:27Z note asks to look at minimising credit use while kit is private; BEH-ORCH-12 already quotes it, and the owner has said paying for GitHub waits until the project makes money.
