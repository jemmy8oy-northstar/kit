# Set aside: a question to the owner, nothing to build
Work-item: set-aside
Flag: Notes that ask for no build: approvals, praise, status reports, a merge instruction with no PR named, and two identical questions whose referent is unclear (level: low)
Cards:
- new: nothing to build
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:14Z ("Just to confirm - these notes are being wrapped into kit spec right?")
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-16 11:45Z (approves the filter; nothing to build)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-27 21:16Z ("What does this mean", first of two identical notes)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-27 21:16Z ("What does this mean", second of two identical notes)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 08:23Z ("Merge it when it ready"; no PR named)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 08:57Z ("Merged")
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 09:17Z ("Merged to main")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-28 23:55Z ("This inbox card feature is incredibly successful. No need to navigate a board.")
Files:
- prototypes/behaviour-ast/behaviours/kit.notes.md
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
Not built. The owner asks whether the notes are folded into the kit spec. The notes file header says each note is folded into the spec or the code and deleted in the same pull request, so the answer is yes in intent; a person should confirm it.
The 2026-10-16 11:45Z note approves a filter already built. The 2026-10-27 21:16Z pair asks “What does this mean”, with no referent in the text; the owner should say which item it means. The 2026-11-03 08:23Z, 08:57Z and 09:17Z notes are a merge instruction and status reports. Merges are done by the pipeline (BEH-ORCH-10) or by the owner, not by a build. Nothing here is built.
The 2026-10-28 23:55Z note is praise for the inbox card feature. Its “no need to navigate a board” remark is not a request; if the owner wants the board changed, that belongs to board-notes-declutter-and-fab-apps or board-pending-columns-and-focus, and he should say so. Nothing here is built.
