# Architecture design page, and feature switches for James and for testers
Work-item: architecture-page-and-feature-flags
Flag: Much of this is already contracted as built (the depth setting, the architecture stage and the feature switches); check what the note still asks for before writing any card.
Cards:
- FEAT-ARCH
- BEH-ARCH-1
- BEH-ARCH-2
- FEAT-FLAGS
- BEH-FLAGS-1
- BEH-FLAGS-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 10:39Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 10:40Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
The 10:39Z note asks for an architecture layer in the spec, as deep as the user wants to go, and for features to be switched on and off separately for James and for testers. Both are already contracted; check the built state before changing anything.
Architecture: FEAT-ARCH in kit-ui.beh near line 2192. BEH-ARCH-1 gives each project an architecture page. BEH-ARCH-2 adds an architecture stage between logic and development, used only when a feature needs a new part or platform choice. A "How involved you are" section on Settings sets goals, behaviours or architecture per project, written as a # kit:depth line (contract near kit-ui.beh:2239).
Switches: FEAT-FLAGS in kit-ui.beh near line 2302. BEH-FLAGS-1 gives Settings one switch for James and one for testers. BEH-FLAGS-2 removes a feature that is off for a group from that group's pages and navigation. The contract near kit-ui.beh:2317 writes a switch as a `# kit:switch FEAT-X operator|testers on|off` line in kit.beh.
The 10:40Z note says to experiment aggressively and refine the UI later. It is not a card. Use it to set the defaults: James sees every feature, and the testers' set can be trimmed with switches.
