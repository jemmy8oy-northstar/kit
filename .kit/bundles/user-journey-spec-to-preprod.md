# A user takes a project from spec to a preprod app they can try
Work-item: user-journey-spec-to-preprod
Flag: This is a vision note that asks whether Claude Code and a second Kit agent are sufficient or necessary; that is a stack choice for James, so raise it as a review request before anything depends on it.
Cards:
- BEH-WELCOME-1
- FEAT-SPEC-AGENT
- BEH-SPECAGENT-1
- BEH-SPECAGENT-3
- BEH-SPECAGENT-5
- BEH-ORCH-3
- new: a feature the coding agent finishes is merged to the project's dev branch (preprod), and James is notified
- new: from Kit, James can start a preprod build of a project and try it, and raise gaps with the spec agent
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 16:43Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
The note walks through the experience James wants: connect GitHub, then see existing projects with their test health or start a new one; design the project in a conversation with a real-time non-coding spec agent, which writes spec and marks features ready; a coding agent builds each ready feature into preprod; James tries the preprod app from Kit, and gaps go back to the spec agent. Technical questions go to an open-questions board, answered when the coding agent picks them up. The note ends by saying improving Kit is the top priority.
Existing cards: BEH-WELCOME-1 (onboarding), BEH-SPECAGENT-1 (the spec conversation), BEH-SPECAGENT-3 (technical questions on the open-questions board), BEH-SPECAGENT-5 (first analysis), FEAT-SPEC-AGENT, and BEH-ORCH-3 (stage order: triage, spec, tests and code, stopping at a flag). The two new cards are not on any card yet; check them before adding code.
Open questions in the note for James: whether Kit needs its own web template for new projects, and whether Claude Code and a Kit agent are both needed. The project has no kit:depth line, so the default applies; the stack question is recorded as an assumption, not decided here.
