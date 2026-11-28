# An epic shows the work left on it; out-of-scope features move to a new epic
Work-item: epic-work-left
Flag: the step list is my reading of his list, and moving features into a new epic changes how the epic tree is stored (level: high)
Cards:
- BEH-EPIC-10
- BEH-EPIC-11
- BEH-EPIC-4
- BEH-EPIC-8
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-28 09:48Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-28 09:50Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (FEAT-EPICS: BEH-EPIC-4, BEH-EPIC-8, new BEH-EPIC-10 and BEH-EPIC-11)
- prototypes/behaviour-ast/ui/src/epics.ts (epic counts, epicsOfTicket)
- prototypes/behaviour-ast/ui/src/components/ (the epic board and the epic page)
Context:
Epic board (BEH-EPIC-4) and epic page (BEH-EPIC-8) count behaviours built out of total today. Work left (BEH-EPIC-10) is separate from that count and must not replace it. Epic status toward the goal counted from tickets is BEH-TICKET-8 in kit.beh; do not change it here.
Epic blocks are parsed by the corpus parser (`epic EPIC-...`, BEH-EPIC-2), so a new epic from BEH-EPIC-11 is written in that same form.
09:50Z is a proposal, not a move: Kit raises a review request and changes nothing until he approves.
