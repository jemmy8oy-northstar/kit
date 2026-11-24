# Kit specs as feature folders under .kit/features
Work-item: kit-feature-folder-layout
Flag: the 2026-11-24 22:28Z note is cut after its first 1500 characters and its source ref is not in kit.notes.md on this branch, so the folder layout is written from the visible text only; the owner's reply may ask for more (level: high)
Cards:
- BEH-PARSE-2 (changed: contract lines added)
- BEH-PARSE-5 (new)
- BEH-PARSE-6 (new)
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 22:28Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.tests.json
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
The owner does not want the split-by-area proposal (kit-ui.beh per area, Pipeline.cs per stage). He wants specs defined under .kit/features as a folder hierarchy: a feature folder holds child feature folders and/or {name}.kit spec files, and may hold a readme.md that describes the feature (not a spec). A child (folder, .kit file or readme) tags its parent with an `in` line, and may tag other features too. A health check must confirm that every kit and readme under a feature folder tags its parent. Existing parse rules: BEH-PARSE-2 refuses an `in` that names no feature, a feature that is its own ancestor, or a duplicate feature id; `in` is a display tag, not a dependency (`serves` is the dependency). FEAT-PARSER is the parse area. Kit's own spec is still prototypes/behaviour-ast/behaviours/kit.beh, so the move of files into .kit/features is not part of this change. Builder: write the parser checks and tests first, keep kit.tests.json mapping each new card to its test, and do not move any spec file in this change.
