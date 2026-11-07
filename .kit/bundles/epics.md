# Epics as their own block, tagged onto features and behaviours at any depth
Work-item: epics
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
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 17:21Z (line 123)
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
The note asks whether epic grouping is the same as the project and feature navigation. The specs answer it: an epic is its own kind of block, and its parent is a project (BEH-EPIC-2). Features and behaviours carry an epic tag at any depth, not a parent link (BEH-EPIC-1). Epic tags are coloured apart from project and feature tags (BEH-EPIC-3). The board gets an epic view above the feature view (BEH-EPIC-4), and each epic has its own page (BEH-EPIC-8). A new epic's children start pending (BEH-EPIC-5). The note asks for the epic to be a separate abstraction in the boards, not a navigation level in the projects view, and the specs match. Nothing here needs a new card. Whether any of this is built was not checked.
The 2026-10-19 17:21Z note at line 123 asks that an epic be ingested into child features, behaviours and nested items, with the AI proposing them. BEH-EPIC-6 already says Kit drafts an epic's children from its description and proposes them, so this is the same card. The 2026-10-19 17:25Z note at line 131 (epics are separate blocks, an epic's parent is a project, a feature can have several parents of any kind) matches BEH-EPIC-2, and BEH-FEAT-2 shows every parent of a feature as a tag. The 2026-10-20 07:12Z note (epic page like behaviours and features) is BEH-EPIC-8. The 2026-10-21 15:52Z note (a broad behaviour should become a feature) is BEH-EPIC-9, which has Kit spot a behaviour that is really a feature and propose the split; the "broad" test is the agent's judgement, per the contract on that card. The 2026-10-22 16:39Z note (the AI proposes behaviours for features and features for epics) is BEH-EPIC-6 and BEH-EPIC-9. Its second half, asking when it needs more context, sits with the questions work item (questions-board-and-sheet).
