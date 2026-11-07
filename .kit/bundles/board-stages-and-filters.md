# Board: layer filter, unstaged column and stage questions
Work-item: board-stages-and-filters
Cards:
- BEH-KIND-1
- BEH-STAGE-7
- BEH-STAGE-9
- BEH-STAGE-11
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-16 11:45Z · BEH-KIND-1
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-20 07:13Z · BEH-STAGE-7
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-27 21:16Z · BEH-STAGE-11
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-27 21:16Z · BEH-STAGE-9
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-28 23:53Z · BEH-STAGE-11
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
- prototypes/behaviour-ast/ui/src/pages/Board.test.tsx
- prototypes/behaviour-ast/ui/src/pages/Stages.test.tsx
- prototypes/behaviour-ast/ui/src/test/stageReading.test.ts
Context:
All four cards already exist in kit-ui.beh and each has a test line in kit-ui.tests.json, so most of this is answers and approval, not new build.
- 2026-10-16 11:45Z approves the layer filter on BEH-KIND-1. Record the approval on the card. Do not rebuild it.
- 2026-10-20 asks for an unstated column so features with no stage can be found and staged. BEH-STAGE-7 is that column ("Unstaged"). Confirm it is built and say so.
- 2026-10-27 21:16Z has two "What does this mean?" notes, one tagged BEH-STAGE-9 and one BEH-STAGE-11. Answer each on its card in plain words. Stage board placement is set by the stage a feature is at (problem, UX, logic, development, UI). The agent decides it, and the owner only reads the board. Do not change the placement rules.
- 2026-10-28 23:53Z asks whether placement is by how much is written. The owner's answer is that it is by stage, not by how much is written. Reword BEH-STAGE-11 if its wording makes this unclear.
