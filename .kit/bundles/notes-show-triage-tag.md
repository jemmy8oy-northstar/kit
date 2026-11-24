# Notes show a triage tag once triaged
Work-item: notes-show-triage-tag
Flag: unsure which card the note belongs to; no existing card covers a triage tag on notes, so a new card sits under the triage card BEH-TICKET-2 (level: low)
Cards:
- BEH-TICKET-6
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-24 22:45Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit.notes.md
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
- The note asks that a note show a triage tag once triage has taken it. Only the Notes page shows it, beside the existing source tag (chat, note button, agent), which kit-ui's BEH-NOTE-15 already covers.
- Triage sets the tag; the user does not type it. Where the mark is stored (a line beside the note, or elsewhere) is the builder's choice; the note's own text is not edited.
- Untriaged notes show no tag. Kit already has triage (BEH-TICKET-2), so the tag is set when triage makes a bundle, a set-aside or a backlog item from the note.
