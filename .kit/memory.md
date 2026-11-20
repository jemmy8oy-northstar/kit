# Memory for this repo

## Running the backend tests in a bare sandbox
- The .NET 10 SDK is not on PATH; install it with dotnet-install.sh (channel 10.0) into a directory outside the repo.
- Without the ICU library, the CLI aborts in TimeZoneInfo unless `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` is set. With it set, two ReportConformanceTests fail (they need ICU). Those two and ClaimerSidecarTests (no `kill` binary) fail on the untouched branch too.
- The gate the CI runs: `dotnet run --project backend/Balenthiran.Kit.Cli -- check kit --repo . --dir prototypes/behaviour-ast/behaviours`.

## Tickets and the pipeline
- The blocked tag is `BlockedBecause` on the ticket row. A question for him is an escalated review; its note may end with `Silence: <default>`, which triage acts on if he stays silent (BEH-TICKET-3).
- `kit.beh` cards may not tag `in FEAT-*` for a feature that lives in `kit-ui.beh`; the parser refuses it (CommittedArtefactTests).
