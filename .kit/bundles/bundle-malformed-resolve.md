# Malformed bundle needs a way to resolve
Work-item: bundle-malformed-resolve
Flag: note is vague on what resolving a malformed bundle means, and no existing card covers bundles (level: low)
Review: revised — BEH-TICKET-5 does cover bundles and its two contracts define resolving (show the bad bundle and wrong line, no builder starts; triage rewrites to shape and records the fix); the kit.notes.md 2026-11-21 13:30Z note does not exist in the file, so Context now says so
Cards:
- BEH-TICKET-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-21 13:30Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- .kit/bundles/
Context:
A bundle at .kit/bundles/<slug>.md was malformed, so the builder could not use it. Kit needs a visible way to resolve this: the board shows the bad bundle and the line that is wrong, and triage rewrites it to the required shape (sections Title, Work-item, Cards, Notes, Files, Context in that order, every list item on its own "- " line). The new card BEH-TICKET-5 is the spec for this; no code exists yet for bundle checking.
The note kit.notes.md#2026-11-21 13:30Z listed under Notes is not present in kit.notes.md (nor is the card source anchor 2026-11-21-1330Z); treat the two BEH-TICKET-5 contracts as the whole spec.
