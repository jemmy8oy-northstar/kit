# Board ordering follows the agent's score, not the project
Work-item: board-ordering-and-scoring
Cards:
- BEH-BOARD-10
- BEH-BOARD-5
- BEH-BOARD-6
- BEH-BOARD-20
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 13:40Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/board.ts
- prototypes/behaviour-ast/ui/src/boardLoad.ts
- prototypes/behaviour-ast/ui/src/pages/Board.tsx
Context:
The note says the Proposed column looks ordered by project, not by how much the agent wants each item reviewed. BEH-BOARD-10 says the agent scores every card it wants looked at, and equal scores take each project in turn. So when scores are equal or missing, the project interleave is what the user sees. BEH-BOARD-5 and BEH-BOARD-6 already say the top priority goes first, and Review requested comes before Proposed. BEH-BOARD-20 says every card carries a score, so unscored cards are a gap to close. Check whether proposed cards actually get a score before changing the sort. I did not read board.ts or Board.tsx, so the cause is unconfirmed. The note has no new behaviour beyond the existing cards.
