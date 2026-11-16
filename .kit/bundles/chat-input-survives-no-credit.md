# Chat and note input is kept when subscription credits run out
Work-item: chat-input-survives-no-credit
Flag: the note says "chart", most likely a typo for chat, and which chat card it belongs to is unsure (level: low)
Review: agreed — card BEH-KEEP-1 already records the chart→chat reading as an unreviewed contract the owner can deny; placement under a BEH-CHAT-* card is a later refinement, not a blocker
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
Built:
- BEH-KEEP-1 note box: a refused note write (credits used up) leaves the typed text in the box (NotesPanel.test.tsx, "with typed text in the note box and credits exhausted, a failed pipeline start leaves the note text in the box")
- BEH-KEEP-1 chat box: a refused chat turn (credits used up) leaves the typed text in the box (Chat.test.tsx, "with typed text in the chat box and credits exhausted, a failed pipeline start leaves the chat text in the box")
- BEH-KEEP-1 note, after the failure: nothing retries or clears it; only a new submit sends it once the pipeline can run (NotesPanel.test.tsx, "after a failed start the kept note is not cleared, sent again, or dropped until he submits it again once the pipeline can run")
- BEH-KEEP-1 chat, after the failure: nothing retries or clears it; only a new send carries it once the pipeline can run (Chat.test.tsx, "after a failed start the kept chat text is not cleared, sent again, or dropped until he sends it again once the pipeline can run")
- Mapped the four tests to BEH-KEEP-1 in prototypes/behaviour-ast/behaviours/kit.tests.json, so BEH-GATE-1 can see them.
- No production code changed: the existing UI already kept the text, so the new tests passed on first run. Breaking the keep logic in NotesPanel.tsx and Chat.tsx made all four fail, then the code was restored.
- Suite: the UI vitest suite (npm test in prototypes/behaviour-ast/ui) ran 571 tests and all passed, and tsc -b is clean. The C# suite was not run: dotnet is not installed here.

Test plan:
- BEH-KEEP-1: with typed text in the note box and credits exhausted, a failed pipeline start leaves the note text in the box (input:Kept).
- BEH-KEEP-1: with typed text in the chat box and credits exhausted, a failed pipeline start leaves the chat text in the box (input:Kept).
- BEH-KEEP-1: after a failed start the kept note or chat text is not cleared or sent anywhere until the user submits it again once the pipeline can run.
