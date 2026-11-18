# Set aside: a question to the owner, nothing to build
Work-item: set-aside
Flag: Notes that ask for no build: approvals, praise, status reports, a merge instruction with no PR named, a question about the pipeline, questions whose referent the text does not name, and a view that notes should stay (already recorded as BEH-NOTE-14); the 2026-11-06 18:07Z and 18:16Z notes are tests with nothing to build; the 2026-11-14 12:50Z wake-button note is already BEH-SETTINGS-7 on main (level: low); the 2026-11-18 22:51Z note is an operational report about a blocked build run, not a behaviour, and the owner asks whether the unchanged bundle was intentional or should rerun (level: high)
Cards:
- new: nothing to build
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-14 17:25Z (asks why the pipeline paused until 8: a question, nothing to build)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:14Z ("Just to confirm - these notes are being wrapped into kit spec right?")
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-16 11:45Z (approves the filter; nothing to build)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-27 21:16Z ("What does this mean", first of two identical notes)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-10-27 21:16Z ("What does this mean", second of two identical notes)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 08:23Z ("Merge it when it ready"; no PR named)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 08:57Z ("Merged")
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-03 09:17Z ("Merged to main")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-28 23:55Z ("This inbox card feature is incredibly successful. No need to navigate a board.")
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-14 09:25Z ("The pipeline approach has changed recently right")
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-15 12:45Z ("What is the home on this it isn't marked as blocked")
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-15 12:48Z ("I don't understand what this is asking")
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 19:05Z ("getting rid of notes in favour of chat"; notes stay, already BEH-NOTE-14 on main)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-06 18:07Z ("This is just a test please discard it")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-06 18:16Z ("This is a test (I need Claude code bot to wake)")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-14 12:50Z ("Can I get a button to flag for a ccb wake for my personal kit only"; already BEH-SETTINGS-7 on main)
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-16 22:56Z ("I think this is ready now"; names no item, nothing to build)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-18 22:51Z ("Blocked: the build run left the bundle unchanged"; asks why, or for a rerun)
Files:
- prototypes/behaviour-ast/behaviours/kit.notes.md
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md
Context:
Not built. The owner asks whether the notes are folded into the kit spec. The notes file header says each note is folded into the spec or the code and deleted in the same pull request, so the answer is yes in intent; a person should confirm it.
The 2026-10-16 11:45Z note approves a filter already built. The 2026-10-27 21:16Z pair asks "What does this mean", with no referent in the text; the owner should say which item it means. The 2026-11-03 08:23Z, 08:57Z and 09:17Z notes are a merge instruction and status reports. Merges are done by the pipeline (BEH-ORCH-10) or by the owner, not by a build. Nothing here is built.
The 2026-10-28 23:55Z note is praise for the inbox card feature. Its "no need to navigate a board" remark is not a request; if the owner wants the board changed, that belongs to board-notes-declutter-and-fab-apps or board-pending-columns-and-focus, and he should say so. Nothing here is built.
The 2026-11-14 09:25Z note asks whether the pipeline approach has changed recently. It is a question for the owner, not work; a person answers it. Nothing here is built.
The 2026-11-15 12:45Z and 12:48Z notes ("What is the home on this it isn't marked as blocked"; "I don't understand what this is asking") do not name the item they mean. They may be replies about a question on the sheet. The owner should say which item, the same as the 10-27 pair. Nothing here is built.
The 2026-11-02 19:05Z note says notes should stay alongside chat, not be replaced by it. BEH-NOTE-14 already records that decision on main and it is built. Nothing new is asked, so nothing here is built.
2026-11-16: the 2026-11-06 18:07Z and 18:16Z notes are tests. Nothing to build; a person should decide whether to discard them.
2026-11-16: the 2026-11-14 12:50Z note asks for a button to wake the agent for the operator's kit only. BEH-SETTINGS-7 in kit-ui.beh on main is that button ("Wake the agent" on Settings, operator only), and its contract quotes this note. Nothing to build; a person should confirm the button works, because the card records it as never pressed when it was written.

2026-11-16 update: the 2026-11-14 17:25Z note asks the owner's question why the pipeline paused until 8. It asks for no build and names no card, so it is set aside for a person to answer. Running the pipeline per project is in the projects item, spec-moves-to-project-kit-dir (level: low).

2026-11-16 update: the 2026-11-16 22:56Z note ("I think this is ready now") names no feature, card or change, so there is nothing to build. A person should say what is ready, if anything. Flag level high because it is unclear what the owner wants.

2026-11-18: the 2026-11-18 22:51Z note reports that a build run was blocked because it left the bundle unchanged. It names no behaviour and asks for no spec change, so nothing was built and no spec was edited. A person should check that build run's log, decide whether the unchanged bundle was intentional, and rerun it if not.
