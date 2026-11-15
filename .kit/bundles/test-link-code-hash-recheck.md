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
