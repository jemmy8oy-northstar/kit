# Run page: what each agent is doing, and what each historic run cost by stage
Work-item: pipeline-run-visibility
Flag: the run page shows how Kit shows his work, and its per-stage cost overlaps the open ticket record-session-cost (BEH-COST-1); the page may belong in kit-ui.beh (level: high)
Cards:
- BEH-VIS-1
- BEH-VIS-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 10:50Z (page for agents running and pipeline stages; historic runs by stage cost; internal only)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
New cards in kit.beh only. They have no tests yet, so kit check kit fails BEH-GATE-1 until a builder maps them.
The owner says this is internal for now. Per-stage cost needs the session cost record, which is the open ticket record-session-cost; build it after that, or show unknown.
Next step toward the goal: record-session-cost lands, then a builder adds the run page.
