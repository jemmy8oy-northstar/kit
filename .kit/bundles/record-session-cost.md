# Record what each Kit session costs
Work-item: record-session-cost
Flag: the note does not say where the cost record should live, and it changes how Kit stores his work, so he should confirm the storage (level: high)
Cards:
- BEH-COST-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-04 10:58Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
The owner wants to start recording what the Claude Code bot costs for each Kit session, so that weekly cost can be worked out from history and compared with subscription price. He is worried that API cost is much lower than people say, so the cost figures are for checking the pricing model, not for billing.
Claude Code keeps session records under ~/.claude/ on the machine that runs it. The cost of a past session can be read from there, so this work should cover sessions already run as well as new ones.
Card BEH-COST-1 is new, in a new FEAT-COST feature (stage ui, copied from its siblings in kit.beh). The contract lines are unreviewed assumptions: one record per session with date, project and model; the record lives outside the project's repo; Kit stores the reported cost, not an estimate, and not the subscription price.
The card is written in the pending shape used in ideas.beh because no spec noun yet binds a session. A builder should add given/when/then only once the nouns exist.
Next step toward the goal: the open chat and tester work come first; this card is a cost check for the pricing question, so it can follow them.
