# Secrets are added and updated from a page, and never read back
Work-item: secrets-ui
Cards:
- BEH-PROJCONF-1
- BEH-USERS-5
- BEH-BOARD-23
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 18:21Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
The note says a secret can be added or updated from a UI, and cannot be read once written. BEH-USERS-5 already says a user's production secrets are stored for them and never shown back in a page or a log. BEH-PROJCONF-1 says each project has an integrations page where the user configures secrets. So the spec is there; check whether the integrations page exists and whether it is write-only.
"Updated" means replacing a value, and that must not show the old value either. The note does not say which secrets it means, and BEH-USERS-5 says production secrets while BEH-PROJCONF-1 says any project secret, so apply the write-only rule to all of them.
BEH-BOARD-23 covers a card that waits on a secret: it is Blocked, not Done. No secrets source file was located at triage; search backend and ui/src for "secret" before choosing files.
