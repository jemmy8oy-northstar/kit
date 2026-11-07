# The Inbox one card at a time: answer, approve, deny, pause, dismiss or deprioritise
Work-item: inbox-card-triage
Flag: The swipe-style gestures and the deprioritise verb are UX choices James only sketched ("I am almost imagining"); confirm the gesture set before building.
Cards:
- BEH-INBOX-2
- BEH-INBOX-3
- BEH-BOARD-16
- BEH-BOARD-26
- BEH-NOTE-7
- new: the Inbox can be worked one card at a time, unseen items first, then by priority
- new: a card can be deprioritised from the Inbox without being denied
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:31Z
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
