# Pipeline stages as scheduled workers, and the merge agent
Work-item: pipeline-workers-and-merge-agent
Flag: BEH-WORKER-1 and BEH-WORKER-3 wake workers from a scheduled Kubernetes job and a backend, while kit-ui.beh (his answer kit#672, 2026-11-05, "I feel like the pool makes more sense"; kit#686, "run pods do not have control plane access") puts runs on a fixed pool of runner pods with no Kubernetes rights for Kit; whether pipeline stages may use a schedule and a backend is his decision, so no card is built until he answers (level: high)
Review: escalated — he must decide whether pipeline stages may be woken by a scheduled Kubernetes job pinging one backend (BEH-WORKER-1, BEH-WORKER-3) given his kit#672 runner-pool and kit#686 no-control-plane answers; the hold is too wide, since BEH-WORKER-2 and BEH-MERGE-1..3 do not touch the pool or Kubernetes rights, and kit.beh has no kit:conflicts line so the default is build. Silence: build BEH-WORKER-2 and BEH-MERGE-1..3 now, build BEH-WORKER-1 and BEH-WORKER-3 on the pool reading (the schedule fires through the one backend, which hands the stage to a runner pod in the existing pool, and Kit gets no Kubernetes rights), and raise a review request on those two cards naming them and the kit-ui.beh pool cards by id with that reading
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
Moved here from the bundle header so that it has one Flag line: the three cited 2026-11-18 notes are not in kit.notes.md on this branch or main, so the six cards cannot be checked against their source (level: low)
No existing card covers these notes, so all six cards are new, added at the end of kit.beh with the review unreviewed line. Note one says pipeline stages should be agents or workers woken by a Kubernetes job schedule, similar to ccb, and asks whether a single backend should be pinged when each job wakes. His reply settles that: one stateless backend, which works well for now and can change later; scaling is not built now (BEH-WORKER-3 says so). Note two says the merge agent should merge open PRs into dev and also manage promotion of allowed repos to main. Note three says triage and merge run sequentially while plan and implement can run in parallel for now. Every contract line is an unreviewed assumption the owner can deny. Nothing outside kit.beh and this bundle was edited, and no existing card was changed or deleted.
Test plan:
- BEH-WORKER-1: a scheduled job for a pipeline stage starts that stage's worker, and no stage runs as an always-on process
- BEH-WORKER-2: a worker woken by its schedule does one unit of work, exits, and keeps nothing in memory or on disk between wakes
- BEH-WORKER-3: every scheduled job pings the one backend, and each ping names the worker to start and carries no work of its own
- BEH-MERGE-1: the merge agent merges open pull requests into dev and leaves main untouched
- BEH-MERGE-2: the merge agent promotes dev to main only for repos the project allows, and does nothing for other repos
- BEH-MERGE-3: triage and merge never run at the same time, while plan and implement may run in parallel
