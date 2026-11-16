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
