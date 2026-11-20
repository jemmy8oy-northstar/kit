# Record a hash of the code each test link exercises, and flag links when that code changes
Work-item: test-link-code-hash-recheck
Flag: records hashes in the spec and shows flagged links to the owner, so it changes what Kit stores and shows (level: high)
Review: agreed — the owner asked for exactly this in the 2026-11-03 23:44Z note; BEH-HEALTH-4 matches it, and the only gap, keeping hashes in <app>.tests.json rather than in the .beh, is already an unreviewed contract he can deny
Cards:
- BEH-HEALTH-4
- BEH-HEALTH-1
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:44Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
- x.tests.json files beside each x.beh (read by BEH-HEALTH-1)
Context:
The owner wants spec and tests coupled at all times, not only when a test is written. BEH-HEALTH-4 already covers the link hash; check its text against the note. The note requires:
- the hash is deterministic: the same code always gives the same hash;
- the spec records which hash each link was last verified against;
- a change to the code the test exercises flags the behaviour and its test link as possibly broken, for quick re-check;
- the owner sees the flagged links; the coding agent computes and records the hashes and does the re-check.
BEH-HEALTH-1 and FEAT-HEALTH count whether a test names a behaviour. They do not detect drift after code changes, so this is an addition to that feature, not a change to its count.
Spec check: BEH-HEALTH-4's contracts match the note (deterministic hash, last-verified hash recorded in <app>.tests.json, flag on change, owner sees flagged links, agent computes and re-checks). No card needed a change; its "my assumption" contracts are unreviewed and stay so.
Test plan:
- BEH-HEALTH-4: hashing the same unchanged source twice gives the same hash, and the hash is the same across line-ending differences (CRLF and LF) and file-listing order.
- BEH-HEALTH-4: changing one byte in a source file the test's coverage run touches makes the link read "possibly broken" on the Health page.
- BEH-HEALTH-4: changing a source file the test's coverage run does not touch leaves the link verified.
- BEH-HEALTH-4: editing the test's own file (so it no longer asserts the behaviour) flags the link, because the test file is in the hashed set.
- BEH-HEALTH-4: a link whose test can no longer be recorded loses its old "verified" hash and reads unrecorded.
- BEH-HEALTH-4: after the agent re-checks and records the new hash, the link reads verified again and <app>.tests.json holds "verified": { "hash", "on" } plus the files hashed over.
- BEH-HEALTH-4: a link recorded against a repo not checked out on Kit's disk reads "not checked" in words and is never counted as all holding.
- BEH-HEALTH-4: the Health page shows the flagged links under its main region, so the owner sees them without opening the file.
- BEH-HEALTH-1: a behaviour with a test that names it counts as covered whether or not it has a recorded hash, and whether or not its link is flagged; the N/M percentage is unchanged by adding or flagging hashes.
Built:
- VerifyLinksTests: The_hash_of_the_same_unchanged_source_is_the_same_every_time (BEH-HEALTH-4)
- VerifyLinksTests: Changing_one_byte_of_a_touched_source_file_flags_the_link (BEH-HEALTH-4)
- VerifyLinksTests: Changing_a_source_file_the_coverage_run_does_not_touch_leaves_the_link_verified (BEH-HEALTH-4)
- VerifyLinksTests: After_a_re_check_and_re_record_the_link_reads_verified_again_with_its_hash_date_and_files (BEH-HEALTH-4)
- LinkHealthTests: A_one_byte_change_to_a_touched_file_lists_the_link_on_the_project_view (BEH-HEALTH-4)
- CoverageTests: BEH_HEALTH_1_A_test_names_its_behaviour_whether_or_not_its_link_is_hashed_or_flagged, two cases: unhashed and flagged with a stale hash (BEH-HEALTH-1)
- Existing tests already covering the other plan lines: CRLF/LF and file order (VerifyLinksTests), test-file edit flags, lost hash on failed record, not-checked other repo (LinkHealthTests), Health page flagged list and not-checked wording (Health.test.tsx).
- Whole suite: .NET 2000 tests passed (Balenthiran.Kit.slnx); UI 561 tests passed (npm test). Both passed.
