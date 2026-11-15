# Record a hash of the code each test link exercises, and flag links when that code changes
Work-item: test-link-code-hash-recheck
Flag: records hashes in the spec and shows flagged links to the owner, so it changes what Kit stores and shows (level: high)
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
