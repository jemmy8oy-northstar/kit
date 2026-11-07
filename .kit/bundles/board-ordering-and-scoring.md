# Board ordering follows the agent's score, not the project
Work-item: board-ordering-and-scoring
Flag: The 2026-10-25 12:34Z note says many cards are unscored, so BEH-BOARD-20 is unmet; confirm where scores are written before changing the sort.
Review: revised — scores are each card's `priority` line in the corpus; BEH-BOARD-20 records the backfill and the Kit write path, and the UI sort already matches the specs, so the context is corrected and no sort change is called for.
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
Checked against the files listed above. Where scores are written: a card's score is its `priority` line in the .beh corpus (BEH-BOARD-10 contract, kit-ui.beh line 687). The agent writes it there, and the UI only reads it.
BEH-BOARD-20 is not an open gap. Its contracts (kit-ui.beh lines 1873-1876) record that on 2026-10-25 the 178 unscored behaviours were each given a priority. A card added through Kit is written with `priority 4`, and the wake sweep in claude-code-bot (tools/kit-backlog.js) lists any card still unscored. That list was empty on 2026-10-27. If cards are unscored now, the fault is in that sweep, which is outside this repo, not in the board sort. The two "my assumption" contracts on BEH-BOARD-20 are still unreviewed.
The sort already does what the cards say (board.ts byPriorityAcrossProjects, Board.tsx line 197). It orders by priority, with a missing priority sorted as 6, so unscored cards go last and are not hidden; they get their own "Unscored" heading (tierOf, BEH-BOARD-14). Within one priority it takes each project in turn (BEH-BOARD-10), and each project's own cards go newest first by `added` from git (boardLoad.ts), so score comes before recency (BEH-BOARD-12, line 700). "New work comes in low" holds because new cards are written at priority 4, not because of the sort. After that come pausedLast (BEH-BOARD-16), questionsFirst (BEH-BOARD-11) and rungFirst (BEH-BELL-3) on Review requested, Proposed, Backlog, Queue and Blocked. Review requested is the first column in RANKED (BEH-BOARD-6).
The 2026-10-19 13:40Z complaint that the board "looks ordered by project" describes the time before the interleave and the backfill. If it still looks that way, the likely cause is many equal scores (built cards all score 5), not a missing sort.
BEH-BOARD-13 (drag to reorder) answers the cost question itself: "every corpus lives in the kit repo, so a reorder is one commit to one repo" (kit-ui.beh line 717). It was denied, stood in for a pause, and is `paused until 2026-11-14`, so do not build it in this bundle.
Remaining work, if any: BEH-BOARD-12 is still `review unreviewed`. Nothing in the listed files needs changing for these cards.
