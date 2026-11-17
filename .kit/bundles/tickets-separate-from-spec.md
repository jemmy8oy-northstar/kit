# Tickets are not specs: triage turns loose input into a spec change and a ticket, and blocks by tag
Work-item: tickets-separate-from-spec
Flag: the note changes how Kit stores and shows work (a ticket is not the spec, and the spec moves onto the feature branch), so the owner should check it; it may overlap the triage routing on pipeline-routing-by-cost, which this item does not change (level: high)
Review: revised — cards match the bundle; one test line named states (planned, implemented) no card defines, so it now tests only new and ready; moving the spec onto the feature branch still waits on the owner through the unreviewed contracts
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
Test plan:
- BEH-TICKET-1: a ticket on the board shows its status and its open questions, and the board shows no spec text; the spec of the change is read from that change's feature branch.
- BEH-TICKET-1: a ticket is one change to the spec, so two changes give two tickets, each with its own feature branch.
- BEH-TICKET-2: a note, a chat message, a comment, an approval and a rejection each reach triage, and each produces a ticket in the new state (four cases).
- BEH-TICKET-2: triage makes its spec change on a new feature branch and never on the default branch.
- BEH-TICKET-2: a card triage removes is marked to be deleted, with the reason written beside it, and is not deleted outright.
- BEH-TICKET-3: a ticket with an open question for him carries the blocked tag and stays the same ticket; no second ticket is created for the block.
- BEH-TICKET-3: blocked combines with the states new and ready, and setting or clearing the tag leaves the state unchanged.
- BEH-TICKET-3: triage's discussion of a blocked ticket is recorded on that ticket, and the ticket moves to ready once no question is open.
- BEH-TICKET-3: if he stays silent on a blocked ticket, triage acts on the default written on that ticket.
- BEH-TICKET-4: a ready ticket goes to technical design, then to implementation, in that order.
- BEH-TICKET-4: technical design and implementation can each set the blocked tag with a question, the same way triage does, and clear it when no question is open.
