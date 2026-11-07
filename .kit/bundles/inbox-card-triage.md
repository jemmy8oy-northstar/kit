# The Inbox one card at a time: answer, approve, deny, pause, dismiss or deprioritise
Work-item: inbox-card-triage
Flag: The swipe-style gestures and the deprioritise verb are UX choices James only sketched ("I am almost imagining"); confirm the gesture set before building. The 2026-10-29 22:08Z note asks for any skip written to the spec or repo to be undone, so check the repo history before reverting anything.
Cards:
- BEH-INBOX-1
- BEH-INBOX-2
- BEH-INBOX-3
- BEH-INBOX-4
- BEH-BOARD-16
- BEH-BOARD-25
- BEH-BOARD-26
- BEH-NOTE-7
- new: the Inbox can be worked one card at a time, unseen items first, then by priority
- new: a card can be deprioritised from the Inbox without being denied
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:31Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-28 23:55Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-29 08:10Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-29 14:56Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-29 22:08Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 00:55Z (replies to direct questions)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 09:42Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 09:46Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 16:23Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/pages/Inbox.tsx
- prototypes/behaviour-ast/ui/src/inbox.ts
- prototypes/behaviour-ast/ui/src/pages/Inbox.test.tsx
- prototypes/behaviour-ast/ui/src/pages/InboxGoal.test.tsx
Context:
The note asks for a page that shows James's items one at a time, card by card: each one can be answered or commented on, approved, denied, paused or dismissed, and maybe deprioritised. Order: items not yet seen first, highest priority first, then the normal priority order once seen.
"Seen" does not exist in the corpus. The builder must add a per-user seen state and decide where it is kept, which is the user's data, not the repo (BEH-USERS-8 scopes it to the user).
Approve, deny and pause already exist elsewhere: BEH-INBOX-2 for the deny reason, BEH-BOARD-16 for pause, BEH-BOARD-26 for a paused card's return date. Reuse them. Answers go through the same path as BEH-SHEET-3 and BEH-NOTE-6. Dismiss matches BEH-NOTE-7.
Deprioritise is not specified. It should move a card down the order, not deny it, and the priority line stays the agent's (see BEH-BOARD-13's contract on the priority line).
Praise (2026-10-28 23:55Z): the one-card Inbox works well and James does not want to navigate a board to act. Nothing to build; keep the Inbox as the place to act.
Deny reason (2026-10-29 08:10Z): BEH-INBOX-2 already asks for a reason only when he has not typed one. The note keeps the confirm-to-deny step even when the reason box is hidden. Check that the confirm still shows.
Skip (2026-10-29 14:56Z and 22:08Z): skip is held on his device and writes nothing to the spec or repo; the contract under BEH-INBOX-1 (kit-ui.beh:1925) says so. The 22:08Z note asks whether any skip reached the spec or repo, and for it to be undone. The contract at kit-ui.beh:1926 records an audit; read its result before changing anything. Any undo is a repo change and goes through a pull request.
Replies (2026-10-31 00:55Z): a reply to a question he asked in a note should come up in the Inbox before the question itself. BEH-INBOX-3 already says the answer comes up first. Check the ordering against the card.
Pause (2026-10-31 09:42Z and 09:46Z): approving a paused card resumes it unless something still blocks it (BEH-BOARD-25). A paused card leaves the Inbox until its return date (BEH-BOARD-26). The 09:46Z note asks that the bell can bring it back early; the bell is covered in bell-and-item-comments (BEH-BELL-1 and BEH-BELL-2).
Project filter (2026-10-31 16:23Z): BEH-INBOX-4 is the filter. Its contract (kit-ui.beh:1966) says it was built 2026-10-31: one button per project with a card waiting, and changing the filter does not reshuffle the order. Confirm before changing.
