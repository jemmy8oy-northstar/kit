# Chat assistant: project picker, next item in view, act from chat
Work-item: kit-chat-assistant
Flag: Chat UX change that alters how the owner sees and acts on his work, and the 12:56Z note also asks to park discarding chat context as a cost saver (BEH-CHAT-5), which is a backlog idea and not to be built now (level: high)
Cards:
- BEH-CHAT-1 "A Chat page talks with the spec agent about the project picked in a select box"
- BEH-CHAT-2 "The chat opens with the next item that needs him already in view, and he just starts typing"
- BEH-CHAT-3 "An item is answered or marked off from inside the chat, and new spec is left as a note"
- BEH-CHAT-4 "When he seems to be talking about another project, the agent asks, and names the projects it might be"
- BEH-CHAT-5 "To save cost, the chat can drop what came before a project change" (backlog: do not build now)
- BEH-CHAT-6 "The chat agent approves, denies or answers an item itself when he says so, and he can undo it"
- BEH-CHAT-9 "A Show again button reposts the item in hand at the foot of the chat"
- new: Skip in the chat reads as "Skip to next item", not as a dismiss
- new: a reply to the item in hand moves that item to the foot of the chat, in the order the agent sees it
- new: after an item is acted on, the chat offers the next item or a discussion of new spec
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 12:56Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 14:12Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:48Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:50Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:52Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:53Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:54Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (the BEH-CHAT-* cards)
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
- prototypes/behaviour-ast/ui/src/pages/Chat.tsx
- prototypes/behaviour-ast/ui/src/pages/Chat.test.tsx
Context:
The owner wants a chat that feels like ChatGPT or Claude, not a page behind a click. On opening, the context already shows the default item to address (the top of the stack, BEH-CHAT-2), with a line such as "address the top item, or discuss other work". The agent infers the route from the message: answer an inbox item, define new spec, or discuss a project. The project defaults to the item's project; he can tap a select box to change it or name it in the message, and the chat then says "Now talking about X project" (BEH-CHAT-1, one chat per project, BEH-CHAT-11). If he seems off topic, the agent asks whether he means a different project and suggests likely ones (BEH-CHAT-4), or offers to define spec for a new project. Replies in the chat keep history across a project change; discarding context is a backlog cost saver (BEH-CHAT-5), not part of this build. Approve, deny, answer and undo from chat (BEH-CHAT-6). The Inbox's talk-it-through and Inbox stay as they are; this is a new feature beside them. Some of these cards already carry built contract lines (see kit/work/chat-show-again-options-and-action-cards and kit/work/chat-answers-links-capabilities-input), so check the gap before rebuilding.
