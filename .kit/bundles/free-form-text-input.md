# Free-form text everywhere: behaviours, question answers, board box, feedback
Work-item: free-form-text-input
Flag: Conflict: the 2026-10-16 07:30Z note asks for a free-text box on the board, but BEH-BOARD-24 says the board has no note box of its own. The later note at 2026-10-26 22:51Z ("Remove notes from the boards page") sides with BEH-BOARD-24. Do not build the board box until the user confirms. Apply the project's kit:conflicts setting.
Cards:
- BEH-NOTE-4
- BEH-NOTE-5
- BEH-NOTE-6
- BEH-NOTE-7
- BEH-NOTE-8
- BEH-SHEET-3
- BEH-TITLE-1
- BEH-NOTE-13
- BEH-FEEDBACK-1
- BEH-BOARD-24
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-12 16:33Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-12 16:34Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:30Z ("Boards could probably have a free form text input")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:30Z ("Oo a backlog idea for when we have real users")
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/components/NoteFab.tsx
- prototypes/behaviour-ast/ui/src/components/TriageChat.tsx
- prototypes/behaviour-ast/ui/src/components/ItemNotes.tsx
- prototypes/behaviour-ast/ui/src/notes.ts
Context:
Four notes ask for free text in place of forms. The 10-12 notes want behaviours and question answers in free text, with a way to dismiss text the AI cannot decode. BEH-NOTE-4 to BEH-NOTE-8 and BEH-SHEET-3 already cover this, and BEH-TITLE-1 covers the heavy-id complaint. Check what is built before adding anything. The 07:30 board note asks for a project picker that suggests while typing, and says that part can be its own enhancement. BEH-NOTE-13 covers the floating button naming a project. The board box itself conflicts with BEH-BOARD-24, as flagged above. The 07:30 backlog note about feedback is already BEH-FEEDBACK-1, which is backlog until there are users. Do not build it now.
