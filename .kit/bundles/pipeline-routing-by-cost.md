# Pipeline skips triage for backlog items and sends reviews to the model their flag level asks for
Work-item: pipeline-routing-by-cost
Flag: notes of 2026-11-08 and 2026-11-14 add the triage-stays-on-the-hook and priority-order contracts to BEH-ROUTE-1 and -2; the stage-merge and hosting questions of the 11-08 note are already answered by BEH-ORCH-11 in kit-ui.beh on main (level: low)
Review: agreed — cards match the 11-08/11-14 notes and kit.beh (ROUTE-1/2 carry the contract lines, ROUTE-3 defaults medium to sonnet); BEH-ORCH-11 in kit-ui.beh already covers the stage-merge and hosting points; "review unreviewed" is used on other cards in kit.beh
Cards:
- BEH-ROUTE-1
- BEH-ROUTE-2
- BEH-ROUTE-3
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 01:50Z (quickly and cheaply identify when something is just for the backlog)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 01:51Z (opus agent identifies backlog items to schedule; pipeline wakes for them; backlog items skip triage)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 02:10Z (haiku uncertainty flag picks opus or sonnet review by level; should cut costs)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-08 22:06Z (triage stays on the hook; later stages act by priority, not by a note; stages kept separate until evaluated; C# backend in the cluster)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-14 13:44Z ("Please continue with the pipeline enhancements!"; nudge only)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
New cards in kit.beh only, because this item may edit only that spec. The cards have no tests yet, so kit check kit fails BEH-GATE-1 until a builder maps them.
Each card carries the line "review unreviewed" as the triage stage was told. Kit's parser refuses an unknown keyword (BEH-PARSE-1), so confirm the line parses before the cards are built.
Assumed: flag level low goes to sonnet review and level high to opus review. The note does not say what medium should do.
2026-11-16 update: BEH-ROUTE-1 and BEH-ROUTE-2 each gained a contract line from the 2026-11-08 22:06Z note: triage alone waits on a note, and the stages after it take work by priority, so existing tickets are actioned too.
Not added: the rest of the 11-08 note (stages kept separate and merges decided from what Runs shows; the jobs live in Kit's C# backend, triage on the hook) is already in BEH-ORCH-11 in kit-ui.beh on main, so nothing was repeated here. Those cards are not in this branch's kit.beh, so a builder should read kit-ui.beh before mapping BEH-ROUTE-1 to tests, to avoid two cards for one rule.
The 2026-11-14 13:44Z note asks to continue the pipeline work. It names no card; the next unbuilt slice is the one BEH-ORCH-11 leaves open.
Next step toward the goal: a builder maps BEH-ROUTE-1 to 3 to tests and reads the flag level in the reviewer's pick.
