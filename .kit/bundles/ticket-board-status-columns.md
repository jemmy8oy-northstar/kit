# Ticket board: show the status columns as the spec board does
Work-item: ticket-board-status-columns
Flag: Unsure whether the ticket board's state columns are already visible or only exist in the data, and the board changes how Kit shows his work (level: high)
Review: revised — the columns are already rendered (Tickets.tsx maps ticketColumns to one section.ticket-column per state with an h4 label), but stacked one under another with a small muted heading, so the gap is layout and labelling, not data
Cards:
- BEH-UI-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-23 22:55Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/ui/src/tickets.ts
- prototypes/behaviour-ast/ui/src/components/Tickets.tsx
Context:
He wants the Tickets board to show its status columns visibly, the way the spec board does. BEH-UI-6 already says tickets keep their state column (built as ticketColumns in ui/src/tickets.ts and Tickets.tsx). One contract line was added to BEH-UI-6 asking for visible per-state columns. Existing contract lines and the review request on BEH-UI-6 are untouched. Checked at review: the columns are not hidden and not data-only. Tickets.tsx renders one <section className="ticket-column" data-state=...> per ticketColumns entry, headed by <h4 className="muted">{state}</h4>, all stacked vertically inside section.tickets, and each card also repeats its state as a Badge. So what he sees is one long list with faint state headings, not columns side by side. The work is presentation only: lay the ticket-column sections out side by side and give each a visible heading (with a count) the way the spec board's status columns are drawn. Find the spec board's column component and its CSS, and reuse its classes or layout rather than inventing new ones. Neither is listed above because review did not search for them. Leave ticketColumns ordering and the BEH-ORCH-16 review request alone. Keep the existing aria-label/data-state hooks so current tests still hold, and add a test that each open state renders as a labelled column.
