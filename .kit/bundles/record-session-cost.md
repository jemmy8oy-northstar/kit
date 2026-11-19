# Record what each Kit session costs
Work-item: record-session-cost
Flag: BEH-COST-1 says sessions already run count, but a runner's ~/.claude/ is gone when its run ends, so past runner sessions may not be recoverable; and its contracts assume Claude Code reports a dollar cost, which the bundle says may not hold (token usage only) (level: high)
Review: revised — past-session recovery scoped to dev machines; new runner sessions take the dollar cost from the run's own result; token-only past records left unpriced by default, raised as a question
Cards:
- BEH-COST-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-04 10:58Z
- ticket record-session-cost, reply of 2026-11-19 16:49Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
The owner wants to start recording what the Claude Code bot costs for each Kit session, so that weekly cost can be worked out from history and compared with subscription price. The note's worry is garbled ("api incurs a much lower cost than if you were using api"); the likely reading is that subscription and API prices differ a lot. The figures are for checking the pricing model, not for billing.
Storage (answered by the owner's reply of 2026-11-19 16:49Z, "On the db"): the cost record lives in the database on Kit's server, not in any repo and not in a runner's ~/.claude/. This sits beside the per-user store behind /api/me/memory. Build it so the record is written there before a runner exits.
Where the cost comes from (revised at review):
- New sessions Kit starts: take the dollar cost from the run's own end-of-run result (headless Claude Code reports a total cost there), and write the record before the runner exits. Do not rely on the runner's ~/.claude/, which is gone when the run ends.
- Past runner sessions: not recoverable. Their ~/.claude/ is gone, so contract 2 ("sessions already run count") holds only for sessions run on a machine that still has its ~/.claude/, such as the owner's dev machine.
- Past sessions on a dev machine: the records under ~/.claude/ likely hold token usage per message, not a dollar cost. Pricing them from tokens would be an estimate, which contract 4 rules out. Default if the owner stays silent: import only past sessions that carry a reported dollar cost, skip token-only ones, and say how many were skipped. The builder raises this as a question on BEH-COST-1 in the corpus ("price past token-only sessions at API rates as a marked estimate, or skip them?") and does not quietly price the tokens.
Card BEH-COST-1 is new, in a new FEAT-COST feature (stage ui, copied from its siblings in kit.beh). The contract lines are unreviewed assumptions: one record per session with date, project and model; the record lives in the database on Kit's server, outside the project's repo; Kit stores the reported cost, not an estimate, and not the subscription price.
The card is written in the pending shape used in ideas.beh because no spec noun yet binds a session. A builder should add given/when/then only once the nouns exist.
Next step toward the goal: the open chat and tester work come first; this card is a cost check for the pricing question, so it can follow them.
Test plan:
- BEH-COST-1: a session Kit starts writes one record holding its date, project, model and the cost Claude Code reports for it, and no estimate
- BEH-COST-1: the record is in the database on Kit's server before the runner exits, and still exists after the runner's ~/.claude/ is removed
- BEH-COST-1: the record is in no project repo; no file is written to the project's repo for it
- BEH-COST-1: a new session's cost is taken from the run's end-of-run result, not from ~/.claude/
- BEH-COST-1: past sessions on a dev machine that carry a reported dollar cost are read from ~/.claude/ and summed by week, matching those costs
- BEH-COST-1: a past session record that holds token usage with no dollar cost is skipped and counted in what is reported, and no cost is priced from the tokens
- BEH-COST-1: no import of past runner sessions is attempted
- BEH-COST-1: the subscription price is never stored in the record
