# Kit chat: start on the next item, project select box, act from the chat
Work-item: kit-chat-entry-and-actions
Flag: "next waiting" in the 17:50Z note may mean the Inbox, not the chat, and the ChatGPT/Claude-style look has no card yet (level: low)
Cards:
- BEH-CHAT-1
- BEH-CHAT-2
- BEH-CHAT-3
- BEH-CHAT-4
- BEH-CHAT-6
- BEH-CHAT-9
- BEH-CHAT-14
- BEH-CHAT-19
- new: the chat page looks like a chat app (ChatGPT or Claude style), not a form
- new: the skip control is labelled "skip to next item", not "skip" (it reads as dismiss)
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 12:56Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 14:12Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:48Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:50Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:52Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:53Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:54Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CHAT-1 to BEH-CHAT-19, around lines 2852-3140)
- prototypes/behaviour-ast/behaviours/kit.notes.md (the source notes)
Context:
- Owner wants the chat to open already showing the next item that needs him, with no click first. It offers three routes in one line: address the next item, define a new spec, or discuss a project. The AI infers the route from what he types.
- A project select box sits at the top, defaulting to the project in view. Changing it says "Now talking about <project>". He can also name a project in his message.
- Off-topic message: the agent asks whether he means a different project and suggests projects, or offers to define a spec for a new one.
- History is kept across a project change (it is cached and cheap). Dropping context to save cost (BEH-CHAT-5) is parked as a backlog cost saver: do not build it in this item.
- Approve, deny, answer and undo from the chat (BEH-CHAT-6). After acting, the chat says "here is the next item", or offers to discuss new spec.
- A reply to, or a Show again of, the item in hand moves that item to the foot of the chat, because the chat is chronological.
- Constraint from the 12:56Z note: "talk it through" and the Inbox stay exactly as they are. Do not change them in this item.
- Record each guess as a "contract my assumption:" line in the corpus, so he can deny it.
Next step toward the goal: the builder takes BEH-CHAT-2 and the select box first, then the look, then the acting controls.
