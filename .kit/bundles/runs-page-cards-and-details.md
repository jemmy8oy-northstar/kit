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
