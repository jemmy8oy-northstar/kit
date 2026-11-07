# The bell raises an item to the agent, and comments sit under items
Work-item: bell-and-item-comments
Flag: Note 19:33Z covers three things: the bell, comments under items, and a floating note button. The bundle takes the note whole, so the reviewer may want comments split into their own bundle.
Cards:
- FEAT-BELL
- BEH-BELL-1
- BEH-BELL-2
- BEH-BELL-3
- FEAT-COMMENTS
- BEH-COMMENT-1
- BEH-COMMENT-2
- BEH-NOTE-12
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 19:33Z (line 145)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-21 06:50Z (line 163)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/components/BellButton.tsx
- prototypes/behaviour-ast/ui/src/components/NoteFab.tsx
- prototypes/behaviour-ast/ui/src/components/ItemNotes.tsx
Context:
Both notes ask for a way to bring an item to the agent's attention. BEH-BELL-1 says ringing an item's bell asks the agent to look at it again. BEH-BELL-2 says a rung item is either reprioritised or answered with why not, which is the nudge the 19:33 note describes. BEH-BELL-3 keeps a rung card at the top of its column until the bell is answered. The 06:50 note ("make this higher priority") is the same card.
The 19:33 note's later idea that the bell lets the AI play with an item in the UI is not specced and not on any card. Leave it out unless asked.
Comments: BEH-COMMENT-1 gives epics, features, behaviours and projects their own comments, and BEH-COMMENT-2 has the agent read and answer them. A floating button adds a note from any page (BEH-NOTE-12). The card text does not spell out the difference between a comment, which sits under one item, and a note, which is a free thought the agent turns into something else. Check the contract text before building.
