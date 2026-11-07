# Project page tabs and the notes tab: order, tab navigation after posting
Work-item: project-page-and-notes
Cards:
- BEH-FEAT-6
- BEH-FEAT-7
- BEH-NOTE-3
- BEH-NOTE-9
- BEH-NOTE-10
- BEH-NOTE-11
- BEH-NOTE-12
- new: after a note is posted, Kit opens the project's Notes tab
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:13Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:14Z ("Just to confirm - these notes are being wrapped into kit spec right?")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:24Z ("Notes should be ordered most recently written at the top")
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/notes.ts
- prototypes/behaviour-ast/ui/src/components/NotesPanel.tsx
- prototypes/behaviour-ast/ui/src/components/NoteFab.tsx
- prototypes/behaviour-ast/ui/src/pages/Project.tsx
Context:
The 07:14 question "are these notes wrapped into the spec?" is answered by the spec: BEH-NOTE-3 says a note is removed in the change that folds it into the spec, and BEH-NOTE-10 and BEH-NOTE-11 cover folded notes. That is an answer for the user, not new work. The 07:13 note wants the describe box above the tabs, and notes as a tab; BEH-FEAT-7 already says so. The new piece is that posting a note moves the user to the Notes tab. No card says this, so it is new. The 07:24 note asks for newest first and a load-more button; BEH-NOTE-9 says newest first, ten at a time, which covers load-more. Check the current Notes panel before changing it. Not verified against code.
