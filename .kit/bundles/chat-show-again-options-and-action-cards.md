# Chat: show again, options, action cards, Kit's capabilities, item links and input
Work-item: chat-show-again-options-and-action-cards
Cards:
- BEH-CHAT-9
- BEH-CHAT-10
- BEH-CHAT-6
- BEH-CHAT-3
- BEH-CHAT-12
- BEH-CHAT-13
- BEH-CHAT-14
- BEH-CHAT-7
- BEH-CHAT-15
- BEH-CHAT-17
- BEH-CHAT-18
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:49Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:50Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:54Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:32Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:35Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:40Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-04 11:00Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 00:01Z
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
- BEH-CHAT-7 (what Kit can and cannot do; 2026-11-03 23:40Z): the card is marked built, with agent instructions and a help line. The note lists what Kit can do: approve, deny (with what is true instead) or answer an item in hand; undo the last thing done to an item (BEH-CHAT-6); show an item again (BEH-CHAT-9); save a note for the coding agent; put a technical question to the coding agent (BEH-SPECAGENT-3); read whole spec cards (BEH-CHAT-8). It cannot reorder, pause, schedule, change priority, or edit the spec; for those it leaves a note and the coding agent makes the change. Check the agent's instructions list exactly these, and nothing it cannot do is offered as a button.
- BEH-CHAT-15 (2026-11-03 23:35Z): an item id written in the chat or on an action card (e.g. BEH-USERS-5) is a link that opens that item. The card is on the list with no build mark seen in this triage; check its built state. Use the item-page route that already exists rather than a new one. Links must work from both places.
- BEH-CHAT-17 (2026-11-04 11:00Z): on a phone, Enter adds a new line and the send button sends; on a desktop, Enter sends. The card does not mention Shift+Enter, which the note asks for on desktop (Shift+Enter adds a new line). Add it to the card's contract or confirm with the owner before building it; do not change the phone behaviour.
- BEH-CHAT-18 (2026-11-06 00:01Z): Kit keeps a rough sense of each user's technical level and pitches replies to it. The card is priority 5, pending, which is the backlog the note asks for. Do not build it here. The note's signals (whether the user hand-edits the repo, how technical their prompts are) are not yet specified; leave them on the card.
