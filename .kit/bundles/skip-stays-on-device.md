# A skip stays on the user's device, and skips already written to the repo are undone
Work-item: skip-stays-on-device
Flag: undoing skips already in the repo means choosing which spec changes to revert; the owner decides which (level: high)
Cards:
- new: a skip is kept on the user's device only, and is not written to the spec or the repo
- new: a skip already written to the repo is undone, and the spec goes back to its state before the skip
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-29 14:56Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-29 22:08Z
Files:
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
- 10-29 14:56Z: a skipped card is skipped only temporarily on the user's device, so the user's view can differ from the agent's. Nothing is marked in the spec. That is the intended behaviour.
- 10-29 22:08Z: asks whether any skipped items were marked skipped in the specs or repo. They should not have been, and this should be undone. Builder: find where skip writes to the spec or repo and stop it. List the commits that wrote skip markers for the owner before reverting anything, since reverting changes the spec. Default: revert only the skip markers, not other edits.
- Both notes concern the same store, so they share one bundle.
