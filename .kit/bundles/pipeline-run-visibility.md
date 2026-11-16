# Run page: what each agent is doing, and what each historic run cost by stage
Work-item: pipeline-run-visibility
Flag: the run page shows how Kit shows his work, and its per-stage cost overlaps the open ticket record-session-cost (BEH-COST-1); the page may belong in kit-ui.beh (level: high)
Review: revised — the run page is a screen and kit.beh's header forbids browser behaviours there, so the cards move to kit-ui.beh; the cost overlap is a dependency on BEH-COST-1, not a duplicate
Cards:
- BEH-VIS-1
- BEH-VIS-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 10:50Z (page for agents running and pipeline stages; historic runs by stage cost; internal only)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
BEH-VIS-1 and BEH-VIS-2 were written into kit.beh, but kit.beh is `kit:layer technical` and its header says not to add browser behaviours there (its 0 generated steps is the finding). A page belongs in kit-ui.beh: the builder moves both cards there unchanged, still unreviewed and owner-only. They have no tests yet, so kit check fails BEH-GATE-1 until a builder maps them.
The owner says this is internal for now. Per-stage cost needs the session cost record, which is the open ticket record-session-cost; build it after that, or show unknown. VIS-2 only reads that record and records no cost itself, so it does not duplicate BEH-COST-1.
Next step toward the goal: record-session-cost lands, then a builder moves the cards to kit-ui.beh and adds the run page.

Test plan:
- BEH-VIS-1: a run that is Running lists each agent that is running, with its current stage
- BEH-VIS-1: a review an agent flagged shows on its stage, with the name of the agent that flagged it
- BEH-VIS-1: a stage with no flagged review shows no review line, and a run with no running agent shows no running agents
- BEH-VIS-1: the page is served to the owner only; a tester's session gets no run page
- BEH-VIS-2: a Finished run shows each stage with the cost recorded for it by BEH-COST-1
- BEH-VIS-2: a stage with no recorded cost shows as unknown, never as zero
- BEH-VIS-2: a Finished run lists the reviews it requested, each with the stage and the agent that requested it
- BEH-VIS-2: with no session cost record at all (BEH-COST-1 not yet landed), every stage cost shows as unknown and the page still renders
- BEH-VIS-2: the page only reads the cost record and writes none (no cost value is stored by the page)
