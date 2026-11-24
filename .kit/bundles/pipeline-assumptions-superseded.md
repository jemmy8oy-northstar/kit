# Pipeline agents may merge assumed behaviour, marked as assumed, and owner behaviour supersedes it quietly
Work-item: pipeline-assumptions-superseded
Flag: the card sits beside BEH-ADJ-1 in kit.beh, but the pipeline's own cards (BEH-ORCH-*) are in kit-ui.beh, which this work item does not edit; the placement is a guess (level: low)
Flag: kit.beh BEH-ADJ-2 and kit-ui.beh BEH-ADJ-2 share one id, the Notes citation 2026-11-19 16:55Z is not in kit.notes.md (its last entry is 2026-11-15 15:20Z), and kit.tests.json has no BEH-ADJ-2 mapping yet, so CI's `kit check kit` fails once the card is built without one (level: high)
Review: agreed — placement in kit.beh under FEAT-RESOLVER is sound (it is about what KitResolve merges and supersedes, not the pipeline stages); BEH-ADJ-1 and BEH-ADJ-2 also exist in kit-ui.beh with different meanings, so refer to this card as kit.beh BEH-ADJ-2 and leave kit-ui.beh untouched
Cards:
- BEH-ADJ-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-19 16:55Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
Card line normalised to the bare id for the shape check; the annotation it carried, "(new, review unreviewed)", is kept here: BEH-ADJ-2 is new and its card is review unreviewed.
Note: pipeline agents need to make assumptions the way claude-code-bot could. The pipeline can merge features tied to a behaviour the owner has not approved; that marks the behaviour as an assumption for future agents, and an owner-defined behaviour may quietly replace it. BEH-ADJ-2 states this in kit.beh beside BEH-ADJ-1 (an inference, included by default and marked unreviewed). It reuses the existing "contract my assumption:" prefix; no new syntax is needed.
Not built: the pipeline cards that already write "contract my assumption:" lines (BEH-ORCH-3, BEH-ORCH-11 and others in kit-ui.beh) show the convention is in use. A builder should check whether the merge path already accepts unapproved assumptions before adding code, and should not change kit-ui.beh from this work item.
Test plan:
- BEH-ADJ-2: given an assumed behaviour with no owner approval, KitResolve merges it and reports review:Assumed.
- BEH-ADJ-2: the merged line carries the "contract my assumption:" prefix, so a later reader can see it was a guess.
- BEH-ADJ-2: an owner-defined behaviour on the same thing replaces the assumed line; the assumed line is gone, not kept beside it.
- BEH-ADJ-2: the supersession reports no conflict:BothSides, so it is not treated as a BEH-RESOLVE-2 conflict.
- BEH-ADJ-2: an assumed behaviour with no owner behaviour on it is still reported review:Assumed and is not dropped.
- BEH-ADJ-2: the card has a test mapping in kit.tests.json naming the C# test, so `kit check kit` passes.
- BEH-ADJ-2: the kit-ui.beh card with the same id keeps its own mapping in kit-ui.tests.json, unchanged by this work item.
