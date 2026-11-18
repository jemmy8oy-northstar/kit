# Each pipeline stage runs on its own schedule
Work-item: pipeline-stages-own-schedule
Flag: no existing card covers per-stage scheduling; the pipeline (BEH-ORCH) cards are not in kit.beh, so BEH-SCHED-1 is a new card in a new area FEAT-SCHEDULE and may belong in another spec file. (level: low)
Cards:
- BEH-SCHED-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 00:13Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.schedule
Context:
- The note asks that each pipeline stage (triage, build, review and so on) run on its own schedule instead of one shared wake.
- Existing cards in kit.beh do not cover scheduling. kit-ui.schedule exists and may hold the current schedule; read it before choosing where the card belongs.
- BEH-SCHED-1 is unreviewed; its two contract lines are assumptions (independent wake per stage; an idle stage does not block others).
