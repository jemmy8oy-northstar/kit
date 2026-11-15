# One chat across kit, kit-ui and kit-hosted
Work-item: kit-one-chat-across-projects
Flag: Conflict with BEH-SPACE-1 (each project is its own space with its own chat): the owner asks for one chat across kit, kit-ui and kit-hosted, which contradicts it. Check the # kit:conflicts line before building. BEH-CHAT-11 is marked built but depends on the three apps sharing a # kit:project line; confirm that in kit.beh, kit-ui.beh and kit-hosted.beh (level: high)
Review: revised — no conflict: kit.beh, kit-ui.beh and kit-hosted.beh all carry `# kit:project kit`, so they are three apps of one project and BEH-SPACE-1's one chat per project is the chat BEH-CHAT-11 built; verify its tests, no spec edit needed
Cards:
- BEH-CHAT-11
- BEH-SPACE-1
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:36Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-hosted.beh
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
Context:
The owner wants one chat for kit, kit-ui and kit-hosted, not three. Review confirmed all three files carry `# kit:project kit`, so they are three apps of one project, kit, not three projects. BEH-CHAT-11 (built) already gives that project one chat: the Chat page picker lists projects, and apps sharing a `# kit:project` line are one entry. Each turn goes to the spec agent of the app the item in hand belongs to; with nothing in hand it goes to the home app, kit (the app named like the project). The agent still reads one app's spec per turn.
BEH-SPACE-1 (each project has its own chat, board and config) does not conflict: kit is one project with one chat, which both cards say. The # kit:conflicts line does not come into it. The work is to verify, not rebuild: run BEH-CHAT-11's three tests in prototypes/behaviour-ast/ui/src/pages/Chat.test.tsx and BEH-SPACE-1's test in Project.test.tsx (both listed in kit-ui.tests.json). If they pass, the item is done. Do not edit kit.beh, kit-ui.beh, kit-hosted.beh or either card.
