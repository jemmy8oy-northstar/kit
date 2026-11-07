# Chat: show again, tappable options and action-card answers
Work-item: chat-show-again-options-and-action-cards
Cards:
- BEH-CHAT-9
- BEH-CHAT-10
- BEH-CHAT-6
- BEH-CHAT-3
- BEH-CHAT-12
- BEH-CHAT-13
- BEH-CHAT-14
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:49Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:50Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:54Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:32Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
Every card above exists in kit-ui.beh with a test line in kit-ui.tests.json, so the job is to verify each against the code and build only what is missing. The Chat page's source is not yet identified; find it from the test paths in kit-ui.tests.json.
- BEH-CHAT-9 (Show again): the card says it is built, beside Approve and Skip, and reposts the item in hand at the foot of the chat. Verify the label is "Show again".
- BEH-CHAT-10 (tappable options for yes/no and multiple-choice questions): the owner can tap one or type a free-form answer. Check the free-form path still works.
- BEH-CHAT-6 and BEH-CHAT-3 (note created from chat, with bubble and undo): CHAT-6 says a saved note shows a Kit line with an Undo, and Undo dismisses the note. The owner wants the bubble to state exactly what happened. Check the wording against that.
- BEH-CHAT-12 (show a posted answer, or a note's text, answer and tag, on request): from the 23:32Z note, item 1.
- BEH-CHAT-13 (action card shows the answer text just added): from the 23:32Z note, item 2.
- BEH-CHAT-14 (no next-up-in-Kit section): from the 23:32Z note, item 3. Confirm the section is gone.
- BEH-CHAT-12 needs the note's tag, which the note-tag work in notes-page-archive-tag-and-warning also adds. Coordinate, and do not build the tag here.
