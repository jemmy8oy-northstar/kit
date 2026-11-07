# Coming back from a card returns to its column
Work-item: board-return-to-card-column
Cards:
- BEH-BOARD-15
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 11:30Z (line 187)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/pages/Board.tsx
- prototypes/behaviour-ast/ui/src/boardLoad.ts
Context:
BEH-BOARD-15 already says that coming back from a card lands on that card's column. The note reports that it does not, and asks whether approve or deny does a full page redirect rather than a client route change, which would also lose scroll and focus. The spec does not answer the redirect question. Check the UI router before building.
A remote branch origin/board-15-return-to-card already exists, outside kit/work/. Look at it before starting so the work is not repeated.
