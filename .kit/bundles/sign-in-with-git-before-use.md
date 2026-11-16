# A new user signs in with git before using the app
Work-item: sign-in-with-git-before-use
Flag: the existing user-account cards in kit-ui.beh (BEH-USERS-*) may already hold part of this and were not read (level: low)
Cards:
- BEH-AUTH-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 21:21Z (new user must sign in with git during onboarding; a user can look around without it today)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
New card in kit.beh only. It has no test yet, so kit check kit fails BEH-GATE-1 until a builder maps it.
Assumed: the landing page and the sign-in page are the only pages open before git sign-in.
Next step toward the goal: a builder finds which project pages open without git today and closes them.
