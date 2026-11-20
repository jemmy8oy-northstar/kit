# Record what each Kit session costs
Work-item: record-session-cost
Flag: BEH-COST-1 says sessions already run count, but a runner's ~/.claude/ is gone when its run ends, so past runner sessions may not be recoverable; and its contracts assume Claude Code reports a dollar cost, which the bundle says may not hold (token usage only) (level: high)
Review: revised — past-session recovery scoped to dev machines; new runner sessions take the dollar cost from the run's own result; token-only past records left unpriced by default, raised as a question
Cards:
- BEH-COST-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-04 10:58Z
- ticket record-session-cost, reply of 2026-11-19 16:49Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
The owner wants to start recording what the Claude Code bot costs for each Kit session, so that weekly cost can be worked out from history and compared with subscription price. The note's worry is garbled ("api incurs a much lower cost than if you were using api"); the likely reading is that subscription and API prices differ a lot. The figures are for checking the pricing model, not for billing.
Storage (answered by the owner's reply of 2026-11-19 16:49Z, "On the db"): the cost record lives in the database on Kit's server, not in any repo and not in a runner's ~/.claude/. This sits beside the per-user store behind /api/me/memory. Build it so the record is written there before a runner exits.
Where the cost comes from (revised at review):
- New sessions Kit starts: take the dollar cost from the run's own end-of-run result (headless Claude Code reports a total cost there), and write the record before the runner exits. Do not rely on the runner's ~/.claude/, which is gone when the run ends.
- Past runner sessions: not recoverable. Their ~/.claude/ is gone, so contract 2 ("sessions already run count") holds only for sessions run on a machine that still has its ~/.claude/, such as the owner's dev machine.
- Past sessions on a dev machine: the records under ~/.claude/ likely hold token usage per message, not a dollar cost. Pricing them from tokens would be an estimate, which contract 4 rules out. Default if the owner stays silent: import only past sessions that carry a reported dollar cost, skip token-only ones, and say how many were skipped. The builder raises this as a question on BEH-COST-1 in the corpus ("price past token-only sessions at API rates as a marked estimate, or skip them?") and does not quietly price the tokens.
Card BEH-COST-1 is new, in a new FEAT-COST feature (stage ui, copied from its siblings in kit.beh). The contract lines are unreviewed assumptions: one record per session with date, project and model; the record lives in the database on Kit's server, outside the project's repo; Kit stores the reported cost, not an estimate, and not the subscription price.
The card is written in the pending shape used in ideas.beh because no spec noun yet binds a session. A builder should add given/when/then only once the nouns exist.
Next step toward the goal: the open chat and tester work come first; this card is a cost check for the pricing question, so it can follow them.
Built:
- BEH_COST_1_a_finished_run_writes_one_record_with_its_date_project_model_and_the_reported_cost_and_no_estimate (SessionCostTests)
- BEH_COST_1_the_record_is_in_the_store_before_the_report_is_answered_and_still_exists_once_claude_home_is_removed (SessionCostTests)
- BEH_COST_1_no_file_is_written_under_the_project_repo_for_a_cost_record (SessionCostTests)
- BEH_COST_1_a_new_sessions_cost_is_the_one_the_run_reported_and_claude_home_is_never_read (SessionCostTests)
- BEH_COST_1_a_failed_run_that_reports_a_cost_is_recorded_too (SessionCostTests)
- BEH_COST_1_a_cost_that_is_not_a_non_negative_number_is_refused_and_nothing_is_recorded (SessionCostTests, two cases)
- BEH_COST_1_a_run_reported_twice_is_recorded_once (SessionCostTests)
- BEH_COST_1_the_subscription_price_is_not_a_field_of_the_record (SessionCostTests)
- BEH_COST_1_the_postgres_store_keeps_one_record_per_run (SessionCostTests; passes without a database, runs only with KIT_TEST_DATABASE set, not run here)
- BEH_COST_1_past_sessions_with_a_reported_cost_are_summed_by_week_and_match_those_costs (SessionCostTests)
- BEH_COST_1_a_past_session_with_only_token_usage_is_skipped_and_counted_and_never_priced_from_its_tokens (SessionCostTests)
- BEH_COST_1_no_import_of_past_runner_sessions_is_attempted_by_the_runner_or_the_server (SessionCostTests)
- BEH_COST_1_the_run_writes_the_dollar_cost_its_own_result_reports_and_not_what_is_under_home (RunnerScriptTests)
- BEH_COST_1_a_result_with_no_reported_cost_writes_no_cost_file (RunnerScriptTests)
- BEH_COST_1_the_pool_reports_the_run_cost_with_its_usage_and_bundles (RunnerScriptTests)
Whole backend suite (dotnet test, 2030 tests): 2027 passed, 3 failed; the 3 failures are environmental and also fail on the untouched branch here (no ICU library, no `kill` binary in this sandbox): ClaimerSidecarTests.A_claim_never_reported_is_reported_failed_when_the_pod_is_stopped, ReportConformanceTests.Globalization_is_ICU_so_locale_order_is_reachable, ReportConformanceTests.Report_reproduces_Node_on_the_branches_the_goldens_miss.

Test plan:
- BEH-COST-1: a session Kit starts writes one record holding its date, project, model and the cost Claude Code reports for it, and no estimate
- BEH-COST-1: the record is in the database on Kit's server before the runner exits, and still exists after the runner's ~/.claude/ is removed
- BEH-COST-1: the record is in no project repo; no file is written to the project's repo for it
- BEH-COST-1: a new session's cost is taken from the run's end-of-run result, not from ~/.claude/
- BEH-COST-1: past sessions on a dev machine that carry a reported dollar cost are read from ~/.claude/ and summed by week, matching those costs
- BEH-COST-1: a past session record that holds token usage with no dollar cost is skipped and counted in what is reported, and no cost is priced from the tokens
- BEH-COST-1: no import of past runner sessions is attempted
- BEH-COST-1: the subscription price is never stored in the record
