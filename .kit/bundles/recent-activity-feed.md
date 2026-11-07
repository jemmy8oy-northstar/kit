# Recent activity: a chronological feed of what changed
Work-item: recent-activity-feed
Cards:
- FEAT-ACTIVITY
- BEH-ACTIVITY-1
- BEH-ACTIVITY-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-20 12:21Z (line 155)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 13:12Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/pages/Activity.tsx
- backend/Balenthiran.Kit.Services/ActivityReader.cs
Context:
The note is the feature FEAT-ACTIVITY, which quotes it in its header. BEH-ACTIVITY-1 says the page lists what changed recently, newest first; its contract says each entry shows when it happened, which project, and the commit's own summary. BEH-ACTIVITY-2 says an entry links to the item it is about. BEH-USERS-8 says each user sees only their own activity, so the feed needs the same per-user scoping as projects. Built state was not checked.
The 2026-10-25 13:12Z note asks that James can go from an activity entry to the item it refers to. BEH-ACTIVITY-2 already says so, so this is a card that is specced but not met, not a new card. Check the link target in Activity.tsx: it may point to a commit rather than to the item.
