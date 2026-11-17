# Pipeline untags blocked cards and wakes each stage only on change
Work-item: pipeline-blocked-watch-and-stage-wake
Flag: The new cards are written into kit.beh as the triage brief asks, but the pipeline cards (BEH-ORCH-1 to -16) and the Blocked cards (BEH-BOARD-17, -18, -23) live in kit-ui.beh, so the owner may want these moved there; the untag rule also changes when a Blocked card returns to the agent (level: high)
Cards:
- BEH-ORCH-17
- BEH-ORCH-18
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-16 22:56Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-16 22:58Z
Files:
- prototypes/behaviour-ast/behaviours/kit.beh
- prototypes/behaviour-ast/behaviours/kit-ui.beh
Context:
Note 22:56Z asks for a component that monitors blocked items and untags them when a blocker changes. Blocked is a card with a needs-context line (BEH-BOARD-17, BEH-BOARD-18); BEH-ORCH-17 says the monitor clears the tag when the blocker it names has changed, deterministically, and says so on the card. Note 22:58Z asks that pipeline stages run as separate jobs that wake, check for changes, and sleep when there are none, with wake frequency tuned over time. BEH-ORCH-18 builds on BEH-ORCH-11 (one job per stage) and BEH-ORCH-6 (run log, where tuning data comes from). Only the wake rule is card text now; frequency tuning is left for later. Both notes are one run loop, so they are one work item. Nothing is built.
