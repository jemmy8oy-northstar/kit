# Testers see projects, boards, inbox and settings
Work-item: tester-feature-access
Flag: The note ends in "right?", so it asks for confirmation. BEH-FLAGS-1's contract already records the set as on for testers (17:41Z). The open gap is BEH-USERS-2: until it is built, testers see the operator's projects. A stronger model should decide whether the bundle is complete. (level: high)
Cards:
- BEH-FLAGS-1 "Settings lists Kit's features with one switch for him and one for testers"
- BEH-FLAGS-2 "A feature switched off for a group is gone from that group's pages and navigation"
- BEH-USERS-2 "Kit lists a user's projects from their own GitHub, with no spec held centrally"
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:41Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh (the `# kit:switch FEAT-X operator|testers on|off` lines)
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-FLAGS-1 and -2 at lines 2305-2330; BEH-USERS-2 at line 1300)
- prototypes/behaviour-ast/ui/src/components/flags.ts
- backend/Balenthiran.Kit.Tests/FlagsTests.cs
Context:
The switches are one line each in kit.beh, in the form `# kit:switch FEAT-X operator|testers on|off`. BEH-FLAGS-1's built contract sets these on for testers: FEAT-PROJECTS, FEAT-PROJECT, FEAT-BOARD, FEAT-STAGES, FEAT-EPICS (the three board views), FEAT-INBOX and FEAT-SETTINGS. "Projects page" is read to include a project's own page, not yet its behaviour pages, notes or schedule. The operator switches stay as they are.
Builder: check those switch lines exist on main and that FlagsTests.cs covers them. Do not change the operator's switches. If the user meant a narrower set, raise that as a question on BEH-FLAGS-1 and keep the current set. Next step toward the goal: once BEH-USERS-2 is built, testers see their own projects, which closes the gap.
Re-triage 2026-11-15 (triage run): note 17:41Z arrived again with the same ref; mapped above, nothing added. The ask is a confirmation ('right?'), so keep the set as it stands unless the owner narrows it.
