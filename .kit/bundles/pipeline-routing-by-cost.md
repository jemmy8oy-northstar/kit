# Pipeline skips triage for backlog items and sends reviews to the model their flag level asks for
Work-item: pipeline-routing-by-cost
Flag: notes of 2026-11-08 and 2026-11-14 add the triage-stays-on-the-hook and priority-order contracts to BEH-ROUTE-1 and -2; the stage-merge and hosting questions of the 11-08 note are already answered by BEH-ORCH-11 in kit-ui.beh on main (level: low)
Review: agreed — cards match the 11-08/11-14 notes and kit.beh (ROUTE-1/2 carry the contract lines, ROUTE-3 defaults medium to sonnet); BEH-ORCH-11 in kit-ui.beh already covers the stage-merge and hosting points; "review unreviewed" is used on other cards in kit.beh
Cards:
- BEH-ROUTE-1
- BEH-ROUTE-2
- BEH-ROUTE-3
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 01:50Z (quickly and cheaply identify when something is just for the backlog)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 01:51Z (opus agent identifies backlog items to schedule; pipeline wakes for them; backlog items skip triage)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 02:10Z (haiku uncertainty flag picks opus or sonnet review by level; should cut costs)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-08 22:06Z (triage stays on the hook; later stages act by priority, not by a note; stages kept separate until evaluated; C# backend in the cluster)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-14 13:44Z ("Please continue with the pipeline enhancements!"; nudge only)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
New cards in kit.beh only, because this item may edit only that spec. The cards have no tests yet, so kit check kit fails BEH-GATE-1 until a builder maps them.
Each card carries the line "review unreviewed" as the triage stage was told. Kit's parser refuses an unknown keyword (BEH-PARSE-1), so confirm the line parses before the cards are built.
Assumed: flag level low goes to sonnet review and level high to opus review. The note does not say what medium should do.
2026-11-16 update: BEH-ROUTE-1 and BEH-ROUTE-2 each gained a contract line from the 2026-11-08 22:06Z note: triage alone waits on a note, and the stages after it take work by priority, so existing tickets are actioned too.
Not added: the rest of the 11-08 note (stages kept separate and merges decided from what Runs shows; the jobs live in Kit's C# backend, triage on the hook) is already in BEH-ORCH-11 in kit-ui.beh on main, so nothing was repeated here. Those cards are not in this branch's kit.beh, so a builder should read kit-ui.beh before mapping BEH-ROUTE-1 to tests, to avoid two cards for one rule.
The 2026-11-14 13:44Z note asks to continue the pipeline work. It names no card; the next unbuilt slice is the one BEH-ORCH-11 leaves open.
Next step toward the goal: a builder maps BEH-ROUTE-1 to 3 to tests and reads the flag level in the reviewer's pick.
Built:
- BEH_ROUTE_3_a_flag_at_level_low_reports_model_by_level_on_sonnet_and_never_opus (OrchestratorTests.cs): low goes to sonnet, never opus (already true before; now pinned)
- BEH_ROUTE_3_a_flag_at_level_high_reports_opus (OrchestratorTests.cs): high goes to opus (already true before; now pinned)
- BEH_ROUTE_3_a_flag_at_level_medium_reports_sonnet (OrchestratorTests.cs): medium goes to sonnet; this FAILED before the change (medium was read as high and went to opus). Code: FlagLevel gains Medium, Orchestrator.ModelFor sends low and medium to the review-low model. Medium's model is the assumed default, not yet confirmed by the owner.
- BEH_ROUTE_2_a_card_marked_ready_to_schedule_in_the_corpus_wakes_the_pipeline_at_spec (PipelineTests.cs): pins the BEH-ORCH-4 wake from the .schedule file; passed before any change, no code added
- BEH_ROUTE_2_a_ready_to_schedule_said_only_in_a_note_does_not_wake_the_pipeline (PipelineTests.cs): pins that only the corpus schedule wakes the pipeline; passed before any change, no code added
Not built (no test written, no code):
- BEH-ROUTE-1, all three tests: the cheap check that skips triage for a backlog item. Blocked: the cards do not say what makes a note's item a backlog item (a card already pending and unscheduled, a ticket already in the backlog state, or a note whose triage would put it on the backlog). Question for the owner: which one? Default I act on: none. Triage runs as it does today, and the backlog outcome is still reached after a triage model call.
- BEH-ROUTE-2, the priority test (stages after triage take the highest-priority waiting ticket): not written. Pipeline.cs orders by priority already (about lines 659 and 871), but I did not write a test that shows it, so it is not claimed.
Suite: `dotnet test Balenthiran.Kit.Tests` ran 2019 tests: 2016 passed, 3 failed. The three also fail on the unchanged tree in this sandbox (ClaimerSidecarTests needs a `kill` binary that is absent; ReportConformanceTests x2 fail because I had to set DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1, since libicu is absent here). So the suite does not pass in this sandbox; the three failures are environmental, not from this change.
Test plan:
- BEH-ROUTE-1: KitTriage on a backlog item reports triage:Skipped, and the check makes no model call (it reads only the item's card and backlog state)
- BEH-ROUTE-1: KitTriage on an item not yet in the backlog runs triage in full and does not report triage:Skipped
- BEH-ROUTE-1: a note on a backlog item still needs a note or chat message to reach triage; the skip check never starts triage on its own
- BEH-ROUTE-2: KitPipeline on an item marked ReadyToSchedule in the corpus reports wake:Scheduled
- BEH-ROUTE-2: the wake reads the corpus mark, so a ReadyToSchedule said only in chat does not wake the pipeline
- BEH-ROUTE-2: once woken, the stages after triage take the highest-priority waiting ticket, including existing tickets, not the most recently hooked note
- BEH-ROUTE-3: KitReview on a flag at level low reports model:ByLevel with sonnet, and never opus
- BEH-ROUTE-3: KitReview on a flag at level high reports opus
- BEH-ROUTE-3: KitReview on a flag at level medium reports sonnet (the assumed default, to be confirmed by the owner)
