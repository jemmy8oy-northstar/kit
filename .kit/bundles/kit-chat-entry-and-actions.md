# Kit chat: start on the next item, project select box, act from the chat
Work-item: kit-chat-entry-and-actions
Flag: "next waiting" in the 17:50Z note may mean the Inbox, not the chat, and the ChatGPT/Claude-style look has no card yet; the 18:54Z capabilities, 18:55Z read-spec and 19:04Z stack/approve notes were already filed on main and marked built, so this run changed no spec (level: low)
Cards:
- BEH-CHAT-1
- BEH-CHAT-2
- BEH-CHAT-3
- BEH-CHAT-4
- BEH-CHAT-6
- BEH-CHAT-7
- BEH-CHAT-8
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
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:54Z (chat knows its capabilities)
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:55Z (chat reads the project spec)
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 19:04Z (stack order, approve, post-as buttons)
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
- 18:54Z (capabilities): BEH-CHAT-7 says the agent knows what Kit is and what it can do, with a help line. Already on main, marked built.
- 18:55Z (read spec): BEH-CHAT-8 gives the agent read_spec, which returns whole cards on demand. Already on main, marked built. Its backlog-scale question (index the spec) sits in kit-ui.beh near line 3200.
- 19:04Z (stack order, approve, buttons): the item in hand is in the thread in message order; the per-message "Post as my answer" and "Save as a note" buttons are replaced by one "Ask Kit to note this" button; the agent approves, denies or answers via tools (BEH-CHAT-6), and show_item brings the item back. Already on main, marked built.
- Record each guess as a "contract my assumption:" line in the corpus, so he can deny it.
Next step toward the goal: the builder takes BEH-CHAT-2 and the select box first, then the look, then the acting controls. Before rebuilding any card above, check its "contract built" lines on main.
