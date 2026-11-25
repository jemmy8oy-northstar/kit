# Triage makes one ticket per change and runs one note at a time
Work-item: triage-tickets-per-note-serial
Flag: the card for serial triage is uncertain: BEH-SCHED-1 says stages are independent and already carries a review request about one-run-per-project, so the serial clause may belong on BEH-TICKET-2 alone (level: low)
Review: revised — the serial clause belongs on BEH-TICKET-2 alone (it already carries both new assumptions); BEH-SCHED-1 already notes triage as the exception and has an open review request, so it is dropped from Cards and left untouched
Cards:
- BEH-TICKET-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 22:31Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.tests.json
Context:
Two notes, same domain (triage). The first says kit can make several tickets from one note. The second says triage must run in serial so parallel triage runs cannot add duplicates or overlapping changes. BEH-TICKET-2 is the triage card; BEH-SCHED-1 is the stage-schedule card, and its existing review request about one run per project stays open and is not changed. Builder: write the tests first, map any new assertion in kit.tests.json, and do not edit other cards.
