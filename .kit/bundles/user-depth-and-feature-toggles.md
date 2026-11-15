# Let each user choose how deep they go, and toggle features per user group
Work-item: user-depth-and-feature-toggles
Flag: the architecture design page and what each depth level shows are the owner's call, and this touches how Kit stores and shows work (level: high)
Cards:
- new: an architecture design page where stack, data store and how parts talk are recorded, raised as a review request before anything that depends on them is built
- new: every level of detail is opt-in: a user can go as high level or as involved as they like, using the project's depth setting
- new: features can be toggled on and off for two user groups separately, the owner and testers, so testers get a more minimal experience
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 10:39Z (architecture design layer; opt-in depth; feature toggles for owner and testers)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 10:40Z (aggressive experimentation now, refine the UI later)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- The project's depth setting already exists in the process as the kit:depth line (goals, behaviours, architecture). The owner says the system currently requires architecture design and wants that level built out as a page. Default: the page is a view over the architecture decisions the project already records, and each decision is a review request.
- Feature toggles: two groups, owner and testers, each with its own on/off set. Default: a toggle table keyed by feature and group, owner's group on by default. Record the group names and defaults as unreviewed assumptions.
- 10-31 10:40Z is the reason for the split: experiment hard now, streamline later. Use it to justify toggles rather than to cut any card.
- Build the toggles and the architecture page as separate changes; the page touches how Kit stores decisions, so review its storage choice first.
