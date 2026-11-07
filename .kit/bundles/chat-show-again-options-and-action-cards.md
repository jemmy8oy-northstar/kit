# Chat: show again, options, action cards, Kit's capabilities, item links and input
Work-item: chat-show-again-options-and-action-cards
Flag: Unsure of four points: BEH-CHAT-18's source note (kit-ui.notes.md#2026-11-06-0001, cited above as 00:01Z) is not in kit-ui.notes.md and the card has no test; BEH-CHAT-13's card says the Answered line, not the action card, shows the answer text; BEH-CHAT-7's can-list has no tool for the coding-agent question and omits show_note and offer_options, which I added as later asks; BEH-NOTE-15 already stores the tag, so the notes-page-archive-tag-and-warning bundle's "new:" tag card would duplicate it.
Review: revised — all four flags hold; most cards are already built, so Context now narrows the build to the CHAT-13 action card and a CHAT-7 tool-list check, notes that CHAT-17's Shift+Enter is already specced and tested, and records CHAT-18's missing source note
Cards:
- BEH-CHAT-9
- BEH-CHAT-10
- BEH-CHAT-6
- BEH-CHAT-3
- BEH-CHAT-12
- BEH-CHAT-13
- BEH-CHAT-14
- BEH-CHAT-7
- BEH-CHAT-15
- BEH-CHAT-17
- BEH-CHAT-18
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:49Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:50Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:54Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:32Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:35Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:40Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-04 11:00Z
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 00:01Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
Every card above exists in kit-ui.beh. All but BEH-CHAT-18 have test lines in kit-ui.tests.json, and all but BEH-CHAT-18 already carry "contract built" lines. So the job is to check each against the code and build only the gaps named below. The Chat page is prototypes/behaviour-ast/ui/src/pages/Chat.test.tsx's subject (ui/src/pages/Chat.tsx); the backend side is in backend/Balenthiran.Kit.Tests/TriageTests.cs's subject.
- BEH-CHAT-9 (Show again): built, beside Approve and "Skip to next item", reposting the item in hand at the foot of the chat. Check that the label is exactly "Show again". No test pins the label yet.
- BEH-CHAT-10 (tappable options): built through offer_options. Typing a free-form answer is covered by an existing test. Verify only.
- BEH-CHAT-6 and BEH-CHAT-3 (note saved from chat, with bubble and Undo): built. The bubble reads "Saved as a note on <project>: “<the note, in full>”" with an Undo, and Undo dismisses exactly that note, then "Undone: …". This matches the 13:54Z ask for a bubble that states exactly what happened. Verify only. A test pinning the bubble wording is the one gap.
- BEH-CHAT-12 (show a posted answer, or a note's text, answer and tag): built through show_note, with tests. BEH-NOTE-15 is already built and stores the origin as the note's `<!-- from: … -->` line, and CHAT-12 only reads it. Build no tag here. The "new:" tag card in the notes-page-archive-tag-and-warning bundle duplicates BEH-NOTE-15. That is for that bundle's review, not this one.
- BEH-CHAT-13 (action card shows the answer text just added): Kit's line quoting the full answer is built. The card's own assumption says the action card itself should also show the full text (the 23:32Z note says "the card shows the text that was added"), and that part is unverified. This is the one real build gap in the bundle: make the action card show the answer's full text, alongside Kit's line.
- BEH-CHAT-14 (no next-up-in-Kit section): built. Kit's line no longer says "Next up". The dock's jump to another project's item (BEH-SPACE-2) stays. Verify only.
- BEH-CHAT-7 (what Kit can and cannot do; 23:40Z note): built, with instructions and a help line. The card's assumptions map the can-list to approve_item, deny_item, answer_item, undo_last, show_item, save_note, ask_coding_agent (BEH-SPECAGENT-3) and read_spec (BEH-CHAT-8). They also add show_note and offer_options as later asks. Check the instructions and help line against that whole list, not only the 23:40Z list. If ask_coding_agent does not exist yet (BEH-SPECAGENT-3 unbuilt), the instructions must not claim it. Record that as an assumption on the card rather than building the tool here. Nothing on the cannot-list (reorder, pause, schedule, priority, spec edit) has a tool or a button; asked for one of them, Kit offers to leave a note.
- BEH-CHAT-15 (item ids as links): built, with four tests. Ids link to the existing /projects/<app>/<epics|features|behaviours>/<id> route in messages, item cards, action cards and shown notes. Verify only; add no new route.
- BEH-CHAT-17 (Enter on phone and desktop): built. Shift+Enter on a desktop is already on the card (from the 11:00Z note) and tested ("on a fine pointer Enter sends and Shift+Enter does not"). Nothing to confirm with the owner. Do not change the phone behaviour.
- BEH-CHAT-18 (technical level): priority 5, pending, needs-context. Do not build it. Its source, kit-ui.notes.md#2026-11-06-0001, is not in kit-ui.notes.md (the file ends at 2026-11-04 18:08Z), so the card's quoted contract is the only record of the ask. Leave the card as it is, and say in the PR that the source note is missing so the owner can point to where it lives.

Test plan:
- BEH-CHAT-9: new, the dock shows a button labelled exactly "Show again" beside Approve and "Skip to next item".
- BEH-CHAT-9: existing "puts the item at the bottom with no model call, no write and no reply spent" still passes.
- BEH-CHAT-10: existing "leaves typing free, and sending his own words retires the options" still passes.
- BEH-CHAT-3: new, a note saved from the chat puts Kit's bubble "Saved as a note on <project>: “<the note, in full>”" in the thread, with an Undo.
- BEH-CHAT-6: existing "saves a note on the project with no behaviour, even with no item in hand, and Undo dismisses that note" still passes.
- BEH-CHAT-12: existing "puts the note found by its item id into the thread with its text, its answer and its origin, and writes nothing" still passes.
- BEH-CHAT-13: new, after an answer is posted the action card itself shows the whole answer text, not only Kit's line; existing "quotes every word of the answer that was posted" still passes.
- BEH-CHAT-14: existing "after an item is dealt with the next one only comes up in the thread, with no Next up line" still passes.
- BEH-CHAT-7: new, the agent's instructions and the help line name the same can-list, made up only of tools that exist, and no button is offered for reorder, pause, schedule, priority or spec edit; existing "the header button and a typed help or /help add the help line and never call the agent" still passes.
- BEH-CHAT-15: existing four tests still pass.
- BEH-CHAT-17: existing "on a coarse pointer Enter does not send and the Send button does" and "on a fine pointer Enter sends and Shift+Enter does not" still pass.
- BEH-CHAT-18: no test; the card is pending and not built here.
