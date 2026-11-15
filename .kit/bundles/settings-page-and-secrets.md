# Settings page: secrets set from the UI, GitHub integration, default merge permission
Work-item: settings-page-and-secrets
Flag: secrets storage touches how Kit keeps his data, and the default merge-to-main permission is a permission decision; the owner decides both (level: high). The "already installed? connect it" callback error (10-31 16:26Z) is a bug on the connect step, and the App-purpose text is my guess (level: low)
Cards:
- new: secrets can be added and updated from a UI
- new: a secret cannot be read back once it is written
- new: a settings page shows the GitHub integration and room for other config later
- new: settings choose the default permission to merge to main, optionally per project for finer control
- new: the "already installed? connect it" step on the GitHub integration works without a callback-URL error, and says what the GitHub App is for
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 18:21Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 08:54Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-27 21:11Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 16:26Z (connect it gives "This GitHub App must be configured with a callback URL"; owner unsure what the App is for)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- Secrets: the owner wants them added and updated from a UI, and never read back once written. Write-only means the UI shows set or not set, never the value. Check how secrets are stored today before building.
- The settings page is one place for GitHub integration and later config. Build only the GitHub part now.
- Merge permission: a default for all projects, with an optional per-project override. Anything that changes merge rights is the owner's decision; default is no merge permission until he sets it.
- 10-31 16:26Z: clicking "already installed? connect it" shows "This GitHub App must be configured with a callback URL". The owner already has the App installed and everything works, so he is unsure what is left. Reproduce the callback error first. Default for the App's purpose: a one-line description on the settings page. Record the wording as an unreviewed assumption.
- Record the default for each as an unreviewed assumption.
