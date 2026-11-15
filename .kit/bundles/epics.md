# Epics as their own block, tagged onto features and behaviours at any depth
Work-item: epics
Flag: Epic tags (BEH-EPIC-3) would share the tag and colour rendering that open ticket project-navigation-and-tags is building (BEH-FEAT-2, BEH-KIND-1); build on that component, or check that ticket first (level: low)
Review: agreed — BEH-EPIC-3 already fixes a distinct secondary colour and opens the epic page, so the bundle stands; build epic tags on the shared tag component from project-navigation-and-tags if it has landed, else add a small epic variant and reconcile later
Cards:
- BEH-EPIC-1
- BEH-EPIC-2
- BEH-EPIC-3
- BEH-EPIC-4
- BEH-EPIC-5
- BEH-EPIC-6
- BEH-EPIC-8
- BEH-EPIC-9
- FEAT-EPICS
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 16:44Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 17:25Z (line 131)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-20 07:12Z (line 151)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-21 15:52Z (line 181)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 16:39Z (line 193)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/epics.ts
- prototypes/behaviour-ast/ui/src/pages/Epics.tsx
- prototypes/behaviour-ast/ui/src/pages/EpicPage.tsx
Context:
The note asks whether epic grouping is the same as the project and feature navigation. The specs answer it: an epic is its own kind of block, and its parent is a project (BEH-EPIC-2). Features and behaviours carry an epic tag at any depth, beside their parents (BEH-EPIC-1). The 17:25Z note also lists epics among a feature's parents; as built it is a tag, and whether it is a parent is open for him (BEH-EPIC-2). Epic tags are coloured apart from project and feature tags (BEH-EPIC-3). The board gets an epic view above the feature view (BEH-EPIC-4), and each epic has its own page (BEH-EPIC-8). A new epic's children start pending (BEH-EPIC-5). The note asks for the epic to be a separate abstraction in the boards, not a navigation level in the projects view, and the specs match. Nothing here needs a new card. Whether any of this is built was not checked.
The 2026-10-19 17:21Z note at line 123 asks that an epic be ingested into child features, behaviours and nested items, with the AI proposing them. BEH-EPIC-6 already says Kit drafts an epic's children from its description and proposes them, so this is the same card. The 2026-10-19 17:25Z note at line 131 (epics are separate blocks, an epic's parent is a project, a feature can have several parents of any kind) matches BEH-EPIC-2, and BEH-FEAT-2 shows every parent of a feature as a tag. The 2026-10-20 07:12Z note (epic page like behaviours and features) is BEH-EPIC-8. The 2026-10-21 15:52Z note (a broad behaviour should become a feature) is BEH-EPIC-9, which has Kit spot a behaviour that is really a feature and propose the split; the "broad" test is the agent's judgement, per the contract on that card. The 2026-10-22 16:39Z note (the AI proposes behaviours for features and features for epics) is BEH-EPIC-6 and BEH-EPIC-9. Its second half, asking when it needs more context, sits with the questions work item (questions-board-and-sheet).
Built:
- features.test.ts: lists the same top level, and the same children of every feature, with or without an epic tag (BEH-EPIC-1)
- features.test.ts: keeps the epic-tagged feature under its own parent, and does not lift it to the top level (BEH-EPIC-1)
- FeaturePage.test.tsx: draws an epic tag apart from the parent badges, so no badge tone or project colour is used (BEH-EPIC-3)
- test/look.test.ts: draws an epic tag only in the secondary role, apart from badge tones and project hues (BEH-EPIC-3)
- board.test.ts: puts a child Kit drafted under an epic in Proposed, pending and not built (BEH-EPIC-6)
- board.test.ts: moves the drafted child to the queue once he approves it, still pending (BEH-EPIC-5, BEH-EPIC-6)
- board.test.ts: leaves a drafted child he denied on no column, so it is not work (BEH-EPIC-6)
- The whole UI suite (prototypes/behaviour-ast/ui, npm test) ran 547 tests and all passed; lint and tsc are clean.

Test plan:
- BEH-EPIC-1: a feature at any depth takes `in EPIC-…` and keeps its project and parent features; the project tree is unchanged
- BEH-EPIC-2: an `epic` block takes one `in PROJECT-…` line; an `in EPIC-…` on a feature resolves; an `in` naming an unknown epic is refused; a behaviour's `in EPIC-…` is refused with the fix in the message (open question, see its contract)
- BEH-EPIC-3: a feature page shows its epic tags in the secondary colour, not a badge tone or project colour, and each tag opens the epic page
- BEH-EPIC-4: the board switch reads Epics, Features, Behaviours; an epic card lists its tagged features and counts their behaviours at any depth, each once
- BEH-EPIC-5: children he writes under a new epic are pending; children Kit drafts sit in Proposed, pending (BEH-EPIC-6); none are written as built
- BEH-EPIC-6: every child Kit drafts lands in Proposed; an epic with no feature is listed by the wake sweep unless its title says out of scope; a draft Kit cannot make becomes a review request, not a guess
- BEH-EPIC-8: the epic page shows title, id, project, built out of total behaviours, and linked features; an unknown epic id names the missing epic; the epic card title and a feature's epic tag both open it
- BEH-EPIC-9: only a behaviour over the broad test shows "Could be a feature"; the split is a review request naming the feature and child behaviours; approving keeps the old id as the feature's source
