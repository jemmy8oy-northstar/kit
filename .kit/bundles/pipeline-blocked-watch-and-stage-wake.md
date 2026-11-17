# Pipeline untags blocked cards and wakes each stage only on change
Work-item: pipeline-blocked-watch-and-stage-wake
Flag: The new cards are written into kit.beh as the triage brief asks, but the pipeline cards (BEH-ORCH-1 to -16) and the Blocked cards (BEH-BOARD-17, -18, -23) live in kit-ui.beh, so the owner may want these moved there; the untag rule also changes when a Blocked card returns to the agent (level: high)
Review: escalated — BEH-ORCH-17 untags cards by their needs-context line, but BEH-BOARD-18 defines Blocked as having that line (there is no separate tag), and BEH-ORCH-16 (approved) already makes blocked a ticket tag that is cleared when his reply wakes triage (slice 3 is built). He must say which the monitor clears: BEH-ORCH-16's ticket tag, deterministically, replacing the clear done by a triage run; or a card's needs-context line, in which case the line goes rather than stays. He must also say whether both cards move into kit-ui.beh under FEAT-AGENT-PIPELINE instead of a second ORCH feature (FEAT-ORCH) in kit.beh
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
Note 22:56Z asks for a component that monitors blocked items and untags them when a blocker changes. Blocked is a card with a needs-context line (BEH-BOARD-17, BEH-BOARD-18); BEH-ORCH-17 says the monitor clears the tag when the blocker it names has changed, deterministically, and says so on the card. Note 22:58Z asks that pipeline stages run as separate jobs that wake, check for changes, and sleep when there are none, with wake frequency tuned over time. BEH-ORCH-18 builds on BEH-ORCH-11 (one job per stage) and BEH-ORCH-6 (run log, where tuning data comes from). Only the wake rule is card text now; frequency tuning is left for later. Both notes are one run loop, so they are one work item. Nothing is built.
Test plan:
- BEH-ORCH-17: a Blocked card whose named blocker is unchanged since the last wake stays Blocked, is not written to, and makes no model call
- BEH-ORCH-17: a Blocked card whose blocker has changed (his reply, his answer, a merged step) loses the blocked tag, returns to the agent's work list, and gets a line on the card saying it was untagged and which blocker changed
- BEH-ORCH-17: untagging leaves the needs-context question text in place and changes no approval, denial or paused line
- BEH-ORCH-17: a Blocked card whose blocker is missing or unreadable (line names nothing, or the named reply or commit is not found) stays Blocked and the wake reports it without failing the stage
- BEH-ORCH-17: a second wake after an untag finds nothing to do, so the card is not untagged or annotated twice
- BEH-ORCH-17: a built card waiting on his secret or step (BEH-BOARD-23) is not untagged merely because its code was merged while his step is still undone
- BEH-ORCH-18: a stage wake with no new notes, no changed ticket and no untagged blocker records "no change" in the run list and makes no model call
- BEH-ORCH-18: a stage wake that finds a new note or changed ticket does that stage's work and records a run rather than "no change"
- BEH-ORCH-18: the change check is deterministic: the same corpus and git state give the same result on repeated checks, with no model call
- BEH-ORCH-18: a stage's no-change wake does not hold up another stage's job for the same project (each stage is its own job, BEH-ORCH-11)
- BEH-ORCH-18: the wake interval is read from a setting at each wake, so a changed setting applies at the next wake without a restart, and the default keeps the current schedule
- BEH-ORCH-18: a "no change" wake is not reported as a lost or dead run (BEH-ORCH-14), while a check that errors (for example git unavailable) is recorded as a failed run, not "no change"
