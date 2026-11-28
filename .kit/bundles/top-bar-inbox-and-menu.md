# Top bar shows the Inbox and a menu, not a row of buttons
Work-item: top-bar-inbox-and-menu
Flag: overrides BEH-NAV-4's built reading, which is under an open review request, and the top-bar cards live in kit-ui.beh, not kit.beh; the principles note is a question, not a build (level: high)
Cards:
- BEH-NAV-4
- BEH-NAV-1
- BEH-NAV-2
- BEH-PRIN-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-28 09:38Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-28 09:40Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/App.tsx (BoardLinks, the top bar)
- prototypes/behaviour-ast/ui/src/components/ProjectNav.tsx (the project row, not the top bar)
Context:
Owner: make the top bar the Inbox and a menu button, the title inline; the menu holds the epic board, ticket board, Projects, Settings and the rest. Built today: the top bar holds Epics, Tickets, Projects and Settings (BEH-NAV-4). His newer note wins, so NAV-4's top-bar line is corrected and the review request on it stays open for him.
Ideas and Principles stay reachable from the menu, so BEH-IDEA-1 and BEH-PRIN-1 still hold as pages in the navbar.
Principles (09:40Z) is a question: do we need them visible at all. Spec keeps them until he answers; no build removes the page or the project tab.
Not the same as the open ticket tab-row-scroll-lock (BEH-NAV-4): that is the project tab row, not the top bar.
