# Ticket tests failing or merge conflicts need a strategy
Work-item: ticket-test-failure-and-merge-conflict-strategy
Flag: the note does not say which stage or which conflict it means, and no open ticket names this strategy; the new card's wording is my reading of "a strategy to resolve this" (level: high)
Cards:
- BEH-TICKET-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-21 13:31Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-ORCH-10 holds the current stop reasons for failed runs and conflicts; not edited here)
Context:
Note text: "Ticket tests are failing or merge conflicts kit needs a strategy to resolve this". No existing card says what the pipeline does when a ticket's tests fail or its branch conflicts. BEH-TICKET-3 and BEH-TICKET-4 already define the blocked tag and the question route, so the new card reuses them. BEH-TICKET-6 is the id chosen because BEH-TICKET-5 is already named by the open ticket bundle-malformed-resolve. Builders should read BEH-TICKET-3/4 and BEH-ORCH-10 before writing code. The two open questions on the card (conflict in kit.beh, attempt count) are left to the owner with the defaults written on it.
