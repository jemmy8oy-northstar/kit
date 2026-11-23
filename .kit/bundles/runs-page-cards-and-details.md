# Runs page as cards, with a run details page
Work-item: runs-page-cards-and-details
Flag: kit.beh's header forbids browser behaviours, and the runs page is a screen; pipeline-run-visibility moved its run-page cards to kit-ui.beh for that reason, so the builder may need to move BEH-UI-8 and BEH-UI-9 there. I edited kit.beh as the triage brief asks. The owner has not said what the details page shows, so its layout is left to the builder (level: high)
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
