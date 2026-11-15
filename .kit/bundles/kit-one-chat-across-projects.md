# One chat across kit, kit-ui and kit-hosted
Work-item: kit-one-chat-across-projects
Flag: Conflict with BEH-SPACE-1 (each project is its own space with its own chat): the owner asks for one chat across kit, kit-ui and kit-hosted, which contradicts it. Check the # kit:conflicts line before building. BEH-CHAT-11 is marked built but depends on the three apps sharing a # kit:project line; confirm that in kit.beh, kit-ui.beh and kit-hosted.beh (level: high)
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
The owner wants one chat for kit, kit-ui and kit-hosted, not three. BEH-CHAT-11 already describes this: the Chat page picker lists projects, and apps sharing a `# kit:project` line are one entry. Each turn goes to the spec agent of the app the item in hand belongs to. With nothing in hand it goes to the project's home app, which is the app named like the project (kit), else the first app. The agent still reads one app's spec per turn. The card has a test line, so verify it rather than rebuild it. If the three apps do not share the project line, the fix is a corpus change, which is a spec edit: raise it for the owner and do not make it silently.
BEH-SPACE-1 says each project has its own chat, board and config. BEH-CHAT-11 says one chat covers every app of a project. The owner's note joins three projects into one chat, which neither card says. This is a conflict under the process, so build neither reading silently; the # kit:conflicts setting decides. Do not edit kit.beh or BEH-SPACE-1 to make it go away.
