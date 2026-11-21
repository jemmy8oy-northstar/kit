# Ticket tests failing or merge conflicts need a strategy
Work-item: ticket-test-failure-and-merge-conflict-strategy
Flag: the note does not say which stage or which conflict it means, and no open ticket names this strategy; the new card's wording is my reading of "a strategy to resolve this" (level: high)
Review: revised — the reading stands as an unreviewed card he can deny, but Context wrongly said no card covers conflicts: BEH-ORCH-11 already gives the merge job an agent that resolves them, so the card builds on that
Cards:
- BEH-TICKET-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-21 13:31Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-ORCH-10 holds the current stop reasons for failed runs and conflicts; BEH-ORCH-11 names the merge job that brings a branch up to date with dev and has an agent resolve a conflict; neither is edited here)
Context:
Note text: "Ticket tests are failing or merge conflicts kit needs a strategy to resolve this". No existing card says what the pipeline does when a ticket's tests fail. For conflicts, BEH-ORCH-11 already says the merge job (built to merged, not built yet) brings the branch up to date with dev and has an agent resolve a conflict, and BEH-ORCH-10 as built stops the item on a failed check or a conflict and leaves the pull request open. BEH-TICKET-6 adds what happens after that: retry, then block with a question. Its conflict half is that merge job, not a second mechanism, and BEH-ORCH-10's stop becomes the point where the retry starts. Tests fail in the build run or in the Actions checks the merge step reads. The plan stage (technical design) only writes the test plan, so the card's words "in technical design or implementation" mean those two places. BEH-TICKET-3 and BEH-TICKET-4 already define the blocked tag and the question route, so the new card reuses them. BEH-TICKET-6 is the id chosen because BEH-TICKET-5 is already named by the open ticket bundle-malformed-resolve. Builders should read BEH-TICKET-3/4, BEH-ORCH-10 and BEH-ORCH-11 before writing code. The two open questions on the card (conflict in kit.beh, attempt count) are left to the owner with the defaults written on it.

Test plan:
- BEH-TICKET-6: a build run whose tests fail sends the ticket back to implementation, records the failing test names on the ticket, and does not move it on to built
- BEH-TICKET-6: a retry whose tests then pass moves the ticket on as before, with no question written and no block tag set
- BEH-TICKET-6: failing Actions checks read by the merge step send the ticket back to implementation from BEH-ORCH-10's stop point, and leave the pull request open
- BEH-TICKET-6: a branch behind dev is brought up to date by the merge job, and a conflict is resolved on kit/work/<slug>, never on dev or main
- BEH-TICKET-6: after the card's two attempts at a fix still fail, the ticket gets the blocked tag and a question with its Silence default, and nothing merges
- BEH-TICKET-6: when both sides edit the same card in kit.beh, the ticket is blocked with a question and neither side is picked silently
- BEH-TICKET-6: a ticket whose tests pass and whose branch does not conflict is not retried and gets no question
- BEH-TICKET-6: the attempt count asserted is the card's two; BEH-ORCH-11 slice 7b built one resolve run per ticket, so the test must fail until the owner answers the attempt-count question or the card is changed
Built:
- BEH_TICKET_6_a_build_run_whose_tests_fail_sends_the_ticket_back_to_implementation_and_records_the_failing_test_names
- BEH_TICKET_6_a_retry_whose_tests_then_pass_moves_the_ticket_on_as_before_with_no_question_and_no_block
- BEH_TICKET_6_failing_actions_checks_read_by_the_merge_step_send_the_ticket_back_to_implementation_and_leave_the_pull_request_open
- BEH_TICKET_6_a_branch_behind_dev_is_brought_up_to_date_by_the_merge_job_and_a_conflict_is_resolved_on_its_own_branch
- BEH_TICKET_6_checks_that_still_fail_after_two_attempts_to_fix_them_block_the_ticket_with_a_question_and_nothing_merges
- BEH_TICKET_6_a_ticket_whose_tests_pass_and_whose_branch_does_not_conflict_is_not_retried_and_gets_no_question
- BEH_TICKET_6_when_both_sides_edit_the_same_card_in_kit_beh_the_ticket_is_blocked_with_a_question_and_neither_side_is_picked
- BEH_TICKET_6_a_conflict_gets_two_resolve_runs_before_its_ticket_is_blocked (FAILS by design until the attempt-count question is answered: one resolve run is built, BEH-ORCH-11 slice 7b)
- the whole suite ran 2147 tests (dotnet test Balenthiran.Kit.slnx): 2146 passed and 1 failed, the test above; it did not pass.
