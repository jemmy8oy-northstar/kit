# Merge agent does not seem to be working
Work-item: merge-agent-not-working
Flag: the note does not say what he saw, and BEH-ORCH-10 is in kit-ui.beh, not kit.beh, so that file was edited (level: high)
Cards:
- BEH-ORCH-10
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-19 19:02Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- grep the repo for PipelineMerger and MergeIfGreenAsync (not located in triage)
Context:
BEH-ORCH-10 says the pipeline merges the pull request of built work once its checks pass, into dev for kit, and that it is behind KIT_PIPELINE_MERGE. The card's "contract built" line says this is already built. Start by reading GET /api/pipeline for open items with their stop reasons and merged flags, and check whether KIT_PIPELINE_MERGE is on for kit. Fix the cause found; do not switch the merge on to make the note go away. Stop-on-failure rules in the card stay as they are.
