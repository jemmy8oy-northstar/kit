# Questions show on the board, and the Questions tab leads with what Kit asks
Work-item: questions-board-and-sheet
Flag: No existing card says Kit sets out the choices for a decision it cannot make, so the 22:48Z ask may need a new card; check BEH-SHEET-1 and BEH-SHEET-2 first.
Cards:
- BEH-BOARD-11
- BEH-SHEET-2
- BEH-SHEET-1
- BEH-SPECAGENT-3
- BEH-SHEET-3
- new: when Kit cannot decide between readings, it raises a question that sets out each choice and what it would mean
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 17:25Z (line 137)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 22:46Z (line 211)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 22:48Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 23:47Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/asks.ts
- prototypes/behaviour-ast/ui/src/components/DecisionsList.tsx
- prototypes/behaviour-ast/ui/src/pages/Board.tsx
- prototypes/behaviour-ast/ui/src/pages/Project.tsx
- backend/Balenthiran.Kit.Services/QuestionSheet.cs
- backend/Balenthiran.Kit.Abstractions/Services/IQuestionSheet.cs
Context:
BEH-BOARD-11 says the agent's open questions show on the board, which is the 17:25 note's ask. BEH-SHEET-2 says the Questions tab leads with what Kit is asking James and calls the rest problems. The 22:46 note says the tab shows problems and not questions. So this is a card that is specced but not met. Check the order in the tab and what asks.ts counts as a question.
BEH-SPECAGENT-3 routes technical questions to the open-questions board, which is related.
The 2026-10-22 22:48Z note asks for Kit to raise guiding questions, because James cannot tell what UX, UI or logic to define to unblock a behaviour or feature. The new card above covers that, and it should say what each choice would mean, so James can answer it in a line. BEH-SHEET-1 and BEH-SHEET-2 describe the sheet Kit has built, not questions Kit raises when it is stuck, so check them before adding the card.
The 2026-10-22 23:47Z note says Kit is asking and there is no way to reply. BEH-SHEET-3 lets James answer under a question. Check whether a reply to a board question goes to the same place as an answer on the sheet, and whether the agent reads it.
