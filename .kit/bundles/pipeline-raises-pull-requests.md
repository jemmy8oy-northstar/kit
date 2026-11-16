# Pipeline raises pull requests for finished work and completes them only where allowed
Work-item: pipeline-raises-pull-requests
Flag: "complete" may mean merge, which the process forbids without a corpus line; the owner should say which (level: high)
Cards:
- BEH-PR-1
- BEH-PR-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 11:28Z (pipeline can raise and complete PRs for completed work)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
New cards in kit.beh only. They have no tests yet, so kit check kit fails BEH-GATE-1 until a builder maps them.
The process never merges an agent's own work unless the corpus says so, so BEH-PR-2 keeps merge behind that line. Existing BEH-ORCH cards in kit-ui.beh may cover part of this and were not read.
Next step toward the goal: the owner says whether complete means merge; a builder then raises a PR for one finished card.
