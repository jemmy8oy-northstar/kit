# Pipeline untags blocked cards and wakes each stage only on change
Work-item: pipeline-blocked-watch-and-stage-wake
Flag: Open question: should BEH-ORCH-17 and -18 move into kit-ui.beh under FEAT-AGENT-PIPELINE, instead of a second ORCH feature (FEAT-ORCH) in kit.beh? The reply settled the tag question but not this one. Both IDs are already taken on origin/main in kit-ui.beh (BEH-ORCH-17 Runs now, BEH-ORCH-18 merge train, both built), and main's kit.beh has no ORCH cards, so this branch (49 commits behind main) needs new ids (BEH-ORCH-20 on) or a rebase before these cards land; and BEH-ORCH-16 (approved) says the first agent clears the tag when nothing is left open, which conflicts with 17 untagging on any reply (level: high)
Review: escalated — flag checked and holds: on origin/main BEH-ORCH-17/-18 are taken in kit-ui.beh (Runs now, merge train) and -19 too, so after a rebase these cards need BEH-ORCH-20/-21; he must decide (1) whether they go under FEAT-AGENT-PIPELINE in kit-ui.beh (default: yes, drop FEAT-ORCH) and (2) whether a reply untags the ticket at once (this 17) or the first agent clears it only when nothing is left open (approved BEH-ORCH-16, already built as slice 3: a reply queues a triage that clears the tag); default: keep 16 and narrow 17 to waking triage, not untagging
Cards:
- BEH-ORCH-17
- BEH-ORCH-18
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-16 22:56Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-16 22:58Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
Note 22:56Z asks for a component that monitors blocked items and untags them when a blocker changes. His reply on this ticket favours blocked as a ticket tag, which is working well now. So BEH-ORCH-17 clears the blocked ticket tag set by BEH-ORCH-16, deterministically, when his reply to the ticket's question has been posted since the tag was set. It replaces the clear a triage run makes today. It does not use a card's needs-context line (BEH-BOARD-17). Note 22:58Z asks that pipeline stages run as separate jobs that wake, check for changes, and sleep when there are none, with wake frequency tuned over time. BEH-ORCH-18 builds on BEH-ORCH-11 (one job per stage) and BEH-ORCH-6 (run log, where tuning data comes from). Only the wake rule is card text now; frequency tuning is left for later. Both notes are one run loop, so they are one work item. Nothing is built.
Test plan:
- BEH-ORCH-17: a blocked ticket whose question has no reply since the tag was set stays blocked, is not written to, and makes no model call
- BEH-ORCH-17: a blocked ticket whose question has been answered by a reply posted after the tag was set loses the blocked tag, returns to the agent's work list, and gets a line on the ticket saying it was untagged and which reply caused it
- BEH-ORCH-17: untagging leaves the question text in place and changes no approval, denial or paused line
- BEH-ORCH-17: a blocked ticket whose question cannot be read (the ticket or its question is not found) stays blocked and the wake reports it without failing the stage
- BEH-ORCH-17: a second wake after an untag finds nothing to do, so the ticket is not untagged or annotated twice
- BEH-ORCH-17: a triage run no longer clears the blocked tag itself; only the monitor does, so the tag is cleared in one place
- BEH-ORCH-18: a stage wake with no new notes, no changed ticket and no untagged blocker records "no change" in the run list and makes no model call
- BEH-ORCH-18: a stage wake that finds a new note or changed ticket does that stage's work and records a run rather than "no change"
- BEH-ORCH-18: the change check is deterministic: the same corpus and git state give the same result on repeated checks, with no model call
- BEH-ORCH-18: a stage's no-change wake does not hold up another stage's job for the same project (each stage is its own job, BEH-ORCH-11)
- BEH-ORCH-18: the wake interval is read from a setting at each wake, so a changed setting applies at the next wake without a restart, and the default keeps the current schedule
- BEH-ORCH-18: a "no change" wake is not reported as a lost or dead run (BEH-ORCH-14), while a check that errors (for example git unavailable) is recorded as a failed run, not "no change"
