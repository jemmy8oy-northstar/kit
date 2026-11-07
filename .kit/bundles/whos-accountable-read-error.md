# The whos-accountable project shows "can't be read" in Kit
Work-item: whos-accountable-read-error
Flag: The note gives no error text or page, so which read failed is unknown; reproduce it before choosing a card.
Cards:
- BEH-PULL-3
- BEH-PULL-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 22:52Z
Files:
- prototypes/behaviour-ast/behaviours/whos-accountable.beh
- prototypes/behaviour-ast/behaviours/whos-accountable.tests.json
- prototypes/behaviour-ast/ui/src/realProjects.ts
- prototypes/behaviour-ast/ui/src/projectFiles.ts
Context:
"Getting who's accountable can't be read error rn". who's accountable is a separate Kit project, not a board field: its corpus is whos-accountable.beh, with kit:repo jemmy8oy-northstar/whos-accountable at dev.
If the project is private, BEH-PULL-3 says it is served only against a live installation token, so an expired or missing token is one cause. BEH-PULL-1 says the project list comes from GitHub, so the list can fail the same way.
Other causes to rule out: the reader refusing when its two counts disagree (BEH-READ-1), and the coverage read failing (BEH-UI-4). Check the token and the repo's access first, then the reader, before changing any code.
