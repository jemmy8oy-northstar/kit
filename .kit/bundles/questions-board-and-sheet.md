# Questions show on the board, and the Questions tab leads with what Kit asks
Work-item: questions-board-and-sheet
Cards:
- BEH-BOARD-11
- BEH-SHEET-2
- BEH-SHEET-1
- BEH-SPECAGENT-3
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 17:25Z (line 137)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 22:46Z (line 211)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/asks.ts
- prototypes/behaviour-ast/ui/src/components/DecisionsList.tsx
- prototypes/behaviour-ast/ui/src/pages/Board.tsx
- prototypes/behaviour-ast/ui/src/pages/Project.tsx
Context:
BEH-BOARD-11 says the agent's open questions show on the board, which is the 17:25 note's ask. BEH-SHEET-2 says the Questions tab leads with what Kit is asking him and calls the rest problems. The 22:46 note says the tab shows problems and not questions. So this is a card that is specced but not met. Check the order in the tab and what asks.ts counts as a question.
BEH-SPECAGENT-3 routes technical questions to the open-questions board, which is related. The 2026-10-22 16:39Z note in the epics item also asks for questions when more context is needed. The later notes of 22:48 and 23:47 (not in this batch) ask Kit to raise guiding questions and to take a reply; fold them into this item when they are triaged.
