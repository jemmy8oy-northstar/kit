# Epics as their own block, tagged onto features and behaviours at any depth
Work-item: epics
Cards:
- BEH-EPIC-1
- BEH-EPIC-2
- BEH-EPIC-3
- BEH-EPIC-4
- BEH-EPIC-5
- BEH-EPIC-8
- FEAT-EPICS
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 16:44Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/epics.ts
- prototypes/behaviour-ast/ui/src/pages/Epics.tsx
- prototypes/behaviour-ast/ui/src/pages/EpicPage.tsx
Context:
The note asks whether epic grouping is the same as the project and feature navigation. The specs answer it: an epic is its own kind of block, and its parent is a project (BEH-EPIC-2). Features and behaviours carry an epic tag at any depth, not a parent link (BEH-EPIC-1). Epic tags are coloured apart from project and feature tags (BEH-EPIC-3). The board gets an epic view above the feature view (BEH-EPIC-4), and each epic has its own page (BEH-EPIC-8). A new epic's children start pending (BEH-EPIC-5). The note asks for the epic to be a separate abstraction in the boards, not a navigation level in the projects view, and the specs match. Nothing here needs a new card. Whether any of this is built was not checked.
