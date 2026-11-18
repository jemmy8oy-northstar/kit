# Agents page: pick an agent, see its recent work and its queue
Work-item: agents-page
Flag: The page is a new view of how Kit shows his work, and the card puts it under FEAT-SERVER because KitUi serves it; unsure whether it should be its own feature (level: high)
Cards:
- BEH-UI-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 00:23Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/ui
Context:
The owner asks for an Agents page: pick an agent from a list, then see what it has done recently, what is unblocked in its queue, and what is in its queue. BEH-UI-5 is a new card in FEAT-SERVER, next to the other KitUi cards, and its contract lines are unreviewed assumptions. A builder should first find how agents and their queues are recorded and read today, since the page should reuse that data rather than add a store. The page must not write anything.
