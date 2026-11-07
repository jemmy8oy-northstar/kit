# Board columns: Backlog holds unplanned work, Queue holds ready work, Blocked is for the human
Work-item: board-columns-blocked-queue
Flag: Conflict: the note asks to drop Scheduled, but BEH-SCHED-1 to BEH-SCHED-3 still specify a schedule that backlog behaviours can be moved into. Check the # kit:conflicts line before building; do not remove the schedule until the user answers.
Cards:
- BEH-BOARD-18
- BEH-BOARD-23
- BEH-BOARD-16
- BEH-SCHED-1
- BEH-SCHED-2
- BEH-SCHED-3
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-22 22:37Z (lines 197-207)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.schedule
- prototypes/behaviour-ast/ui/src/components/SchedulePanel.tsx
- prototypes/behaviour-ast/ui/src/pages/Board.tsx
Context:
BEH-BOARD-18 already gives the pending columns as Backlog, Queue, Blocked and In progress, with no Scheduled, so the board is closer to the note than the schedule specs are. BEH-BOARD-23 says a card whose code is built but waits on him is Blocked. BEH-BOARD-16 says a paused card stays on Backlog and the agent does not build it, which is the note's "no plan right now" case.
What the note adds: Queue should hold the ready-to-execute work, and Backlog should hold unplanned work. The open part is whether the schedule (BEH-SCHED-1 to 3, kit-ui.schedule, SchedulePanel) goes away or becomes the Queue. That is the conflict in the flag above.
