# Board ordering follows the agent's score, not the project
Work-item: board-ordering-and-scoring
Flag: The 2026-10-25 12:34Z note says many cards are unscored, so BEH-BOARD-20 is unmet; confirm where scores are written before changing the sort.
Cards:
- BEH-BOARD-10
- BEH-BOARD-5
- BEH-BOARD-6
- BEH-BOARD-20
- BEH-BOARD-12
- BEH-BOARD-13
- BEH-BOARD-14
- BEH-BELL-3
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 13:40Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-21 00:08Z (line 159)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-21 06:50Z (line 167)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-21 07:10Z (line 171)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-21 15:47Z (line 175)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 12:34Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/board.ts
- prototypes/behaviour-ast/ui/src/boardLoad.ts
- prototypes/behaviour-ast/ui/src/pages/Board.tsx
Context:
The note says the Proposed column looks ordered by project, not by how much the agent wants each item reviewed. BEH-BOARD-10 says the agent scores every card it wants looked at, and equal scores take each project in turn. So when scores are equal or missing, the project interleave is what the user sees. BEH-BOARD-5 and BEH-BOARD-6 already say the top priority goes first, and Review requested comes before Proposed. BEH-BOARD-20 says every card carries a score, so unscored cards are a gap to close. Check whether proposed cards actually get a score before changing the sort. I did not read board.ts or Board.tsx, so the cause is unconfirmed. The note has no new behaviour beyond the existing cards.
The 2026-10-25 12:34Z note says there are lots of unscored items. That confirms the gap BEH-BOARD-20 names: the agent scores each new card on its next wake, but cards that were on the board before that rule may have no score. Check whether they were scored before the sort is changed. The sort should not be changed to hide unscored cards.
The 2026-10-21 00:08Z note at line 159 is the starvation complaint: the agent keeps taking new work and old work never moves. BEH-BOARD-12 already says Backlog is worked top priority first, new work comes in low, and old work is not starved. The contract at kit-ui.beh line 700 orders by (score, most recent). Check that a new card really enters at low and that the sort uses score before recency.
The 2026-10-21 06:50Z note at line 167 (bell ordered board items) is BEH-BELL-3: a rung card sits at the top of its column until its bell is answered.
The 2026-10-21 07:10Z note at line 171 asks how to reorder the backlog without Kit committing to every repo. BEH-BOARD-13 is drag to reorder. Its contract (kit-ui.beh around line 718) proposes one ordered list of card ids in one file beside the corpora, with unlisted cards falling back to (score, most recent). Check whether that file lives in the kit repo or in each project's repo, because the cost the note worries about depends on it.
The 2026-10-21 15:47Z note at line 175 (the proposed column cannot be read in order) is BEH-BOARD-14: a ranked column shows where each priority starts.
