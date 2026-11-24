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
Built:
- BEH-UI-6: each open state renders as a labelled column, and no column for a state with none open (Board.test.tsx, "shows each open state as a labelled column…")
- BEH-UI-6: each column's heading shows its open-ticket count, and the counts add up to the Tickets total (Board.test.tsx, "gives each column a visible heading…")
- BEH-UI-6: the state columns are siblings in one .board container, so they sit side by side (Board.test.tsx, "lays the state columns out as siblings…")
- Suite: 618 tests ran (5 todo) with `npx vitest run` in prototypes/behaviour-ast/ui, and all passed.

Test plan:
- BEH-UI-6: each open state renders as a labelled column: one region per state that has open tickets, named by its state (aria-label and data-state kept), and no region for a state with none open (Board.test.tsx, beside the BEH-ORCH-16 tickets test)
- BEH-UI-6: each column's visible heading shows that state's open-ticket count, matching the count in the Tickets heading's total (Board.test.tsx)
- BEH-UI-6: the state columns are siblings in one container that carries the board's side-by-side layout class (the .board class the spec board uses), so they sit in a row, not stacked (Board.test.tsx, structure only; jsdom does not lay out)
- BEH-UI-6: column order stays new, ready, planned, built, stopped with an unlisted state last, and merged tickets stay off the board (tickets.test.ts, unchanged, must still pass)
- BEH-UI-6: a blocked ticket still sorts above unblocked ones within its priority in its column, and the reply form still shows only on blocked tickets (existing tests in Board.test.tsx, must still pass unchanged)
- BEH-UI-6: the spec board's own column test (Board.test.tsx column('Queue'), column('Done')) still passes, so the shared .board class changes nothing there
