# Restructure site navigation to match GitHub
Work-item: site-navigation-github-style
Flag: the owner's ask is open-ended ("take inspiration from GitHub"), so the exact top-bar and tab items are my guess (level: high)
Review: revised — the guesses are already unreviewed contracts on BEH-NAV-4, so no person is needed before building; fixed the Notes anchor to the card's form and noted the BEH-NAV-3 overlap
Cards:
- BEH-TICKET-6
- BEH-NAV-4
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-25-1916
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
Owner wants the site restructured like GitHub: a global top bar with only global links, and a tab row under each project for its views. Existing navbar cards BEH-NAV-1, BEH-NAV-2 and BEH-NAV-3 live in kit-ui.beh, which triage does not edit; a builder must reconcile them with BEH-NAV-4 once the owner confirms the layout. BEH-NAV-3 already says projects are separate like GitHub repos. Feature and spec boards leave the top bar (see BEH-TICKET-6); their pages stay. BEH-NAV-3 names the global set as "the board and Settings"; BEH-NAV-4 widens it to the epic board, the ticket board, the projects list and Settings, so the builder updates BEH-NAV-3 to match rather than leaving both. The note BEH-NAV-4 cites (kit.notes.md#2026-11-25-1916) has no 2026-11-25 entry in kit.notes.md yet; the builder checks the owner's words there before building, and if it is still missing, builds from BEH-NAV-4's contracts as written.

Built:
- BEH-NAV-4: the global top bar on the epic board page holds only the epic board, the ticket board, the projects list and Settings, with no project views in it (new)
- BEH-NAV-4: opening a project shows its views as a tab row beneath the top bar, and the tab for the current page is marked active (new)
- BEH-NAV-4: the tab row appears on every page under a project (spec, notes, chat, runs, health, settings), and every tab stays inside that project (new)
- BEH-NAV-4: the project tab row changes only the layout: the project page keeps its heading, its tab row and its behaviours (new)
- BEH-TICKET-6: the global top bar links to the epic board and the ticket board, and neither the feature board nor the spec board is linked from it (revised, was "the navbar has one Board link")
- BEH-TICKET-6: the feature and spec pages stay reachable by their own URLs after the boards leave the top bar (new; the feature-page URL test was already there and still passes)
- BEH-NAV-1: the navbar carries the ticket board and the projects, on every page (revised: the link is Tickets, not Board)
- BEH-SPACE-5: keeps Projects and Settings in the main navigation, and not the Inbox or Runs (revised: Notifications left the top bar)
- BEH-TICKET-6: BEH-EPIC-4 on /epics marks the Epics link current in the top bar (revised, was the Board link)
- BEH-FLAGS-2: a feature off for testers leaves the Tickets link out of their bar (revised, was the Health link)
- BEH-FLAGS-2: a feature on for him shows Tickets in his bar (revised, was Health)
- BEH-FLAGS-2: a switched-off board hides its page and leaves the epic board in the top bar (revised, was "points the Board link at one that is on")
- BEH-FLAGS-2: when the switches cannot be read everything shows, including Tickets (revised, was Health)
- BEH-STAGE-8 and BEH-EPIC-4: the board switch keeps the top bar link for the current page current (revised, was one Board link)
- BEH-IDEA-1: Ideas leaves the top bar and the Ideas project still opens at its URL (revised, was "links Ideas from every page")
- BEH-PRIN-1: Principles leaves the top bar and its page still opens by its URL (revised, was "links Principles from every page")
- BEH-ACTIVITY-1: Activity leaves the top bar and its page still opens by its URL (revised, was "links Activity from every page")
- BEH-NOTIFY-1: Notifications leaves the top bar and its page still opens by its URL (revised, was "links Notifications from every page")
Whole suite: 674 UI tests passed (5 todo) and 2245 backend tests passed; the Stage-0 gates for kit and kit-ui pass, and UI lint, build and typecheck pass.

Test plan:
- BEH-NAV-4: the global top bar on the epic board page holds only the epic board, the ticket board, the projects list and Settings, with no project views in it
- BEH-NAV-4: opening a project shows its views as a tab row beneath the top bar, and the tab for the current page is marked active
- BEH-NAV-4: the tab row appears on every page under a project (spec, notes, chat, runs, health, settings), and clicking a tab stays inside that project
- BEH-NAV-4: the project tab row changes only the layout; each page's content and cards render as before
- BEH-TICKET-6: the global top bar links to the epic board and the ticket board, and neither the feature board nor the spec board is linked from it
- BEH-TICKET-6: the feature and spec pages remain reachable by their own URLs after the boards leave the top bar
