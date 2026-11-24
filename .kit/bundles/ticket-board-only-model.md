# Kit runs on a ticket board and an epic board only
Work-item: ticket-board-only-model
Flag: decommissioning the feature and spec boards changes how Kit shows work, and triage may edit only kit.beh, so the kit-ui.beh cards that draw those boards are named for deletion on BEH-TICKET-6 and not edited here; the owner should confirm that list (level: high)
Review: escalated — BEH-TICKET-6 now carries the earlier default, but the list still has gaps: BEH-BOARD-2 to 7 and 22 to 26 also hang off the columns BEH-BOARD-1 draws, yet they are neither listed nor parked with 10 to 21, and BEH-STAGE-3 to 6 and BEH-EPIC-1 to 4 all sit on page:KitStages, which goes with the feature board; he must say whether the spec board's rules (columns, priority order, pause, whose move, Denied) move onto the ticket board or are deleted, and which page the epic cards move to. ui/src/tickets.ts and ui/src/components/Tickets.tsx do not exist, so they are dropped from Files. Silence: leave every BEH-BOARD card untouched and read its rules as applying to the ticket board; mark only the eight cards BEH-TICKET-6 names as to be deleted, without removing them; keep BEH-STAGE-3 to 6 and BEH-EPIC-1 to 3 as they are; and keep BEH-TICKET-6's assumption that the epic board is its own page
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
Context:
Review: escalated — the deletion list is incomplete and clashes with a kept card: BEH-STAGE-10 and BEH-STAGE-11 also place features on the retired feature board, BEH-BOARD-10 to 19 and 21 rank and badge the spec board's columns and are orphaned if BEH-BOARD-1 goes, and kept BEH-EPIC-4 is "a third option on the BEH-STAGE-8 switch" on page:KitStages, so it cannot stand once BEH-STAGE-8 is deleted; he must say which of these cards are deleted, which are rewritten onto tickets, and where the epic board lives. Silence: take the feature and spec boards out of the navbar only; mark the six named cards plus BEH-STAGE-10 and BEH-STAGE-11 as to be deleted, with the reason beside each (BEH-TICKET-2), without removing them; leave BEH-BOARD-10 to 21 untouched; and give BEH-EPIC-4 an unreviewed assumption that the epic board is its own page, not a switch option
Note (2026-11-23 22:55Z): kit pipeline has moved to a ticket board model instead of a spec board model. The epic to feature to spec hierarchy stays, but not on the boards. Kit should have an epic board and a ticket board only. A ticket may touch many features or specs but progresses toward one or a couple of epics. Kit stays big picture: tickets show granularly what Kit is up to, and epics show the goals and their status. Asked: move all projects to the ticket model and decommission the feature and spec boards.
Spec edit: BEH-TICKET-1 gets one added contract line (the ticket board is the only work board). BEH-TICKET-6, 7 and 8 are new cards at the end of kit.beh, each with review unreviewed.
BEH-TICKET-6 names the kit-ui.beh cards for deletion, each with its reason: BEH-STAGE-1, BEH-STAGE-7, BEH-STAGE-8, BEH-STAGE-9, BEH-STAGE-12 and BEH-BOARD-1 (the six named), and BEH-STAGE-10 and BEH-STAGE-11. Their text is not edited here. A builder removes them only after the owner confirms the list, since BEH-BOARD-1 and the board cards share the same page.
BEH-TICKET-6 also says BEH-BOARD-10 to BEH-BOARD-21 stay untouched, and gives BEH-EPIC-4 an unreviewed assumption that the epic board is its own page, not a switch option. BEH-EPIC-4 is in kit-ui.beh, so that assumption is written in kit.beh on BEH-TICKET-6, not on BEH-EPIC-4.
Kept: BEH-EPIC-4 (epic view) and BEH-NARR-1 (project goal from open epics), which are the epic board's cards. BEH-UI-6 (ticket columns) already matches the ticket board.
Moving every project to the ticket model is a builder task across projects, not part of this spec edit.
Next step toward the goal: owner confirms the kit-ui.beh deletion list; then a builder maps BEH-TICKET-6 to 8 to tests and removes the two retired boards.
Review gaps (2026-11-24): BEH-STAGE-10 and 11 are feature-board placement cards missing from the list; BEH-BOARD-10 to 19 and 21 hang off the columns BEH-BOARD-1 draws; BEH-EPIC-4 depends on BEH-STAGE-8's switch and page:KitStages.
Test plan:
- BEH-TICKET-1: opening the board shows tickets, and each ticket shows its status and its open questions; the navbar and the board page show no feature or spec board.
- BEH-TICKET-6: the navbar and the boards show only the epic board and the ticket board; the eight named cards (BEH-STAGE-1, 7, 8, 9, 10, 11, 12 and BEH-BOARD-1) are marked as to be deleted with a reason beside each and are not removed; BEH-BOARD-10 to 21 are unchanged; BEH-EPIC-4 is its own page, not an option on the BEH-STAGE-8 switch; a ticket's feature and spec pages still open from its feature branch.
- BEH-TICKET-7: a ticket naming several epics counts toward each of them; a ticket touching several features and specs lists all of them; a ticket naming no epic appears on the ticket board and counts toward no epic's status.
- BEH-TICKET-8: each open epic's status is counted from the tickets that name it, in the states new, ready, planned, built, blocked and done; the project's first open epic is shown first as its goal; computing the status opens no feature or spec; a blocked ticket is counted once, in the state it is in, and not again as a separate blocked item (BEH-TICKET-3 makes blocked a tag).

