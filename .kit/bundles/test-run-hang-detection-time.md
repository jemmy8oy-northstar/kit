# Shorten the time to call a test run hung
Work-item: test-run-hang-detection-time
Flag: he asked for a shorter time but gave no number, so the limit is set from a measured cold run, not guessed (level: high)
Cards:
- BEH-ORCH-17
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-19 19:29Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- grep the repo for Orchestrator.TestSeconds and RunnerPool.LeaseMargin (not located in triage)
Context:
Answer his question first: a warm test run of kit's suite takes 378 seconds (build 225s, .NET tests 31s, UI unit tests 74s), and a cold one is not yet measured. Today a hung test run is given up at 30 minutes and counted lost at 40 (BEH-ORCH-17 slice 2). Slice 3 on the card: time a cold run on the runner image, record it on BEH-ORCH-17, then set the test limit to about three times the slower time, rounded up to the minute. Keep the 10-minute lease margin. Do not change other stages' limits.
