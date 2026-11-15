# Spec conflicts: build one reading, let him confirm, and set what happens per project
Work-item: spec-conflict-handling
Flag: the indexing of the spec for finding similar behaviours is an idea he marked backlog and said needs design; it is not for this item (level: high)
Cards:
- BEH-CONFLICT-1
- BEH-CONFLICT-2
- BEH-CONFLICT-3
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:47Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-CONFLICT-1 to BEH-CONFLICT-4, around lines 3148-3200)
- prototypes/behaviour-ast/behaviours/kit.beh (BEH-RESOLVE-2: a conflict is two behaviours that cannot both hold)
- prototypes/behaviour-ast/behaviours/kit.notes.md (the source note)
Context:
- Default is non-blocking: the agent builds one reading of the conflicting specs anyway. On approval he confirms or denies that reading. Denying says which reading is true, and the agent rebuilds to it.
- Settings gets a per-project choice for conflicts: build anyway, ask first, or wait until resolved. "Wait" spends nothing on that card, because building costs his money. The project's `# kit:conflicts` line carries the choice; the dev process already reads it.
- The chat agent spots conflicts while he is proposing spec, so they are settled before anything is built (BEH-CONFLICT-3). The code agent gives the triage/chat agent context to help him resolve.
- BEH-CONFLICT-4 (index the spec so similar behaviours are found quickly at scale; vectorise per snapshot, or verb and meaning matching) is parked. Do not build it here.
- Record each guess as a "contract my assumption:" line in the corpus, so he can deny it.
Next step toward the goal: the builder makes the "build anyway with a review request" path work first, then the Settings choice.
