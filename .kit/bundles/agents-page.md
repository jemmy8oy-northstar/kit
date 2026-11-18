# Agents page: pick an agent, see its recent work and its queue
Work-item: agents-page
Flag: The page is a new view of how Kit shows his work, and the card puts it under FEAT-SERVER because KitUi serves it; unsure whether it should be its own feature (level: high)
Review: revised — a page belongs in kit-ui.beh as its own page feature, not in kit.beh's FEAT-SERVER: kit.beh's header says it is the command-line layer and must not gain browser behaviours
Cards:
- BEH-UI-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 00:23Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/pages/Activity.tsx
- prototypes/behaviour-ast/ui/src/pages/Runs.tsx
- prototypes/behaviour-ast/ui
Context:
The owner asks for an Agents page: pick an agent from a list, then see what it has done recently, what is unblocked in its queue, and what is in its queue. BEH-UI-5 was written as a new card in kit.beh's FEAT-SERVER, and its contract lines are unreviewed assumptions. A builder should first find how agents and their queues are recorded and read today, since the page should reuse that data rather than add a store. The page must not write anything.
Placement (review): kit.beh is the technical, command-line layer. Its header says not to add browser behaviours there, and that kit-ui.beh holds the UI. In kit-ui.beh each page is its own feature (FEAT-ACTIVITY, FEAT-INBOX, FEAT-HEALTH, ...). So the builder moves BEH-UI-5 out of kit.beh into kit-ui.beh under a new feature such as FEAT-AGENTS "Agents", writes it with that file's browser verbs, and keeps its two contract lines as unreviewed assumptions. Its id may change to match kit-ui.beh's numbering; say so in the PR. Before adding new reads, check the Activity and Runs pages, which may already show agent work.

Test plan:
- BEH-UI-5: the Agents page lists the agents it knows, one per entry, and shows no recent work or queue until one is picked
- BEH-UI-5: picking an agent shows its recent runs from GET /api/runs, newest first, each with status, stage and cost, and only runs for that agent's project and work items
- BEH-UI-5: the queue shows approved and pending cards with no needs-context line as unblocked, in board order, and cards with a needs-context line as waiting on him, read from the same project data the project view reads
- BEH-UI-5: an agent with no runs shows "No recent work", and an empty queue shows "Nothing in the queue", not a blank section
- BEH-UI-5: browsing the page issues only GET requests; no POST, PUT or PATCH is sent from it, and no corpus file changes
- BEH-UI-5: the card's text and contracts sit in kit-ui.beh under the new FEAT-AGENTS page feature with browser verbs, not in kit.beh FEAT-SERVER; the builder moves it and records any id change in the PR
