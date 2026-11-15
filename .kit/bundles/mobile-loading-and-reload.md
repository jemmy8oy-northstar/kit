# Mobile: pull down to reload, and a proper loading state
Work-item: mobile-loading-and-reload
Flag: the "checking..." text sits in the header, so it may overlap mobile-header-declutter (BEH-NAV-2); the loading state is not in that ticket's scope (level: low)
Cards:
- new: pulling down on the installed iPhone app reloads the current page
- new: a loading state that replaces the "checking..." text shown in the top left
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 09:18Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:30Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- The owner has to switch page and back to refresh the installed iPhone app. Pull-down reload is the fix.
- The loading state: the owner wants something better than "checking..." in the top left. Keep the design open; default is a simple in-page loading indicator, recorded as an unreviewed assumption.
- Check mobile-header-declutter before editing the header so the two do not conflict.
