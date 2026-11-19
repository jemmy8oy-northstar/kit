# A new user connects GitHub before project pages open
Work-item: sign-in-with-git-before-use
Cards:
- BEH-AUTH-1
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-07 21:21Z (new user must sign in with git during onboarding; a user can look around without it today)
- ticket sign-in-with-git-before-use, reply of 2026-11-19 16:50Z (owner: connect GitHub before project pages open, after the password sign-in)
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh (BEH-USERS-1, BEH-USERS-10: password first, GitHub after; left unchanged)
Context:
Resolves the escalated conflict with BEH-USERS-1 and BEH-USERS-10. The owner's reply keeps the password-first onboarding and makes GitHub connection the gate before any project page opens. The BEH-AUTH-1 title still reads "sign in with git"; only its contract lines were changed, as the triage rules require.
Assumed: the landing page and the sign-in page are the only pages open before git sign-in.
Next step toward the goal: a builder maps BEH-AUTH-1 to a test (BEH-GATE-1 fails until then) and finds which project pages open without GitHub connected today and closes them.
