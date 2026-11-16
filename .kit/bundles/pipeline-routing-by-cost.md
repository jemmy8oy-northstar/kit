# Pipeline skips triage for backlog items and sends reviews to the model their flag level asks for
Work-item: pipeline-routing-by-cost
Flag: the notes ask for cheaper routing; the BEH-ORCH cards in kit-ui.beh may already cover part of it and were not checked (level: low)
Cards:
- BEH-ROUTE-1
- BEH-ROUTE-2
- BEH-ROUTE-3
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 01:50Z (quickly and cheaply identify when something is just for the backlog)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 01:51Z (opus agent identifies backlog items to schedule; pipeline wakes for them; backlog items skip triage)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 02:10Z (haiku uncertainty flag picks opus or sonnet review by level; should cut costs)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
New cards in kit.beh only, because this item may edit only that spec. The cards have no tests yet, so kit check kit fails BEH-GATE-1 until a builder maps them.
Each card carries the line "review unreviewed" as the triage stage was told. Kit's parser refuses an unknown keyword (BEH-PARSE-1), so confirm the line parses before the cards are built.
Assumed: flag level low goes to sonnet review and level high to opus review. The note does not say what medium should do.
Next step toward the goal: a builder maps BEH-ROUTE-1 to 3 to tests and reads the flag level in the reviewer's pick.
