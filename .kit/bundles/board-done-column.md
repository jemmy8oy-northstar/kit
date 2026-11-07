# Feature board Done column
Work-item: board-done-column
Flag: Likely already built. BEH-STAGE-12 records "built: the Done column after UI requirements", and the note may just restate it. The note's column meaning (spec still to provide, development, done) may not match the five stages in BEH-STAGE-1. A stronger model should confirm before any change.
Cards:
- BEH-STAGE-12 "A feature whose behaviours are all built sits in a Done column at the end of the feature board"
- BEH-STAGE-1 "A separate board takes every feature through five stages: problem, UX, logic, development, UI"
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 16:47Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-STAGE-1 at line 955, BEH-STAGE-12 at line 1072)
- prototypes/behaviour-ast/ui/src/pages/Stages.tsx
- prototypes/behaviour-ast/ui/src/pages/Stages.test.tsx
- prototypes/behaviour-ast/ui/src/stages.ts
- prototypes/behaviour-ast/ui/src/test/stageReading.test.ts
Context:
The note asks for a Done column on the feature board, with the other columns meaning what spec still needs providing, in development, and done. BEH-STAGE-12 already puts a feature in a Done column once all its behaviours are built, and its contract records it built, derived in the page from the behaviour board's columns. A feature is never given a sixth stage word, because production Kit's parser throws on an unknown stage.
Builder: confirm the Done column renders on main. If it does, the note is satisfied; say so in the PR and change nothing. If the note's column meaning differs from the five stages, raise that as a question on BEH-STAGE-12 rather than editing the card. Next step toward the goal: confirm the feature board on main, then close any gap.
