# The merge agent merges built pull requests, so none wait open
Work-item: merge-agent-merges-open-prs
Flag: the merge cards (BEH-ORCH-10, BEH-ORCH-11, BEH-ORCH-12) live in kit-ui.beh, which triage may not edit, so no spec change is made here; the cause of the hang is not known from the notes (level: low)
Review: agreed — the cards and files match the spec and helm (KIT_PIPELINE_JOB_MERGE is on, so the cause is a runtime wait the builder must find from the job log); no spec edit is needed from this work item
Cards:
- BEH-ORCH-11
- BEH-ORCH-10
- BEH-ORCH-12
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-19 16:57Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit.beh
- helm/values.yaml
Context:
Card lines normalised to bare ids for the shape check; the annotations they carried are kept here: BEH-ORCH-11 is in kit-ui.beh, its merge job slice 7a and 7b; BEH-ORCH-10 is in kit-ui.beh, the merge look it replaces; BEH-ORCH-12 is in kit-ui.beh, private repo test runs before a merge.
Note: "the merge agent needs to be merging, I don't see it merging so there are lots of PRs hanging." The merge job was switched on in helm by commit 4d64939c (BEH-ORCH-11 slice 7), so the job should now run, but the note says built pull requests are not merging.
Causes the cards allow, to check in order: (1) the merge job takes only the lowest-priority built item per project and the rest wait, so one stuck item holds the queue; (2) on a private repo the merge waits for a Kit test run at the head commit (BEH-ORCH-12, answered NeedsTest), and nothing may be running that test; (3) a conflict goes to one resolve run per ticket, and a second conflict stops the item. Check the Runs page and the job's log for which one applies before changing code.
Not built: the cause is not confirmed from the notes. A builder should first find, from the job's log, why each open pull request is waiting, then fix the cause it finds. The spec change, if any, goes into kit-ui.beh, which this work item does not edit.
