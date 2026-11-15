# Triage chat: API key, model and spend
Work-item: chat-agent-secrets-and-spend
Flag: The three notes are about the chat agent's key, model and spend rather than a feature, and the 10-31 23:53Z desk-time note has no concrete ask. A stronger model should decide whether this stays a separate bundle or folds into kit-chat-assistant. (level: low)
Cards:
- BEH-SPECAGENT-4 "The coding agent's blockers go on the same board, and the spec agent talks each one through with the user" (its built contract already names the claude-haiku-5-5 chat, the kit-anthropic key and the daily cap)
- new: Kit shows how much of the Anthropic credit the chat has used, so the $10 credit's life can be read
- BEH-ORCH-1 "New notes and chat are triaged by a cheap model into a context bundle per work item, and the run ends"
- BEH-USERS-7 "A user sees what their compute and AI tokens cost, and pays for them"
- BEH-USERS-5 "A user's production secrets are stored for them, and never shown back in a page or a log"
- BEH-BOARD-23 "A card whose code is built but still waits on him, for a secret or a step, is Blocked, not Done"
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 23:53Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 12:04Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 12:12Z
Files:
- backend/Balenthiran.Kit.Database/AnthropicTriageChat.cs (DefaultModel = "claude-haiku-5-5", MaxTokens = 4096)
- backend/Balenthiran.Kit.Abstractions/Services/ITriageChat.cs
- backend/Balenthiran.Kit.Services/StageModelSettings.cs
- backend/Balenthiran.Kit.Services/StageModels.cs
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-ORCH-1 line 1367, BEH-BOARD-23 line 831, BEH-USERS-5 line 1512, BEH-USERS-7 line 1565)
Context:
12:04Z: the user has added a secret named kit-anthropic, holding the key under the name Anthropic__ApiKey, to unblock the triage chat, and asked for Haiku. The model is already the default in AnthropicTriageChat.cs (claude-haiku-5-5). This is already built under BEH-SPECAGENT-4, so no new card is needed for it. Builder: confirm the key is read from that secret at runtime. Never write the key value into any file, log or PR.
12:12Z: the Anthropic account has $10 of credit with no auto top-up. The user wants to know how long it lasts on personal use, so the chat's spend needs to be visible to him. BEH-USERS-7 covers the cost a user sees, but it waits on BEH-USERS-9 per its contract, so the new card above is for the operator's own view. Related cost saver: BEH-CHAT-5 (drop turns before a project change), built per its contract.
23:53Z (10-31): the user has desk time and asked Kit to prepare any secrets or desk work. The note names no specific item. Builder: list the steps that wait on him from cards in Blocked (BEH-BOARD-23) and put that list in the PR for him to act on at his desk. Do not guess secrets and do not create any.
Next step toward the goal: confirm the chat's key and model on main, then add the spend view.
