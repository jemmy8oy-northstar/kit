# Agent keeps the user moving toward the project goal
Work-item: agent-steers-user-toward-goal
Flag: the owner gave the rules in outline only, so the exact prompts and how the narrative looks are not decided (level: high)
Cards:
- new: the agent names the project's goal (the first epic with unbuilt behaviours) and keeps the user pushing toward it
- new: with no epics, the agent asks what the project is working towards, or whether it is maintained
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 10:41Z (agent drives a narrative; rules keep the user pushing; epics show the big goals; probe when there are none)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- The Kit process already defines the goal as the first epic with unbuilt behaviours, and `# kit:goal maintained` for maintained projects. Build the rules on that definition rather than inventing a second one.
- The owner's rules are unreviewed until he confirms them. Default: the agent states the goal in one sentence at the start of each session and ends with the next step toward it, and records the rules it follows as unreviewed assumptions.
- Check the open epics ticket before building, so the goal display does not duplicate epic work.