Built:
- BEH-TICKET-1 (UI, components/Tickets.test.tsx): shows each ticket with its status and its open question
- BEH-TICKET-1 (UI, App.test.tsx): the board page keeps its ticket view and the navbar has no Features link
- BEH-TICKET-6 (UI, App.test.tsx): the navbar has one Board link and no feature or spec board in it
- BEH-TICKET-6 (UI, App.test.tsx): the board switch offers the epic board and the ticket board and no feature or spec view
- BEH-TICKET-6 (UI, App.test.tsx): BEH-EPIC-4 is its own page: /epics opens the epic board, with no switch option standing for it
- BEH-TICKET-6 (UI, App.test.tsx): a ticket's feature page still opens from its feature branch
- BEH-TICKET-6 (UI, test/ticketBoardSpec.test.ts): marks the eight cards BEH-TICKET-6 names as to be deleted, with a reason beside each, and removes none
- BEH-TICKET-6 (UI, test/ticketBoardSpec.test.ts): BEH-BOARD-10 to BEH-BOARD-21 carry no deletion mark and are still in the corpus
- BEH-TICKET-6 (UI, test/ticketBoardSpec.test.ts): BEH-EPIC-4 and BEH-NARR-1 are kept, unmarked
- BEH-TICKET-7 (UI, components/Tickets.test.tsx): still shows a ticket that names no epic, on the ticket board
- BEH-TICKET-7 (UI, components/Tickets.test.tsx): lists every feature its specs sit in, and every spec it touches
- BEH-TICKET-7 (UI, pages/Epics.test.tsx): counts a ticket toward each epic it names, through the features its specs sit in
- BEH-TICKET-7 (UI, pages/Epics.test.tsx): counts a ticket that names no epic toward no epic
- BEH-TICKET-8 (UI, pages/Epics.test.tsx): counts each open epic's tickets by state, a blocked ticket once in the state it is in
- BEH-TICKET-8 (UI, pages/Epics.test.tsx): computing the status opens no feature or spec page
- BEH-TICKET-8 (UI, pages/Epics.test.tsx): shows a project's first open epic first, as its goal
Whole suite: 2143 backend tests (dotnet test ./backend) and 631 UI tests (npm test), all passed; UI lint and build, and the kit and kit-ui Stage-0 gates, pass.
