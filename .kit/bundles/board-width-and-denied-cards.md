# Board: columns use the width of a wide screen, and a denied card can be found
Work-item: board-width-and-denied-cards
Flag: The 2026-10-25 13:15Z note reads as both a bug report and a question; if the Denied column exists, the fault is findability, not deletion, so confirm which before changing data.
Cards:
- BEH-BOARD-19
- BEH-BOARD-21
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 12:33Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 13:15Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/pages/Board.tsx
- prototypes/behaviour-ast/ui/src/components/BoardViews.tsx
- prototypes/behaviour-ast/ui/src/board.ts
Context:
Width (12:33Z): on a desktop the columns are cramped. BEH-BOARD-19 says a wide-screen board uses the window's width, and no column is narrower than a card can be read. The card is specced and not met.
Denied card (13:15Z): James denied a card, believing it needed refunding, and cannot find it now. BEH-BOARD-21 says a denied card can still be found on the board, under Denied. Check whether that column exists, and whether a denied card is dropped from the data or only from the view. Denial reasons (BEH-INBOX-2) are not part of this note; do not change them here.
