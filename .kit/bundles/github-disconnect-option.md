# Let a user disconnect their GitHub account
Work-item: github-disconnect-option
Flag: new card has no matching feature in kit.beh (it describes the command line), so it may belong in the hosted app's corpus; the change removes a stored GitHub token (level: high)
Review: escalated — card is a hosted-app Settings screen placed in kit.beh, the command-line corpus (kit:layer technical; its header says not to add browser behaviours) which defines no FEAT-SETTINGS; owner to decide where BEH-ACCOUNT-1 lives (kit-ui.beh or the hosted corpus) and confirm that deleting the stored GitHub token is wanted
Cards:
- BEH-ACCOUNT-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 00:10Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
Note: "There should be an option to disconnect your GitHub". Add a Settings option that removes Kit's stored GitHub token for the signed-in user, after a confirm step, and leaves GitHub-side repositories and pull requests untouched. Reconnecting must stay possible. The card's contract lines are unreviewed assumptions for the owner to confirm or deny. Feature FEAT-SETTINGS is referenced by kit.beh's switch lines but not defined in this file; confirm the card's placement before building.
