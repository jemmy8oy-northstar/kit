# Kit health: test coverage per feature and behaviour, and idle test-health work
Work-item: kit-health-test-coverage
Flag: scheduling idle test-health work is a choice about how the agent spends its time, and the owner has not confirmed it; build the view first (level: high)
Cards:
- new: a Kit health view shows the percentage of features and behaviours with test coverage
- BEH-GATE-1
- new: when no request or priority work is waiting, Kit schedules test-health work
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:33Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- The owner wants Kit to have a reason to work on test generation: a health view with the share of features and behaviours that have tests.
- BEH-GATE-1 already fails the build for a behaviour with no test. The health view should count the same mapping rather than invent a second one.
- Idle scheduling is tech debt by his own description. Build the view first; hold the idle scheduling until he confirms it, and record that hold as an unreviewed question with the default that it stays off.
