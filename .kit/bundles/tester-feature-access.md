# Testers can use the projects, boards, inbox and settings pages
Work-item: tester-feature-access
Flag: the 17:41Z switches are already built (BEH-FLAGS-1), so BEH-FLAGS-3 is only the "they work for testers" half; open whether testers may act on their own items, since writes are operator-only today and BEH-USERS-8 lets anyone open a public project by URL, which the tester rule forbids (level: high)
Review: revised — no decision needed: FLAGS-1's "every write stays operator-only" held only until BEH-USERS-2, and FLAGS-3 already records tester writes on own items as an unreviewed assumption; fixed the test that contradicted BEH-USERS-8's public read by URL
Cards:
- BEH-FLAGS-3
- BEH-SCOPE-1
- BEH-SCOPE-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:41Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 21:21Z (first tester sees my proposed projects; proposed projects should be per user, stored in the db until a fit repo exists)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 21:21Z (tester can't see their own holiday-planner project as a contributor; spec files not in this repo; find projects where users are contributors)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-USERS-* for tester accounts; BEH-SETTINGS-* for the Settings page; BEH-INBOX-* for the Inbox)
- prototypes/behaviour-ast/behaviours/kit.notes.md (the source note)
Context:
- He asks to enable these four pages for testers now, and says they must also work for testers, not just show. He named them as the place to start.
- Chat and the other features are not in this item.
- A tester sees only their own projects, notes and memory (BEH-USERS-8); this item must keep that rule. BEH-USERS-8 also lets anyone open a project in a public repo by its URL (as BEH-GATE-H1 does); keep that: a tester's lists, counts and Inbox hold only their own projects, a project in a private repo is served to its owner only, and every write to a project they do not own is refused.
- Writes: BEH-FLAGS-1 says "every write stays operator-only" only until a tester sees their own projects (BEH-USERS-2, which FLAGS-3 depends on). FLAGS-3 already records, as an unreviewed assumption, that a tester may approve, deny, answer, pause and lower priority on their own items; build that reading and leave the assumption for him to deny. Do not edit either card to settle it.
- Testers doing many cards cost him real money (see the cost estimate in BEH-USERS-9, kit-ui.beh line 1479), so do not turn on agent work for testers here.
- The guesses are already "contract my assumption:" lines on BEH-FLAGS-3; record any new guess the same way, so he can deny it.
- The switches themselves (FEAT-PROJECTS, FEAT-PROJECT, FEAT-BOARD, FEAT-STAGES, FEAT-EPICS, FEAT-INBOX, FEAT-SETTINGS on for testers in kit.beh) are already in place; BEH-FLAGS-1 records that.
Next step toward the goal: the builder checks which of the four pages a tester account can reach today, then opens the missing ones.
Test plan:
- BEH-FLAGS-3: a tester's Projects page lists only the repos of their own GitHub, and none of the operator's projects.
- BEH-FLAGS-3: a tester opens a board of their own project and sees its cards; a board of another user's project in a private repo is refused, and one in a public repo can be read by URL but never appears in their lists and accepts no write from them (BEH-USERS-8).
- BEH-FLAGS-3: a tester's Inbox lists only items in their own projects, and approving one writes to that project's repo only.
- BEH-FLAGS-3: a tester's write (approve, deny, answer, pause, lower priority) to a project they do not own is refused, and each operator-only write (promotion, idle, depth, conflicts, features) is refused for them.
- BEH-FLAGS-3: a tester's Settings shows their account and GitHub only, with no operator section in the page or in the responses it reads.
- BEH-FLAGS-3: no tester action starts or records an agent run.
- BEH-FLAGS-3: a page switched off for testers still shows "Not available" (BEH-FLAGS-2), and the pages still on for them keep working.
- BEH-SCOPE-1: a proposed project made by a tester is stored against that tester, and a second tester signed in to the same Kit never lists it; the operator's proposals are not listed for a tester.
- BEH-SCOPE-1: a proposal with no connected repo is kept in the database against its owner, and is never written to a repo the tester is not a contributor on.
- BEH-SCOPE-2: a connected repo that carries a corpus and names the tester as a contributor is listed for that tester, and for no other user.
- BEH-SCOPE-2: a connected repo that does not name the tester as a contributor is not listed for them, and a repo the tester contributes to that is not yet connected does not appear.
- BEH-SCOPE-2: a connected repo the tester contributes to, but with no .beh spec files, is not listed as a project.
2026-11-16: two more notes on tester visibility of projects. BEH-SCOPE-1 (proposed projects per user) and BEH-SCOPE-2 (projects found by contributor) are new in kit.beh, because this item may edit only kit.beh. They overlap BEH-USERS-8 and BEH-FLAGS-3 in kit-ui.beh, which were not edited; the owner should say whether the scope cards replace them or sit beside them. Assumptions are contract lines on the new cards.
