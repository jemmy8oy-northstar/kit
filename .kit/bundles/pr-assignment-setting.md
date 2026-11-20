# Setting to assign the developer to all PRs, and whether they appear in the inbox
Work-item: pr-assignment-setting
Flag: scope (per developer or per project) and placement (kit.beh has no FEAT-SETTINGS) are still unanswered; the 2026-11-20 reply asks "failed? rerun?" with no spec change stated, so no card was edited for it (level: high)
Review: escalated — owner must decide whether to auto-assign the developer to every PR (per developer or per project, default off) since it changes the inbox; also BEH-ASSIGN-1 sits in kit.beh, the command-line-only layer, where FEAT-SETTINGS is not declared, so it likely belongs in the screen-layer file
Cards:
- BEH-ASSIGN-1
Notes:
- ticket pr-assignment-setting, reply of 2026-11-19 16:54Z
- ticket pr-assignment-setting, reply of 2026-11-20 00:05Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 18:27Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- .kit/bundles/pr-assignment-setting.md
Context:
A developer setting chooses whether the signed-in developer is assigned to all PRs, which decides whether those PRs appear in their inbox. No existing card covers PR assignment, so BEH-ASSIGN-1 is new and sits under FEAT-SETTINGS with priority 3 (a guess). Its given/when/then lines use placeholder nouns (assigned:Everything, inbox:Shown) that the parser may refuse until bound; the contract lines carry the intent.
Owner's reply (2026-11-19 16:54Z): default off, yes. The card now says so, and an existing inbox stays the same until a developer turns the setting on. He did not answer the scope question (per developer or per project), so the card keeps per developer and it is still open: ask him before building. The reply also asked why the question was put to him; the question is the one the card needed answered, not a re-check of default off.
Owner's reply (2026-11-20 00:05Z): "failed? rerun?" on the blocked build. It states no spec change, so the spec is unchanged. It reads as a question about the last build run: that run left no list of what was built, so whether it failed is unknown. Default I will act on: do not rerun until he says so; a builder should first record what the run built.
Blocked: no tests written and no code changed. The bundle's premise does not match the code. Kit's Inbox (FEAT-INBOX, BEH-INBOX-1) lists Kit's own cards that need him (review requests, proposals, needs-context lines, paused cards); it does not list GitHub pull requests, so "those PRs appear in their inbox" has no existing screen to appear on. Nothing in backend or ui assigns a PR to a GitHub account either (no assignee call). Building it means a new GitHub write, a per-user setting, and a new PR list in the Inbox, which changes how Kit shows his work and needs the scope answer first. Question for him: should PRs be added to the Inbox, or should the setting only auto-assign and leave the Inbox as it is? Default I will act on if he is silent: do nothing until he answers.

Test plan:
- BEH-ASSIGN-1: with the setting never set, the developer is not assigned to a PR they are not already on, and the inbox is unchanged (default off)
- BEH-ASSIGN-1: turning the setting on assigns the signed-in developer to every open PR, and those PRs appear in their inbox
- BEH-ASSIGN-1: turning the setting on does not change an inbox that was already showing; it changes only after the developer turns it on
- BEH-ASSIGN-1: the setting on one developer does not assign or show PRs for another developer (per developer reading; blocked until he answers the scope question, so do not build the per-project case)
