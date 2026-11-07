# Phone and loading: a one-line header, a real loading screen, and pull to reload in the installed app
Work-item: mobile-shell-and-loading
Flag: PullToRefresh.tsx and its test already exist, so the 2026-10-27 21:30Z note may be a bug in the installed-app case rather than a missing feature; check before adding a card.
Cards:
- BEH-NAV-2
- BEH-LOADING-1
- BEH-LOADING-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 12:32Z ("kit header on mobile is really cluttered")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 09:18Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:30Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/components/Loading.tsx
- prototypes/behaviour-ast/ui/src/components/Loading.test.tsx
- prototypes/behaviour-ast/ui/src/components/PullToRefresh.tsx
- prototypes/behaviour-ast/ui/src/components/PullToRefresh.test.tsx
- prototypes/behaviour-ast/ui/src/hooks/refresh.ts
- prototypes/behaviour-ast/ui/public/manifest.json
- prototypes/behaviour-ast/ui/public/sw.js
Context:
Header (2026-10-25 12:32Z): BEH-NAV-2 says that on a phone the header is one tidy line, with the links in a row that scrolls. James says it is cluttered, so the card is not met. The navbar component was not located at triage; find it under ui/src/components before changing anything.
Loading (2026-10-27 09:18Z): BEH-LOADING-1 says the screen shows Kit's name and a spinner, and says so if the server is slow. James says it shows only "checking..." in the top left, so the card is not met. Loading.tsx is the first place to look.
Pull to reload (2026-10-27 21:30Z): BEH-LOADING-2 says pulling the page down reloads it on the installed iPhone app. James has to switch page and back to reload. PullToRefresh.tsx and hooks/refresh.ts already exist, so check whether the gap is only in the installed-app (standalone) case, and whether sw.js caching is serving a stale page.
