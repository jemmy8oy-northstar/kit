# Testers can use the projects, boards, inbox and settings pages
Work-item: tester-feature-access
Flag: no existing card names tester access to these pages, so the card line is new (level: low)
Cards:
- new: testers can open the projects, boards, inbox and settings pages, and they work for them
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:41Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-USERS-* for tester accounts; BEH-SETTINGS-* for the Settings page; BEH-INBOX-* for the Inbox)
- prototypes/behaviour-ast/behaviours/kit.notes.md (the source note)
Context:
- He asks to enable these four pages for testers now, and says they must also work for testers, not just show. He named them as the place to start.
- Chat and the other features are not in this item.
- A tester sees only their own projects, notes and memory (BEH-USERS-8); this item must keep that rule.
- Testers doing many cards cost him real money (see the cost estimate in BEH-NOTIFY, around kit-ui.beh line 1787), so do not turn on agent work for testers here.
- Record the guess as a "contract my assumption:" line in the corpus, so he can deny it.
Next step toward the goal: the builder checks which of the four pages a tester account can reach today, then opens the missing ones.
