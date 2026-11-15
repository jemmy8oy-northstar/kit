# Questions tab leads with what Kit asks, and Kit's questions can be answered
Work-item: questions-tab-and-replies
Flag: 22:48Z asks what UX and logic should unblock Kit, and 21:31Z asks for a card-style triage page, both the owner's design calls; 16:39Z touches the open epics ticket (kit/work/epics); 10-31 00:55Z is about reply order in the inbox; 10-31 09:48Z comment-reading is my reading of the note (level: high)
Review: revised — the INBOX-5 default no longer reverses BEH-INBOX-3, which is already built from the same 00:55Z note; the rest stands, with SHEET-4, STAGE-13 and COMMENT-3 already recording their guesses as unreviewed assumptions and epics left to kit/work/epics
Cards:
- BEH-SHEET-2
- BEH-SHEET-3
- BEH-BOARD-11
- BEH-NOTE-8
- BEH-CHAT-10
- BEH-STAGE-13
- BEH-SHEET-4
- BEH-INBOX-5
- BEH-COMMENT-3
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 22:46Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 22:48Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 23:47Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 16:39Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:31Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 00:55Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 09:48Z (Kit reads comments to see if it needs to respond)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- backend/Balenthiran.Kit.Services/QuestionSheet.cs
- backend/Balenthiran.Kit.Abstractions/Services/IQuestionSheet.cs
Context:
- BEH-SHEET-2 says the Questions tab leads with what Kit is asking and calls the rest problems. The 22:46Z note says it shows problems, not questions. Check QuestionSheet.cs for what gets listed as a question before building.
- BEH-SHEET-3 covers answering under a question. The 23:47Z note says Kit's asks have no way to reply. If SHEET-3 is already built, the gap is on the path the ask takes (board or Inbox), so verify where the reply is missing before building.
- BEH-BOARD-11 puts the agent's open questions on the board. BEH-NOTE-8 resolves a sheet decision in free text. BEH-CHAT-10 offers yes/no and multiple-choice questions as tappable options; that is the default for a question with a fixed set of answers.
- 22:48Z asks Kit to raise the questions that guide him toward the right decisions. The owner has not said which decisions or what the layout is. Default: each raised question names the card it is about and offers the options Kit would take. Leave the layout to him.
- 16:39Z: the AI proposes behaviours for features and features for epics, and asks when it lacks context. BEH-STAGE-5 is the nearest card. The open epics ticket (kit/work/epics) owns epics as their own block; check the new card against it so the two do not build the same thing.
- 21:31Z (10-27): the owner imagines a tinder-style page of items mine to address, ordered by priority but unseen first, where each item can be approved, denied, paused, dismissed, commented on or deprioritised. This is a design sketch. Default: build the one-at-a-time page on the same items the Questions tab already lists, and record the choice set as an unreviewed assumption. Do not invent new item kinds.
- 10-31 00:55Z: BEH-INBOX-3 is already built from this same note. It reads the note as a wish: Kit's replies (review requests) come before Kit's own questions in the Inbox. BEH-INBOX-5 reads it the other way round (question first, reply under it). The note says "replies can come up in the inbox maybe before the questions", which favours INBOX-3. Default: keep INBOX-3's built order and do not reorder the Inbox. For BEH-INBOX-5, build only this: a reply sits on the same card as the question it answers. Then raise a review request on BEH-INBOX-5 naming both cards and the reading kept (INBOX-3), so he decides. Do not edit either card to make the conflict go away.
- 10-31 09:48Z: Kit should read comments and decide whether it needs to respond. Default: respond to comments addressed to Kit or asking it a question, and leave other comments alone. Check whether the comment path already does this (BEH-NOTE-8, BEH-CHAT-10) before building, and record the trigger as an unreviewed assumption.
Test plan:
- BEH-SHEET-2: the Questions tab lists each needs-context line and each question review request above "Problems in the spec", with its card linked beneath it, and a project with only questions shows a count
- BEH-SHEET-3: every Kit question on the tab has an answer box and Reply; a blank box sends nothing; a reply becomes a plain note on the card and shows as waiting for Kit
- BEH-BOARD-11: a review request whose note ends in a question mark sits at the top of the review column with the question badge, and names its card
- BEH-NOTE-8: a free-text resolution of a sheet decision leaves a note tagged with the decision key, and the conflict stays on the sheet until the note is folded
- BEH-CHAT-10: a Kit question with a fixed set of answers shows them as buttons on the tab and on the board's review card; a tap sends its words as his answer, and free text stays open
- BEH-STAGE-13: a feature or epic draft that needs a fact he has not given leaves a review request on that item naming the missing context, and no drafted child
- BEH-SHEET-4: the one-at-a-time page shows the Questions tab's items only, unseen first then by priority, with approve, deny, pause, dismiss, comment, deprioritise and answer offered only where the item allows them
- BEH-INBOX-5: given a question note and Kit's reply to it, the reply sits on the question's card, BEH-INBOX-3's built order (review requests first among unseen cards) still holds, and a review request on BEH-INBOX-5 names both cards and the reading kept
- BEH-COMMENT-3: a comment that asks Kit something, or is addressed to it by name, gets a review request on its card; any other comment is read and cited with no reply
