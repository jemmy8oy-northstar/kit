# Pipeline stages as scheduled workers, and the merge agent
Work-item: pipeline-workers-and-merge-agent
Flag: Whether one backend is pinged by every scheduled job, or each stage has its own endpoint, is an open owner question that changes how Kit runs work; BEH-WORKER-3 takes one backend as the default (level: high)
Cards:
- BEH-WORKER-1
- BEH-WORKER-2
- BEH-WORKER-3
- BEH-MERGE-1
- BEH-MERGE-2
- BEH-MERGE-3
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 08:14Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 08:14Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 08:17Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.notes.md
- .kit/bundles/pipeline-workers-and-merge-agent.md
Context:
No existing card covers these notes, so all six cards are new, added at the end of kit.beh with the review unreviewed line. Note one says pipeline stages should be agents or workers woken by a Kubernetes job schedule, similar to ccb, and asks whether a single backend should be pinged when each job wakes. Note two says the merge agent should merge open PRs into dev and also manage promotion of allowed repos to main. Note three says triage and merge run sequentially while plan and implement can run in parallel for now. Every contract line is an unreviewed assumption the owner can deny. Nothing outside kit.beh and this bundle was edited, and no existing card was changed or deleted.
