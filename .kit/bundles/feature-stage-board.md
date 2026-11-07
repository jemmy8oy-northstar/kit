# Feature stage board: what the pipeline means, and how it sits beside the behaviour board
Work-item: feature-stage-board
Flag: The gap check between BEH-STAGE-9 to BEH-STAGE-12 and the current Stages page was not done; the builder must check before deciding no new card is needed.
Review: revised — gap check done: no new card needed, but the page has an architecture column (BEH-ARCH-2) the STAGE cards never name, and BEH-STAGE-10's all-built-at-ui now shows as Done (BEH-STAGE-12).
Cards:
- BEH-STAGE-1
- BEH-STAGE-8
- BEH-STAGE-9
- BEH-STAGE-10
- BEH-STAGE-11
- BEH-STAGE-12
- BEH-ARCH-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 16:16Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 16:19Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/stages.ts
- prototypes/behaviour-ast/ui/src/pages/Stages.tsx
- prototypes/behaviour-ast/ui/src/components/BoardViews.tsx
Context:
The 16:16 note says the pipeline is run by the bot. It groups each feature into a rough completion stage, from the spec the dev has written and the implementation state, so the human can see what work is left. BEH-STAGE-9 to BEH-STAGE-12 already say this: the agent places each feature, and a feature with anything built sits at development. The 16:19 note says the feature board is a higher-level view than the behaviour board, like ADO, and the two should be switchable, not merged. BEH-STAGE-8 already specifies a one-tap switch between features and behaviours. The note also says a project board and an epic board are backlog ideas, not for now. The epic view is in the epics bundle (BEH-EPIC-4). Do not build a project board. No new card is needed unless the builder finds a gap in BEH-STAGE-9 to BEH-STAGE-12 against the current Stages page. Gap check (review stage, against stages.ts, Stages.tsx and BoardViews.tsx): BEH-STAGE-8 is met (the Epics, Features and Behaviours switch on both pages, and one Board link). BEH-STAGE-9 is met: the page only reads the stage line and nothing can be dragged. BEH-STAGE-10 and -11 are rules for how the agent writes the stage line, not for the page, so they have no gap against it. BEH-STAGE-12 is built (Done column, from the behaviour board's columnOf). No new card is needed. Two gaps in what the cards say:
1. The page has six stages, with Architecture between logic and development (BEH-ARCH-2, built 2026-11-01). BEH-STAGE-1's title and contract, BEH-STAGE-7's "one of problem, ux, logic, development, ui" and BEH-STAGE-9's column list still say five. Do not edit those approved cards to fix this yourself. Raise a review request on BEH-STAGE-1 naming BEH-ARCH-2 and proposing the six-stage wording.
2. BEH-STAGE-10 (approved) puts a fully built feature at ui, "so the board reads nothing left but polish". BEH-STAGE-12 (unreviewed) shows that feature in Done and leaves its ui stage line alone. They can both hold, because one is the written line and the other is the column it shows in, but the ui column no longer shows fully built features. Name this in BEH-STAGE-12's review request and do not pick a winner.
Test plan:
- BEH-STAGE-1: the Stages page shows the columns problem, ux, logic, architecture, development, ui, with Unstaged first and Done last; held until the review request on BEH-STAGE-1 is answered, since the card still says five
- BEH-STAGE-8: the Features and Behaviours switch works on both pages in one tap, and the header has one Board link for both; the Epics option is covered by BEH-EPIC-4
- BEH-STAGE-9: the Stages page only reads each feature's stage line; no card can be dragged and no page writes a stage
- BEH-STAGE-10: a feature with some behaviour built shows in development, and one with every behaviour built shows in ui
- BEH-STAGE-11: a feature with no behaviour built shows in the column its stage line names (problem, ux or logic); the page does not compute the column from the spec's content
- BEH-STAGE-12: a feature whose live behaviours are all in the behaviour board's Done column shows in Done, after ui; a paused, blocked, proposed or in-review behaviour keeps it in its stage column; a denied behaviour is ignored; a feature with no live behaviours is never Done; the written stage line is unchanged. The ui-column reading against BEH-STAGE-10 is held until the review request is answered
- BEH-ARCH-2: a feature whose stage line is architecture shows in the architecture column, between logic and development; no other feature moves
