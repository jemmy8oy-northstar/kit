# Project page tabs and the notes tab: order, tab navigation after posting
Work-item: project-page-and-notes
Flag: Not checked against the Notes panel code; whether notes already sort newest first and page ten at a time is unconfirmed.
Review: revised — NotesPanel already lists newest first, ten at a time; the describe box already opens Notes, so the new card narrows to the floating button.
Cards:
- BEH-FEAT-6
- BEH-FEAT-7
- BEH-NOTE-3
- BEH-NOTE-9
- BEH-NOTE-10
- BEH-NOTE-11
- BEH-NOTE-12
- BEH-NOTE-16
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
The 07:14 question "are these notes wrapped into the spec?" is answered by the spec: BEH-NOTE-3 says a note is removed in the change that folds it into the spec, and BEH-NOTE-10 and BEH-NOTE-11 cover folded notes. That is an answer for the user, not new work. The 07:13 note wants the describe box above the tabs, and notes as a tab; BEH-FEAT-7 already says so, and it is built (AskForBehaviour sits above ProjectTabs in Project.tsx). BEH-FEAT-7 also already has the contract "asking for a behaviour opens the Notes tab", and Project.tsx does it (onAsked calls select('notes'); the tab lives in ?tab=notes). The 07:24 note asks for newest first and a load-more button; BEH-NOTE-9 says newest first, ten at a time, and it is built: NotesPanel reverses the list and shows ten with a "Show N more (M hidden)" button. So the only gap is the floating note button (NoteFab.tsx, BEH-NOTE-12): its submit posts the note and clears the box but never moves the user to the Notes tab. The new card is that: after a note is posted from the button, go to /projects/<app>?tab=notes. Open point to record as an assumption: on a behaviour's page the note is tagged with that behaviour, so leaving the page may be unwanted; the default is to move to the Notes tab anyway, as the 07:13 note asks. Checked against code on 2026-11-07.
Test plan:
- BEH-FEAT-6: the project page opens on Features, and on Questions when the url says ?tab=questions; the Questions tab shows its waiting count.
- BEH-FEAT-7: the describe box renders above the tab list; a Notes tab is present; asking for a behaviour selects Notes.
- BEH-NOTE-3: a note that a behaviour cites is reported as folded and must be deleted from <app>.notes.md in the same change; the check fails while a folded note remains in the file.
- BEH-NOTE-9: notes are listed newest first; ten show, and "Show N more (M hidden)" reveals the rest.
- BEH-NOTE-10: a note cited in a behaviour's source line leaves the open list and appears under Folded, linking each citing behaviour.
- BEH-NOTE-11: a folded note is hidden until Show archived is pressed, the button states how many there are, and dismissing an archived note deletes it from the file.
- BEH-NOTE-12: the floating button opens a box on every page; on a behaviour's page the note is tagged with that behaviour; on a page with no project the box asks which project.
- BEH-NOTE-16: a note written from the floating button on a project page moves the page to ?tab=notes; a note on a behaviour's page also moves there; a refused note does not move the page; a note written with a project chosen in the box moves to that project's Notes tab.
