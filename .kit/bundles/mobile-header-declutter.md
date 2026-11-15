# Mobile header: one tidy line
Work-item: mobile-header-declutter
Flag: The card reads as built in the stylesheet, so there may be nothing to change, and the phone screenshot predates the fix; the owner should say whether to retake it or build something more. The card is in kit-ui.beh, not kit.beh as the brief said. (level: high)
Review: escalated — confirmed: BEH-NAV-2 carries a built contract (119px to 93px) and a checked-against-index.css contract, and the phone screenshot still shows the tagline and links beside the brand, so it predates the fix; owner to decide whether this item closes as built with just a retaken phone screenshot, or whether the header still looks cluttered on their phone today and needs a new card saying what more to change.
Cards:
- BEH-NAV-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 12:32Z (header on mobile is cluttered)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- docs/screenshots/ui/title-board-phone.png
Context:
- BEH-NAV-2 already says that on a phone the header is one line, with the links in a row that scrolls. The note reports it is cluttered, so the card is either unbuilt or regressed. Check the current header against the card, using the phone screenshot as a reference, before changing anything.
- BEH-NAV-1 says every page carries the navbar; keep it on every page while fixing the phone layout.
- No new card: the owner asked for a look at the header, and the card already states the target.
Test plan:
- BEH-NAV-2 layout.test.ts (existing): on a phone the header wraps, the tagline is hidden, and the nav is a full-width nowrap line that scrolls sideways.
- BEH-NAV-2 (new, browser): at 390px wide the header is no taller than 93px and the brand is alone on the first line.
- BEH-NAV-1 Board.test.tsx (existing): the navbar links the board and the projects and marks the current view.
- BEH-NAV-1 (new, browser): at 390px wide the navbar region renders on the projects, board, and settings pages.
