# Triage makes one ticket per change and runs one note at a time
Work-item: triage-tickets-per-note-serial
Flag: the card for serial triage is uncertain: BEH-SCHED-1 says stages are independent and already carries a review request about one-run-per-project, so the serial clause may belong on BEH-TICKET-2 alone (level: low)
Review: revised — the serial clause belongs on BEH-TICKET-2 alone (it already carries both new assumptions); BEH-SCHED-1 already notes triage as the exception and has an open review request, so it is dropped from Cards and left untouched
Cards:
- BEH-TICKET-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 22:31Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.tests.json
Context:
Two notes, same domain (triage). The first says kit can make several tickets from one note. The second says triage must run in serial so parallel triage runs cannot add duplicates or overlapping changes. BEH-TICKET-2 is the triage card; BEH-SCHED-1 is the stage-schedule card, and its existing review request about one run per project stays open and is not changed. Builder: write the tests first, map any new assertion in kit.tests.json, and do not edit other cards.
Built:
- BEH_TICKET_2_a_note_that_asks_for_two_separate_changes_gives_two_tickets_one_per_change_each_on_its_own_feature_branch
- BEH_TICKET_2_the_triage_task_asks_for_one_work_item_for_each_separate_change_a_note_asks_for
- BEH_TICKET_2_a_note_that_asks_for_one_change_gives_one_ticket_not_a_split
- BEH_TICKET_2_a_second_note_waits_while_triage_runs_for_the_first_and_its_triage_starts_only_after_the_first_notes_tickets_are_written
- BEH_TICKET_2_a_second_note_is_triaged_against_the_tickets_the_first_note_made_so_it_adds_no_duplicate_ticket
Whole suite: 2187 backend tests ran and all passed (Release, full CI shape); UI lint, build and 648 tests passed; both Stage-0 gates pass.

Test plan:
- BEH-TICKET-2: a note that asks for two separate changes gives two tickets, one per change, each on its own feature branch
- BEH-TICKET-2: a note that asks for one change gives one ticket, not a split
- BEH-TICKET-2: a second note waits while triage runs for the first, and its triage starts only after the first note's tickets are written
- BEH-TICKET-2: a second note is triaged against the tickets the first note made, so it adds no duplicate ticket for a change already made
