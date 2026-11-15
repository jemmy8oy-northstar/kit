# Move each project's spec into .kit/ in its own repo, and note today's state against BEH-USERS-2
Work-item: spec-moves-to-project-kit-dir
Flag: this changes where Kit keeps each project's spec and how it finds projects, so the owner should check it (level: high)
Review: revised — the .beh-anywhere fallback must not end with BEH-USERS-12 alone, since BEH-USERS-3 still starts user repos at behaviours/<name>.beh; added that to Context and Test plan
Cards:
- BEH-USERS-12
- BEH-USERS-2
- BEH-LIST-1
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-04 18:07Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-04 18:08Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
- 18:07Z (target): each project's spec lives in a hidden .kit/ in its own repo, the same way for every project, including kit. Kit's own spec moves out of the kit repo into kit's .kit/. BEH-USERS-12 already says this; it moves files but not their shape.
- 18:08Z (today's state): all spec lives centrally in the kit repo. BEH-USERS-2's "no spec held centrally" describes the target, not today. Note this against BEH-USERS-2.
- Detection: today a project is found by a .beh file on the default branch. After the move, detection must also find .kit/. Keep the .beh-anywhere fallback until the move lands, so no project drops off the list mid-migration (BEH-USERS-12). BEH-LIST-1 is touched only if the detection rule changes.
- Fallback end: BEH-USERS-12 moves only the specs Kit holds centrally. BEH-USERS-3 still starts a user's repo with behaviours/<name>.beh, outside .kit/, so dropping the .beh-anywhere rule when BEH-USERS-12 lands would drop those projects. Keep the fallback until BEH-USERS-3's starter file is also in .kit/ (BEH-USERS-3 is touched only if the owner wants that changed now).
- Open for the owner: whether BEH-USERS-2 should state the target or today's state. He asked for it to be settled. Default if he stays silent: the card states the target, with today's state beside it, and the owner is asked to confirm.
Built:
- BEH-USERS-2: A_repo_whose_kit_dir_holds_a_beh_file_is_listed_as_a_project (UserProjectsTests.cs)
- BEH-USERS-2: A_repo_with_a_beh_file_outside_kit_dir_is_still_listed_until_BEH_USERS_12_lands (UserProjectsTests.cs)
- BEH-USERS-2: A_repo_started_by_BEH_USERS_3_is_still_listed_after_BEH_USERS_12_lands (UserProjectsTests.cs)
- BEH-USERS-2: A_repo_with_neither_a_kit_dir_holding_a_beh_file_nor_any_beh_file_is_not_listed (UserProjectsTests.cs)
Suite not run: no .NET SDK in this environment (`dotnet` is not installed), so the whole suite count and result are unknown and the new tests were neither compiled nor seen to fail. Test plan items 4, 5 and 6 were not written: existing tests cover them (GitHubCorpusDirectoryTests "A_project_moved_into_its_own_repos_kit_dir", GitHubCorpusReaderTests "A_refused_put_says_whether_it_was_stale", Projects.test.tsx "reports a corpus that will not parse").
Test plan:
- BEH-USERS-2: a repo whose default branch has .kit/ holding a .beh file is listed as a project
- BEH-USERS-2: a repo with a .beh file anywhere on its default branch (no .kit/) is still listed, until BEH-USERS-12 lands
- BEH-USERS-2: a repo with neither .kit/ nor any .beh file on its default branch is not listed
- BEH-USERS-12: holiday-planning's spec is read from its own repo's .kit/, and the old copy in kit is ignored while both exist
- BEH-USERS-12: a project's .kit/ holds every file of its spec under today's names, and a write names the blob last read (refused if changed)
- BEH-LIST-1: a corpus found by the BEH-USERS-2 detection rule is listed, and one that will not parse is listed as an error, not as zero behaviours
- BEH-USERS-2: a repo started by BEH-USERS-3 (behaviours/<name>.beh, no .kit/) is still listed after BEH-USERS-12 lands
