# Project navigation and layer tags (ui, ux, logic) on features and cards
Work-item: project-navigation-and-tags
Flag: BEH-KIND-1 names the layers ui, ux and technical while the 2026-10-16 notes say ui, ux and logic implementation, and the 10-11 question on why kit shows three projects is for the owner to answer, since it changes how work is grouped (level: high).
Review: revised — layer names are settled by the approved BEH-KIND-1 (kit#284), and BEH-FEAT-4/5 (`# kit:project kit`) already answer the three-projects question, so no owner decision is needed; dropped the new card and corrected Context
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
The 10-11 note asks for a navbar with board and project views, parents treated as tags so a feature can sit under several features, colour by project, and filter by project. BEH-NAV-1, BEH-FEAT-2 and BEH-BOARD-2 already say this, so the work is to check it is built, not to write new specs.
The two 10-16 notes say the layer filter is not visible anywhere. BEH-KIND-1 and BEH-KIND-2 are approved specs for it, and the code already has it. Board.tsx reads `?layer=`, draws LayerChips beside the project filter and puts a layer badge on each card. Project.tsx draws the same chips on the Features tab and on feature pages, carries the filter into feature links, and puts a layer badge on each behaviour. So the work is to find out why he does not see it (not deployed, chips hard to spot, or a page that has no badge), not to build it from nothing.
Layer naming is settled, not open. BEH-KIND-1 is review approved, cites this same 10-16 07:14Z note as its source, and defines technical as "the logic under both" (kit#284), so "logic implementation" in the note is the technical layer. Keep ui, ux, technical. If the chip label is what confused him, a clearer label or tooltip is a ui change inside BEH-KIND-1, not a spec change.
BEH-FEAT-4 and BEH-FEAT-5 (kit#246) already answer the 10-11 question on why kit shows three projects. Files that carry the same `# kit:project` line are one project card and one page, with features drawn from every file, and kit-ui.beh carries `# kit:project kit`. So kit.beh, kit-ui.beh and kit-hosted.beh are files of the one kit project, and their features sit together on its page. Check that the project list shows a single kit card. If kit.beh or kit-hosted.beh lacks the line, that is a bug against BEH-FEAT-4, not a new decision. No question card is needed.
Built:
- BEH-NAV-1: the navbar renders on the project list (App.test.tsx)
- BEH-NAV-1: the navbar renders on the board (App.test.tsx)
- BEH-NAV-1: the navbar renders on a project page (App.test.tsx)
- BEH-NAV-1: the navbar renders on a feature page (App.test.tsx)
- BEH-NAV-1: the navbar renders on a behaviour page (App.test.tsx)
- BEH-FEAT-4: kit.beh carries `# kit:project kit` (Projects.test.tsx)
- BEH-FEAT-4: kit-ui.beh carries `# kit:project kit` (Projects.test.tsx)
- BEH-FEAT-4: kit-hosted.beh carries `# kit:project kit` (Projects.test.tsx)
- BEH-FEAT-4: the three kit files make one kit card linking to /projects/kit, beside snip-it (Projects.test.tsx)
- BEH-KIND-1: the technical chip is tooltipped as logic implementation; it failed first, the tooltip was added in LayerChips.tsx (LayerChips.test.tsx)
- BEH-KIND-1: the ui and ux chips are tooltipped with what they are (LayerChips.test.tsx)
- BEH-KIND-1: a layer chip reports its layer when pressed and shows whether it is on (LayerChips.test.tsx)
- Whole UI suite (`npx vitest run` in prototypes/behaviour-ast/ui): 552 tests ran, all passed.
Test plan:
- BEH-NAV-1: every page (board, project list, project, feature, behaviour) renders the navbar, and the board link marks the board view as current
- BEH-FEAT-1: a project page lists its top-level features and untagged behaviours, and a feature card counts each behaviour beneath it once
- BEH-FEAT-2: a feature listed under two parents appears under both, and its page shows each parent as a tag linking to that parent's page
- BEH-FEAT-3: a feature opened from a parent keeps that path in the url and the breadcrumb shows it; with no path the crumbs are project and feature only
- BEH-FEAT-4: the project list shows one kit card for kit.beh, kit-ui.beh and kit-hosted.beh, each carrying `# kit:project kit`, linking to one kit page
- BEH-FEAT-5: the kit page draws features from all three files, and each link opens the file the feature lives in with counts summed across files
- BEH-BOARD-2: each card's colour matches its project's filter button, and selecting a project filter narrows the board and is kept in the url
- BEH-KIND-1: each board and project-page card shows its layer word, the technical chip is labelled and tooltipped as logic implementation, and selecting a layer chip narrows the board to that layer and is kept in `?layer=`
- BEH-KIND-2: the Features tab shows the three layer chips at its top, a feature with no behaviours in the chosen layers is hidden, and opening a feature keeps `?layer=`
