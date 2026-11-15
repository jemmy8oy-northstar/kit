# Chat and action cards: posted answers, item links, what Kit can do, phone Enter
Work-item: chat-answers-links-capabilities-input
Flag: these cards already exist and cite the notes, so this may be a check against existing wording rather than new work; the BEH-CHAT-7 reading is mine (level: low)
Review: revised — all six cards already match their notes, so no card text changes; corrected the BEH-CHAT-17 context line, as the card already says Shift+Enter. The BEH-CHAT-7 can/cannot reading matches the 23:40Z note. BEH-CHAT-12 shows a tag, which has an existing raised conflict in tracks.beh, so leave it.
Cards:
- BEH-CHAT-12
- BEH-CHAT-13
- BEH-CHAT-14
- BEH-CHAT-15
- BEH-CHAT-7
- BEH-CHAT-17
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:32Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:35Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:40Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-04 11:00Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/tracks.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
Chat and action-card changes from the owner's review of 2026-11-03, plus his chat-input and capability asks. The cards above already exist and cite these notes. Check each card's text against its note and change only what differs; do not add duplicate cards.
- BEH-CHAT-12: on request, the chat shows a posted answer to an item, or a note's text, answer and tag, without opening the note.
- BEH-CHAT-13: when an answer is added to a note in an action card, the card shows the text that was added.
- BEH-CHAT-14: no "next up in Kit" section; the next item comes up in the chat anyway.
- BEH-CHAT-15: an item ID written in the chat or on an action card (e.g. BEH-USERS-5) is a link that opens that item.
- BEH-CHAT-7: the chat knows what Kit can do. Can: approve, deny (with what is true instead) or answer an item in hand; undo the last thing done to an item; show an item again; save a note for the coding agent; put a technical question to the coding agent; read whole spec cards. Cannot: reorder, pause, schedule or change the priority of items, or edit the spec. For those it leaves a note and the coding agent makes the change.
- BEH-CHAT-17: on a phone, Enter adds a new line and the send button sends; on a desktop, Enter sends and Shift+Enter adds a new line. The card already says Shift+Enter adds a new line; nothing to add.
Test plan:
- BEH-CHAT-12: asking the Chat for a posted answer or a note shows its text, answer and tag in the thread, and the note is not opened.
- BEH-CHAT-13: after an answer is posted to a note on an action card, the card shows the answer's full text.
- BEH-CHAT-14: after an item is dealt with, no "Next up in Kit" text appears, and the next item still comes up in the thread.
- BEH-CHAT-15: an item id known to Kit, in chat text or on an action card, renders as a link to its item page; an unknown id stays plain text.
- BEH-CHAT-7: the help line lists the can and cannot items; asking for a cannot item leaves a note for the coding agent and does not claim the change was made.
- BEH-CHAT-17: with a coarse pointer, Enter adds a new line and does not send; with a fine pointer, Enter sends and Shift+Enter adds a new line.
