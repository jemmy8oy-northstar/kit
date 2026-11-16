# Tickets are not specs: triage turns loose input into a spec change and a ticket, and blocks by tag
Work-item: tickets-separate-from-spec
Flag: the note changes how Kit stores and shows work (a ticket is not the spec, and the spec moves onto the feature branch), so the owner should check it; it may overlap the triage routing on pipeline-routing-by-cost, which this item does not change (level: high)
Cards:
- BEH-TICKET-1
- BEH-TICKET-2
- BEH-TICKET-3
- BEH-TICKET-4
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-15 15:20Z (target pipeline: input surface, triage, tickets separate from spec, blocked as a tag, technical design and implementation after ready)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
New cards in kit.beh only, because this item may edit only that spec. The file's header marks it as the command-line layer, while these cards describe the board and the agents, so a builder may move them to kit-ui.beh (level: low).
The input surface is writing a note, speaking to chat, leaving a comment, and approving or rejecting; BEH-TICKET-2 covers all four.
The owner asked whether blocked should be a tag rather than a ticket; BEH-TICKET-3 takes the tag reading as the default.
Next step toward the goal: a builder maps BEH-TICKET-1 to 4 to tests once the owner has read this item.
