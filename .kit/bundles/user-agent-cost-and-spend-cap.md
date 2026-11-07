# User agent: cost estimate and spend cap
Work-item: user-agent-cost-and-spend-cap
Flag: The 2026-11-03 23:30Z note is tagged BEH-SPECAGENT-2, but its text is the user's spend cap for their Kit agent. BEH-USERS-9 already quotes it at kit-ui.beh near line 1488. Confirm the tag before filing. The 2026-10-31 00:57Z note asks for an audit of Kit's own materials; a list of what a generic user agent may reuse is a document rather than a card, so the builder may want to answer it as a note.
Cards:
- BEH-USERS-9
- BEH-USERS-7
- BEH-USERS-4
- new: an audit lists which of Kit's own materials a user's generic agent may reuse, and which must not leave James's own repo
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-31 16:45Z · BEH-USERS-9
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 23:30Z · BEH-SPECAGENT-2
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 00:57Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
Context:
- Cost estimate: the owner asks how much a feature will cost to execute with the Anthropic API on Opus 5.5, and expects it to be expensive. Give a per-stage token estimate and a cost figure only from current Anthropic pricing, looked up in the claude-api skill or the official pricing page. Do not answer from memory. Post the estimate as an answer on BEH-USERS-9.
- Spend cap: the owner's answer is that the user can spend up to the money credited to their account, and may also set a money allowance per day, week or month for their Kit agent, drawn from that credit. BEH-USERS-9 already quotes this (kit-ui.beh near line 1488) and says the cap is the credit plus the user's allowance. BEH-USERS-7 (cost and payment, pending, priority 4) says it waits for BEH-USERS-9. No new card is needed for the cap unless the builder finds the allowance has no place in BEH-USERS-9.
- The API key is already in use for chat, per the same note.
- BEH-USERS-7 has no tests yet. Leave it pending unless USERS-9 is built.
- The 2026-10-31 00:57Z note asks the same cost question as the 16:45Z note above, plus where Kit stands on supporting other users and whether its own materials can be reused for a generic bot without leaking. Answer the cost once, on BEH-USERS-9.
- Audit: BEH-USERS-4 says every user's agent runs the same generic dev process, so the process file is the reusable part. Anything that is James's own (his keys, secrets, repo memory, personal notes) must not reach a user's agent. The output is an answer on BEH-USERS-9 or a note listing the candidates. No file changes.
