# Merge agent does not seem to be working
Work-item: merge-agent-not-working
Flag: the note does not say what he saw, and BEH-ORCH-10 is in kit-ui.beh, not kit.beh, so that file was edited (level: high)
Review: revised — editing kit-ui.beh is right (the card lives there); the note is vague, so Context now has the builder diagnose both merge paths, including the merge job he likely means by "merge agent"
Cards:
- BEH-ORCH-10
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-19 19:02Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- grep the repo for PipelineMerger and MergeIfGreenAsync (not located in triage)
Context:
BEH-ORCH-10 says the pipeline merges the pull request of built work once its checks pass, into dev for kit, and that it is behind KIT_PIPELINE_MERGE. The card's "contract built" line says this is already built. Start by reading GET /api/pipeline for open items with their stop reasons and merged flags, and check whether KIT_PIPELINE_MERGE is on for kit, and also whether KIT_PIPELINE_JOB_MERGE is on: "merge agent" most likely means the merge job and its conflict-resolve run (the card's slice 7a and 7b contracts), and with that switch on the BEH-ORCH-10 look merges nothing, so check the Merge job's looks and resolve runs in Runs too. The note anchor kit.notes.md#2026-11-19 19:02Z is not in kit.notes.md (on this branch or on main); the quote is all there is, so do not look for more of it there. Fix the cause found; do not switch the merge on to make the note go away. Stop-on-failure rules in the card stay as they are.

Test plan:
- BEH-ORCH-10: with KIT_PIPELINE_MERGE unset or off, a built item whose pull request is green is not merged; GET /api/pipeline shows merged false and the item stays built (existing BEH_ORCH_10 off test)
- BEH-ORCH-10: with KIT_PIPELINE_MERGE=on, a built item whose Actions runs at the head all succeeded and which GitHub reports mergeable is merged with merge method "merge" into the base branch dev (never main), and the item and its ticket become merged
- BEH-ORCH-10: with the switch on, a built item whose Actions runs are still running, has no checks yet, or has unknown mergeability is not merged and keeps no stop reason
- BEH-ORCH-10: a failed run, a conflict (not mergeable) or a closed pull request stops the item with its plain reason in GET /api/pipeline, and leaves the pull request open
- BEH-ORCH-10: an Actions API answering 403 or 404 stops the item with a reason that says Kit cannot read Actions, and merges nothing
- BEH-ORCH-10: the merge names the head commit that was checked; a head that moved after the check is not merged unseen
- BEH-ORCH-10: whichever cause the fix targets (switch off, no Actions read access, or a stop reason), the Runs page shows it on the built item's pull request, and the "Merged" line appears once merged
- BEH-ORCH-10: with KIT_PIPELINE_JOB_MERGE=on, the BEH-ORCH-10 look merges nothing (BEH_ORCH_11 test) and a built green item is still merged by the merge job, not left unseen
- BEH-ORCH-10: the fix does not change the default: KIT_PIPELINE_MERGE stays off when unset, and a typo still throws at start (existing BEH_ORCH_10 test)
