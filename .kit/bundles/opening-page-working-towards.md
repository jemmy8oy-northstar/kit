# Opening page shows the projects page with Working towards first
Work-item: opening-page-working-towards
Flag: the note does not say which card the opening page belongs to, and "the current projects page" plus "first thing that shows" could mean the projects list or each project's own lead; I read it as the projects page opens first and each project leads with Working towards (level: high)
Cards:
- BEH-NAV-1
- BEH-NARR-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 22:46Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/pages/Projects.tsx
Context:
- The note says that when the UI is separated by project, the opening page should be the current projects page, and "Working towards: {epic}" should be the first thing shown.
- BEH-NARR-1 (kit-ui.beh) already builds "Working towards" on the project page. The new contract asks for it on the projects page too, and first.
- BEH-NAV-1 (kit-ui.beh) has the navbar and the projects link. The new contract makes the projects page the opening page once the UI is split by project.
- The cards live in kit-ui.beh, not kit.beh, so the spec edit went there.
- No open ticket covers this, so it is a new slug.
