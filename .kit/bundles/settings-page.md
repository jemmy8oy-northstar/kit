# Settings: the GitHub connection, and a default for whether the agent may merge to main
Work-item: settings-page
Flag: Whether the agent may merge to main is James's decision; build the default as off, and do not let any project merge without its own setting.
Cards:
- BEH-SETTINGS-1
- BEH-SETTINGS-3
- BEH-SETTINGS-4
- new: Settings sets a default for whether the agent may promote to main, which a project's own setting overrides
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 08:54Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:11Z
Files:
- prototypes/behaviour-ast/ui/src/pages/Settings.tsx
- prototypes/behaviour-ast/ui/src/pages/Settings.test.tsx
- backend/Balenthiran.Kit.WebApi/KitSettings.cs
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
The 08:54Z note asks for Settings to show the GitHub integration and room for other config later. BEH-SETTINGS-1 and BEH-SETTINGS-3 specify it; check the built page against them.
The 21:11Z note asks for a button in Settings to choose the default permission to merge to main, with a per-project choice for finer control. BEH-SETTINGS-4 already gives the per-project choice, so the default is the new part. The project's own choice should win over the default. The dev process bars the agent from merging its own work unless the project's corpus says it may, so the default must be read from the settings that the corpus allows, not hard-coded.
