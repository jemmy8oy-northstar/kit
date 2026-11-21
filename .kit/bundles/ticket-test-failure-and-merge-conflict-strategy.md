# Ticket tests failing or merge conflicts need a strategy
Work-item: ticket-test-failure-and-merge-conflict-strategy
Flag: the note does not say which stage or which conflict it means, and no open ticket names this strategy; the new card's wording is my reading of "a strategy to resolve this" (level: high)
Review: revised — the reading stands as an unreviewed card he can deny, but Context wrongly said no card covers conflicts: BEH-ORCH-11 already gives the merge job an agent that resolves them, so the card builds on that
Cards:
- BEH-TICKET-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-21 13:31Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-ORCH-10 holds the current stop reasons for failed runs and conflicts; BEH-ORCH-11 names the merge job that brings a branch up to date with dev and has an agent resolve a conflict; neither is edited here)
Context:
Note text: "Ticket tests are failing or merge conflicts kit needs a strategy to resolve this". No existing card says what the pipeline does when a ticket's tests fail. For conflicts, BEH-ORCH-11 already says the merge job (built to merged, not built yet) brings the branch up to date with dev and has an agent resolve a conflict, and BEH-ORCH-10 as built stops the item on a failed check or a conflict and leaves the pull request open. BEH-TICKET-6 adds what happens after that: retry, then block with a question. Its conflict half is that merge job, not a second mechanism, and BEH-ORCH-10's stop becomes the point where the retry starts. Tests fail in the build run or in the Actions checks the merge step reads. The plan stage (technical design) only writes the test plan, so the card's words "in technical design or implementation" mean those two places. BEH-TICKET-3 and BEH-TICKET-4 already define the blocked tag and the question route, so the new card reuses them. BEH-TICKET-6 is the id chosen because BEH-TICKET-5 is already named by the open ticket bundle-malformed-resolve. Builders should read BEH-TICKET-3/4, BEH-ORCH-10 and BEH-ORCH-11 before writing code. The two open questions on the card (conflict in kit.beh, attempt count) are left to the owner with the defaults written on it.
