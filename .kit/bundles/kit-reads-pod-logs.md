# Kit reads the logs of the pods it runs for a project
Work-item: kit-reads-pod-logs
Flag: The 2026-11-06 13:50Z note is cut off in the notes file's triage input after "The user should not have to wire these", and kit-ui.notes.md has no 2026-11-06 entries. BEH-EXTDATA-2 already quotes the full sentence (kit-ui.beh near line 2973), so the card is the reference until the note text is checked.
Cards:
- BEH-EXTDATA-2
Notes:
- prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-06 13:50Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit-ui.tests.json
Context:
The note asks that Kit read the internal logs of the backend and service pods that Kit has run for a project, without the user wiring anything up, since the pods come from Kit itself.
BEH-EXTDATA-2 (kit-ui.beh near line 2962) already has this. Its contract sets the boundary: only the pods Kit deployed for that project, read with Kit's own cluster access, never a user's credentials. That is what separates it from BEH-EXTDATA-1, which reads sources outside Kit that a user wires up.
Check the built state of BEH-EXTDATA-2 and whether kit-ui.tests.json has a test line for it before writing code. If the card is built, confirm the boundary still holds (no user credential is read) and stop. Do not widen the card to other pods.
