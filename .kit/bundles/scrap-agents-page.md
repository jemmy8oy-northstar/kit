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
Test plan:
- BEH-AGENTPAGE-1: Agents.test.tsx is removed and no test under ui/src names BEH-AGENTPAGE-1, so the vitest count drops by the 7 Agents tests and nothing else.
- BEH-AGENTPAGE-1: kit-ui.tests.json has no BEH-AGENTPAGE-1 key, so no mapping names a missing file (BEH-GATE-3 passes).
- BEH-AGENTPAGE-1: kit-ui.beh has no BEH-AGENTPAGE-1 and no FEAT-AGENTS, and the corpus still parses (Every_committed_corpus_parses and the stage-0 gate `check kit`).
- BEH-AGENTPAGE-1: App.tsx has no KitAgents route and no import of pages/Agents, and the navbar has no agents link.
- BEH-AGENTPAGE-1: the navbar still shows Runs (BEH-USERS-9), and the Runs page still reads GET /api/runs unchanged.
- BEH-AGENTPAGE-1: the full UI suite (npm test in prototypes/behaviour-ast/ui) passes after removal, with no test importing the deleted page.
