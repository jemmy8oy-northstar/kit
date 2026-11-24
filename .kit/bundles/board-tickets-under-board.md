# Tickets sit under the board, not under behaviours
Work-item: board-tickets-under-board
Flag: this changes how Kit shows his work on the board, and the note does not say whether behaviours still appear on the board at all; the card that holds the nesting is also a guess (level: high)
Review: revised — BEH-TICKET-6 (on dev since #906) already says there is no spec board, so behaviours do not appear on the board; the nesting is anchored on TICKET-6 as well as TICKET-1
Cards:
- BEH-TICKET-1
- BEH-TICKET-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 22:41Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.tests.json
Context:
The note asks that, instead of tickets being under board -> behaviours, they sit under board -> tickets. BEH-TICKET-6 (merged to dev in #906, after this branch was cut from bdf792b7) already answers whether behaviours still appear on the board: Kit has an epic board and a ticket board and no feature or spec board, and a ticket's specs are read from its feature and spec pages. So board -> tickets is the ticket board of BEH-TICKET-6, and BEH-TICKET-7 already says a ticket lists the features and specs it touches. The nesting contract already on BEH-TICKET-1 (commit 9f6ae9b4) stays; the builder merges origin/dev first and adds no further spec line. Builder: find the board view that still nests tickets under behaviours, write the test first that tickets are direct children of the ticket board, and map it in kit.tests.json under BEH-TICKET-1 and BEH-TICKET-6. Do not delete the kit-ui.beh cards BEH-TICKET-6 lists; that waits for the owner. The note's anchor (2026-11-24 22:41Z) is not in kit.notes.md on this branch or on dev.
