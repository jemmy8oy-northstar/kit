# Malformed bundle needs a way to resolve
Work-item: bundle-malformed-resolve
Flag: note is vague on what resolving a malformed bundle means, and no existing card covers bundles (level: low)
Cards:
- BEH-TICKET-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-21 13:30Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- .kit/bundles/
Context:
A bundle at .kit/bundles/<slug>.md was malformed, so the builder could not use it. Kit needs a visible way to resolve this: the board shows the bad bundle and the line that is wrong, and triage rewrites it to the required shape (sections Title, Work-item, Cards, Notes, Files, Context in that order, every list item on its own "- " line). The new card BEH-TICKET-5 is the spec for this; no code exists yet for bundle checking.
