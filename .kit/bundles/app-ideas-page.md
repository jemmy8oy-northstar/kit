# App ideas page, and turning an idea into a repo
Work-item: app-ideas-page
Flag: Unsure whether creating a repo from an idea belongs under FEAT-USER-REPOS or is a new card. The GitHub App permission to create repos is also a secret and access decision, so it is the user's to make.
Review: escalated — the page and ramble-to-spec are already built (BEH-IDEA-1, ideas.beh); what is left is James deciding whether to grant the GitHub App repo-creation permission, and whether that step is a new card or FEAT-USER-REPOS
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
The note asks for a place to ramble about app ideas, to begin a spec from them, and eventually to let Kit create a repo for a new idea through GitHub App permission, so an idea can be deployed from inside Kit. The first two are already built: BEH-IDEA-1 (approved, not pending) puts app ideas on their own navbar page, off the board, and ideas.beh is that page, with rambles left as notes and folded into each idea's behaviours. That file quotes this same note, so the note is already folded. Nothing yet covers turning an idea into a repo. ideas.beh says "creating repos stays James's call (kit#284)", and each idea's needs-context asks him to create the repo by hand. Letting Kit do it means granting the GitHub App permission to create repos, which is an access decision for James. Build nothing here until he answers. Default if he says nothing: ideas still graduate by hand, as they do now.
