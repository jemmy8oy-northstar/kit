# Epic view shows the features under each epic
Work-item: epic-view-shows-features
Flag: Note may already be covered by BEH-EPIC-2, -4 and -8; unsure which card the wording belongs to (level: low)
Review: agreed — the wording belongs on BEH-EPIC-4 (board card) and BEH-EPIC-8 (page), which both already carry the contract; BEH-EPIC-2 needs no change and stays open on behaviour tags. The 22:43Z note text is not in kit-ui.notes.md, only cited by the contracts.
Cards:
- BEH-EPIC-4
- BEH-EPIC-8
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-24 22:43Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
Note asks that the epic view show the features under an epic, and asks whether features can tag epics at all. Spec already lets a feature tag an epic with `in EPIC-...` (BEH-EPIC-2, BEH-EPIC-1). BEH-EPIC-4 (epic board card) and BEH-EPIC-8 (epic page) already list tagged features as an assumption; this change adds the contract that the features under an epic are shown, at any depth, in both views. Owner has not said whether a behaviour may take the epic tag directly (open in BEH-EPIC-2); this change does not decide that.
