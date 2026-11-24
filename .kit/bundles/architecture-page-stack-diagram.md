# Architecture page draws a deterministic diagram of the project's stack
Work-item: architecture-page-stack-diagram
Flag: this adds a diagram to a page the owner reads, and its layout is still open (level: high)
Review: agreed — the bundle matches BEH-ARCH-4 and both owner notes; the open layout and spec-drawing questions are already left to unreviewed assumptions at build time, as the owner asked, so no person needs to decide now
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

Test plan:
- BEH-ARCH-4: generating the diagram twice for the same repo gives byte-identical output, and a repo whose files are read in a different order gives the same output too (new; deterministic is the card's whole claim)
- BEH-ARCH-4: a repo that declares a database, an external API and a Helm chart shows each one as a part of the diagram, taken from the repo's own files (new)
- BEH-ARCH-4: a repo with none of the three shows the page without error and draws no invented part (new; edge case)
- BEH-ARCH-4: the diagram is produced by code and makes no model call, so it works with the model unavailable (new CONTROL-style check of the "plain code" assumption on the card)
- BEH-ARCH-4: opening the project's Architecture tab still lists the BEH-ARCH-1 decisions and their approve and deny buttons unchanged (existing BEH-ARCH-1 slice A and B1 tests; BEH-ARCH-4 must not change them)
- BEH-ARCH-4: the test does not assert the exact layout or whether spec items are drawn, since the owner left both open (no test; recorded here so the builder does not pin them by accident)

Built:
- BEH-ARCH-4: generating the diagram twice for the same repo gives byte-identical output, and a repo whose files are read in a different order gives the same output too (stackDiagram.test.ts)
- BEH-ARCH-4: a repo that declares a database, an external API and a Helm chart shows each one as a part, taken from the repo's own files (stackDiagram.test.ts)
- BEH-ARCH-4: a repo with none of the three shows no diagram and draws no invented part (stackDiagram.test.ts)
- BEH-ARCH-4: the diagram is produced by code and makes no model call, so it works with the model unavailable (stackDiagram.test.ts)
- BEH-ARCH-4: a repo with none of the three shows the page without error and draws no invented part (components/StackDiagram.test.tsx)
- Whole UI suite (prototypes/behaviour-ast/ui, `npx vitest run`): 566 tests, all passed. The existing BEH-ARCH-1 tests (DecisionsList and Project) pass unchanged. The C# backend suite was not run: dotnet is not installed here.
