# Kit specs as feature folders under .kit/features
Work-item: kit-feature-folder-layout
Flag: the 2026-11-24 22:28Z note is cut after its first 1500 characters and its source ref is not in kit.notes.md on this branch, so the folder layout is written from the visible text only; the owner's reply may ask for more; BEH-PARSE-5's second contract skips any folder with neither a readme nor a .kit file, which would also drop the child features of a folder that holds only subfolders, and the spec is silent on that (level: high)
Review: revised — confirmed the 22:28Z note is absent from kit.notes.md; Context now says BEH-PARSE-5/6 rest only on the visible text, are unreviewed, and must be built as written with nothing added
Cards:
- BEH-PARSE-2
- BEH-PARSE-5
- BEH-PARSE-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 22:28Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.tests.json
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
The owner does not want the split-by-area proposal (kit-ui.beh per area, Pipeline.cs per stage). He wants specs defined under .kit/features as a folder hierarchy: a feature folder holds child feature folders and/or {name}.kit spec files, and may hold a readme.md that describes the feature (not a spec). A child (folder, .kit file or readme) tags its parent with an `in` line, and may tag other features too. A health check must confirm that every kit and readme under a feature folder tags its parent. Existing parse rules: BEH-PARSE-2 refuses an `in` that names no feature, a feature that is its own ancestor, or a duplicate feature id; `in` is a display tag, not a dependency (`serves` is the dependency). FEAT-PARSER is the parse area. Kit's own spec is still prototypes/behaviour-ast/behaviours/kit.beh, so the move of files into .kit/features is not part of this change. Builder: write the parser checks and tests first, keep kit.tests.json mapping each new card to its test, and do not move any spec file in this change. The 2026-11-24 22:28Z note is not in kit.notes.md on this branch (BEH-PARSE-5 and BEH-PARSE-6 cite kit.notes.md#2026-11-24-2228Z, which does not resolve), and only its first 1500 characters were seen, so both cards are unreviewed assumptions drawn from that visible text. Build exactly what their contracts say and add no rule the visible text does not state; anything more the owner asks for in reply comes back as a new card, not a change to these.
Test plan:
- BEH-PARSE-2: a kit or readme inside a feature folder whose `in` names no feature in the project is refused as BadFeatureTag, naming its file and line
- BEH-PARSE-2: a folder child whose `in` makes its feature its own ancestor is refused as BadFeatureTag, naming its line
- BEH-PARSE-2: a feature id declared by two folders, or by a folder and a .kit file, is refused as BadFeatureTag, naming both
- BEH-PARSE-5: .kit/features/Alpha/readme.md plus Alpha/Beta/beta.kit tagged `in Alpha` parse to features Alpha and Beta, reported as feature:FromFolders
- BEH-PARSE-5: a readme.md is read as the feature's description and is never parsed as a spec (no behaviour is created from it)
- BEH-PARSE-5: an empty folder under .kit/features is skipped and does not fail the parse
- BEH-PARSE-5: a folder holding only child folders (no readme, no .kit file) is pinned to the reading the owner picks on the Flag; until then the test asserts only that the parse does not crash
- BEH-PARSE-6: a .kit file under a feature folder with no `in` line is refused as MissingParentTag, naming the file
- BEH-PARSE-6: a readme.md under a feature folder with no `in` line is refused as MissingParentTag, naming the file
- BEH-PARSE-6: a child that tags its parent and also tags another feature parses with no refusal, the extra tag is kept as a display tag and is not a dependency
- BEH-PARSE-6: a missing parent tag makes the check fail the same build as a behaviour with no test (BEH-GATE-1), with a non-zero exit
