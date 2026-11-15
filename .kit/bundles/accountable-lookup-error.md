# "Who's accountable" can be read without an error
Work-item: accountable-lookup-error
Flag: a bug report on a card I could not identify from the list of existing cards; the owning card is unknown (level: low)
Cards:
- new: "who's accountable" for an item can be read without an error
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 22:52Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- The owner reports the error "Getting who's accountable can't be read" as current. This is a bug, not a new behaviour: reproduce it first, then find which read path fails.
- Find the owning card by searching the corpus for "accountable" before choosing a card id; it may already exist.
