# Pipeline agents may merge assumed behaviour, marked as assumed, and owner behaviour supersedes it quietly
Work-item: pipeline-assumptions-superseded
Flag: the card sits beside BEH-ADJ-1 in kit.beh, but the pipeline's own cards (BEH-ORCH-*) are in kit-ui.beh, which this work item does not edit; the placement is a guess (level: low)
Cards:
- BEH-ADJ-2 (new, review unreviewed)
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-19 16:55Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
Note: pipeline agents need to make assumptions the way claude-code-bot could. The pipeline can merge features tied to a behaviour the owner has not approved; that marks the behaviour as an assumption for future agents, and an owner-defined behaviour may quietly replace it. BEH-ADJ-2 states this in kit.beh beside BEH-ADJ-1 (an inference, included by default and marked unreviewed). It reuses the existing "contract my assumption:" prefix; no new syntax is needed.
Not built: the pipeline cards that already write "contract my assumption:" lines (BEH-ORCH-3, BEH-ORCH-11 and others in kit-ui.beh) show the convention is in use. A builder should check whether the merge path already accepts unapproved assumptions before adding code, and should not change kit-ui.beh from this work item.
