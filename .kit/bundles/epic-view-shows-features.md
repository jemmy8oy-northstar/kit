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
Test plan:
- BEH-EPIC-4: existing Epics.test.tsx "shows an epic its tagged features, linked, and their behaviours built out of total at any depth" covers a tagged feature listed and linked on its epic's card; keep it.
- BEH-EPIC-4 (new, Epics.test.tsx): a feature tagged into the epic three levels below the project's top feature is listed on the card, and its untagged parents are not.
- BEH-EPIC-4 (new, Epics.test.tsx): a feature tagged into an epic with no behaviours is listed on the card, and the card reads "0 of 0 behaviours built".
- BEH-EPIC-8: existing EpicPage.test.tsx "shows the epic its title, id and project, its behaviours built out of total, and its features linked" covers the features list; keep it.
- BEH-EPIC-8 (new, EpicPage.test.tsx): the same deep-tagged feature and the no-behaviour feature appear linked in the Features region, and the epic's page and card list the same feature ids.
