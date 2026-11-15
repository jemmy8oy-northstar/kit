# Testers see projects, boards, inbox and settings
Work-item: tester-feature-access
Flag: The note ends in "right?", so the owner is asking for confirmation of the four features as the starting set for testers (level: low)
Cards:
- BEH-FLAGS-1 "Settings lists Kit's features with one switch for him and one for testers"
- BEH-FLAGS-2 "A feature switched off for a group is gone from that group's pages and navigation"
- BEH-USERS-2 "Kit lists a user's projects from their own GitHub, with no spec held centrally"
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:41Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh (the `# kit:switch FEAT-X operator|testers on|off` lines)
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-FLAGS-1 and -2 near lines 2305-2330; BEH-USERS-2 near line 1300)
Context:
The owner asks for four features to be on for testers: the projects page, the boards, the inbox and settings. He says the features need to work for testers as well, so these are the starting set. BEH-FLAGS-1 already records the set as on for testers in its contract line, so the switches may already be set; check before changing anything. The open gap is BEH-USERS-2: until a user's projects come from their own GitHub, testers see the operator's projects, which matters for what "working" means for them.
