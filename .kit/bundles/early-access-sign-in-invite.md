# Early access: invite-only username and password sign-in, GitHub connected after
Work-item: early-access-sign-in-invite
Flag: the two bare "Yes" notes have no question beside them, so I attached them here as confirmations of the sign-in questions; the 10-28 reinstall question is the owner's to answer; the 10-31 test-user question is also the owner's (level: low)
Cards:
- BEH-USERS-1
- BEH-USERS-10
- BEH-USERS-11
- BEH-GATE-H1
- BEH-USERS-14
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:35Z ("Invite only to start with")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:35Z ("code word is maybe not necessary")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Back tracking again")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Yes", first)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Can always change later if need be")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Yes", second)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-28 00:03Z ("it won't let me install it because it's already installed; uninstall and reinstall with OAuth to test?")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 00:55Z ("Am I able to act as a test user myself by logging in with a different organisation or my personal account?")
Files:
- prototypes/behaviour-ast/behaviours/kit-hosted.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- backend/Balenthiran.Kit.Services/SignInThrottle.cs
- backend/Balenthiran.Kit.Tests/AuthConformanceTests.cs
- backend/Balenthiran.Kit.Tests/SignInRaceTests.cs
Context:
- BEH-USERS-1 already says an early-access user signs in with a username and password the operator made, then connects GitHub. The 06:38Z note asks for the same. Check the build before writing new code.
- BEH-USERS-10 is the later GitHub sign-in for test users. BEH-USERS-11 is the operator resetting a sign-in. The general-release GitHub-only sign-in is not yet a card; it is later, so do not build it now.
- Invite-only: the 06:35Z note asked how whitelisting GitHub accounts would work, then dropped the code word. The owner's 06:38Z answer is that he creates the accounts himself, so there is no self sign-up in early access. The new card records that default.
- BEH-GATE-H1: nothing of Kit is read by anyone not signed in. Keep it for the new sign-in path.
- "Can always change later" and the two bare "Yes" notes read as confirmations of the username and password choice. Treat that choice as the owner's decision, reversible.
- 10-28 00:03Z: the owner cannot reinstall the installed app and asks whether to uninstall and reinstall to test the OAuth sign-in. This is a question for him, not build work. Default: he decides; no code change here. Test OAuth through BEH-USERS-10.
- 10-31 00:55Z: asks whether the owner can act as a test user himself by signing in with another organisation or a personal account. Default: yes, by signing in through BEH-USERS-10 with a second GitHub account. No code change. This is his decision to confirm.
Test plan:
- BEH-USERS-1: AuthConformanceTests.Every_step_answers_as_ui_js_does (existing) — each recorded sign-in step answers as the UI does.
- BEH-USERS-1: AuthConformanceTests.Every_scenario_replays_over_a_real_socket (existing) — every recorded scenario replays against a real server.
- BEH-USERS-1: AuthConformanceTests.The_throttle_cooldown_is_capped (existing) — the cooldown after failed guesses never exceeds its cap.
- BEH-USERS-1: SignInRaceTests.Guesses_sent_at_once_are_throttled_exactly_as_guesses_sent_in_turn (existing) — concurrent guesses are throttled the same as sequential ones.
- BEH-USERS-1 (new): a username and password the operator made signs the user in; a wrong password is refused; after the user changes it, only the new one signs in.
- BEH-USERS-10 (new): a GitHub account that a user has connected signs in as that one user; a GitHub account no user has connected is refused with 401 github-not-connected.
- BEH-USERS-10 (new): once GitHub is connected, the password is refused with 401 github-sign-in, and only after the right password is given.
- BEH-USERS-11 (new, card paused until 2026-11-14): the operator's reset clears the GitHub link and the new password signs the user in.
- BEH-GATE-H1 (new): signed out, each project read answers 401 not-signed-in, and an unknown project gets the same answer.
- BEH-USERS-14 (new): no sign-up route exists, a sign-up request answers not found, and an unknown username is refused with the same answer as a wrong password.
