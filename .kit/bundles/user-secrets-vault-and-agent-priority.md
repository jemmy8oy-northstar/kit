# Vault choice for user secrets, and the user agent ahead of secrets in the schedule
Work-item: user-secrets-vault-and-agent-priority
Flag: this stores users' production secrets, which is how Kit stores data; the vault is the owner's decision and the reorder is his call, so both are recorded as his (level: high)
Cards:
- BEH-USERS-5
- BEH-USERS-9
- BEH-SCHED-2
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:37Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:39Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
- the project's schedule file (path not checked; see BEH-SCHED-2)
Context:
Two owner decisions. Record them as his, not as Kit's guesses.
- Vault (23:37Z): OCI Vault for now. Kit writes secrets to OCI Vault and the cluster reads them; Kit can write a value but never read it back. Keep the storage choice behind a seam, so a move to another vault (e.g. HashiCorp Vault) can happen later. Record this on BEH-USERS-5. Nothing else was decided in that note. It unblocks BEH-USERS-5, BEH-PROJCONF-1 (integrations page) and the secrets part of BEH-USERS-9.
- Priority (23:39Z): lower BEH-USERS-5's priority and reorder the schedule so BEH-USERS-9 (the agent Kit runs for users) is built ahead of it. The owner's reason: getting users the coding agent matters more than secrets right now. The vault decision stays recorded against BEH-USERS-5.
