# App ideas page, and turning an idea into a repo
Work-item: app-ideas-page
Flag: Unsure whether creating a repo from an idea belongs under FEAT-USER-REPOS or is a new card. The GitHub App permission to create repos is also a secret and access decision, so it is the user's to make.
Cards:
- BEH-IDEA-1
- FEAT-USER-REPOS
- new: from an idea, Kit drafts a spec and, with GitHub App permission, creates a new repo for it
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-15 23:10Z
Files:
- prototypes/behaviour-ast/behaviours/ideas.beh
- prototypes/behaviour-ast/behaviours/ideas.notes.md
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
The note asks for a place to ramble about app ideas, to begin a spec from them, and eventually to let Kit create a repo for a new idea through GitHub App permission, so an idea can be deployed from inside Kit. BEH-IDEA-1 already puts app ideas on their own navbar page, never on the board. Nothing yet covers turning an idea into a repo. The repo step depends on the GitHub App permission, which is a user decision, so ask before building it. Start with the page and the idea-to-spec step only.
