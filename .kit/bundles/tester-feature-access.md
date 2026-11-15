# Testers can use the projects, boards, inbox and settings pages
Work-item: tester-feature-access
Flag: the 17:41Z switches are already built (BEH-FLAGS-1), so BEH-FLAGS-3 is only the "they work for testers" half; open whether testers may act on their own items, since writes are operator-only today and BEH-USERS-8 lets anyone open a public project by URL, which the tester rule forbids (level: high)
Cards:
- BEH-FLAGS-3
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:41Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-USERS-* for tester accounts; BEH-SETTINGS-* for the Settings page; BEH-INBOX-* for the Inbox)
- prototypes/behaviour-ast/behaviours/kit.notes.md (the source note)
Context:
- He asks to enable these four pages for testers now, and says they must also work for testers, not just show. He named them as the place to start.
- Chat and the other features are not in this item.
- A tester sees only their own projects, notes and memory (BEH-USERS-8); this item must keep that rule.
- Testers doing many cards cost him real money (see the cost estimate in BEH-USERS-9, kit-ui.beh line 1479), so do not turn on agent work for testers here.
- Record the guess as a "contract my assumption:" line in the corpus, so he can deny it.
- The switches themselves (FEAT-PROJECTS, FEAT-PROJECT, FEAT-BOARD, FEAT-STAGES, FEAT-EPICS, FEAT-INBOX, FEAT-SETTINGS on for testers in kit.beh) are already in place; BEH-FLAGS-1 records that.
Next step toward the goal: the builder checks which of the four pages a tester account can reach today, then opens the missing ones.
Test plan:
- BEH-FLAGS-3: a tester's Projects page lists only the repos of their own GitHub, and none of the operator's projects.
- BEH-FLAGS-3: a tester opens a board of their own project and sees its cards; a board of another user's project is refused.
- BEH-FLAGS-3: a tester's Inbox lists only items in their own projects, and approving one writes to that project's repo only.
- BEH-FLAGS-3: a tester's write (approve, deny, answer, pause, lower priority) to a project they do not own is refused, and each operator-only write (promotion, idle, depth, conflicts, features) is refused for them.
- BEH-FLAGS-3: a tester's Settings shows their account and GitHub only, with no operator section in the page or in the responses it reads.
- BEH-FLAGS-3: no tester action starts or records an agent run.
- BEH-FLAGS-3: a page switched off for testers still shows "Not available" (BEH-FLAGS-2), and the pages still on for them keep working.
