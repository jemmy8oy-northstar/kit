# Spec conflicts: build one reading, let him confirm, and set what happens per project
Work-item: spec-conflict-handling
Flag: the indexing of the spec for finding similar behaviours is an idea he marked backlog and said needs design; it is not for this item (level: high)
Review: revised — agreed: CONFLICT-4 indexing is parked (needs design) and stays out of Cards; corrected the kit-ui.beh line range to 2785-2850
Cards:
- BEH-CONFLICT-1
- BEH-CONFLICT-2
- BEH-CONFLICT-3
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:47Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CONFLICT-1 to BEH-CONFLICT-4, around lines 2785-2850)
- prototypes/behaviour-ast/behaviours/kit.beh (BEH-RESOLVE-2: a conflict is two behaviours that cannot both hold)
- prototypes/behaviour-ast/behaviours/kit.notes.md (the source note)
Context:
- Default is non-blocking: the agent builds one reading of the conflicting specs anyway. On approval he confirms or denies that reading. Denying says which reading is true, and the agent rebuilds to it.
- Settings gets a per-project choice for conflicts: build anyway, ask first, or wait until resolved. "Wait" spends nothing on that card, because building costs his money. The project's `# kit:conflicts` line carries the choice; the dev process already reads it.
- The chat agent spots conflicts while he is proposing spec, so they are settled before anything is built (BEH-CONFLICT-3). The code agent gives the triage/chat agent context to help him resolve.
- BEH-CONFLICT-4 (index the spec so similar behaviours are found quickly at scale; vectorise per snapshot, or verb and meaning matching) is parked. Do not build it here.
- Record each guess as a "contract my assumption:" line in the corpus, so he can deny it.
Built:
- No new tests this run: every test in the Test plan already exists on this branch, carries its card id, and passes (ConflictModeTests, UserMemoryTests BEH_CONFLICT_1, TriageTests BEH-CONFLICT-3, Settings.test.tsx "When specs conflict"). No code was changed.
- Whole suite: backend 1758 ran, 1756 passed, 2 failed (ReportConformanceTests: Report_reproduces_Node_on_the_branches_the_goldens_miss and Globalization_is_ICU_so_locale_order_is_reachable). Both fail only because this sandbox has no ICU library, so .NET ran in invariant globalization; they are not related to conflict handling. UI suite: 540 ran, all passed. The whole suite did not fully pass here.
Test plan:
- BEH-CONFLICT-1: UserMemoryTests BEH_CONFLICT_1: the shared process (GET /api/process, agent.md) has a "## When two specs conflict" section.
- BEH-CONFLICT-2: ConflictModeTests: "ask" and "wait" are read from the project's # kit:conflicts line into the summary; no line means build.
- BEH-CONFLICT-2: ConflictModeTests: only the exact words build, ask and wait count; anything else reads as build.
- BEH-CONFLICT-2: ConflictModeTests: only the project's primary file can set the choice.
- BEH-CONFLICT-2: ConflictModeTests: the writer adds, replaces and removes the line, and build restores the file's text.
- BEH-CONFLICT-2: ConflictModeTests: choosing the value the file already has changes nothing.
- BEH-CONFLICT-2: ConflictModeTests: a mode other than build, ask or wait is refused.
- BEH-CONFLICT-2: ConflictModeTests: the operator's choice over the route is a commit-shaped edit.
- BEH-CONFLICT-2: ConflictModeTests: a signed-in user who is not the operator is refused and nothing is written.
- BEH-CONFLICT-2: ConflictModeTests: a secondary file of a project is refused, saying where to set the choice.
- BEH-CONFLICT-2: ConflictModeTests: the request body must name build, ask or wait.
- BEH-CONFLICT-2: Settings.test.tsx: the operator sees one choice per project, build anyway unless the project says otherwise.
- BEH-CONFLICT-2: Settings.test.tsx: choosing posts the named mode and says so.
- BEH-CONFLICT-2: Settings.test.tsx: a refused choice shows the server's reason.
- BEH-CONFLICT-2: Settings.test.tsx: the conflict section is hidden from anyone who is not the operator.
- BEH-CONFLICT-3: TriageTests: before saving new spec, the chat looks for a card it conflicts with and asks which holds.
Next step toward the goal: the builder makes the "build anyway with a review request" path work first, then the Settings choice.
