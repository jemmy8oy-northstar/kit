# Scheduled Opus review as a part of the agent pipeline
Work-item: scheduled-opus-review
Flag: this is a timer, an explicit exception to the no-timer rule in BEH-ORCH-3, so check it against the corpus conflict setting (level: high)
Cards:
- new: scheduled Opus review, its own part of FEAT-AGENT-PIPELINE
- FEAT-AGENT-PIPELINE
- BEH-ORCH-3
- BEH-SETTINGS-5
- BEH-SETTINGS-6
Notes:
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-07 01:32Z
- ../home/app/kit/prototypes/behaviour-ast/behaviours/kit-ui.notes.md#2026-11-15 12:51Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
The owner's note (11-07 01:32Z): add a scheduled Opus review to the agent pipeline, as its own part of FEAT-AGENT-PIPELINE, not a custom agent under BEH-AGENTS-1. It is an exception to the no-timer rule in BEH-ORCH-3. A cheap model still triages most tasks as the pipeline does now; the Opus review runs on a timer. The review checks neglected tasks, a sample of completed work, and the architecture, to keep things in check.
The owner's answer (11-15 12:51Z): the cadence setting goes on the Settings page as its own row, next to the pipeline, and can be set per project. The choices are daily, every two days, or weekly. This settles the open question between a new Settings row and BEH-SETTINGS-5 or BEH-SETTINGS-6: it is a new row, and it does not replace either of those.
Builder: the review is a timer, so record the exception to BEH-ORCH-3 as a contract, and do not fold it into BEH-AGENTS-1.
