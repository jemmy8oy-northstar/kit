# Per-project views: inbox, runs and settings scoped to one project, global notifications and settings kept above
Work-item: per-project-views-and-settings
Flag: the spec edit goes to kit-ui.beh, not kit.beh, because BEH-SPACE-1 lives there and kit.beh is the command-line layer (level: low)
Cards:
- BEH-SPACE-1
- BEH-SPACE-5
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-23 22:57Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src
Context:
The owner wants Kit organised by project (repo), as git is: each project is isolated, and its boards, inbox, chat, runs and settings are viewed one project at a time. Global settings stay, and a global notifications page stays valuable. BEH-SPACE-1 already has a built navigation with the Inbox and Settings kept global; the new contract lines say the Inbox and runs move into each project, which corrects that built line. BEH-SPACE-5 is the new card for per-project inbox, runs and settings, with global notifications and global settings kept above projects. Global board and global inbox remain as all-projects views (BEH-SPACE-3). A builder should first read how the Inbox, runs and settings pages are filtered today, reuse the existing project filter (BEH-SPACE-3 uses /board?project=x), and not add a store. Nothing here decides the pipeline or the data store.
