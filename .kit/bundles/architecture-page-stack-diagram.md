# Architecture page draws a deterministic diagram of the project's stack
Work-item: architecture-page-stack-diagram
Flag: this adds a diagram to a page the owner reads, and its layout is still open (level: high)
Cards:
- BEH-ARCH-4
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 18:01Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-15 12:52Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
BEH-ARCH-4 is already in the corpus as a behaviour alongside BEH-ARCH-1, which it extends and does not change: a project's architecture page draws a diagram of its stack, the same picture every time for the same repo. Generation must be deterministic.
The diagram's parts come from the repo: the databases, the external APIs, and the Helm chart.
Priority: the owner's latest note (11-15 12:52Z) overrides the earlier "backlog, not for now" on the 11-06 note. Do not backlog it. Keep it as a pending behaviour at low priority, so higher-priority work is built first. Do not build it ahead of the queue.
Open and left open by the owner: the exact layout, and whether anything from the spec is also drawn. Do not decide either; record them as unreviewed assumptions if a builder must pick.
