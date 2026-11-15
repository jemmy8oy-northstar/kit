# Questions tab leads with what Kit asks, and Kit's questions can be answered
Work-item: questions-tab-and-replies
Flag: 22:48Z asks what UX and logic should unblock Kit, and 21:31Z asks for a card-style triage page, both the owner's design calls; 16:39Z touches the open epics ticket (kit/work/epics); 10-31 00:55Z is about reply order in the inbox (level: high)
Cards:
- BEH-SHEET-2
- BEH-SHEET-3
- BEH-BOARD-11
- BEH-NOTE-8
- BEH-CHAT-10
- new: the AI proposes behaviours for a feature and features for an epic, and asks when it lacks context
- new: a page that shows items one at a time, the ones mine to address first, with approve, deny, pause, dismiss, comment and deprioritise as choices
- new: a reply to a direct question asked as a note appears after that question in the inbox, not before it
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 22:46Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 22:48Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 23:47Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 16:39Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:31Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 00:55Z
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
- 10-31 00:55Z: when a direct question is asked as a note, the reply can show in the inbox before the question does. Default: show the question first, then the reply under it. Check how the inbox orders the two before changing it.
