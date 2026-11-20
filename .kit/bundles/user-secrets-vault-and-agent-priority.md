# Vault choice for user secrets, and the user agent ahead of secrets in the schedule
Work-item: user-secrets-vault-and-agent-priority
Flag: this stores users' production secrets, which is how Kit stores data; the vault is the owner's decision and the reorder is his call, so both are recorded as his (level: high)
Review: revised — both owner decisions are already on the cards and schedule as his (notes 23:37Z, 23:39Z), so no one needs to decide anything; Files now names kit-ui.schedule, which lists USERS-9 (line 8) before USERS-5 (line 11)
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
- prototypes/behaviour-ast/behaviours/kit-ui.schedule
Context:
Two owner decisions. Record them as his, not as Kit's guesses.
- Vault (23:37Z): OCI Vault for now. Kit writes secrets to OCI Vault and the cluster reads them; Kit can write a value but never read it back. Keep the storage choice behind a seam, so a move to another vault (e.g. HashiCorp Vault) can happen later. Record this on BEH-USERS-5. Nothing else was decided in that note. It unblocks BEH-USERS-5, BEH-PROJCONF-1 (integrations page) and the secrets part of BEH-USERS-9.
- Priority (23:39Z): lower BEH-USERS-5's priority and reorder the schedule so BEH-USERS-9 (the agent Kit runs for users) is built ahead of it. The owner's reason: getting users the coding agent matters more than secrets right now. The vault decision stays recorded against BEH-USERS-5.
Card check (cards not changed, the owner's two decisions are already in them and in kit-ui.schedule on main, commit 14fbe9e5):
- BEH-USERS-5 already records the OCI Vault answer (kit-ui.notes.md#2026-11-03-2337), the write-only rule and the seam, and has priority 3 with BEH-USERS-9 (priority 2) ahead of it on the schedule. Stale: its line "paused until 2026-11-14" has passed (today is 2026-11-16).
- BEH-PROJCONF-1 and BEH-EXTDATA-1 still say they "wait on your vault choice", which is now made.
- The notes say the vault unblocks "the secrets part of BEH-USERS-9", but BEH-USERS-9 has no secrets contract; only BEH-USERS-5 says other users' secrets come with it.

Test plan:
- BEH-USERS-5: the card records the OCI Vault answer with its source note, that Kit writes a value but never reads it back, and that the storage choice sits behind a seam.
- BEH-USERS-5: its priority is 3, below BEH-USERS-9's 2, as the owner asked at 23:39Z.
- BEH-USERS-9: it is pending at priority 2 and stands before BEH-USERS-5 on kit-ui.schedule.
- BEH-SCHED-2: the schedule reorder changes only order; the set of ids on kit-ui.schedule is the same before and after, with none added, dropped or duplicated, and BEH-USERS-9 still ahead of BEH-USERS-5 (already true on main, so the change is expected to be a no-op).

Built:
- BEH_USERS_5_records_the_OCI_Vault_answer_with_its_source_note (backend/Balenthiran.Kit.Tests/CommittedArtefactTests.cs)
- BEH_USERS_5_says_Kit_writes_a_secret_but_can_never_read_it_back
- BEH_USERS_5_keeps_the_storage_choice_behind_a_seam
- BEH_USERS_5_is_pending_at_priority_3_below_BEH_USERS_9_as_the_owner_asked
- BEH_USERS_9_is_pending_at_priority_2_and_stands_before_BEH_USERS_5_on_the_schedule
- BEH_SCHED_2_the_schedule_lists_each_id_once_and_each_is_a_behaviour_in_the_corpus
- BEH_SCHED_2_a_reorder_of_the_real_schedule_keeps_the_same_ids (action "up")
- BEH_SCHED_2_a_reorder_of_the_real_schedule_keeps_the_same_ids (action "down")
- Whole suite: backend 2001 tests, 1998 passed, 3 failed (ReportConformanceTests x2, ClaimerSidecarTests x1; the same 3 fail without this change: no ICU library and no kill binary in this sandbox); UI vitest 561 tests, all passed; kit check kit-ui passes.
