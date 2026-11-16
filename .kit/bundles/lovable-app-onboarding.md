# Onboard an existing Lovable app onto Kit from its GitHub repo
Work-item: lovable-app-onboarding
Flag: this brings EPIC-ONBOARD out of "backlog, out of scope" into scope, a scope change for the owner to see (level: high)
Cards:
- EPIC-ONBOARD
- BEH-LOVABLE-1
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 18:04Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-15 12:50Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
The owner's note (11-06 18:04Z) says: drop the "backlog, out of scope for now" label on EPIC-ONBOARD and bring it into scope. Add a behaviour under it: a user can onboard an existing app built in Lovable onto Kit. Other tech stacks come after, once that works.
The owner's answer (11-15 12:50Z): the Lovable app is already in the user's own GitHub through Lovable's GitHub sync, and onboarding starts from that repo. So the input is a GitHub repo the user owns, not a Lovable export.
The corpus already records his note as a contract on EPIC-ONBOARD (kit-ui.beh, near line 1248). Check that before adding a second card for the same behaviour, and keep one card.
Test plan:
- BEH-LOVABLE-1: opening the projects page (KitProjects) shows the onboarding entry in KitMain, so a user can start onboarding an existing Lovable app.
- BEH-LOVABLE-1: the onboarding source is a GitHub repo the user owns (the one Lovable's GitHub sync writes to); a Lovable export file is not accepted as the input.
- BEH-LOVABLE-1: a repo the user does not own is refused as an onboarding source.
- BEH-LOVABLE-1: onboarding produces a first spec of what the app already does as unreviewed cards, each of which the user can approve or deny; none is approved by default.
- BEH-LOVABLE-1: a non-Lovable repo or other tech stack is not offered in this slice, since other stacks wait until a Lovable app is onboarded end to end.
- EPIC-ONBOARD: its title no longer carries "out of scope", and FEAT-ONBOARD-LOVABLE sits under it, so the epic is the goal the board shows as working towards.
