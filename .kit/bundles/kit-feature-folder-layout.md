# Kit specs as feature folders under .kit/features
Work-item: kit-feature-folder-layout
Flag: the 23:07Z note is cut after its first 1500 characters and its text is not in kit.notes.md on this branch, so the owner's reply is taken from the visible text only, and the rest of the note may ask for more; the 22:28Z note is also absent; BEH-PARSE-5 and BEH-PARSE-6 rest on the visible text only and stay unreviewed (level: high)
Review: agreed — checked: neither note is in kit.notes.md, but both cards are unreviewed, tests-first, move no files and add nothing past the visible text, so a fuller note only adds new cards; the builder should know BEH-PARSE-5/6 cite #2026-11-24-2228Z although their text comes from the 23:07Z reply
Cards:
- BEH-PARSE-2
- BEH-PARSE-5
- BEH-PARSE-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 22:28Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 23:07Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.tests.json
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
The owner does not want the split-by-area proposal (kit-ui.beh per area, Pipeline.cs per stage) built; the 23:07Z reply asks for the feature folder layout instead, so do not build the split. He wants specs defined under .kit/features as a folder hierarchy: a feature folder holds child feature folders and/or {name}.kit spec files, and may hold a readme.md that describes the feature (not a spec). A child (folder, .kit file or readme) tags its parent with an `in` line, and may tag other features too. A health check must confirm that every kit and readme under a feature folder tags its parent. Existing parse rules: BEH-PARSE-2 refuses an `in` that names no feature, a feature that is its own ancestor, or a duplicate feature id; `in` is a display tag, not a dependency (`serves` is the dependency). FEAT-PARSER is the parse area. Kit's own spec is still prototypes/behaviour-ast/behaviours/kit.beh, so the move of files into .kit/features is not part of this change. Builder: write the parser checks and tests first, keep kit.tests.json mapping each new card to its test, and do not move any spec file in this change. The 22:28Z and 23:07Z notes are not in kit.notes.md on this branch (BEH-PARSE-5 and BEH-PARSE-6 cite kit.notes.md#2026-11-24-2228Z, which does not resolve), so both cards are unreviewed assumptions drawn from the visible text. Build exactly what their contracts say and add no rule the visible text does not state; anything more the owner asks for in reply comes back as a new card, not a change to these. BEH-PARSE-5 was corrected: a folder holding only child feature folders is a feature, since the note says "and/or".
Built:
- BEH-PARSE-2: BEH_PARSE_2_a_kit_or_readme_in_a_feature_folder_whose_in_names_no_feature_is_refused_naming_its_file_and_line (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-2: BEH_PARSE_2_a_folder_child_whose_in_makes_its_feature_its_own_ancestor_is_refused_naming_its_line (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-2: BEH_PARSE_2_a_feature_id_declared_by_two_folders_or_by_a_folder_and_a_kit_file_is_refused_naming_both (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-5: BEH_PARSE_5_a_kit_and_a_readme_under_feature_folders_parse_to_features_reported_from_folders (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-5: BEH_PARSE_5_a_readme_is_the_feature_description_and_is_never_parsed_as_a_spec (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-5: BEH_PARSE_5_an_empty_folder_under_features_is_skipped_and_does_not_fail_the_parse (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-5: BEH_PARSE_5_a_folder_holding_only_child_folders_is_a_feature_and_its_child_features_parse (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-6: BEH_PARSE_6_a_kit_file_under_a_feature_folder_with_no_in_line_is_refused_naming_the_file (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-6: BEH_PARSE_6_a_readme_under_a_feature_folder_with_no_in_line_is_refused_naming_the_file (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-6: BEH_PARSE_6_a_kit_file_whose_only_in_names_a_feature_other_than_its_parent_is_refused_naming_the_file (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-6: BEH_PARSE_6_a_child_that_tags_its_parent_and_another_feature_parses_and_the_extra_tag_is_display_only (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
- BEH-PARSE-6: BEH_PARSE_6_a_missing_parent_tag_fails_the_check_with_a_non_zero_exit_as_a_behaviour_with_no_test_does (backend/Balenthiran.Kit.Tests/FeatureFolderTests.cs)
Whole suite: 2194 backend tests ran (dotnet test ./backend, Release) and all 2194 passed; kit and kit-ui gates exit 0; UI lint, build and npm test (648) passed.

Test plan:
- BEH-PARSE-2: a kit or readme inside a feature folder whose `in` names no feature in the project is refused as BadFeatureTag, naming its file and line
- BEH-PARSE-2: a folder child whose `in` makes its feature its own ancestor is refused as BadFeatureTag, naming its line
- BEH-PARSE-2: a feature id declared by two folders, or by a folder and a .kit file, is refused as BadFeatureTag, naming both
- BEH-PARSE-5: .kit/features/Alpha/readme.md plus Alpha/Beta/beta.kit tagged `in Alpha` parse to features Alpha and Beta, reported as feature:FromFolders
- BEH-PARSE-5: a readme.md is read as the feature's description and is never parsed as a spec (no behaviour is created from it)
- BEH-PARSE-5: an empty folder under .kit/features is skipped and does not fail the parse
- BEH-PARSE-5: a folder holding only child folders (no readme, no .kit file) is a feature and its child features parse; the test asserts that
- BEH-PARSE-6: a .kit file under a feature folder with no `in` line is refused as MissingParentTag, naming the file
- BEH-PARSE-6: a readme.md under a feature folder with no `in` line is refused as MissingParentTag, naming the file
- BEH-PARSE-6: a .kit file under a feature folder whose only `in` names a feature other than its parent folder is refused as MissingParentTag, naming the file (the parent tag must be present, not just any tag)
- BEH-PARSE-6: a child that tags its parent and also tags another feature parses with no refusal, the extra tag is kept as a display tag and is not a dependency
- BEH-PARSE-6: a missing parent tag makes the check fail the same build as a behaviour with no test (BEH-GATE-1), with a non-zero exit
