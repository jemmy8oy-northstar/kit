# Chat assistant: project picker, next item in view, act from chat
Work-item: kit-chat-assistant
Flag: Nine notes in one domain, and the spec already records each as built (BEH-CHAT-1 to -9 contract lines), so this is likely a gap check, not new build. A stronger model should confirm the code meets those contract lines before any change.
Cards:
- BEH-CHAT-1 "A Chat page talks with the spec agent about the project picked in a select box"
- BEH-CHAT-2 "The chat opens with the next item that needs him already in view, and he just starts typing"
- BEH-CHAT-3 "An item is answered or marked off from inside the chat, and new spec is left as a note"
- BEH-CHAT-4 "When he seems to be talking about another project, the agent asks, and names the projects it might be"
- BEH-CHAT-5 "To save cost, the chat can drop what came before a project change"
- BEH-CHAT-6 "The chat agent approves, denies or answers an item itself when he says so, and he can undo it"
- BEH-CHAT-7 "The chat knows what Kit is and what it can do, and explains it when asked"
- BEH-CHAT-9 "A Show again button reposts the item in hand at the foot of the chat"
- BEH-TRANSCRIPT-1 "His Chat transcripts are saved to a dedicated volume, outside the repo, and never for other users"
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 12:56Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 14:12Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:48Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:50Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:52Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:53Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:54Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:54Z (chat logging question)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 18:54Z (chat knows its capabilities)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CHAT-1 to -18 at lines 2508-2770; BEH-TRANSCRIPT-1 at line 2856)
- prototypes/behaviour-ast/ui/src/pages/Chat.tsx
- prototypes/behaviour-ast/ui/src/pages/Chat.test.tsx
- prototypes/behaviour-ast/ui/src/components/TriageChat.tsx
- prototypes/behaviour-ast/ui/src/components/TriageChat.test.tsx
- prototypes/behaviour-ast/ui/src/chat.ts
- backend/Balenthiran.Kit.Database/AnthropicTriageChat.cs
- backend/Balenthiran.Kit.Abstractions/Services/ITriageChat.cs
Context:
Mapping, all to existing cards, no new card needed. 12:56Z: project select box and "Now talking about" (CHAT-1), suggestions replaced by opening on the next item (CHAT-2), off-topic check (CHAT-4), history kept and cost saver (CHAT-5). 14:12Z: opens on the top waiting item, chat-style layout (CHAT-1, CHAT-2). 17:48Z: "Skip to next item" label (CHAT-3). 17:50Z: project first, then preview (CHAT-2). 17:52Z: reply puts the item into the thread above the reply; superseded by the 19:04Z note, which is not in this batch and is already built (CHAT-2). 17:53Z: approve, deny, answer and undo from chat (CHAT-6). 17:54Z: after an action the chat offers the next item (CHAT-3). 18:54Z logging question: the spec answers it in BEH-TRANSCRIPT-1 (dedicated volume, not for other users); whether the code does this is not checked in triage. 18:54Z capabilities: BEH-CHAT-7 (Kit's help line and the agent's instructions).
Builder: check the code against the contract lines in kit-ui.beh, not the notes. Only build the gap, if any. Other kit/work branches already touch chat (kit/work/chat-show-again-options-and-action-cards, kit/work/kit-one-chat-across-projects), and chat-* branches exist on origin; look at them before you build to avoid a clash. The model is claude-haiku-5-5 (AnthropicTriageChat.DefaultModel). Next step toward the goal: confirm whether the chat is built end to end on main, then close the gap.
Re-triage 2026-11-15: notes 12:56Z and 14:12Z arrived again with the same source refs and are already mapped above. Nothing new is added to the cards. The ChatGPT/Claude-style look is covered by BEH-CHAT-1 and -2; check the rendered page against it, not a new card.
