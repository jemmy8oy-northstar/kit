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
- Conflict (build, no kit:conflicts line): the test plan's slow-build clause conflicts with one live run per project (kit-ui.beh BEH-ORCH-3 and BEH-ORCH-14; RunnerPool). Built the one-run-per-project reading, so a triage waits while a build on its project runs. Only the idle half of the second test plan item is tested. Raised as a review request on BEH-SCHED-1 in kit.beh.
- The first test plan item passes on existing code once the build job switch is on (KIT_PIPELINE_JOB_BUILD, BEH-ORCH-11); with it off, production still chains triage to spec and build, which is the owner's call (it spends his subscription).
- kit.beh gained `feature FEAT-SCHEDULE`, declared the same way as in kit-ui.beh. Without it the committed-corpus test failed on this branch.
Built:
- BEH-SCHED-1: PipelineTests.BEH_SCHED_1_a_build_that_has_not_woken_does_not_run_while_triage_runs_on_its_own_wake
- BEH-SCHED-1: PipelineTests.BEH_SCHED_1_an_idle_build_does_not_hold_back_triage_when_triage_wakes
- the whole suite (dotnet test, backend/Balenthiran.Kit.Tests) ran 2019 tests: 2016 passed and 3 failed, all for environment reasons (no kill binary; no ICU, so invariant globalization), so it did not pass in full.

Test plan:
- BEH-SCHED-1: when the triage stage's own wake fires, triage runs; the build stage's wake has not fired, so build does not run (a stage runs when its own schedule fires, not when another stage runs).
- BEH-SCHED-1: while the build stage is idle or a build run is still in progress, triage's wake still fires and triage runs on time (an idle or slow stage does not delay triage).
