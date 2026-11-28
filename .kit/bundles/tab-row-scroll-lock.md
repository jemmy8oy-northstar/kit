# Project tab row stays in its strip when scrolled
Work-item: tab-row-scroll-lock
Flag: "locked vertically" is ambiguous; I read it as the tab row only, not the top bar, and as horizontal-only scrolling (level: low)
Review: revised — reading is sound (BEH-NAV-4 already says the row moves along the row only, tab row not top bar); corrected the ProjectNav.tsx path to prototypes/behaviour-ast/ui/src/components/ProjectNav.tsx, and note the kit.notes.md#2026-11-28 09:36Z anchor is not in that file, so the card text is the spec
Cards:
- BEH-NAV-4
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-28 09:36Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/ui/src/components/ProjectNav.tsx
Context:
The project tab row (Overview, Chat, Inbox, Board, Runs, Health, Notes, Settings) sits under the top bar on every project page (BEH-NAV-4). When the user scrolls it with a movement that is not exactly horizontal, its tabs move out of the tab area. Required: the row moves only horizontally, and a diagonal or vertical component must not shift tabs out of the strip. Fix the tab row only; do not change the top bar or page content. Check with a diagonal trackpad or wheel scroll that every tab stays inside the strip.

Test plan:
- BEH-NAV-4: layout.test.ts (reads index.css, as it does for BEH-NAV-2) checks that `.project-nav` and `.tabs` scroll on the x axis only (overflow-x auto, no overflow-y scroll) and that neither rule sets a transform or translate that would move the row vertically.
- BEH-NAV-4: a component test renders ProjectNav for a project with all flags on and checks the eight tabs in order (Overview, Chat, Inbox, Board, Runs, Health, Notes, Settings), that the active one has aria-current="page", and that Runs is absent for a signed-in non-operator (BEH-USERS-9), so the fix changes no tab content.
- BEH-NAV-4: a browser check (Playwright, not jsdom) dispatches wheel events with a diagonal deltaX and deltaY over the tab row at 390px and 1440px wide, then asserts that the row's scrollTop stays 0, that the row's top and bottom edges do not move, and that every tab's bounding box sits inside the strip's bounding box.
- BEH-NAV-4: a browser check confirms the top bar and a project page's content have the same bounding boxes before and after the scroll, so the change is confined to the tab row.
