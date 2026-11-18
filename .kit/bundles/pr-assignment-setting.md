# Setting to assign the developer to all PRs, and whether they appear in the inbox
Work-item: pr-assignment-setting
Flag: unsure which feature the setting belongs to and whether the owner wants inbox contents changed; it touches what the inbox shows (level: high)
Cards:
- BEH-ASSIGN-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 18:27Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- .kit/bundles/pr-assignment-setting.md
Context:
A developer setting chooses whether the signed-in developer is assigned to all PRs, which decides whether those PRs appear in their inbox. No existing card covers PR assignment, so BEH-ASSIGN-1 is new and sits under FEAT-SETTINGS with priority 3 (a guess). Its given/when/then lines use placeholder nouns (assigned:Everything, inbox:Shown) that the parser may refuse until bound; the contract lines carry the intent. Default is off, so current inboxes stay the same. Confirm the owner wants this per developer before building.
