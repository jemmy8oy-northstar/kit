# Denied items stay findable after they are denied
Work-item: denied-items-findable
Flag: the owner may mean a denied item should be kept in view, or that it should be findable again when he thinks it needs refunding; which one is not clear, and it may belong to questions-tab-and-replies (level: low)
Cards:
- new: a denied item stays in the list and can be found again, not deleted
- new: the deny flow asks for a reason only when the original text input is empty; confirm-to-deny still shows
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 13:15Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-29 08:10Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- The owner denied an item he thought needed refunding and could not find it afterwards. Check first whether denial deletes the item or only hides it from the default view.
- Default: a denied item is kept, marked as denied, and can be listed again from the board or the Questions tab. Record the default as an unreviewed assumption. Do not change how items are deleted elsewhere.
- 10-29 08:10Z: in the inbox, the reason-for-denying text input shows only if the original text input is empty. Confirm-to-deny still shows, at least for now. Default: follow the note and record it as an unreviewed assumption. Find the inbox deny flow in kit-ui.beh before changing it.
