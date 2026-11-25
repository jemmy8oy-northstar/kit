# Restructure site navigation to match GitHub
Work-item: site-navigation-github-style
Flag: the owner's ask is open-ended ("take inspiration from GitHub"), so the exact top-bar and tab items are my guess (level: high)
Cards:
- BEH-TICKET-6
- BEH-NAV-4
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-25 19:16Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
Owner wants the site restructured like GitHub: a global top bar with only global links, and a tab row under each project for its views. Existing navbar cards BEH-NAV-1, BEH-NAV-2 and BEH-NAV-3 live in kit-ui.beh, which triage does not edit; a builder must reconcile them with BEH-NAV-4 once the owner confirms the layout. BEH-NAV-3 already says projects are separate like GitHub repos. Feature and spec boards leave the top bar (see BEH-TICKET-6); their pages stay.
