# Ticket board: keep state columns, order by priority, blocked first
Work-item: ticket-board-priority
Flag: Unsure what the owner wants for ticket priority and blocked ordering, and the board changes how Kit shows his work (level: high)
Review: agreed — BEH-UI-5 is unreviewed and its three assumptions (state columns, priority order, blocked first within a priority) are open for the owner to deny; nothing reviewed is changed, so no decision is needed before building
Cards:
- BEH-UI-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 21:10Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- docs/design/ui.md
Context:
Tickets keep their state columns. Each column is ordered by priority (1 to 5, as in BEH-PARSE-3). Within one priority, blocked tickets sit at the top. BEH-UI-5 is new and unreviewed; its three contract lines are assumptions for the owner to confirm or deny. No existing card was changed.
Built:
- BEH-UI-5: keeps each ticket in the column of its state, and leaves merged tickets off the board (prototypes/behaviour-ast/ui/src/tickets.test.ts)
- BEH-UI-5: orders each column by priority, 1 first and 5 last (prototypes/behaviour-ast/ui/src/tickets.test.ts)
- BEH-UI-5: within one priority lists a blocked ticket above the unblocked ones, and never above a higher priority (prototypes/behaviour-ast/ui/src/tickets.test.ts)
- BEH-UI-5: shows the open tickets in one column per state, each column by priority with blocked first within a priority (prototypes/behaviour-ast/ui/src/pages/Board.test.tsx)
Whole suite: 571 tests in the UI's vitest run (50 files), all passed; the C# backend suite was not run because dotnet is not installed here, and backend is unchanged.
Test plan:
- BEH-UI-5: reading the board reports column:State, and each ticket stays in the state column it is in
- BEH-UI-5: within one column, tickets are ordered by priority from 1 (first) to 5 (last)
- BEH-UI-5: within one priority, a blocked ticket is listed above the unblocked tickets of that priority
