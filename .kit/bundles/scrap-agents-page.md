# Scrap the agents page
Work-item: scrap-agents-page
Flag: the owner says the page "adds nothing" in a tentative note, so it may be a doubt rather than a decision, and the card sits in kit-ui.beh, not kit.beh as the triage brief names (level: high)
Review: agreed — "lets scrap it" is a direct instruction, not a doubt, and the delete is held as an unreviewed contract he can deny; kit-ui.beh is the right file (the card moved there from kit.beh), and the bundle already names it
Cards:
- BEH-AGENTPAGE-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-23 22:58Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
- prototypes/behaviour-ast/ui/src/pages/Agents.tsx
- prototypes/behaviour-ast/ui/src/pages/Agents.test.tsx
- prototypes/behaviour-ast/ui
Context:
The owner's note: "I feel like the agents page adds nothing, lets scrap it". The agents page is BEH-AGENTPAGE-1 under FEAT-AGENTS in kit-ui.beh (the card was moved there from kit.beh as BEH-UI-5). Its contract line added here says delete this card. A builder removes the page and its KitAgents route, the navbar link, Agents.test.tsx and the BEH-AGENTPAGE-1 mappings in kit-ui.tests.json, since BEH-GATE-3 makes a mapping to a missing test a hard failure. The feature FEAT-AGENTS goes once it is empty. The page was built on origin branch kit/work/agents-page (its ticket slug is agents-page, not open on the board); check whether that branch is merged into the integration branch before removing code. Nothing else reads the page's data, and the runs and project data it showed are unchanged.
