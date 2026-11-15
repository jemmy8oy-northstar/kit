# Ideas for later: user feedback box, working status, user technical-level profile, Kit reading pod logs, chat transcripts, security and custom agents, external integrations and data
Work-item: backlog
Flag: all these are ideas for later: the owner marked them so, or asked for a separate feature to be filed; each already has a backlog card on main in kit-ui.beh, so this run changed no spec (level: low)
Cards:
- BEH-FEEDBACK-1
- new: Kit shows when it is working
- BEH-CHAT-18
- new: Kit reads the internal logs of the backend and service pods it runs for a project
- BEH-TRANSCRIPT-1
- BEH-SECAGENT-1
- BEH-AGENTS-1
- BEH-HOOKOUT-1
- BEH-HOOKIN-1
- BEH-EXTDATA-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:30Z ("Oo a backlog idea for when we have real users")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 23:12Z ("Kit shows when it is working - backlog if too high of a cost now")
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 00:01Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 13:50Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:54Z (chat logging and a personal transcript volume; the question about save-as-note is answered in the chat work item)
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:57Z (security agent; custom agents such as a health or QA agent)
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:59Z (outbound hooks; inbound triggers; logs and log triggers, filed as three features)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (lines ~3205 onward: FEAT-CHAT-TRANSCRIPTS, FEAT-SECURITY-AGENT, FEAT-CUSTOM-AGENTS, FEAT-HOOKS-OUT, FEAT-HOOKS-IN, FEAT-EXTERNAL-DATA)
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CHAT-18, BEH-FEEDBACK-1)
Context:
Ideas only, not to be specced or built now. Each card above already holds its idea on main. BEH-FEEDBACK-1 holds the feedback idea. The waiting rule is in the corpus: none of these waits for a priority, and the feedback and working-status cards wait until Kit has users other than the owner (see BEH-EPIC-7). Nothing in this bundle should be picked up until the owner moves it out of backlog.
The 18:54Z note asks whether chats are logged. BEH-TRANSCRIPT-1 is the personal transcript volume idea (his own chats only, outside the repo, nothing saved for testers); it needs a volume in the cluster from the owner (claude-code-bot#58).
The 18:57Z note: the security agent (BEH-SECAGENT-1) is off by default and spends per audit, so it waits on the spend cap. Custom agents (BEH-AGENTS-1) reuse its switch and ticket filing.
The 18:59Z note asked for three features to be separated. They are filed as BEH-HOOKOUT-1 (Kit triggers integrations), BEH-HOOKIN-1 (integrations trigger Kit; a public credentialed endpoint, needs his go) and BEH-EXTDATA-1 (Kit reads external sources such as logs). Each needs a platform choice or the owner's decision before it is built.
The pod-logs note (11-06 13:50Z) is BEH-EXTDATA-2 on main, which this bundle lists as "new: Kit reads the internal logs..." above. A branch kit/work/kit-reads-pod-logs also exists on origin from earlier work; it is not an open ticket, so it is not reused here.
Next step toward the goal: none from this bundle; the goal is the open chat and tester work, and these wait.
