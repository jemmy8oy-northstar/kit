# Project navigation and layer tags (ui, ux, logic) on features and cards
Work-item: project-navigation-and-tags
Flag: BEH-KIND-1 names the layers ui, ux and technical while the 2026-10-16 notes say ui, ux and logic implementation, and the 10-11 question on why kit shows three projects is for the owner to answer, since it changes how work is grouped (level: high).
Cards:
- BEH-NAV-1
- BEH-FEAT-1
- BEH-FEAT-2
- BEH-FEAT-3
- BEH-FEAT-4
- BEH-FEAT-5
- BEH-BOARD-2
- BEH-KIND-1
- BEH-KIND-2
- new: answer why kit lists its corpora (kit, kit-ui, kit-hosted) as three projects, and whether they become features of the kit project
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-11 12:39Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:14Z ("I feel like when browsing we should be able to filter the different types ui, ux and logic implementation")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:24Z ("Are ui/ux/logic implementation even treated as tags")
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/pages/Projects.tsx
- prototypes/behaviour-ast/ui/src/pages/Project.tsx
- prototypes/behaviour-ast/ui/src/pages/Board.tsx
- prototypes/behaviour-ast/ui/src/components/githubNavigation.ts
Context:
The 10-11 note asks for a navbar with board and project views, parents treated as tags so a feature can sit under several features, colour by project, and filter by project. BEH-NAV-1, BEH-FEAT-2 and BEH-BOARD-2 already say this, so the work is to check it is built, not to write new specs. The two 10-16 notes say the layer filter is not visible anywhere. BEH-KIND-1 and BEH-KIND-2 already specify the layer on every card and a layer filter on features. The gap is implementation, not spec. I did not check whether the layer is currently shown or stored, so start there. Layer naming: the spec says ui, ux, technical; the note says logic implementation. Use the spec's names unless the user says otherwise.
The 10-11 note also asks why kit has three projects, and wants them to be features of the kit project, with parents as tags (BEH-FEAT-2). The three are the corpora kit.beh, kit-ui.beh and kit-hosted.beh. Whether they should become features of kit is the owner's decision, so raise it as a question on the card and build nothing on it until he answers. Default if he is silent: the three stay separate projects.
