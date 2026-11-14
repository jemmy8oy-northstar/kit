# Notes page: archive, source tag and the public-warning question
Work-item: notes-page-archive-tag-and-warning
Flag: BEH-GATE-H2 is used by two cards with different text: kit-ui's "Every note box warns that what you type is public" and kit-hosted's "A write is refused unless the caller proved they are the operator". The 2026-10-31 01:01Z note does not say which one it is on. Check the note's position before answering.
Review: revised — the note sits in kit-ui.notes.md, so it is on kit-ui's BEH-GATE-H2 (the public warning), not kit-hosted's; that card already carries the answer, so Context now says to fold the note, not re-answer.
Cards:
- BEH-NOTE-9
- BEH-NOTE-11
- BEH-GATE-H2
- new: a note carries a tag showing where it came from (chat, floating note button, or the agent's background work), shown on the Notes page only
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:24Z (notes newest first, load-more button)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-16 11:53Z · BEH-NOTE-10
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-31 01:01Z · BEH-GATE-H2
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 13:55Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
- prototypes/behaviour-ast/behaviours/kit-hosted.beh
Context:
- Archive: BEH-NOTE-11 already covers the owner's request. Folded notes archive themselves, a Show archived button shows them with a count, and nothing expires yet. The 2026-10-16 note approves the archive and raises expiry for later. Expiry is not to be built now. The card has tests, so confirm it is built and record the approval.
- Warning question: "What does this mean?" on 2026-10-31 01:01Z is on kit-ui's BEH-GATE-H2 ("Every note box warns that what you type is public"): the note is in kit-ui.notes.md, which holds only kit-ui's notes. kit-ui.beh already has a "his question (kit-ui.notes.md#2026-10-31-0101)" contract answering it, so delete the note from kit-ui.notes.md and leave the card as is. Do not touch kit-hosted's BEH-GATE-H2. Resolve the duplicate ID with the owner before any rename.
- Source tag: there is no card for it yet. A note needs a tag showing where it came from (the chat, the floating note button, or the agent's background work). It shows on the Notes page only, not in the chat for now. The builder should say in the card where the tag is stored (the note's line in kit-ui.notes.md, or elsewhere) and should not change the chat.
- Related cards: BEH-NOTE-9 (notes listed newest first), BEH-NOTE-12 (floating note button), BEH-FEAT-7 (notes are a tab).

Triage added the 2026-10-16 07:24Z note: notes are listed most recent first, and past the first page a load-more button fetches the rest, so the whole list is not pulled into the UI. BEH-NOTE-9 already covers newest-first ordering. Check it first; the load-more paging may need a new card (level low: not sure whether BEH-NOTE-9 or BEH-NOTE-11 already covers paging).
