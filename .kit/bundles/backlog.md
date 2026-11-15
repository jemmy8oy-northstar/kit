# Ideas for later: user feedback box, working status, user technical-level profile, and Kit reading pod logs
Work-item: backlog
Flag: all four are ideas for later: the owner marked three so, and the technical-level profile is noted as "to be backlogged for more detail later" (level: low)
Cards:
- BEH-FEEDBACK-1
- new: Kit shows when it is working
- BEH-CHAT-18
- new: Kit reads the internal logs of the backend and service pods it runs for a project
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:30Z ("Oo a backlog idea for when we have real users")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 23:12Z ("Kit shows when it is working - backlog if too high of a cost now")
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 00:01Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 13:50Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
Ideas only, not to be specced or built now. BEH-FEEDBACK-1 already holds the feedback idea as a card. It waits until Kit has users other than the owner (see BEH-EPIC-7, the kit-supports-users epic). Nothing in this bundle should be picked up until the owner moves it out of backlog.
The working-status note (10-26 23:12Z) is a backlog idea by the owner's own words. Pick it up only if the cost of showing Kit's working state is judged low; otherwise leave it here.
The technical-level profile (11-06 00:01Z): Kit keeps a rough profile of each user's technical level, from signs such as whether they hand-edit the repo and how technical their prompts are, to set the tone of replies. BEH-CHAT-18 already holds the idea. Not specified yet; it waits for more detail from the owner.
The pod-logs note (11-06 13:50Z): Kit should read the internal logs of the backend and service pods that Kit itself runs for a project, so the user never wires them up. The owner said it is wanted later, not now, and unscheduled. A branch kit/work/kit-reads-pod-logs also exists on origin from earlier work; it is not an open ticket, so it is not reused here.
