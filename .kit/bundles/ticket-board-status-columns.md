# Ticket board: show the status columns as the spec board does
Work-item: ticket-board-status-columns
Flag: Unsure whether the ticket board's state columns are already visible or only exist in the data, and the board changes how Kit shows his work (level: high)
Cards:
- BEH-UI-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-23 22:55Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/ui/src/tickets.ts
- prototypes/behaviour-ast/ui/src/components/Tickets.tsx
Context:
He wants the Tickets board to show its status columns visibly, the way the spec board does. BEH-UI-6 already says tickets keep their state column (built as ticketColumns in ui/src/tickets.ts and Tickets.tsx). One contract line was added to BEH-UI-6 asking for visible per-state columns. Existing contract lines and the review request on BEH-UI-6 are untouched. Builder should check the spec board's column rendering and match it, and confirm whether the ticket board is hiding the columns or they are just not labelled.
