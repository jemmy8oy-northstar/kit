# Each pipeline stage runs on its own schedule
Work-item: pipeline-stages-own-schedule
Flag: no existing card covers per-stage scheduling; the pipeline (BEH-ORCH) cards are not in kit.beh, so BEH-SCHED-1 is a new card in a new area FEAT-SCHEDULE and may belong in another spec file. (level: low)
Review: revised — kit-ui.schedule is only kit-ui's build-order queue (BEH-USERS ids), not a pipeline schedule, so dropped it from Files; BEH-SCHED-1 already sits in kit.beh (FEAT-SCHEDULE) and stays there, whether BEH-ORCH specs live in another file is left for a person to say.
Cards:
- BEH-SCHED-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 00:13Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
- The note asks that each pipeline stage (triage, build, review and so on) run on its own schedule instead of one shared wake.
- Existing cards in kit.beh do not cover scheduling. kit-ui.schedule is only kit-ui's build-order queue (BEH-USERS ids); it holds no pipeline schedule and is not relevant. BEH-SCHED-1 is defined in kit.beh under FEAT-SCHEDULE.
- BEH-SCHED-1 is unreviewed; its two contract lines are assumptions (independent wake per stage; an idle stage does not block others).
