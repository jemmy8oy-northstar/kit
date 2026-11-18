# Kit chat: start on the next item, project select box, act from the chat
Work-item: kit-chat-entry-and-actions
Flag: the owner's reply "Surely it can live in The db for the user" is read as: the chat's look and per-user state live in the database for that user, not in a repo card; "it" is not explicit (level: high)
Review: escalated — the reading is unlikely, since a look is code and cannot be kept in a database; "it" most likely means the chat history, which BEH-CHAT-1 says is forgotten when he leaves the page. Owner to decide: keep each user's chat history in Kit's database (a new behaviour, not in this item), or did "it" mean something else? Default if silent: build the cards as listed, keep the history in the page, and record per-user history in the database as a parked unreviewed suggestion
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
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-15 12:50Z (when in chat, see the state of the top-of-stack item being shown)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 12:56Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 14:12Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:48Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:50Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:52Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:53Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:54Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:54Z (chat knows its capabilities)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:55Z (chat reads the project spec)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 19:04Z (stack order, approve, post-as buttons)
- ticket kit-chat-entry-and-actions, reply of 2026-11-18 21:22Z ("Surely it can live in The db for the user")
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CHAT-1 to BEH-CHAT-19, around lines 2852-3140; BEH-CHAT-1 gained the reply contract)
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
- Malformed-bundle fix (2026-11-18): the card line "none new: ..." was removed from Cards because Cards takes ids only. The reason it gave is kept here: the chat-app look is already BEH-CHAT-1's 14:12Z contract built (messages left and right, docked box), so no card is added.
- Owner reply (2026-11-18 21:22Z): "Surely it can live in The db for the user". Recorded as an unreviewed contract on BEH-CHAT-1. The reading is open: the owner should confirm what "it" is.
- Record each guess as a "contract my assumption:" line in the corpus, so he can deny it.
Test plan:
- BEH-CHAT-1: picking a project in the select box puts "Now talking about <project>" in the thread and keeps the earlier history; a message naming exactly one other project moves the chat before it is sent; naming two, or the current one too, does not
- BEH-CHAT-2: opening the page with an item waiting shows that item in the thread above the message box with no click and no request sent; with nothing waiting it says so and offers "<project>: next waiting is “<title>”", which switches project when taken
- BEH-CHAT-3: Approve and "Skip to next item" make the Inbox's own review write, Skip leaves the item waiting, and the next item comes up; "Post as my answer" and "Save as a note" are gone and "Ask Kit to note this chat" is the one note control
- BEH-CHAT-4: an off-topic message gets the agent's question naming other projects from its context, and the agent does not change project itself
- BEH-CHAT-6: the Chat page offers approve_item, deny_item, answer_item, undo_last, show_item and save_note while Talk it through offers none; an action with no item in hand is dropped and Kit says so; a refused write shows "That did not go through, so nothing was done"; Undo restores the review and brings the item back to the thread; Approve and Skip wait while a reply is pending
- BEH-CHAT-7: the "What can Kit do?" button and a message of "help" or "/help" put the fixed help line in the thread with no model call and no reply spent; the agent's instructions carry the can and cannot list
- BEH-CHAT-8: read_spec returns whole cards by id, at most ten ids and 20,000 characters a call, at most two reads per reply; it is offered on the Chat page and in Talk it through
- BEH-CHAT-9: "Show again" puts the item in hand at the foot of the thread with its question, sends no request, and waits while a reply is on its way
- BEH-CHAT-14: the line after an action reads "Approved “title”. Reply to answer the next item, or we can define new spec or discuss x.", never says "Next up" or repeats the next item's title, and the dock's jump to another project's waiting item still works
- BEH-CHAT-19: the chat shows the board state (pending, approved, blocked, built) of the item in hand beside its id, and it updates after an answer or approval from the chat

Next step toward the goal: the builder takes BEH-CHAT-2 and the select box first, then the look, then the acting controls. Before rebuilding any card above, check its "contract built" lines on main.
