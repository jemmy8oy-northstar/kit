# A new user connects GitHub before project pages open
Work-item: sign-in-with-git-before-use
Flag: BEH-AUTH-1's title says "sign in with git" and its given is user:New, but the owner's reply and the card's own contract make the gate connecting GitHub after the password sign-in, until it is connected; the title and given may need the owner's wording (level: low)
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

Test plan:
- BEH-AUTH-1: a signed-in user who has not connected GitHub gets the connect-GitHub step, not a project page, when opening KitProjects
- BEH-AUTH-1: the landing page and the sign-in page load for a user who has not connected GitHub
- BEH-AUTH-1: the password sign-in (BEH-USERS-1) still works for a new user and leads to the connect-GitHub step, not to a project page
- BEH-AUTH-1: after GitHub is connected, the user's project pages open
- BEH-AUTH-1: a user who has connected GitHub signs in with "Sign in with GitHub" (BEH-USERS-10) and is not sent back to the connect step
- BEH-AUTH-1: the test is mapped to BEH-AUTH-1 so BEH-GATE-1 passes; it fails until the gate is built
