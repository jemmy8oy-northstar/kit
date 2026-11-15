# Let each user choose how deep they go, and toggle features per user group
Work-item: user-depth-and-feature-toggles
Flag: the architecture design page and what each depth level shows are the owner's call, and this touches how Kit stores and shows work (level: high); the tester toggles overlap kit/work/tester-feature-access, which already holds note 17:41Z and BEH-FLAGS-1/-2 (level: low)
Cards:
- new: an architecture design page where stack, data store and how parts talk are recorded, raised as a review request before anything that depends on them is built
- new: every level of detail is opt-in: a user can go as high level or as involved as they like, using the project's depth setting
- new: features can be toggled on and off for two user groups separately, the owner and testers, so testers get a more minimal experience
- BEH-FLAGS-1 "Settings lists Kit's features with one switch for him and one for testers"
- BEH-FLAGS-2 "A feature switched off for a group is gone from that group's pages and navigation"
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 10:39Z (architecture design layer; opt-in depth; feature toggles for owner and testers)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 10:40Z (aggressive experimentation now, refine the UI later)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-02 17:41Z (testers see projects, boards, inbox and settings; asked to enable these features for testers)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-FLAGS-1 and -2 at lines 2305-2330)
- prototypes/behaviour-ast/behaviours/kit.beh (the `# kit:switch FEAT-X operator|testers on|off` lines)
Context:
- The project's depth setting already exists in the process as the kit:depth line (goals, behaviours, architecture). The owner says the system currently requires architecture design and wants that level built out as a page. Default: the page is a view over the architecture decisions the project already records, and each decision is a review request.
- Feature toggles: two groups, owner and testers, each with its own on/off set. Default: a toggle table keyed by feature and group, owner's group on by default. Record the group names and defaults as unreviewed assumptions.
- 10-31 10:40Z is the reason for the split: experiment hard now, streamline later. Use it to justify toggles rather than to cut any card.
- 11-02 17:41Z: the owner wants testers to see projects, boards, inbox and settings first. BEH-FLAGS-1 and -2 already state this as contract lines, so the build is the toggle table and the gating, not new specs. Before building the tester toggles, check kit/work/tester-feature-access; BEH-USERS-2 (a tester's projects from their own GitHub) is a separate gap and is left to that branch.
- Build the toggles and the architecture page as separate changes; the page touches how Kit stores decisions, so review its storage choice first.
