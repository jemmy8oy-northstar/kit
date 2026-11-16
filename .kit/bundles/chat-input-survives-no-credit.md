# Chat and note input is kept when subscription credits run out
Work-item: chat-input-survives-no-credit
Flag: the note says "chart", most likely a typo for chat, and which chat card it belongs to is unsure (level: low)
Cards:
- BEH-KEEP-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 10:56Z (note and chat inputs are not lost when credits run out)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
New card in kit.beh only. It has no test yet, so kit check kit fails BEH-GATE-1 until a builder maps it.
The existing chat cards (BEH-CHAT-* in kit-ui.beh) were not read; the owner should say whether this belongs on one of them.
Next step toward the goal: a builder keeps typed input across a failed pipeline start.
