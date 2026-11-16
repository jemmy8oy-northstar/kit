# Kit chat help: what Kit is, and that the chat makes no changes
Work-item: kit-chat-help
Flag: BEH-CHAT-7 already holds this and its contract says built, so there is nothing to build and no spec was edited (level: low)
Review: agreed — BEH-CHAT-7 in kit-ui.beh carries the 19:28Z note as a contract and has built contracts for the help text and the help button; nothing to build.
Cards:
- BEH-CHAT-7
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 19:28Z (chat help function)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
The note asks for a help function that explains what Kit is, and that the chat does not make changes: the owner discusses and defines specs with the chat, and a coding agent builds in the background. BEH-CHAT-7 in kit-ui.beh holds this. Its contracts say the agent's instructions cover what Kit is, that the chat changes no code, that a coding agent builds the spec in the background, and the tools it has; and a "What can Kit do?" header button or a "help" message puts Kit's help line in the thread without spending a reply. The card is not in kit.beh, so this run did not edit it. A builder should check the card against the note and change nothing unless the owner says the help text is wrong.

Test plan:
- BEH-CHAT-7: the agent's instructions name what Kit is, that the chat changes no code, that a coding agent builds the spec in the background, and every tool it has.
- BEH-CHAT-7: asked what it can do, the agent gives the can and cannot list, and for a cannot item it offers to leave a note rather than claiming it made the change.
- BEH-CHAT-7: clicking the "What can Kit do?" header button puts Kit's fixed help line in the thread, with no model call and no reply spent.
- BEH-CHAT-7: a message that is just "help" or "/help" puts the same help line in the thread, with no model call and no reply spent, and the agent is not told of it.
