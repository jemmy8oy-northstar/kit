# Tickets are not specs: triage turns loose input into a spec change and a ticket, and blocks by tag
Work-item: tickets-separate-from-spec
Flag: the note changes how Kit stores and shows work (a ticket is not the spec, and the spec moves onto the feature branch), so the owner should check it; it may overlap the triage routing on pipeline-routing-by-cost, which this item does not change (level: high)
Review: revised — cards match the bundle; one test line named states (planned, implemented) no card defines, so it now tests only new and ready; moving the spec onto the feature branch still waits on the owner through the unreviewed contracts
Cards:
- BEH-TICKET-1
- BEH-TICKET-2
- BEH-TICKET-3
- BEH-TICKET-4
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-15 15:20Z (target pipeline: input surface, triage, tickets separate from spec, blocked as a tag, technical design and implementation after ready)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
New cards in kit.beh only, because this item may edit only that spec. The file's header marks it as the command-line layer, while these cards describe the board and the agents, so a builder may move them to kit-ui.beh (level: low).
The input surface is writing a note, speaking to chat, leaving a comment, and approving or rejecting; BEH-TICKET-2 covers all four.
The owner asked whether blocked should be a tag rather than a ticket; BEH-TICKET-3 takes the tag reading as the default.
Next step toward the goal: a builder maps BEH-TICKET-1 to 4 to tests once the owner has read this item.
Built:
- BEH-TICKET-1: a ticket on the board shows its status and open questions, and no spec text (BEH_TICKET_1_a_ticket_on_the_board_shows_its_status_and_open_questions_and_no_spec_text)
- BEH-TICKET-1: the spec of a change is read from that change's feature branch (BEH_TICKET_1_the_spec_of_a_change_is_read_from_that_changes_feature_branch)
- BEH-TICKET-1: two changes give two tickets, each with its own feature branch (BEH_TICKET_1_two_changes_give_two_tickets_each_on_its_own_feature_branch)
- BEH-TICKET-2: a note reaches triage and makes a ticket in the new state (BEH_TICKET_2_a_note_reaches_triage_and_makes_a_ticket_in_the_new_state)
- BEH-TICKET-2: a comment on a card reaches triage and makes a ticket in the new state (BEH_TICKET_2_a_comment_on_a_card_reaches_triage_and_makes_a_ticket_in_the_new_state)
- BEH-TICKET-2: a chat message saved as a note reaches triage and makes a ticket in the new state (BEH_TICKET_2_a_chat_message_he_has_Kit_note_reaches_triage_as_a_note_and_makes_a_ticket_in_the_new_state)
- BEH-TICKET-2: triage makes its spec change on a feature branch and never on the default branch (BEH_TICKET_2_triage_makes_its_spec_change_on_a_feature_branch_and_never_on_the_default_branch)
- BEH-TICKET-2: a card triage removes is marked to be deleted, with its reason, and not deleted outright (BEH_TICKET_2_a_card_triage_removes_is_marked_to_be_deleted_with_its_reason_and_not_deleted_outright)
- BEH-TICKET-3: a ticket with an open question for him carries the blocked tag and stays the same ticket (BEH_TICKET_3_a_ticket_with_an_open_question_for_him_is_blocked_and_stays_the_same_ticket)
- BEH-TICKET-3: blocked combines with new, and setting or clearing the tag leaves it new (BEH_TICKET_3_blocked_combines_with_new_and_setting_and_clearing_the_tag_leaves_the_state_new)
- BEH-TICKET-3: blocked combines with ready, and setting or clearing the tag leaves it ready (BEH_TICKET_3_blocked_combines_with_ready_and_setting_and_clearing_the_tag_leaves_the_state_ready)
- BEH-TICKET-3: his reply is recorded on the ticket, and the ticket moves to ready once no question is open (BEH_TICKET_3_his_reply_is_recorded_on_the_ticket_and_the_ticket_moves_to_ready_once_no_question_is_open)
- BEH-TICKET-3: if he stays silent on a blocked ticket, triage acts on the default written on it (BEH_TICKET_3_if_he_stays_silent_on_a_blocked_ticket_triage_acts_on_the_default_written_on_it)
- BEH-TICKET-4: a ready ticket goes to technical design, then to implementation, in that order (BEH_TICKET_4_a_ready_ticket_goes_to_technical_design_then_to_implementation_in_that_order)
- BEH-TICKET-4: technical design can block with a question and clears it when none is open (BEH_TICKET_4_technical_design_can_block_its_ticket_with_a_question_and_clear_it_when_no_question_is_open)
- BEH-TICKET-4: implementation can block with a question and clears it when none is open (BEH_TICKET_4_implementation_can_block_its_ticket_with_a_question_and_clear_it_when_no_question_is_open)
The whole suite ran 2030 tests and did not fully pass: 2027 passed and 3 failed (ClaimerSidecarTests and two ReportConformanceTests). The same 3 fail on the untouched branch in this sandbox (no ICU library, no kill binary), so none is from this change. The untouched branch also failed CommittedArtefactTests (kit.beh's cards tagged in FEAT-BOARD, which is not in that file); that is fixed here by dropping the tag.
Test plan:
- BEH-TICKET-1: a ticket on the board shows its status and its open questions, and the board shows no spec text; the spec of the change is read from that change's feature branch.
- BEH-TICKET-1: a ticket is one change to the spec, so two changes give two tickets, each with its own feature branch.
- BEH-TICKET-2: a note, a chat message, a comment, an approval and a rejection each reach triage, and each produces a ticket in the new state (four cases).
- BEH-TICKET-2: triage makes its spec change on a new feature branch and never on the default branch.
- BEH-TICKET-2: a card triage removes is marked to be deleted, with the reason written beside it, and is not deleted outright.
- BEH-TICKET-3: a ticket with an open question for him carries the blocked tag and stays the same ticket; no second ticket is created for the block.
- BEH-TICKET-3: blocked combines with the states new and ready, and setting or clearing the tag leaves the state unchanged.
- BEH-TICKET-3: triage's discussion of a blocked ticket is recorded on that ticket, and the ticket moves to ready once no question is open.
- BEH-TICKET-3: if he stays silent on a blocked ticket, triage acts on the default written on that ticket.
- BEH-TICKET-4: a ready ticket goes to technical design, then to implementation, in that order.
- BEH-TICKET-4: technical design and implementation can each set the blocked tag with a question, the same way triage does, and clear it when no question is open.
