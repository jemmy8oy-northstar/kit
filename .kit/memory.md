# Kit repo memory

- The board's Tickets section (pipeline tickets, BEH-ORCH-16) is `prototypes/behaviour-ast/ui/src/components/Tickets.tsx`, with ordering in `ui/src/tickets.ts`. Cards (behaviours) on the board are `ui/src/board.ts`. They are different things.
- The UI test suite is `npm test` in `prototypes/behaviour-ast/ui` (vitest, 571 tests). The C# backend tests need `dotnet`, which is not installed on the build runner, so they have not run here.
- BEH-UI-5 (kit.beh) builds on tickets, not cards, and conflicts with the built BEH-ORCH-16 blocked-first ordering. It is under review; see its `review requested` line.
