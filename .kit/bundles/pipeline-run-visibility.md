# Run page: what each agent is doing, and what each historic run cost by stage
Work-item: pipeline-run-visibility
Flag: the run page shows how Kit shows his work, and its per-stage cost overlaps the open ticket record-session-cost (BEH-COST-1); the page may belong in kit-ui.beh (level: high)
Review: revised — the run page is a screen and kit.beh's header forbids browser behaviours there, so the cards move to kit-ui.beh; the cost overlap is a dependency on BEH-COST-1, not a duplicate
Cards:
- BEH-VIS-1
- BEH-VIS-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 10:50Z (page for agents running and pipeline stages; historic runs by stage cost; internal only)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
BEH-VIS-1 and BEH-VIS-2 were written into kit.beh, but kit.beh is `kit:layer technical` and its header says not to add browser behaviours there (its 0 generated steps is the finding). A page belongs in kit-ui.beh: the builder moves both cards there unchanged, still unreviewed and owner-only. They have no tests yet, so kit check fails BEH-GATE-1 until a builder maps them.
The owner says this is internal for now. Per-stage cost needs the session cost record, which is the open ticket record-session-cost; build it after that, or show unknown. VIS-2 only reads that record and records no cost itself, so it does not duplicate BEH-COST-1.
Next step toward the goal: record-session-cost lands, then a builder moves the cards to kit-ui.beh and adds the run page.
Built:
- BEH-VIS-1: BEH_VIS_1_a_running_run_lists_each_running_agent_with_its_current_stage (RunVisibilityTests.cs)
- BEH-VIS-1: BEH_VIS_1_a_review_an_agent_flagged_shows_on_its_stage_with_the_agent_that_flagged_it (RunVisibilityTests.cs)
- BEH-VIS-1: BEH_VIS_1_a_stage_with_no_flagged_review_shows_no_review_line_and_a_run_with_no_running_agent_shows_none (RunVisibilityTests.cs)
- BEH-VIS-1: BEH_VIS_1_the_run_page_is_served_to_the_owner_alone (AgentRunTests.cs)
- BEH-VIS-2: BEH_VIS_2_a_finished_run_shows_each_stage_with_the_cost_recorded_for_it (RunVisibilityTests.cs)
- BEH-VIS-2: BEH_VIS_2_a_stage_with_no_recorded_cost_shows_as_unknown_never_as_zero (RunVisibilityTests.cs)
- BEH-VIS-2: BEH_VIS_2_a_finished_run_lists_the_reviews_it_requested_with_the_stage_and_agent (RunVisibilityTests.cs)
- BEH-VIS-2: BEH_VIS_2_with_no_session_cost_record_every_stage_cost_is_unknown_and_the_page_still_renders (AgentRunTests.cs)
- BEH-VIS-2: BEH_VIS_2_the_page_only_reads_the_cost_record_and_writes_none (RunVisibilityTests.cs)
- Whole suite: 2023 tests ran; 2020 passed and 3 failed (ClaimerSidecarTests.A_claim_never_reported_is_reported_failed_when_the_pod_is_stopped, ReportConformanceTests.Globalization_is_ICU_so_locale_order_is_reachable, ReportConformanceTests.Report_reproduces_Node_on_the_branches_the_goldens_miss). Those 3 fail the same way on the untouched branch here (no ICU, no node/kill on PATH, DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1), so it is not a clean pass.
- Not built: the React run page (ui/); the route GET /api/runs/visibility serves its data only. Overlap for the reviewer: BEH-ORCH-9 (kit-ui.beh, built slice 1, Runs Pipeline section) already shows flags, reviewers and per-stage run cost for past items, and this route repeats it. Not resolved here.

Test plan:
- BEH-VIS-1: a run that is Running lists each agent that is running, with its current stage
- BEH-VIS-1: a review an agent flagged shows on its stage, with the name of the agent that flagged it
- BEH-VIS-1: a stage with no flagged review shows no review line, and a run with no running agent shows no running agents
- BEH-VIS-1: the page is served to the owner only; a tester's session gets no run page
- BEH-VIS-2: a Finished run shows each stage with the cost recorded for it by BEH-COST-1
- BEH-VIS-2: a stage with no recorded cost shows as unknown, never as zero
- BEH-VIS-2: a Finished run lists the reviews it requested, each with the stage and the agent that requested it
- BEH-VIS-2: with no session cost record at all (BEH-COST-1 not yet landed), every stage cost shows as unknown and the page still renders
- BEH-VIS-2: the page only reads the cost record and writes none (no cost value is stored by the page)
