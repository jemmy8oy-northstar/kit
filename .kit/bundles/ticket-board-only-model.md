# Kit runs on a ticket board and an epic board only
Work-item: ticket-board-only-model
Flag: decommissioning the feature and spec boards changes how Kit shows work, and triage may edit only kit.beh, so the kit-ui.beh cards that draw those boards are named for deletion on BEH-TICKET-6 and not edited here; the owner should confirm that list (level: high)
Cards:
- BEH-TICKET-1
- BEH-TICKET-6
- BEH-TICKET-7
- BEH-TICKET-8
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-23 22:55Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- ui/src/tickets.ts
- ui/src/components/Tickets.tsx
Context:
Note (2026-11-23 22:55Z): kit pipeline has moved to a ticket board model instead of a spec board model. The epic to feature to spec hierarchy stays, but not on the boards. Kit should have an epic board and a ticket board only. A ticket may touch many features or specs but progresses toward one or a couple of epics. Kit stays big picture: tickets show granularly what Kit is up to, and epics show the goals and their status. Asked: move all projects to the ticket model and decommission the feature and spec boards.
Spec edit: BEH-TICKET-1 gets one added contract line (the ticket board is the only work board). BEH-TICKET-6, 7 and 8 are new cards at the end of kit.beh, each with review unreviewed.
BEH-TICKET-6 names the kit-ui.beh cards for deletion: BEH-STAGE-1, BEH-STAGE-7, BEH-STAGE-8, BEH-STAGE-9, BEH-STAGE-12 and BEH-BOARD-1. Their text is not edited here. A builder removes them only after the owner confirms the list, since BEH-BOARD-1 and the board cards share the same page.
Kept: BEH-EPIC-4 (epic view) and BEH-NARR-1 (project goal from open epics), which are the epic board's cards. BEH-UI-6 (ticket columns) already matches the ticket board.
Moving every project to the ticket model is a builder task across projects, not part of this spec edit.
Next step toward the goal: owner confirms the kit-ui.beh deletion list; then a builder maps BEH-TICKET-6 to 8 to tests and removes the two retired boards.
