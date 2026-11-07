# Board: layer filter, unstaged column and stage questions
Work-item: board-stages-and-filters
Flag: the owner says placement is by stage, not by how much is written, but the rule for features with no code still places them by their writing (one-line idea at problem, step-by-step at ux, written rules at logic); kept as the bundle says, so the owner should confirm that rule.
Review: agreed — the no-code rule fits the owner's own words (BEH-STAGE-9 quotes "the spec that the dev has filled in and implementation state"; the 2026-10-28 note says "how much of ux beh ui impl"), and it is already on BEH-STAGE-11 as an unreviewed assumption the owner can deny, so no decision blocks the work.
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

Built:
The whole UI suite ran 540 tests (47 files) and all passed. I wrote no new tests: the nine named tests already existed and pass. The only change is the reworded first contract on BEH-STAGE-11 in kit-ui.beh, which states the same rule (stage by the agent's reading; writing is only the evidence for a feature with no code).

Test plan:
- BEH-KIND-1: Board.test.tsx "shows each card its layer and filters by layer, in the url beside the project filter" checks each card shows its layer, the board filters by the chosen layer, and the choice is kept in the url beside the project filter.
- BEH-STAGE-7: Stages.test.tsx "puts the Unstaged column first, holding the features with no stage line and none that has one" checks Unstaged is the first column and holds exactly the features with no stage line.
- BEH-STAGE-7: Stages.test.tsx "counts a behaviour under two staged features once in each, and a stage-less one gets a card too" checks a behaviour under two features is counted once in each, and a feature with no stage still gets a card.
- BEH-STAGE-9: stageReading.test.ts "gives every feature in a real project a stage line" checks every feature in the real corpora has a stage line.
- BEH-STAGE-9: stageReading.test.ts "writes only the stage words the board has columns for" checks every stage word written is one the board has a column for.
- BEH-STAGE-11: stageReading.test.ts "puts a feature with nothing built at problem, ux or logic" checks a feature with nothing built is placed at problem, ux or logic. It does not check that the placement is right.
