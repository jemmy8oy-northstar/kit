# Early-access sign-in: a username and password James creates, then GitHub; GitHub only at release
Work-item: early-access-sign-in
Flag: The two 2026-10-23 06:38Z "Yes" notes have no stated referent and may answer a question Kit posted, so confirm the username-and-password plan before building; overlaps sign-in-persistence.
Cards:
- BEH-USERS-1
- BEH-USERS-10
- BEH-USERS-11
- BEH-GATE-H3
- new: an early-access user can change the password James set for them
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:35Z ("Invite only to start with")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:35Z ("code word is maybe not necessary")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Back tracking ... username and password")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Yes")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Can always change later if need be")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-23 06:38Z ("Yes", second)
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-hosted.beh
- backend/Balenthiran.Kit.Services/SignedSessionStore.cs
- backend/Balenthiran.Kit.Services/SignInThrottle.cs
- backend/Balenthiran.Kit.DataModels/Models/SignInState.cs
- prototypes/behaviour-ast/ui/src/components/SignIn.tsx
Context:
The 06:35Z ideas (a whitelist of GitHub accounts, a code word to try to sign up) were dropped by James in the next notes. The plan that replaces them: for early access James creates each account, with a username and password, so there is no sign-up form, code word or GitHub allow-list. A user then connects GitHub (BEH-USERS-1). GitHub becomes the only sign-in at general release (BEH-USERS-10).
James said "they can change the password after", and no card covers that. The new card above covers it. Operator reset is BEH-USERS-11.
The 06:38Z "Yes" notes have no stated referent. Do not assume what they agree to; they may be answers to a question Kit posted in the 2026-10-22 22:47Z thread. "Can always change later" means the sign-in method should sit behind one place, so changing it later is cheap.
BEH-GATE-H3 (a sign-in survives a deploy, and lasts seven days) is the same sign-in surface as the sign-in-persistence work item. Keep them separate unless the reviewer merges them.
