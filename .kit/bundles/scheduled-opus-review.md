# Scheduled Opus review as a part of the agent pipeline
Work-item: scheduled-opus-review
Flag: this is a timer, an explicit exception to the no-timer rule in BEH-ORCH-3, so check it against the corpus conflict setting (level: high)
Review: revised — not a spec conflict: the owner's approved note on BEH-ORCH-13 names it an exception to BEH-ORCH-3, so no conflict setting applies; Notes paths and Context corrected
Cards:
- BEH-ORCH-13
- FEAT-AGENT-PIPELINE
- BEH-ORCH-3
- BEH-SETTINGS-5
- BEH-SETTINGS-6
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-07 01:32Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-15 12:51Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
The owner's note (11-07 01:32Z): add a scheduled Opus review to the agent pipeline, as its own part of FEAT-AGENT-PIPELINE, not a custom agent under BEH-AGENTS-1. It is an exception to the no-timer rule in BEH-ORCH-3. A cheap model still triages most tasks as the pipeline does now; the Opus review runs on a timer. The review checks neglected tasks, a sample of completed work, and the architecture, to keep things in check.
The owner's answer (11-15 12:51Z): the cadence setting goes on the Settings page as its own row, next to the pipeline, and can be set per project. The choices are daily, every two days, or weekly. This settles the open question between a new Settings row and BEH-SETTINGS-5 or BEH-SETTINGS-6: it is a new row, and it does not replace either of those.
Builder: the review is a timer, so record the exception to BEH-ORCH-3 as a contract, and do not fold it into BEH-AGENTS-1.
Spec stage: BEH-ORCH-13 (kit-ui.beh, priority 4, pending) already covers this card in full, with the owner's 11-07 and 11-15 notes as contracts, so no card is added and the "new:" line names it instead. BEH-ORCH-13 does not yet record the exception to BEH-ORCH-3 as a contract; the builder adds that to BEH-ORCH-13. Review: this is not a conflict between two specs for the "# kit:conflicts" setting to settle. The owner settled it himself: his approved note on BEH-ORCH-13 (11-07) names the timer an exception to BEH-ORCH-3, and BEH-ORCH-3's no-timer line is "my assumption", which already foresees "a periodic check by a stronger model". So build the timer, raise no conflict review request, and do not edit BEH-ORCH-3's existing contracts; the builder adds to BEH-ORCH-13 a contract quoting his note that the timer is the one exception to BEH-ORCH-3, and that only the review run starts on it. (Had it been a conflict, the line to read is in the file named for the project: kit-ui.beh declares "# kit:project kit", so that is kit.beh, not kit-ui.beh.) BEH-ORCH-13 also says the review has its own switch, off until he turns it on, because it spends his subscription: build it switched off; switching it on is his. The Notes paths point at a directory that is not in this repo; the same anchors are in prototypes/behaviour-ast/behaviours/kit-ui.notes.md (## 2026-11-07 01:32Z and ## 2026-11-15 12:51Z · BEH-ORCH-13). Open, not in the card: whether a review runs for a project whose pipeline is off (BEH-ORCH-15). The test plan assumes neither answer.

Test plan:
- BEH-ORCH-13: with the cadence off (the start state of every project), no review run starts however long the timer runs.
- BEH-ORCH-13: with the review's own switch unset, no review run starts on any project, whatever its cadence.
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

Built:
- BEH-ORCH-13: with the cadence off, no review run starts however long the timer runs (ReviewSweepTests.BEH_ORCH_13_With_the_cadence_off_no_review_run_starts_however_long_the_timer_runs)
- BEH-ORCH-13: with the review switch unset, no run starts on any project whatever its cadence (ReviewSweepTests.BEH_ORCH_13_With_the_review_switch_unset_no_run_starts_on_any_project_whatever_its_cadence)
- BEH-ORCH-13: daily, every-two-days and weekly each start one run per interval and never more often, over 30 days (ReviewSweepTests.BEH_ORCH_13_A_project_on_a_cadence..., 3 cases)
- BEH-ORCH-13: setting one project's cadence changes no other's, and a project never set starts off (ReviewSweepTests.BEH_ORCH_13_Setting_the_cadence_on_one_project_changes_no_other_and_a_new_project_starts_off)
- BEH-ORCH-13: the run uses the review model and asks for neglected tickets, a sample of finished work and the architecture (ReviewSweepTests.BEH_ORCH_13_The_review_run_uses_the_review_model_and_asks_for_neglected_tickets_a_sample_and_the_architecture)
- BEH-ORCH-13: the run pushes no kit/work/* branch, changes no code, and writes its findings as notes on the project (ReviewSweepTests.BEH_ORCH_13_The_review_run_pushes_no_work_branch_and_changes_no_code_and_writes_its_findings_as_notes)
- BEH-ORCH-13: a note the review writes is picked up by the next triage like any other (ReviewSweepTests.BEH_ORCH_13_A_note_the_review_writes_is_picked_up_by_the_next_triage_like_any_other)
- BEH-ORCH-13: the review is a pipeline stage on the pool, not a custom agent (ReviewSweepTests.BEH_ORCH_13_The_review_is_a_stage_of_the_pipeline_run_on_the_pool_and_not_a_custom_agent)
- BEH-ORCH-13: the settings route reads the switch and each project's cadence (ReviewSweepTests.BEH_ORCH_13_The_settings_page_reads_each_project_cadence_and_the_switch)
- BEH-ORCH-13: an unknown cadence is refused and changes nothing (ReviewSweepTests.BEH_ORCH_13_An_unknown_cadence_is_refused_and_changes_nothing)
- BEH-ORCH-13: Settings shows one cadence row per project, off unless set, with the four choices (Settings.test.tsx, Review by a stronger model)
- BEH-ORCH-13: Settings says when the review switch is off (Settings.test.tsx)
- BEH-ORCH-13: a cadence row posts the named cadence and says so (Settings.test.tsx)
- BEH-ORCH-13: a refused cadence shows the server reason (Settings.test.tsx)
- BEH-ORCH-13: the row is not shown to anyone but the operator (Settings.test.tsx)
- BEH-ORCH-3: the timer starts only the review run; no triage, spec or build starts with no note or action (ReviewSweepTests.BEH_ORCH_3_The_timer_starts_only_the_review_run_and_queues_no_triage_spec_or_build)
- BEH-SETTINGS-5: the idle choice and GET /api/settings/idle are unchanged by the new row (ReviewSweepTests.BEH_SETTINGS_5_The_idle_choice_and_its_endpoint_are_unchanged_by_the_review_row)
- BEH-SETTINGS-6: the ordered idle list keeps its items and switches and gains no review row (ReviewSweepTests.BEH_SETTINGS_6_The_ordered_idle_list_keeps_its_items_and_switches_and_gains_no_review_row)
Whole suite: backend 2,008 tests ran, 2,005 passed, 3 failed (ReportConformanceTests x2 and ClaimerSidecarTests x1, which fail the same on the base commit here: no ICU and no kill binary in this sandbox, so the run used invariant globalization); UI 566 tests in 49 files all passed; typecheck and lint clean. Not passed: the 3 backend failures above.
