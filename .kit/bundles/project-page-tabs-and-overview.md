# Project tabs hold every project view; the overview shows features, then what the project is
Work-item: project-page-tabs-and-overview
Flag: the README source for the overview text and the six-feature preview are my guesses (level: low)
Cards:
- BEH-SPACE-6
- BEH-FEAT-6
- BEH-FEAT-7
- BEH-FEAT-1
- BEH-FEAT-8
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-28 09:41Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-28 09:42Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-28 09:45Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/components/ProjectNav.tsx (the project tab bar, BEH-SPACE-6)
- prototypes/behaviour-ast/ui/src/ (the project page: its own tab row for Features, Questions, Notes, Schedule, Principles)
Context:
Today the project page has its own tab row under the title (features, questions, notes, schedule, principles). ProjectNav is the bar of Overview, Chat, Inbox, Board, Runs, Notes and Settings. The note moves the first group into ProjectNav.
Overview: features first (six at most), a View all features button that opens the Features tab, then a description of the project (README at repo root, if any).
Open ticket tab-row-scroll-lock (BEH-NAV-4) is about the row staying in its strip; more tabs make scrolling more likely, but it is a separate fix and is not folded in.
Top-bar item top-bar-inbox-and-menu edits BEH-NAV-4 only; no card here is shared with it.
