# Each project says what it is working towards, and the agent asks when it has no goal
Work-item: project-narrative-goals
Flag: Much of the note is already contracted as built (kit-ui.beh near lines 2375 and 2388); confirm what is left before a spec is written.
Cards:
- FEAT-NARRATIVE
- BEH-NARR-1
- BEH-NARR-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 10:41Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- backend/Balenthiran.Kit.Services/Process/agent.md
Context:
The note asks for the agent to drive a narrative: epics show the big goals, and when there are none the agent should probe what the project is working towards, or whether it is being maintained. It also asks for rules that keep the user pushing in the right direction.
FEAT-NARRATIVE is in kit-ui.beh near line 2348. BEH-NARR-1 says each project shows what it is working towards, taken from its open epics. BEH-NARR-2 asks the goal question when a project has none. Answering "just maintaining it" writes `# kit:goal maintained` into the project's own file (contract near kit-ui.beh:2375).
The dev process that every user's agent follows has a "Keep the user moving toward their goal" section (contract near kit-ui.beh:2388, served from backend/Balenthiran.Kit.Services/Process/agent.md). The rules the note asks for are probably already there. Compare the wording against the note before adding a card.
