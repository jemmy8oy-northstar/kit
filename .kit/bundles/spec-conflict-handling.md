# Spec conflicts: build anyway, ask first, or wait
Work-item: spec-conflict-handling
Flag: The note asks for a non-blocking conflict rule (build anyway, confirm when approving) and also mentions indexing specs by vector or semantic match, which the owner calls "needs more design" and which is BEH-CONFLICT-4 (level: low)
Cards:
- BEH-CONFLICT-1 "When two specs conflict, the agent builds one reading anyway, and the user confirms or denies it when approving"
- BEH-CONFLICT-2 "Settings says what the agent does when specs conflict: build anyway, ask first, or wait until it is resolved"
- BEH-CONFLICT-3 "The chat agent spots a conflict while the user is proposing spec, so it is settled before anything is built"
- BEH-CONFLICT-4 "The spec is indexed, so a behaviour like a new one is found quickly however large the spec grows"
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:47Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CONFLICT-1 to -4, near lines 2791-2850)
- prototypes/behaviour-ast/behaviours/kit.beh (BEH-RESOLVE-2 near line 231; the `# kit:conflicts` line)
Context:
The owner wants a non-blocking conflict rule: when specs conflict, the agent builds one reading anyway and the user confirms or denies when approving (BEH-CONFLICT-1). Settings should let him choose per project between build anyway, ask first, and wait until resolved, because waiting avoids spend he may not want (BEH-CONFLICT-2; the # kit:conflicts line and the onConflict field already carry this). The chat agent should spot conflicts while he proposes spec, so they are settled before anything is built (BEH-CONFLICT-3). Indexing specs by vector or semantic match for scale is design work, not build work (BEH-CONFLICT-4). The dev process at GET /api/process already has the "When two specs conflict" section, so do not restate it.
