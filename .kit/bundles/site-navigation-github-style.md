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

Test plan:
- BEH-NAV-4: the global top bar on the epic board page holds only the epic board, the ticket board, the projects list and Settings, with no project views in it
- BEH-NAV-4: opening a project shows its views as a tab row beneath the top bar, and the tab for the current page is marked active
- BEH-NAV-4: the tab row appears on every page under a project (spec, notes, chat, runs, health, settings), and clicking a tab stays inside that project
- BEH-NAV-4: the project tab row changes only the layout; each page's content and cards render as before
- BEH-TICKET-6: the global top bar links to the epic board and the ticket board, and neither the feature board nor the spec board is linked from it
- BEH-TICKET-6: the feature and spec pages remain reachable by their own URLs after the boards leave the top bar
