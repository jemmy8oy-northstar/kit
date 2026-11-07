# Feature stage board: what the pipeline means, and how it sits beside the behaviour board
Work-item: feature-stage-board
Flag: The gap check between BEH-STAGE-9 to BEH-STAGE-12 and the current Stages page was not done; the builder must check before deciding no new card is needed.
Cards:
- BEH-STAGE-1
- BEH-STAGE-8
- BEH-STAGE-9
- BEH-STAGE-10
- BEH-STAGE-11
- BEH-STAGE-12
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 16:16Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 16:19Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/stages.ts
- prototypes/behaviour-ast/ui/src/pages/Stages.tsx
- prototypes/behaviour-ast/ui/src/components/BoardViews.tsx
Context:
The 16:16 note says the pipeline is run by the bot. It groups each feature into a rough completion stage, from the spec the dev has written and the implementation state, so the human can see what work is left. BEH-STAGE-9 to BEH-STAGE-12 already say this: the agent places each feature, and a feature with anything built sits at development. The 16:19 note says the feature board is a higher-level view than the behaviour board, like ADO, and the two should be switchable, not merged. BEH-STAGE-8 already specifies a one-tap switch between features and behaviours. The note also says a project board and an epic board are backlog ideas, not for now. The epic view is in the epics bundle (BEH-EPIC-4). Do not build a project board. No new card is needed unless the builder finds a gap in BEH-STAGE-9 to BEH-STAGE-12 against the current Stages page. That was not checked.
