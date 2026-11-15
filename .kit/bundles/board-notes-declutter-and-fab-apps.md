# Board: remove the notes section; floating note button suggests apps
Work-item: board-notes-declutter-and-fab-apps
Flag: sits beside board-pending-columns-and-focus, which changes board columns; this bundle only removes the notes section and adds app suggestions, so the two should not overlap (level: low)
Cards:
- new: the board page has no notes section
- new: when a note names an app, the floating-action note offers that app as a suggestion
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 22:51Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- The floating action button now takes notes, so the notes section on the board is a duplicate. Remove it to declutter the board.
- The owner calls the app suggestion "a good feature": when a note mentions an app, the note-adding action suggests that app. Build it on the floating note only.
- Check board-pending-columns-and-focus before editing the board page, so the column change and this removal do not conflict.
