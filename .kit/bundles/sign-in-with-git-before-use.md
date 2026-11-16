# A new user signs in with git before using the app
Work-item: sign-in-with-git-before-use
Flag: the existing user-account cards in kit-ui.beh (BEH-USERS-*) may already hold part of this and were not read (level: low)
Review: escalated — BEH-USERS-1 and BEH-USERS-10 (kit-ui.beh, approved) make a new user's first sign-in a username and password, with GitHub connected after and used only from then on; BEH-AUTH-1 requires git sign-in before any project page opens, so a new user could never get in. Decision for a person: does BEH-AUTH-1 replace the password-first onboarding, or mean "connect GitHub before project pages open" after the password sign-in?
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
