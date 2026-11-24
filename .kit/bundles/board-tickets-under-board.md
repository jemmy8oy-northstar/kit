# Tickets sit under the board, not under behaviours
Work-item: board-tickets-under-board
Flag: this changes how Kit shows his work on the board, and the note does not say whether behaviours still appear on the board at all; the card that holds the nesting is also a guess (level: high)
Cards:
- BEH-TICKET-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 22:41Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.tests.json
Context:
The note asks that, instead of tickets being under board -> behaviours, they sit under board -> tickets. BEH-TICKET-1 is the card that says tickets appear on the board, so the nesting contract is added there. No other card is edited. Builder: find the board view that nests tickets under behaviours, write the test first, and map it in kit.tests.json. Do not change how behaviours are listed elsewhere without a new review request.
