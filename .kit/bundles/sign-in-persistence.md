# A sign-in survives a Kit deploy
Work-item: sign-in-persistence
Flag: This is the same sign-in surface as kit-supports-other-users. It is kept separate because BEH-GATE-H3 sits in kit-hosted.beh under FEAT-LOCK. Merge the two if the reviewer treats them as one fix.
Cards:
- BEH-GATE-H3
- FEAT-LOCK
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-19 19:28Z (line 141)
Files:
- prototypes/behaviour-ast/behaviours/kit-hosted.beh
Context:
BEH-GATE-H3 is this note verbatim, and its contract already records the cause: the server forgot its tokens on restart, while the browser kept a live seven-day cookie, so every deploy signed the user out. The chosen fix is that the token carries its expiry and an HMAC keyed from KIT_PASSWORD, so it is checked without a session table (kit#41). Changing the password signs every session out, by design. The part of the card read has no built line, so whether the fix is live was not checked. The server's token code was not located; search for KIT_PASSWORD to find it.
