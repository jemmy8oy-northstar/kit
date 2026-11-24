# Tickets that are merged and resolved go to a closed column
Work-item: ticket-closed-column
Flag: the note asks for a closed column, which reverses the earlier reading on BEH-UI-6 that merged tickets are off the board; the card choice is also a guess (level: high)
Review: revised — not a conflict: "merged is off the board" was an unreviewed my-assumption on BEH-UI-6 (kit.beh), so his newer note overrides it; card stands, Files and Context tightened
Cards:
- BEH-UI-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 18:29Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/ui/src/tickets.ts
- prototypes/behaviour-ast/ui/src/components/Tickets.tsx
Context:
Note (2026-11-24 18:29Z): Kit tickets could have a closed column when they are merged and resolved.
Spec edit: BEH-UI-6 contract line "merged is off the board" is corrected to say a merged and resolved ticket goes to a closed column after the open columns and is not counted as open.
Card: BEH-UI-6 lives in behaviours/kit.beh (not kit-ui.beh) and owns the ticket state columns, so it is the right card. The replaced line was an unreviewed assumption, not a reviewed spec, so this is his correction of a guess, not a two-spec conflict.
Reading of "merged and resolved": tickets have no separate resolved field, so a ticket in state merged counts as merged and resolved. Record this as a contract my assumption on BEH-UI-6.
Builder: in ticketColumns (ui/src/tickets.ts) stop dropping merged tickets and return them as one "closed" column after every open column, sorted the same way (rendered in ui/src/components/Tickets.tsx). openTickets keeps excluding merged, so the open list and open count do not change. Update the ticketColumns doc comment, which still says merged tickets are off the board. Keep open-ticket ordering (priority, blocked first) unchanged. Map BEH-UI-6 to tests so the Stage-0 gate passes.
Next step toward the goal: builder implements the closed column and its test; owner confirms the closed-column placement on the review request.

Test plan:
- BEH-UI-6 (ui/src/tickets.test.ts, "keeps each ticket in the column of its state..."): replace the assertion that merged tickets are left off the board. A merged ticket lands in one "closed" column placed after every open column.
- BEH-UI-6 (ui/src/tickets.test.ts): openTickets still drops merged tickets, so the open list and its count are unchanged by the closed column.
- BEH-UI-6 (ui/src/tickets.test.ts): with no merged ticket, ticketColumns returns no closed column.
- BEH-UI-6 (ui/src/tickets.test.ts): the closed column orders its tickets by priority, 1 first, and a blocked ticket sits above the unblocked ones within one priority, as the open columns do.
- BEH-UI-6 (ui/src/tickets.test.ts): the closed column comes after an unlisted-state column, so the open columns keep their order and the closed column is last.
- BEH-UI-6 (ui/src/pages/Board.test.tsx, the "Merged one" test near line 259): replace the assertion that no merged region exists. The closed region exists, is last, holds the merged ticket, and the Tickets total still counts only open tickets.
- BEH-UI-6 (ui/src/pages/Board.test.tsx, the "Already merged" test near line 203): the same tickets still produce no open-count change in the section heading.
- BEH-UI-6 (ui/src/tickets.test.ts, "orders each column by priority..." and "within one priority lists a blocked ticket..."): unchanged; they must still pass for open columns.
- Note for builder: the "merged and resolved" reading (a ticket in state merged) is not yet a contract line on the card. The card is not edited at this stage, so record that contract when the builder works the card.

Built:
- BEH-UI-6 (ui/src/tickets.test.ts, "keeps each ticket in the column of its state, and puts merged tickets in a closed column after the open ones"): replaces the "leaves merged tickets off the board" assertion; merged lands in a "closed" column last.
- BEH-UI-6 (ui/src/tickets.test.ts, "still drops merged tickets from openTickets, so the open list and its count are unchanged by the closed column"): new.
- BEH-UI-6 (ui/src/tickets.test.ts, "returns no closed column when no ticket is merged"): new.
- BEH-UI-6 (ui/src/tickets.test.ts, "orders the closed column by priority, and a blocked merged ticket sits above the unblocked ones"): new.
- BEH-UI-6 (ui/src/tickets.test.ts, "places the closed column after an unlisted-state column, which goes after the listed ones"): new.
- BEH-UI-6 (ui/src/tickets.test.ts, "orders each column by priority..." and "within one priority lists a blocked ticket..."): unchanged, still pass.
- BEH-UI-6 (ui/src/pages/Board.test.tsx, "shows each open state as a labelled column, then a closed column last for merged tickets..."): replaces the "no merged region" assertion; the closed region is last, holds "Merged one", and the Tickets total stays 2. Its column-list expectation now includes "closed".
- BEH-UI-6 (ui/src/pages/Board.test.tsx, "leads a project's own board with its open tickets..." with the "Already merged" ticket): adds the closed column count "closed 1"; the Tickets total stays 2.
Full suite: 2179 backend tests (dotnet test ./backend) and 632 UI tests (npm test, 5 todo) ran; all passed. Lint, build and both Stage-0 gates (kit, kit-ui) also pass.
