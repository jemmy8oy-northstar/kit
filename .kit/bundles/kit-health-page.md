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
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
The note asks for a Kit health feature that shows what percentage of features or behaviours have test coverage, and for the agent to schedule test improvement when it is not busy with new requests or priority work. James calls this tech debt, and says the point of Kit is that the spec drives the tests and the tests drive the code.
The health feature is FEAT-HEALTH in kit-ui.beh near line 1972. BEH-HEALTH-1 gives the share of behaviours a test names, per project and in total. BEH-HEALTH-2 gives the idle test work to the project with the worst health. BEH-HEALTH-3 lists the behaviours no test names. BEH-HEALTH-4 flags a test link when the code it exercises changes. Each project's header names where its tests live (the BEH-HEALTH-1 comment in kit.beh, line 9).
The note does not say whether these cards are built. Check the built state of each before writing code. The idle scheduling in BEH-HEALTH-2 depends on the agent's idle settings (BEH-SETTINGS-5 and -6, work item agent-wake-and-notifications).
