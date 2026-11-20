# Pipeline raises pull requests for finished work and completes them only where allowed
Work-item: pipeline-raises-pull-requests
Flag: "complete" may mean merge, which the process forbids without a corpus line; the owner should say which (level: high)
Review: agreed — the safe reading is already an unreviewed contract on BEH-PR-2 (merge only where the corpus allows, else leave open), so the owner can deny it without holding up BEH-PR-1
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
Built:
- BEH_PR_1_finished_work_raises_exactly_one_pull_request_and_the_pull_request_names_the_card_it_builds (PipelineTests.cs)
- BEH_PR_1_the_pull_request_targets_the_integration_branch_the_run_started_from_and_never_main (PipelineTests.cs)
- BEH_PR_1_work_that_is_not_finished_raises_no_pull_request (PipelineTests.cs)
- BEH_PR_2_a_raised_pull_request_with_merging_off_is_left_open_and_nothing_is_merged (PipelineTests.cs)
Suite: the whole C# suite ran 2018 tests, 2015 passed and 3 FAILED (ReportConformanceTests x2, ClaimerSidecarTests x1: no `kill` binary here and an ICU check; the same 3 fail with these test changes stashed). Not a clean pass.
Test plan:
- BEH-PR-1: finished work raises exactly one pull request, and the pull request names the card it builds.
- BEH-PR-1: the pull request targets the project's integration branch and never the default branch (main).
- BEH-PR-1: work that is not finished raises no pull request.
- BEH-PR-2: a raised pull request with no corpus line allowing merge is left open and reports pullRequest:Left, and nothing is merged.
- BEH-PR-2: a raised pull request with a corpus line allowing merge is merged; BEH-PR-2's "then reports pullRequest:Left" does not cover this path, so the expectation waits on the owner's answer to the Flag.
- BEH-PR-2: a pull request the pipeline itself opened is never merged on green checks alone when the corpus has no merge line.
