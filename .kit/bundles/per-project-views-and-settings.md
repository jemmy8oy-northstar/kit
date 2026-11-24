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

Test plan:
- BEH-SPACE-1: the project page's own navigation has its Chat, Inbox, board, runs and settings, and the main navigation keeps only Projects, global Notifications and global Settings (replaces the built assertion in Project.test.tsx that the main navigation keeps the Inbox).
- BEH-SPACE-5: a project's Inbox lists only items of that project; another project's waiting item does not appear in it.
- BEH-SPACE-5: a project's runs list shows only that project's pipeline runs; a run of another project is absent. Check first whether the runs list is filtered on the server or in the UI, and test the layer that does the filtering.
- BEH-SPACE-5: a project's settings show only that project's configuration (schedule, principles, architecture); global Settings shows nothing that belongs to one project.
- BEH-SPACE-5: switching from project A to project B changes every per-project view to B's items; no project filter from A is still applied afterwards.
- BEH-SPACE-5: the global Inbox and the global board with no project filter still list items from all projects (no regression of BEH-SPACE-3).
- BEH-SPACE-5: global Notifications and global Settings are reachable from the entry level and are not rendered inside a project page.
