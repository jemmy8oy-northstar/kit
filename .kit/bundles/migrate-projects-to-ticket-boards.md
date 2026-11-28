# Every kit project moves to the ticket boards; behaviour boards retire
Work-item: migrate-projects-to-ticket-boards
Flag: which open items count as the remaining open tickets, and whether every project (not only kit) moves, are my reading; this touches how Kit stores and shows his work (level: high)
Cards:
- BEH-TICKET-9
- BEH-TICKET-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-28 09:52Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh (behaviour board cards BEH-STAGE-*, BEH-BOARD-*: not edited here)
- prototypes/behaviour-ast/behaviours/ (each project .beh file; the kit ones are kit, kit-ui, kit-hosted, ideas)
- backend/Balenthiran.Kit.Database/UserProjects.cs (project detection lists any .beh on a repo's default branch)
- prototypes/behaviour-ast/ui/src/board.ts, ui/src/tickets.ts, ui/src/components/Tickets.tsx
Context:
BEH-TICKET-6 already says there is no feature or spec board, and carries the open deletion list (BEH-STAGE-1, 7, 8, 9, 10, 11, 12 and BEH-BOARD-1). That list is unconfirmed and is not touched here; retiring the boards does not delete cards.
Projects in other users' repos need their tickets written in their own repos, which this item does not reach; say so if it is wanted.
Overlaps top-bar-inbox-and-menu on BEH-NAV-4 only through the layout line that BEH-TICKET-6 points at.
