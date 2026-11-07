# Kit health: the share of specs with tests, and test work when the agent is idle
Work-item: kit-health-page
Flag: The note asks for the share of features as well as behaviours with a test, and BEH-HEALTH-1 counts behaviours only; confirm the feature share is wanted before its card is written.
Cards:
- BEH-HEALTH-1
- BEH-HEALTH-2
- BEH-HEALTH-3
- BEH-HEALTH-4
- new: the health page also shows, per project and in total, the share of features that have at least one behaviour a test names
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:33Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:44Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
The note asks for a Kit health feature that shows what percentage of features or behaviours have test coverage, and for the agent to schedule test improvement when it is not busy with new requests or priority work. James calls this tech debt, and says the point of Kit is that the spec drives the tests and the tests drive the code.
The health feature is FEAT-HEALTH in kit-ui.beh near line 1972. BEH-HEALTH-1 gives the share of behaviours a test names, per project and in total. BEH-HEALTH-2 gives the idle test work to the project with the worst health. BEH-HEALTH-3 lists the behaviours no test names. BEH-HEALTH-4 flags a test link when the code it exercises changes. Each project's header names where its tests live (the BEH-HEALTH-1 comment in kit.beh, line 9).
The note does not say whether these cards are built. Check the built state of each before writing code. The idle scheduling in BEH-HEALTH-2 depends on the agent's idle settings (BEH-SETTINGS-5 and -6, work item agent-wake-and-notifications).
Added in triage from the 2026-11-03 23:44Z note (from chat), which asks for spec and tests to stay coupled after the test is written, not only when it is first written. BEH-HEALTH-4 (kit-ui.beh near line 2025) already says a test link records a hash of the code it exercises and a change to that code flags the link. Check that card against the note's three extra requirements, and add whichever is missing to BEH-HEALTH-4's contract rather than making a new card:
- the hash is deterministic: the same code always gives the same hash;
- the spec records which hash each link was last verified against;
- the coding agent computes and records the hashes and does the re-check, and the owner sees the flagged links.
The note says this is an addition to FEAT-HEALTH and BEH-HEALTH-1, because those count whether a test names a behaviour and do not detect when a test has stopped enforcing it after the code changes. Keep the count on BEH-HEALTH-1 as it is.
