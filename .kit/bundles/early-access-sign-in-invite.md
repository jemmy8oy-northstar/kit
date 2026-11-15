# Early access: invite-only username and password sign-in, GitHub connected after
Work-item: early-access-sign-in-invite
Flag: the two bare "Yes" notes have no question beside them, so I attached them here as confirmations of the sign-in questions (level: low)
Cards:
- BEH-USERS-1
- BEH-USERS-10
- BEH-USERS-11
- BEH-GATE-H1
- new: early access accounts can only be made by the operator for invited people, with no sign-up page and no code word
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:35Z ("Invite only to start with")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:35Z ("code word is maybe not necessary")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Back tracking again")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Yes", first)
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Can always change later if need be")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Yes", second)
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
