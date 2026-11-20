# Move each project's spec into .kit/ in its own repo, and note today's state against BEH-USERS-2
Work-item: spec-moves-to-project-kit-dir
Flag: this changes where Kit keeps each project's spec and how it finds projects, so the owner should check it; BEH-USERS-2, BEH-USERS-3 and BEH-LIST-1 each say the .beh-anywhere fallback holds "until BEH-USERS-12 lands", which the Review line and Context's Fallback end rule both rule out, so those three cards need the owner's word before the fallback is dropped (level: high)
Review: revised — the .beh-anywhere fallback must not end with BEH-USERS-12 alone, since BEH-USERS-3 still starts user repos at behaviours/<name>.beh; added that to Context and Test plan
Cards:
- BEH-PROJ-1
- BEH-PROJ-2
- BEH-USERS-12
- BEH-USERS-2
- BEH-LIST-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-15 12:47Z (projects separate like GitHub repos; global board and settings; pipeline switch per project, all off but kit)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-15 12:47Z (second note at this time: kit .kit specs live in the project repo, not centrally in kit)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-15 13:00Z (top priority: split out the projects and move notes and specs into their repos)
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-04 18:07Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-04 18:08Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh (new BEH-PROJ-1 and BEH-PROJ-2)
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
- 18:07Z (target): each project's spec lives in a hidden .kit/ in its own repo, the same way for every project, including kit. Kit's own spec moves out of the kit repo into kit's .kit/. BEH-USERS-12 already says this; it moves files but not their shape.
- 18:08Z (today's state): all spec lives centrally in the kit repo. BEH-USERS-2's "no spec held centrally" describes the target, not today. Note this against BEH-USERS-2.
- Detection: today a project is found by a .beh file on the default branch. After the move, detection must also find .kit/. Keep the .beh-anywhere fallback while the move is under way, so no project drops off the list mid-migration (BEH-USERS-12). BEH-LIST-1 is touched only if the detection rule changes.
- Fallback end: BEH-USERS-12 moves only the specs Kit holds centrally. BEH-USERS-3 still starts a user's repo with behaviours/<name>.beh, outside .kit/, so dropping the .beh-anywhere rule when BEH-USERS-12 lands would drop those projects. Keep the fallback until BEH-USERS-3's starter file is also in .kit/ (BEH-USERS-3 is touched only if the owner wants that changed now).
- Open for the owner: whether BEH-USERS-2 should state the target or today's state. He asked for it to be settled. Default if he stays silent: the card states the target, with today's state beside it, and the owner is asked to confirm.

Test plan:
- BEH-USERS-2: a repo whose default branch has .kit/ holding a .beh file is listed as a project
- BEH-USERS-2: a repo with a .beh file anywhere on its default branch (no .kit/) is still listed, and stays listed after BEH-USERS-12 lands (the fallback is kept; see the BEH-USERS-3 case below)
- BEH-USERS-2: a repo with neither .kit/ nor any .beh file on its default branch is not listed
- BEH-USERS-12: holiday-planning's spec is read from its own repo's .kit/, and the old copy in kit is ignored while both exist
- BEH-USERS-12: a project's .kit/ holds every file of its spec under today's names, and a write names the blob last read (refused if changed)
- BEH-LIST-1: a corpus found by the BEH-USERS-2 detection rule is listed, and one that will not parse is listed as an error, not as zero behaviours
- BEH-USERS-2: a repo started by BEH-USERS-3 (behaviours/<name>.beh, no .kit/) is still listed after BEH-USERS-12 lands

Built:
- BEH-USERS-2: A_repo_whose_kit_dir_holds_a_beh_file_is_listed_as_a_project (UserProjectsTests.cs)
- BEH-USERS-2: A_repo_with_a_beh_file_outside_kit_dir_is_still_listed_until_BEH_USERS_12_lands (UserProjectsTests.cs) — its name says the fallback ends at BEH-USERS-12, which the Fallback end rule above does not allow; rename it when the card is settled
- BEH-USERS-2: A_repo_started_by_BEH_USERS_3_is_still_listed_after_BEH_USERS_12_lands (UserProjectsTests.cs)
- BEH-USERS-2: A_repo_with_neither_a_kit_dir_holding_a_beh_file_nor_any_beh_file_is_not_listed (UserProjectsTests.cs)
- The four BEH-USERS-2 tests above passed on the code already on this branch: UserProjects.cs already lists any .beh on the default branch, which covers .kit/ and the fallback. No C# change was needed for detection, so these tests did not fail first.
- Test plan items 4, 5 and 6 were not written again: existing tests cover them (GitHubCorpusDirectoryTests "A_project_moved_into_its_own_repos_kit_dir_is_read_from_there_not_twice", GitHubCorpusDirectoryTests "A_write_to_a_moved_project_goes_to_its_own_repo_not_the_clone", GitHubCorpusReaderTests "A_refused_put_says_whether_it_was_stale", Projects.test.tsx "reports a corpus that will not parse"). They pass in the runs below.
- Fix: BEH-PROJ-1 and BEH-PROJ-2 moved from kit.beh to kit-ui.beh, beside BEH-LIST-1. They name FEAT-PROJECTS, which only kit-ui.beh defines, and CommittedArtefactTests "Every_committed_corpus_parses" failed on kit.beh:371 for that reason. Placement is a choice I made (level: low).
- Backend, `dotnet test backend/Balenthiran.Kit.Tests` (.NET 10 SDK installed for this run, DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1): 1,997 tests ran, 1,994 passed, 3 failed. All three are this sandbox, not the change: ClaimerSidecarTests "A_claim_never_reported_is_reported_failed_when_the_pod_is_stopped" (no `kill` binary), ReportConformanceTests "Report_reproduces_Node_on_the_branches_the_goldens_miss" and "Globalization_is_ICU_so_locale_order_is_reachable" (ICU missing, so ordinal sort). They need a machine with ICU and `kill` to confirm.
- UI, `npx vitest run` in prototypes/behaviour-ast/ui: 49 files, 561 tests, all passed.

2026-11-16 update: notes of 2026-11-15 12:47Z (two) and 13:00Z add the per-project pipeline switch and the global board and settings (BEH-PROJ-1 and BEH-PROJ-2, new cards, now in kit-ui.beh beside BEH-LIST-1), and say notes move with the spec (BEH-USERS-12 gains that contract line in kit-ui.beh). Unsure whether BEH-PROJ-1 and -2 belong in kit.beh, which is the command-line layer, or in kit-ui.beh (level: low). The 12:47Z note names no existing card for the pipeline switch, so BEH-PROJ-1 is new rather than a change to an existing card (level: low).
