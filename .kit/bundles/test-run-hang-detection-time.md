# Shorten the time to call a test run hung
Work-item: test-run-hang-detection-time
Flag: he asked for a shorter time but gave no number, so the limit is set from a measured cold run, not guessed (level: high)
Review: agreed — matches BEH-ORCH-17 slice 3: the limit waits on a cold run timed on the runner (3x the slower of warm 378s and cold, rounded up to the minute), stays 1800s until then, lease margin and other stages unchanged
Cards:
- BEH-ORCH-17
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-11-19 19:29Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- grep the repo for Orchestrator.TestSeconds and RunnerPool.LeaseMargin (not located in triage)
Context:
Answer his question first: a warm test run of kit's suite takes 378 seconds (build 225s, .NET tests 31s, UI unit tests 74s), and a cold one is not yet measured. Today a hung test run is given up at 30 minutes and counted lost at 40 (BEH-ORCH-17 slice 2). Slice 3 on the card: time a cold run on the runner image, record it on BEH-ORCH-17, then set the test limit to about three times the slower time, rounded up to the minute. Keep the 10-minute lease margin. Do not change other stages' limits.

Test plan:
- BEH-ORCH-17: Orchestrator.TestSeconds is three times the slower recorded time (warm 378 s or the cold figure), rounded up to a whole minute, once the cold run is recorded on this card; it stays 1800 until then. The test computes the expected value from the recorded figures, so a changed figure fails loudly rather than silently.
- BEH-ORCH-17: Orchestrator.SecondsFor(Test) returns TestSeconds, and SecondsFor returns null for every other stage (Build, Lint, and the rest), so only the test limit changes; extends the existing Assert.Null(Build) in PipelineTests.cs to all non-test stages.
- BEH-ORCH-17: RunnerPool.LeaseFor(TestSeconds) is TestSeconds plus RunnerPool.LeaseMargin (10 minutes), and LeaseFor(null) is still the 150-minute lease.
- BEH-ORCH-17: a test run claimed at T has MaxSeconds = TestSeconds, GivesUpAt = T + TestSeconds and LostAt = T + TestSeconds + 10 minutes; not counted lost one second before LostAt, counted lost at it. Replaces the hard-coded 1800 / 30 min / 40 min asserts in PipelineTests.cs (around lines 333-349) with values derived from the constants.
- BEH-ORCH-17: Runs page "gives up at HH:MM" for a test run shows claimedAt plus the claim's givesUpAt, so it follows the new limit and not a hard-coded 30 minutes (existing Runs.test.tsx "says when a run with its own time limit gives up").
