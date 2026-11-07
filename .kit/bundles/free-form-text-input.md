# Free-form text everywhere: behaviours, question answers, board box
Work-item: free-form-text-input
Flag: The 2026-10-26 22:51Z note removes notes from the board page, which settles the BEH-BOARD-24 conflict in favour of no board box; apply the project's kit:conflicts setting and confirm before removing it.
Review: revised — no conflict is left to settle: BEH-BOARD-24 comes from his own 22:51Z note, BEH-BOARD-9 was removed at his word, and the board box is already gone (built), so Context no longer asks to remove it.
Cards:
- BEH-NOTE-4
- BEH-NOTE-5
- BEH-NOTE-6
- BEH-NOTE-7
- BEH-NOTE-8
- BEH-SHEET-3
- BEH-TITLE-1
- BEH-NOTE-12
- BEH-NOTE-13
- BEH-BOARD-24
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-12 16:33Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-12 16:34Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:30Z ("Boards could probably have a free form text input")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 22:51Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/components/NoteFab.tsx
- prototypes/behaviour-ast/ui/src/components/TriageChat.tsx
- prototypes/behaviour-ast/ui/src/components/ItemNotes.tsx
- prototypes/behaviour-ast/ui/src/notes.ts
- prototypes/behaviour-ast/ui/src/pages/Board.tsx
Context:
Four notes ask for free text in place of forms. The 10-12 notes want behaviours and question answers in free text, with a way to dismiss text the AI cannot decode. BEH-NOTE-4 to BEH-NOTE-8 and BEH-SHEET-3 already cover this, and BEH-TITLE-1 covers the heavy-id complaint. Check what is built before adding anything. The 07:30 board note asks for a project picker that suggests while typing, and says that part can be its own enhancement. BEH-NOTE-13 covers the floating button naming a project. The board box was asked for, then withdrawn by his later 22:51Z note: BEH-BOARD-9 is removed and BEH-BOARD-24 stands, so there is no live conflict. The 07:30 backlog note about feedback has moved to the backlog bundle (BEH-FEEDBACK-1).
The 2026-10-26 22:51Z note asks to remove notes from the board page, because the floating button now adds notes. That supports BEH-BOARD-24 (the board has no note box of its own), and BEH-BOARD-24 is built: Board.tsx has no note box, so the floating button is already the only way to add a note. Do not rebuild a board box. It also asks that the floating button suggest an app when the note names one. BEH-NOTE-13 is built in NoteFab.tsx (mentioned(), one tap per project), so do not add a second suggestion.
