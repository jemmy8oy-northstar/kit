# Record what each Kit session costs
Work-item: record-session-cost
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
Claude Code keeps session records under ~/.claude/ on the machine that runs it. The cost of a past session can be read from there, so this work should cover sessions already run as well as new ones. Two cautions for the builder. First, Kit sessions run on CI runners whose ~/.claude/ is gone when the run ends, so past runner sessions may not be recoverable, and new ones must be recorded before the run exits. Second, session records may hold token usage rather than a dollar cost. If so, the "reported cost, not an estimate" contract cannot hold, and the builder should raise that rather than quietly price the tokens.
Card BEH-COST-1 is new, in a new FEAT-COST feature (stage ui, copied from its siblings in kit.beh). The contract lines are unreviewed assumptions: one record per session with date, project and model; the record lives in the database on Kit's server, outside the project's repo; Kit stores the reported cost, not an estimate, and not the subscription price.
The card is written in the pending shape used in ideas.beh because no spec noun yet binds a session. A builder should add given/when/then only once the nouns exist.
Next step toward the goal: the open chat and tester work come first; this card is a cost check for the pricing question, so it can follow them.
