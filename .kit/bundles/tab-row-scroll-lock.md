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
