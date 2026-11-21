# Malformed bundle needs a way to resolve
Work-item: bundle-malformed-resolve
Flag: Context says no code checks bundles, but ContextBundle.Parse already names the bad line and Pipeline stops the item with "its bundle is malformed: ..." (PipelineTests), and BEH-TICKET-5's source note is missing, so the board part is the only new work and is unconfirmed (level: high)
Review: revised — BEH-TICKET-5 does cover bundles and its two contracts define resolving (show the bad bundle and wrong line, no builder starts; triage rewrites to shape and records the fix); the kit.notes.md 2026-11-21 13:30Z note does not exist in the file, so Context now says so
Cards:
- BEH-TICKET-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-21 13:30Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- .kit/bundles/
Context:
A bundle at .kit/bundles/<slug>.md was malformed, so the builder could not use it. Kit needs a visible way to resolve this: the board shows the bad bundle and the line that is wrong, and triage rewrites it to the required shape (sections Title, Work-item, Cards, Notes, Files, Context in that order, every list item on its own "- " line). The new card BEH-TICKET-5 is the spec for this. Checking exists already: backend/Balenthiran.Kit.Services/ContextBundle.cs Parse rejects a bad bundle and names the line, and Pipeline.cs stops the item with that reason. Not confirmed: whether the board shows a malformed bundle and its line, and whether triage rewrites one.
The note kit.notes.md#2026-11-21 13:30Z listed under Notes is not present in kit.notes.md (nor is the card source anchor 2026-11-21-1330Z); treat the two BEH-TICKET-5 contracts as the whole spec.
Built:
- BEH-TICKET-5: a bundle that fails its shape check is listed on the board with the line that is wrong, and no build run starts on it (BEH_TICKET_5_a_bundle_that_fails_its_shape_check_is_listed_on_the_board_with_the_line_that_is_wrong_and_no_build_starts)
- BEH-TICKET-5: a well-formed bundle is not listed as malformed, and its build run starts as before (BEH_TICKET_5_a_well_formed_bundle_is_not_listed_as_malformed_and_its_build_run_starts_as_before)
- BEH-TICKET-5: triage rewrites a malformed bundle so it parses, keeps its Cards and Notes, and puts each list item on its own "- " line (BEH_TICKET_5_triage_rewrites_a_malformed_bundle_so_that_it_parses_keeps_its_cards_and_notes_and_puts_each_item_on_its_own_line)
- BEH-TICKET-5: after triage rewrites a malformed bundle, the fix is recorded beside the ticket (BEH_TICKET_5_after_triage_rewrites_a_malformed_bundle_the_fix_is_recorded_beside_the_ticket)
- The whole backend test project (Balenthiran.Kit.Tests) ran 1640 tests and passed; the stage-0 check (kit check) names all 39 built behaviours.
Test plan:
- BEH-TICKET-5: a bundle that fails ContextBundle.Parse is listed on the board with the line Parse names as wrong, and no build run starts on it
- BEH-TICKET-5: a well-formed bundle is not listed as malformed, and a build run starts on it as before
- BEH-TICKET-5: triage rewrites a malformed bundle so that it parses, keeps its Cards and Notes items unchanged, and puts every list item on its own "- " line
- BEH-TICKET-5: after triage rewrites a malformed bundle, the fix is recorded beside the ticket
