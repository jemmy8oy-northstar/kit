# Pipeline runs page shows most recent runs first
Work-item: pipeline-runs-recent-first
Flag: the runs page is KitRuns in kit-ui.beh (FEAT-AGENT-PIPELINE, BEH-ORCH-9), whose Pipeline section lists items oldest first, so BEH-UI-5 (newest first) conflicts with it unless "run" means a run row and not a work item; the conflict is unresolved, and the owner must say which list he means (level: high)
Review: agreed — BEH-UI-5 is a self-contained UI card (actor engineer, runs command:KitUi) and its FEAT-SERVER placement is harmless; the ORCH file was outside this review, so the builder still confirms placement per Context
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
- Spec-stage finding: the runs page is the KitRuns page in kit-ui.beh, specified under FEAT-AGENT-PIPELINE (BEH-ORCH-6, BEH-ORCH-9 and the other cards that open page:KitRuns). BEH-UI-5 in kit.beh is a second card for that page and is not moved here: the owner decides placement.
- The run list (GET /api/runs, backend IAgentRun.cs) is already documented as newest first. The Pipeline section (BEH-ORCH-9, built) lists work items oldest first. BEH-UI-5 only conflicts with BEH-ORCH-9 if it means the Pipeline section's items.
- Conflict policy: kit.beh has no `# kit:conflicts` line, so the policy is build. Build one reading and raise a review request on BEH-UI-5 naming BEH-UI-5 and BEH-ORCH-9 and the reading built. Do not edit BEH-ORCH-9 to make the conflict go away.
- The Notes anchor kit.notes.md#2026-11-18 08:12Z does not exist; the last entry in kit.notes.md is 2026-11-15 15:20Z. The request's source is unverified, and the owner should confirm it.

Test plan:
- BEH-UI-5: with three runs whose start times are out of insertion order, the run list returns them newest start first.
- BEH-UI-5: a second page of the run list continues the order, with no run repeated or skipped across the page boundary and no reversal.
- BEH-UI-5: a run still running (no finish time) is placed by its start time, not at the bottom.
- BEH-UI-5: the runs page (Runs.test.tsx) renders rows in the order the API returns them, with no client-side re-sort.
- BEH-ORCH-9 (conflict with BEH-UI-5, see Flag): the Pipeline section still lists items oldest first, as its existing test expects, until the owner settles the conflict.
