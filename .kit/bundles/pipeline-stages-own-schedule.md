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
- Correction (spec stage, 2026-11-18): BEH-SCHED-1 is NOT free. kit-ui.beh:116 already defines BEH-SCHED-1 "A project lists its scheduled behaviours in order, apart from its backlog" (kit#118, FEAT-SCHEDULE, tested in kit-ui.tests.json, served by GET /api/projects/<app>/schedule over <app>.schedule). kit.beh:370 reuses the same id for a pipeline stage schedule, so the project has two BEH-SCHED-1 meanings. FEAT-SCHEDULE is declared only in kit-ui.beh:94, not kit.beh. The earlier line saying kit-ui.schedule is irrelevant is wrong: that file is the build-order queue the existing BEH-SCHED-1..3 read.
- Not decided here: whether the pipeline card takes a new id (e.g. a BEH-ORCH- or BEH-PIPE- id) or the id is kept. Renaming a card is the owner's call; the card is left as written.

Test plan:
- BEH-SCHED-1: when the triage stage's own wake fires, triage runs; the build stage's wake has not fired, so build does not run (a stage runs when its own schedule fires, not when another stage runs).
- BEH-SCHED-1: while the build stage is idle or a build run is still in progress, triage's wake still fires and triage runs on time (an idle or slow stage does not delay triage).
