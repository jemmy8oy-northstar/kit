# Users: secrets vault and hosted databases
Work-item: user-secrets-and-database-hosting
Flag: The 2026-10-26 22:50Z note is tagged BEH-USERS-3, but its client ID and secret are the kit-github-connect secret that BEH-USERS-2 describes. Confirm the card before filing.
Cards:
- BEH-USERS-5
- BEH-USERS-6
- BEH-USERS-2
- BEH-BOARD-23
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-26 22:50Z · BEH-USERS-3
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-26 23:00Z · BEH-USERS-6
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-28 23:53Z · BEH-USERS-5
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-29 08:04Z · BEH-USERS-6
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-31 01:00Z · BEH-USERS-5
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-31 01:07Z · BEH-USERS-6
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
Context:
BEH-USERS-5 (production secrets) and BEH-USERS-6 (per-user databases) are both unbuilt, with no test lines in kit-ui.tests.json.
- Secrets: the owner says this is not Kubernetes secrets and the future direction is a vault. He asked about OpenBao and Infisical, and said he was thinking OCI or HashiCorp. A later note in the same file (2026-11-03 23:37Z, outside this batch and not triaged here) records OCI Vault for now, behind a seam so another vault can replace it. Keep that decision on BEH-USERS-5. Do not switch to OpenBao or Infisical.
- Databases: the owner asks whether outsourcing is better, whether the user should bring their own account or Kit should spin up instances, which suits pricing, and whether Neon fits. BEH-USERS-6 already records "their own account first" (kit-ui.beh contract near line 1557). Answer the questions on the card. Check Neon's pricing and terms from its current docs rather than from memory. Do not pick a provider without the owner.
- BEH-BOARD-23 (a built card waiting on the owner for a secret goes to Blocked, not Done) is already in place. Its contract records it was built on 2026-10-26. The 2026-10-26 22:50Z note is the rule's source. Reply that the rule is in place.
- BEH-USERS-2 owns the kit-github-connect client ID and secret (kit-ui.beh near line 1312). If the owner's secret belongs there, say so on that card.
