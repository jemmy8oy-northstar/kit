# Pipeline runs page shows most recent runs first
Work-item: pipeline-runs-recent-first
Flag: the pipeline runs page's own spec is not in kit.beh, so BEH-UI-5 is placed under FEAT-SERVER as a guess; the card may belong to the ORCH area in another file (level: low)
Cards:
- BEH-UI-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 08:12Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
- The owner asks that the pipeline runs page list the most recent run first. No existing card in kit.beh covers run ordering; the "runs" matches in kit.beh are the "when runs command" clause, not the runs page.
- BEH-UI-5 is new and marked review unreviewed. Its contract lines are assumptions: newest by start time at the top, and paging keeps that order.
- Before building, find where the pipeline runs page is specified (the ORCH cards, e.g. BEH-ORCH-1 and BEH-ORCH-14, are in another file) and move or confirm BEH-UI-5 there.
