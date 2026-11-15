# Board: Queue and Blocked columns, no Scheduled, focus kept on return, wider columns
Work-item: board-pending-columns-and-focus
Flag: "Kit approving" is unclear: it may mean the owner approving a card Kit raised, or Kit approving its own work; I read it as the owner's approval of a paused card (level: low)
Cards:
- BEH-BOARD-18
- BEH-BOARD-16
- BEH-BOARD-12
- BEH-BOARD-15
- new: board columns are wide enough to read on desktop
- new: when the owner approves a paused item and it has no blockers, the item unpauses and goes back to the Queue
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 22:37Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 11:30Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 12:33Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 09:42Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- BEH-BOARD-18 already sets the pending columns to Backlog, Queue, Blocked, In progress, with no Scheduled. The 22:37Z note asks for the same, so the spec holds and the gap is probably the build. Check the board before assuming.
- Blocked sits before In progress, as the note asks. Items that need the human to act go there.
- Backlog means parked: not planned now, either because the agent needs to think more or because the human paused it. BEH-BOARD-16 is the pause; Queue holds ready-to-run work, and BEH-BOARD-12 orders backlog top priority first.
- BEH-BOARD-15 says coming back from a card lands on that card's column. The 11:30Z note reports it does not. The note also asks whether the board does a full page redirect or an in-app reroute. Decide by reading the router: a reroute keeps board state, a full load does not. Either way, the column and scroll position must survive the return.
- The 12:33Z note (columns too cramped on desktop) has no card. The new card is for review; no width is set by the note.
- 10-31 09:42Z: approving a paused item unpauses it when it has no blockers. Default: unpause moves it to the Queue, not In progress, and it does not run until the Queue picks it up. Record the default as an unreviewed assumption.
