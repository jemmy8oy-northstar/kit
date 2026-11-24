# Enable dev -> main release for kit
Work-item: kit-dev-to-main-release
Flag: Turning on the dev -> main release for kit lets the pipeline merge into main; the spec line states the assumption that this is his intent (level: high)
Cards:
- BEH-PR-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 23:08Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
Note asks that kit be able to perform a dev -> main release. The release is a project setting, toggled in project settings, default off. Build step: switch the setting on for the kit project only (its # kit:... settings line or the settings store, whichever the project settings page writes), leave the default off for all other projects. Do not run a release; only enable the ability. BEH-PR-2 was edited with one contract line recording this assumption, marked unreviewed.
