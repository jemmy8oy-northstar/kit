# Let a user disconnect their GitHub account
Work-item: github-disconnect-option
Flag: new card has no matching feature in kit.beh (it describes the command line), so it may belong in the hosted app's corpus; the change removes a stored GitHub token (level: high)
Review: escalated — card is a hosted-app Settings screen placed in kit.beh, the command-line corpus (kit:layer technical; its header says not to add browser behaviours) which defines no FEAT-SETTINGS; owner to decide where BEH-ACCOUNT-1 lives (kit-ui.beh or the hosted corpus) and confirm that deleting the stored GitHub token is wanted
Cards:
- BEH-ACCOUNT-1
Notes:
- ticket github-disconnect-option, reply of 2026-11-19 16:53Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 00:10Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
Note: "There should be an option to disconnect your GitHub". Add a Settings option that removes Kit's stored GitHub token for the signed-in user, after a confirm step, and leaves GitHub-side repositories and pull requests untouched. Reconnecting must stay possible.
Owner's answer (2026-11-19 16:53Z): where it lives does not matter, so triage decides; and yes, deleting the stored GitHub token is what he wants. Both are now recorded on BEH-ACCOUNT-1 as contract lines. The escalated review above predates that answer and is kept as the record; no question on this card is open. The card keeps FEAT-SETTINGS, which kit.beh's switch lines name but do not define; a builder should check that before wiring the screen.
Test plan:
- BEH-ACCOUNT-1: precondition: a connected user has a stored GitHub token to remove; if Kit keeps none (kit-ui.beh BEH-USERS-1 and BEH-USERS-3 say it never keeps a token), this fails and the premise is settled before building
- BEH-ACCOUNT-1: disconnect after the confirm step removes the signed-in user's stored token and no other user's
- BEH-ACCOUNT-1: cancelling the confirm step leaves the stored token and the connection in place
- BEH-ACCOUNT-1: disconnect makes no GitHub write; the repositories, branches and pull requests on GitHub are the same before and after
- BEH-ACCOUNT-1: after a disconnect the command reports status Disconnected, and the user can connect GitHub again
- BEH-ACCOUNT-1: the disconnect option is on the Settings page (FEAT-SETTINGS, defined in kit-ui.beh)
