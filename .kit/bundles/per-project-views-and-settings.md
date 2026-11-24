# Per-project views: inbox, runs and settings scoped to one project, global notifications and settings kept above
Work-item: per-project-views-and-settings
Flag: the spec edit goes to kit-ui.beh, not kit.beh, because BEH-SPACE-1 lives there and kit.beh is the command-line layer; the change re-scopes how Kit shows his work (level: high)
Review: revised — scope and file are right and every guess is an unreviewed contract; Context now says the global Notifications page is the existing global Inbox across all projects, so no new page is built
Cards:
- BEH-SPACE-1
- BEH-SPACE-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-23 22:57Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src
Context:
The owner wants Kit organised by project (repo), as git is: each project is isolated, and its boards, inbox, chat, runs and settings are viewed one project at a time. Global settings stay, and a global notifications page stays valuable. BEH-SPACE-1 already has a built navigation with the Inbox and Settings kept global; the new contract lines say the Inbox and runs move into each project, which corrects that built line. BEH-SPACE-5 is the new card for per-project inbox, runs and settings, with global notifications and global settings kept above projects. Global board and global inbox remain as all-projects views (BEH-SPACE-3); the "global Notifications" in both cards is that existing global Inbox, not a new page. A builder should first read how the Inbox, runs and settings pages are filtered today, reuse the existing project filter (BEH-SPACE-3 uses /board?project=x), and not add a store. Nothing here decides the pipeline or the data store.

Built:
- BEH-SPACE-5 keeps Projects, Notifications and Settings in the main navigation, and not the Inbox or Runs (App.test.tsx)
- BEH-SPACE-5 reaches the all-projects Inbox from the entry page, not from the main navigation (App.test.tsx)
- BEH-SPACE-5 a project has Its inbox, Its runs and Its settings beside its chat and board, each on its own page (Project.test.tsx)
- BEH-SPACE-5 a project page does not render the global Settings or Notifications (Project.test.tsx)
- BEH-SPACE-5 a project's inbox lists only that project's cards, and another project's waiting card is absent (Inbox.test.tsx)
- BEH-SPACE-5 switching to another project shows that project's cards, with no project filter left in the address (Inbox.test.tsx)
- BEH-SPACE-5 the global inbox with no project picked still lists every project (Inbox.test.tsx)
- BEH-SPACE-5 a project's runs list shows only that project's runs and pipeline items (Runs.test.tsx)
- BEH-SPACE-5 global Settings shows no per-project switch and keeps the global settings (Settings.test.tsx)
- BEH-SPACE-5 the global board with no project filter lists every project (Board.test.tsx)
Whole suite: 625 UI tests (5 todo) in 53 files and 2143 backend tests, all passed.

Test plan:
- BEH-SPACE-1: the project page's own navigation has its Chat, Inbox, board, runs and settings, and the main navigation keeps only Projects, global Notifications and global Settings (replaces the built assertion in Project.test.tsx that the main navigation keeps the Inbox).
- BEH-SPACE-5: a project's Inbox lists only items of that project; another project's waiting item does not appear in it.
- BEH-SPACE-5: a project's runs list shows only that project's pipeline runs; a run of another project is absent. Check first whether the runs list is filtered on the server or in the UI, and test the layer that does the filtering.
- BEH-SPACE-5: a project's settings show only that project's configuration (schedule, principles, architecture); global Settings shows nothing that belongs to one project.
- BEH-SPACE-5: switching from project A to project B changes every per-project view to B's items; no project filter from A is still applied afterwards.
- BEH-SPACE-5: the global Inbox and the global board with no project filter still list items from all projects (no regression of BEH-SPACE-3).
- BEH-SPACE-5: global Notifications and global Settings are reachable from the entry level and are not rendered inside a project page.
