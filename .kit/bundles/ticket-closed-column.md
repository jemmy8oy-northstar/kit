# Tickets that are merged and resolved go to a closed column
Work-item: ticket-closed-column
Flag: the note asks for a closed column, which reverses the earlier reading on BEH-UI-6 that merged tickets are off the board; the card choice is also a guess (level: high)
Cards:
- BEH-UI-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 18:29Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/tickets.ts
- prototypes/behaviour-ast/ui/src/components/Tickets.tsx
Context:
Note (2026-11-24 18:29Z): Kit tickets could have a closed column when they are merged and resolved.
Spec edit: BEH-UI-6 contract line "merged is off the board" is corrected to say a merged and resolved ticket goes to a closed column after the open columns and is not counted as open.
Builder: add a closed column to the Tickets board (ticketColumns in ui/src/tickets.ts, rendered in ui/src/components/Tickets.tsx) for tickets in the merged state that are resolved. Keep open-ticket ordering (priority, blocked first) unchanged. Map BEH-UI-6 to tests so the Stage-0 gate passes.
Next step toward the goal: builder implements the closed column and its test; owner confirms the closed-column placement on the review request.
