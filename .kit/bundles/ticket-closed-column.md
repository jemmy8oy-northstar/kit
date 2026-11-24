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
