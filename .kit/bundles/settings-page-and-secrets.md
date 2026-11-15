# Settings page: secrets set from the UI, GitHub integration, default merge permission
Work-item: settings-page-and-secrets
Flag: secrets storage touches how Kit keeps his data, and the default merge-to-main permission is a permission decision; the owner decides both (level: high)
Cards:
- new: secrets can be added and updated from a UI
- new: a secret cannot be read back once it is written
- new: a settings page shows the GitHub integration and room for other config later
- new: settings choose the default permission to merge to main, optionally per project for finer control
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 18:21Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 08:54Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:11Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- Secrets: the owner wants them added and updated from a UI, and never read back once written. Write-only means the UI shows set or not set, never the value. Check how secrets are stored today before building.
- The settings page is one place for GitHub integration and later config. Build only the GitHub part now.
- Merge permission: a default for all projects, with an optional per-project override. Anything that changes merge rights is the owner's decision; default is no merge permission until he sets it.
- Record the default for each as an unreviewed assumption.
