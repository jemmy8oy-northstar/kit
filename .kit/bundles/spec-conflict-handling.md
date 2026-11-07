# Spec conflicts: build anyway, ask first, or wait
Work-item: spec-conflict-handling
Cards:
- BEH-CONFLICT-1 "When two specs conflict, the agent builds one reading anyway, and the user confirms or denies it when approving"
- BEH-CONFLICT-2 "Settings says what the agent does when specs conflict: build anyway, ask first, or wait until it is resolved"
- BEH-CONFLICT-3 "The chat agent spots a conflict while the user is proposing spec, so it is settled before anything is built"
- BEH-CONFLICT-4 "The spec is indexed, so a behaviour like a new one is found quickly however large the spec grows"
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:47Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CONFLICT-1 to -4 at lines 2791-2850)
- prototypes/behaviour-ast/behaviours/kit.beh (BEH-RESOLVE-2 at line 231; the `# kit:conflicts` line)
- backend/Balenthiran.Kit.Services/ConflictsDirective.cs
- backend/Balenthiran.Kit.Tests/ConflictModeTests.cs
- backend/Balenthiran.Kit.DataModels/Models/Conflict.cs
- backend/Balenthiran.Kit.DataModels/Models/ConflictQuestion.cs
- backend/Balenthiran.Kit.Abstractions/DataModels/IConflict.cs
Context:
The note asks for three things, all covered by existing cards. Non-blocking conflict, where the build goes ahead and the user confirms or denies at approval: BEH-CONFLICT-1. A setting per project for build anyway, ask first, or wait, so no spend happens on a conflicting card until it is settled: BEH-CONFLICT-2, whose contract says the default is build anyway and that wait spends nothing. Chat spotting conflicts during proposal: BEH-CONFLICT-3. The backlog idea of indexing or vectorising the spec as it grows is BEH-CONFLICT-4; it is the only card here with no built line in the spec, so it is backlog. No new card is needed.
Builder: check the code against the built lines of CONFLICT-1 to -3 and the ConflictModeTests. Do not start CONFLICT-4 from this bundle; it needs design first. Other origin branches touch the chat conflict work (chat-conflict-3-chat-5), so look at them before building. Next step toward the goal: confirm the three built cards on main, then decide whether to take CONFLICT-4 on as a design.
