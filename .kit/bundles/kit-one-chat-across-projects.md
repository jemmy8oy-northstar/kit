# One chat across kit, kit-ui and kit-hosted
Work-item: kit-one-chat-across-projects
Flag: BEH-CHAT-11 is marked built, but it depends on the three apps sharing a `# kit:project` line. Confirm that in kit.beh, kit-ui.beh and kit-hosted.beh before calling this done.
Cards:
- BEH-CHAT-11
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:36Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-hosted.beh
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
Context:
The owner wants one chat for kit, kit-ui and kit-hosted, not three. BEH-CHAT-11 already describes this: the Chat page picker lists projects, and apps sharing a `# kit:project` line are one entry. Each turn goes to the spec agent of the app the item in hand belongs to. With nothing in hand it goes to the project's home app, which is the app named like the project (kit), else the first app. The agent still reads one app's spec per turn. The card has a test line, so verify it rather than rebuild it. If the three apps do not share the project line, the fix is a corpus change, which is a spec edit: raise it for the owner and do not make it silently.
