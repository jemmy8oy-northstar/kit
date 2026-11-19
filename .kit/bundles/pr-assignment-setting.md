# Setting to assign the developer to all PRs, and whether they appear in the inbox
Work-item: pr-assignment-setting
Flag: scope (per developer or per project) and placement (kit.beh has no FEAT-SETTINGS) are still unanswered; the setting changes what the inbox shows (level: high)
Review: escalated — owner must decide whether to auto-assign the developer to every PR (per developer or per project, default off) since it changes the inbox; also BEH-ASSIGN-1 sits in kit.beh, the command-line-only layer, where FEAT-SETTINGS is not declared, so it likely belongs in the screen-layer file
Cards:
- BEH-ASSIGN-1
Notes:
- ticket pr-assignment-setting, reply of 2026-11-19 16:54Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 18:27Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- .kit/bundles/pr-assignment-setting.md
Context:
A developer setting chooses whether the signed-in developer is assigned to all PRs, which decides whether those PRs appear in their inbox. No existing card covers PR assignment, so BEH-ASSIGN-1 is new and sits under FEAT-SETTINGS with priority 3 (a guess). Its given/when/then lines use placeholder nouns (assigned:Everything, inbox:Shown) that the parser may refuse until bound; the contract lines carry the intent.
Owner's reply (2026-11-19 16:54Z): default off, yes. The card now says so, and an existing inbox stays the same until a developer turns the setting on. He did not answer the scope question (per developer or per project), so the card keeps per developer and it is still open: ask him before building. The reply also asked why the question was put to him; the question is the one the card needed answered, not a re-check of default off.
