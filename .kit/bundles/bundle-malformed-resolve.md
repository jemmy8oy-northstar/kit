# Malformed bundle needs a way to resolve
Work-item: bundle-malformed-resolve
Flag: Context says no code checks bundles, but ContextBundle.Parse already names the bad line and Pipeline stops the item with "its bundle is malformed: ..." (PipelineTests), and BEH-TICKET-5's source note is missing, so the board part is the only new work and is unconfirmed (level: high)
Review: revised — BEH-TICKET-5 does cover bundles and its two contracts define resolving (show the bad bundle and wrong line, no builder starts; triage rewrites to shape and records the fix); the kit.notes.md 2026-11-21 13:30Z note does not exist in the file, so Context now says so
Cards:
- BEH-TICKET-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-21 13:30Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- .kit/bundles/
Context:
A bundle at .kit/bundles/<slug>.md was malformed, so the builder could not use it. Kit needs a visible way to resolve this: the board shows the bad bundle and the line that is wrong, and triage rewrites it to the required shape (sections Title, Work-item, Cards, Notes, Files, Context in that order, every list item on its own "- " line). The new card BEH-TICKET-5 is the spec for this. Checking exists already: backend/Balenthiran.Kit.Services/ContextBundle.cs Parse rejects a bad bundle and names the line, and Pipeline.cs stops the item with that reason. Not confirmed: whether the board shows a malformed bundle and its line, and whether triage rewrites one.
The note kit.notes.md#2026-11-21 13:30Z listed under Notes is not present in kit.notes.md (nor is the card source anchor 2026-11-21-1330Z); treat the two BEH-TICKET-5 contracts as the whole spec.
Test plan:
- BEH-TICKET-5: a bundle that fails ContextBundle.Parse is listed on the board with the line Parse names as wrong, and no build run starts on it
- BEH-TICKET-5: a well-formed bundle is not listed as malformed, and a build run starts on it as before
- BEH-TICKET-5: triage rewrites a malformed bundle so that it parses, keeps its Cards and Notes items unchanged, and puts every list item on its own "- " line
- BEH-TICKET-5: after triage rewrites a malformed bundle, the fix is recorded beside the ticket
