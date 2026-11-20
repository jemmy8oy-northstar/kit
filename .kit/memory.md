# Memory about this repo

Kept in the repo so every session that works on it reads the same facts.

## Running the backend tests here

- The backend is .NET 10 (`backend/Balenthiran.Kit.slnx`). The sandbox has no SDK by default; `dotnet-install.sh --channel 10.0` works.
- Without libicu the SDK aborts, so run with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`. That mode also makes three tests fail (ReportConformanceTests x2, ClaimerSidecarTests.A_claim_never_reported_is_reported_failed_when_the_pod_is_stopped: no ICU, no `kill` binary). They fail on main too; CI has both.
- `dotnet test` prints nothing in this shell. Use `--logger "trx;LogFileName=..."` and read the outcomes from the TRX file.
- The corpus check runs with `dotnet run --project backend/Balenthiran.Kit.Cli --no-build -- check kit --repo . --dir prototypes/behaviour-ast/behaviours`.
- StyleCop (SA1402) is on: one type per file.

## Session cost (BEH-COST-1)

- A run's dollar cost is the `total_cost_usd` of its `claude -p --output-format json` result. kit-run writes it to `KIT_COST_FILE`, pool.sh sends it as `costUsd` in the finished report, and the server records it in the `session_cost` table before it answers.
- The list-price estimate the proxy stores (`AddUsage`) is a different number. Never store it as a session cost.
