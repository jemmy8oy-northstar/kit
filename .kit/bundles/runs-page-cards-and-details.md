# Runs page as cards, with a run details page
Work-item: runs-page-cards-and-details
Flag: kit.beh's header forbids browser behaviours, and the runs page is a screen; pipeline-run-visibility moved its run-page cards to kit-ui.beh for that reason, so the builder may need to move BEH-UI-8 and BEH-UI-9 there. I edited kit.beh as the triage brief asks. The owner has not said what the details page shows, so its layout is left to the builder (level: high)
Review: revised — the runs page is a screen, so as with pipeline-run-visibility the builder moves BEH-UI-7, BEH-UI-8 and BEH-UI-9 unchanged from kit.beh to kit-ui.beh beside BEH-VIS-1 and BEH-VIS-2; the open details layout is already an unreviewed contract, so no question for the owner
Cards:
- BEH-UI-7
- BEH-UI-8
- BEH-UI-9
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-23 23:00Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- .kit/bundles/pipeline-run-visibility.md
Context:
The owner says the runs page is a tremendous amount of text and wants it usable: recent runs as cards, each with a state tag and total cost, and a click through to a details page that the builder designs. BEH-UI-7 (newest first) still holds and is kept; BEH-UI-8 is the card list, BEH-UI-9 the details page. No open ticket covers the runs page. The page already exists from pipeline-run-visibility (BEH-VIS-1 and BEH-VIS-2, cards in kit-ui.beh, possibly built), so extend that page rather than adding a second one. Run cost depends on the session cost record (BEH-COST-1, open); until it lands, show unknown, never zero. Do not edit BEH-VIS-1 or BEH-VIS-2.
Where the cards go: kit.beh is `kit:layer technical` and its header says no screen goes there. The builder moves BEH-UI-7, BEH-UI-8 and BEH-UI-9 to kit-ui.beh next to BEH-VIS-1 and BEH-VIS-2, word for word and still unreviewed. BEH-UI-7's own contract already says it sits in the server area only because the runs page spec was not in kit.beh. That spec is in kit-ui.beh, so moving it settles that contract too.
Built so far: BEH-VIS-1 and BEH-VIS-2 are built as the route GET /api/runs/visibility, but there is no React page in ui/ yet. The cards and the details page are the first screen on that route. The details page is where VIS-1's stages and agents and VIS-2's stage costs and reviews go. BEH-ORCH-9 (kit-ui.beh, Runs Pipeline section) already shows per-stage run cost. Reuse it, do not repeat it.
Next step toward the goal: a builder moves the three cards to kit-ui.beh and builds the run cards and details page on /api/runs/visibility.
Built:
- BEH-UI-7: BEH_UI_7_the_recent_runs_are_newest_start_first_whatever_order_they_were_given_in (RunVisibilityTests.cs)
- BEH-UI-7: BEH_UI_7_the_runs_page_reads_its_cards_from_the_visibility_route_newest_start_first (AgentRunTests.cs)
- BEH-UI-7: BEH-UI-7 shows each recent run as a card read from GET /api/runs/visibility, not as a text row (Runs.test.tsx)
- BEH-UI-8: BEH_UI_8_each_recent_run_carries_the_state_the_run_record_holds (RunVisibilityTests.cs)
- BEH-UI-8: BEH_UI_8_the_visibility_route_gives_each_recent_run_its_tokens_its_failure_and_unknown_cost (AgentRunTests.cs)
- BEH-UI-8: BEH_UI_8_with_no_cost_record_every_recent_run_costs_unknown (RunVisibilityTests.cs)
- BEH-UI-8: BEH_UI_8_a_run_with_no_recorded_cost_is_unknown_and_a_run_recorded_at_zero_is_zero (RunVisibilityTests.cs)
- BEH-UI-8: BEH-UI-8 shows each run state as a tag matching the state the run record holds (Runs.test.tsx)
- BEH-UI-8: BEH-UI-8 shows a run with no recorded cost as unknown, never as zero (Runs.test.tsx)
- BEH-UI-8: BEH-UI-8 shows a run recorded at cost 0.00 differently from a run with no recorded cost (Runs.test.tsx)
- BEH-UI-9: BEH-UI-9 opens the details page for the run whose card was clicked, and no other run (Runs.test.tsx)
- BEH-UI-9: BEH-UI-9 shows the stages and their recorded costs, with unknown for a stage with no cost, and the reviews it requested (BEH-VIS-2) (RunDetails.test.tsx)
- BEH-UI-9: BEH-UI-9 shows the agents running on the run work item and any review each flagged (BEH-VIS-1) (RunDetails.test.tsx)
- BEH-UI-9: BEH-UI-9 does not repeat the cost by stage that BEH-ORCH-9 shows on the runs page (RunDetails.test.tsx)
- BEH-UI-9: BEH-UI-9 says run not found for a run id that no longer exists, not an empty page (RunDetails.test.tsx)
- BEH-UI-9: BEH-UI-9 reads the run from GET /api/runs/visibility, the route the runs page cards read (RunDetails.test.tsx)
- BEH-UI-7, BEH-UI-8, BEH-UI-9: BEH_UI_9_the_run_cards_and_details_are_declared_in_kit_ui_beh_beside_the_run_page (CommittedArtefactTests.cs)
- Whole suite: C# `dotnet test backend/Balenthiran.Kit.Tests` ran 2150 tests, 2150 passed; UI `npx vitest run` ran 633 tests, 628 passed and 5 todo, 0 failed; `npx tsc -b` clean.

Test plan:
- BEH-UI-7: the runs page lists recent runs by start time, newest at the top, and paging to older runs does not reverse that order
- BEH-UI-7: the runs page shows each run as a card, not a text row, and the list is read from GET /api/runs/visibility, the route BEH-VIS-1 and BEH-VIS-2 already serve
- BEH-UI-8: each card shows the run's state as a tag (running, passed, failed or blocked) matching the state the run record holds
- BEH-UI-8: each card shows the run's total cost; while BEH-COST-1 has no record for a run, the card shows unknown, never zero
- BEH-UI-8: a run with no recorded cost and a run with cost 0.00 recorded are shown differently, so unknown is never rendered as zero
- BEH-UI-9: clicking a run card opens that run's details page for that run only, and the URL or route carries the run's id
- BEH-UI-9: the details page shows the stages and agents from BEH-VIS-1 and the stage costs and reviews from BEH-VIS-2, and does not repeat the per-stage cost already shown by BEH-ORCH-9
- BEH-UI-9: a run id that no longer exists shows a clear "run not found" message, not an empty page
- BEH-UI-7, BEH-UI-8, BEH-UI-9: the card and details text sits in kit-ui.beh beside BEH-VIS-1 and BEH-VIS-2 with its contracts unchanged, and BEH-VIS-1 and BEH-VIS-2 are not edited in the PR
