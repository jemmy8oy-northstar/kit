# Scheduled Opus review as a part of the agent pipeline
Work-item: scheduled-opus-review
Flag: this is a timer, an explicit exception to the no-timer rule in BEH-ORCH-3, so check it against the corpus conflict setting (level: high)
Cards:
- BEH-ORCH-13
- FEAT-AGENT-PIPELINE
- BEH-ORCH-3
- BEH-SETTINGS-5
- BEH-SETTINGS-6
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-07 01:32Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-15 12:51Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
The owner's note (11-07 01:32Z): add a scheduled Opus review to the agent pipeline, as its own part of FEAT-AGENT-PIPELINE, not a custom agent under BEH-AGENTS-1. It is an exception to the no-timer rule in BEH-ORCH-3. A cheap model still triages most tasks as the pipeline does now; the Opus review runs on a timer. The review checks neglected tasks, a sample of completed work, and the architecture, to keep things in check.
The owner's answer (11-15 12:51Z): the cadence setting goes on the Settings page as its own row, next to the pipeline, and can be set per project. The choices are daily, every two days, or weekly. This settles the open question between a new Settings row and BEH-SETTINGS-5 or BEH-SETTINGS-6: it is a new row, and it does not replace either of those.
Builder: the review is a timer, so record the exception to BEH-ORCH-3 as a contract, and do not fold it into BEH-AGENTS-1.
Spec stage: BEH-ORCH-13 (kit-ui.beh, priority 4, pending) already covers this card in full, with the owner's 11-07 and 11-15 notes as contracts, so no card is added and the "new:" line names it instead. BEH-ORCH-13 does not yet record the exception to BEH-ORCH-3 as a contract; the builder adds that to BEH-ORCH-13. kit-ui.beh has no "# kit:conflicts" line, so the conflict between BEH-ORCH-3 (no timer) and BEH-ORCH-13 (timer) follows the default "build": build the timer reading and raise a review request on BEH-ORCH-13 naming both ids. The Notes paths point at a directory that is not in this repo; the same anchors are in prototypes/behaviour-ast/behaviours/kit-ui.notes.md (## 2026-11-07 01:32Z and ## 2026-11-15 12:51Z · BEH-ORCH-13). Open, not in the card: whether a review runs for a project whose pipeline is off (BEH-ORCH-15). The test plan assumes neither answer.

Test plan:
- BEH-ORCH-13: with the cadence off (the start state of every project), no review run starts however long the timer runs.
- BEH-ORCH-13: a project on daily, every two days or weekly starts one review run per interval, and never more often than its interval.
- BEH-ORCH-13: setting the cadence on one project changes no other project's cadence, and a newly added project starts off.
- BEH-ORCH-13: the review run uses the review model (KIT_STAGE_MODEL_REVIEW, Opus), and its task text asks for neglected tickets, a sample of finished work and the architecture.
- BEH-ORCH-13: the review run pushes no kit/work/* branch and changes no code; its findings are written as notes on the project.
- BEH-ORCH-13: a note written by the review is picked up by the next triage run like any other note.
- BEH-ORCH-13: the review is a stage of FEAT-AGENT-PIPELINE and is not registered as a custom agent under BEH-AGENTS-1.
- BEH-ORCH-13: Settings shows a cadence row per project, next to the pipeline row, with the choices off, daily, every two days and weekly.
- BEH-ORCH-3: the timer starts only the review run; triage, spec and build still start only when a user's action queues them.
- BEH-SETTINGS-5: the idle choice and GET /api/settings/idle are unchanged by the new row.
- BEH-SETTINGS-6: the ordered idle list keeps its items and switches, and the review row is not added to it.
