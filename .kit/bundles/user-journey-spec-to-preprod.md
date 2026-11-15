# A user takes a project from spec to a preprod app they can try
Work-item: user-journey-spec-to-preprod
Flag: This is a vision note that asks whether Claude Code and a second Kit agent are sufficient or necessary; that is a stack choice for James, so raise it as a review request before anything depends on it. (level: high)
Cards:
- BEH-WELCOME-1
- FEAT-SPEC-AGENT
- BEH-SPECAGENT-1
- BEH-SPECAGENT-3
- BEH-SPECAGENT-5
- BEH-ORCH-3
- new: a feature the coding agent finishes is merged to the project's dev branch (preprod), and James is notified
- new: from Kit, James can start a preprod build of a project and try it, and raise gaps with the spec agent
- new: a new project is created with its branches, branch rules and actions set up by Kit
- new: the spec agent asks which classes of user there are, and groups the workflows by class as spec
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-31 16:43Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/behaviours/kit.beh
Context:
The note walks through the experience James wants: connect GitHub, then see existing projects with their test health or start a new one; design the project in a conversation with a real-time non-coding spec agent, which writes spec and marks features ready; a coding agent builds each ready feature into preprod; James tries the preprod app from Kit, and gaps go back to the spec agent. Technical questions go to an open-questions board, answered when the coding agent picks them up. The note ends by saying improving Kit is the top priority.
Existing cards: BEH-WELCOME-1 (onboarding), BEH-SPECAGENT-1 (the spec conversation), BEH-SPECAGENT-3 (technical questions on the open-questions board), BEH-SPECAGENT-5 (first analysis), FEAT-SPEC-AGENT, and BEH-ORCH-3 (stage order: triage, spec, tests and code, stopping at a flag). The two new cards are not on any card yet; check them before adding code.
Added on this pass: the note also asks for a new project to get its branches, branch rules and actions set up by Kit, and for the spec agent to group workflows by user class. Check both new cards against BEH-WELCOME-1 and the SPECAGENT cards before adding code, and drop any that already hold. The note's next step, a page where spec questions are discussed with the spec agent before a coder picks them up, overlaps BEH-SPECAGENT-3; check it before building.
Open questions in the note for James: whether Kit needs its own web template for new projects, and whether Claude Code and a Kit agent are both needed. The project has no kit:depth line, so the default applies; the stack question is recorded as an assumption, not decided here.
