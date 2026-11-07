# Early-access sign-in: a username and password James creates, then GitHub; GitHub only at release
Work-item: early-access-sign-in
Flag: Confirm the username-and-password plan with James before building; it overlaps sign-in-persistence. The 2026-10-31 test-user question is for James, and its default is in the context.
Cards:
- BEH-USERS-1
- BEH-USERS-10
- BEH-USERS-11
- BEH-GATE-H3
- BEH-SETTINGS-3
- BEH-USERS-8
- new: an early-access user can change the password James set for them
- new: the GitHub App connect works when the app is already installed, with no callback-URL error
- new: James can try the app as a test user through a separate account (question for James, see context)
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:35Z ("Invite only to start with")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:35Z ("code word is maybe not necessary")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Back tracking ... username and password")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-28 00:03Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 00:55Z ("act as test user myself")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 16:26Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-hosted.beh
- backend/Balenthiran.Kit.Services/SignedSessionStore.cs
- backend/Balenthiran.Kit.Services/SignInThrottle.cs
- backend/Balenthiran.Kit.DataModels/Models/SignInState.cs
- prototypes/behaviour-ast/ui/src/components/SignIn.tsx
Context:
The 06:35Z ideas (a whitelist of GitHub accounts, a code word to try to sign up) were dropped by James in the next notes. The plan that replaces them: for early access James creates each account, with a username and password, so there is no sign-up form, code word or GitHub allow-list. A user then connects GitHub (BEH-USERS-1). GitHub becomes the only sign-in at general release (BEH-USERS-10).
James said "they can change the password after", and no card covered that. The new card above covers it. Operator reset is BEH-USERS-11.
The 06:38Z "Yes" and "Can always change later" notes are not about sign-in: their headings put them under BEH-USERS-2 and BEH-USERS-3, so they are now in the kit-supports-other-users bundle.
BEH-GATE-H3 (a sign-in survives a deploy, and lasts seven days) is the same sign-in surface as the sign-in-persistence work item. Keep them separate unless the reviewer merges them.
Reinstalling the GitHub App (2026-10-28 00:03Z): he asks whether to uninstall the App and install it again through OAuth to test the feature. Uninstalling removes his existing App connection, so that is his decision. Default: leave it installed and ask him.
Callback URL (2026-10-31 16:26Z): on "already installed? connect it" he gets "This GitHub App must be configured with a callback URL", and he does not know what the App is for when everything works. Installing the App and signing in with GitHub (BEH-USERS-10) are different steps, and connecting from Settings is BEH-SETTINGS-3. Check which step that button uses before fixing the callback.
Test user sign-in (2026-10-31 00:55Z): he asks whether he can act as a test user himself by logging in with another organisation or his personal account. This is a question for James. Default: he signs in as a test user through a separate account; nothing impersonates another user's session. BEH-USERS-8 keeps each user to their own projects, and BEH-USERS-11 is the operator reset.
