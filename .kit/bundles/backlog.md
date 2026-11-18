# Hints when items wait on the user
Work-item: backlog
Flag: The note is a tentative idea ("Maybe kit should... or something like this"), not a clear request to build now; it also changes how Kit shows work to the owner. (level: high)
Cards:
- new: Kit shows a hint when items need the user's action and the pipeline is not acting on them
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 00:14Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
The owner suggests Kit show hints when it is not taking action on something the user needs to do. Example: the pipeline is not running items in the states new, ready, planning or development, and those items need action. Open ticket pipeline-stages-own-schedule (BEH-SCHED-1) covers per-stage schedules only, not hints, so this is a new idea. Not specced or built; parked in backlog for the owner to decide.
