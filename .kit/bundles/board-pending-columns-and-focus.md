# Board: Queue and Blocked columns, no Scheduled, focus kept on return, wider columns
Work-item: board-pending-columns-and-focus
Flag: "Kit approving" is unclear: it may mean the owner approving a card Kit raised, or Kit approving its own work; I read it as the owner's approval of a paused card (level: low)
Review: agreed — BEH-BOARD-25 already settles it: "approving" is the owner's approval of a paused card (SetReview), and the Queue default is recorded as an assumption on BEH-BOARD-18; no decision needed.
Cards:
- BEH-BOARD-18
- BEH-BOARD-16
- BEH-BOARD-12
- BEH-BOARD-15
- BEH-BOARD-19
- BEH-BOARD-25
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

Stage notes (spec stage, 2026-11-15):
- Cards live in kit-ui.beh, not kit.beh; kit.beh has no BEH-BOARD cards, so all edits are in kit-ui.beh.
- The two "new" items already exist and were not re-added, to avoid duplicate cards: BEH-BOARD-19 (wide columns, source 2026-10-25-1233, marked built) and BEH-BOARD-25 (approve paused resumes, source 2026-10-31-0942, built). The default in the 10-31 note is recorded as an unreviewed assumption on BEH-BOARD-18, which is a named card.
- Board check: the code's COLUMNS in ui/src/pages/Board.tsx are Review requested, Proposed, Backlog, Queue, Blocked, In progress, Done, Denied, with no Scheduled. The spec and the build agree for BEH-BOARD-18.
- BEH-BOARD-15 decision: the card link is an in-app react-router Link (a reroute); the board re-reads its data on mount, so its state is lost. The column and card are carried in the url, and the build restores them. Scroll: the card is centred, or the column is brought to its start when the card has moved.
- The 2026-10-22 22:37Z note means BEH-BOARD-12's ordering applies to the Queue, not Backlog; the card's contract now says so, and its title and region line are unchanged.

Built:
- puts a card that waits on him in Blocked, not In progress, even with a needs-context line and nothing else (BEH-BOARD-18)
- an approved card that was paused lands in the Queue, not In progress or Backlog (BEH-BOARD-18, BEH-BOARD-25)
- centres the card it comes back to on screen, and brings the column to its start when the card has moved (BEH-BOARD-15)
- The UI suite (`npx vitest run` in prototypes/behaviour-ast/ui) ran 564 tests, all passed; `tsc -b` is clean.

Test plan:
- BEH-BOARD-18: the board shows Backlog, Queue, Blocked and In progress in that order with no Scheduled region; a Blocked card sits before In progress; an approved paused card lands in the Queue, not In progress.
- BEH-BOARD-16: a paused card sits at the foot of Backlog under a Paused heading with a "paused" badge; its page offers Pause and Resume beside Approve and Deny; the wake sweep does not list it as work.
- BEH-BOARD-12: within a priority the Queue's most recently added card is first; equal scores take each project in turn; the agent's next pick is the top of the Queue.
- BEH-BOARD-15: opening a card and coming back restores its column and card (Board.test.tsx "comes back from a card ..."); a card that has left its column lands on the column (Board.test.tsx "lands on the column ..."); the card is centred on return.
- BEH-BOARD-19: at 1440px the board is not held to the 72rem page width and no column is narrower than 15rem; a narrower window scrolls sideways.
- BEH-BOARD-25: approving a paused card with no needs-context line removes its paused line in the same write and places it in the Queue; approving one with a needs-context line leaves it paused; denying a paused card drops it and leaves the paused line.
