# Repo memory

## Running the backend tests in a bare sandbox
- No `dotnet` on PATH: the SDK is net10.0. Install with `dotnet-install.sh --channel 10.0` into a temp dir.
- ICU is missing and nobody can `apt-get install` without root. Without it the SDK aborts, and `ReportConformanceTests` expects ICU collation. Fix without root: `apt-get download` `libicu72` and `procps` from a private apt cache (`-o Dir::State::Lists=... -o Dir::Cache=...`), `dpkg -x` them into a local dir, then set `LD_LIBRARY_PATH` to its `usr/lib/aarch64-linux-gnu` and put its `bin` on `PATH`.
- `ClaimerSidecarTests` starts `kill` by name, so `procps` must be on PATH.
- Setting `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` avoids the ICU abort but makes `ReportConformanceTests` fail. Do not use it.
- Full run: `dotnet test backend/Balenthiran.Kit.slnx`, about 30 s, 1997 tests.

## Sign-in gates
- The sign-in gate answers before routing, for every method. A signed-out request to any path, even one that does not exist, gets 401 `not-signed-in`. A signed-in non-operator gets 403 `not-yours` on any write. Only a signed-in operator reaches the 404 `no-such-route`. See BEH-GATE-H1 and BEH-USERS-8.

## Pipeline PRs (BEH-PR-1/2, bundle pipeline-raises-pull-requests)
- Raising a pull request after a build is already shipped as BEH-ORCH-3 (`Pipeline.OpenPullRequestAsync`), and merge-off is BEH-ORCH-10 with `KIT_PIPELINE_MERGE` off by default. BEH-PR-2's "merge only with a corpus line" conflicts with BEH-ORCH-10's "merge on green checks when the switch is on". Raised as a review request on BEH-PR-2 in kit.beh; the built reading is BEH-ORCH-10 as shipped. Owner's answer on "complete means merge" is still open.

## Stage-0 gate
- `dotnet run --project backend/Balenthiran.Kit.Cli --no-build -- check kit --repo . --dir prototypes/behaviour-ast/behaviours`. `--configuration Release` has no build output here, so leave it out.

- `depends-on` in a `.beh` file takes decision ids only (`ADR-<PART>-<N>`). A behaviour id there fails `CommittedArtefactTests.Every_committed_corpus_parses` and so the whole backend suite. Write a dependency on a behaviour as a `contract` line instead.
- Running `dotnet test ./backend` on a host without ICU fails with "Couldn't find a valid ICU package". `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` avoids the crash but then `ReportConformanceTests` fails on purpose. Install libicu, or extract `libicu72` from Debian into a local directory and set `LD_LIBRARY_PATH`.
- Mapping entries in `kit.tests.json` and `kit-ui.tests.json` are a human's claim that the named test asserts the behaviour. The `kit-ui` mapping can name C# tests (see the BEH-FLAGS-1 entries). `verified` is optional and nothing reads it.
- Tester scope (BEH-FLAGS-3, BEH-SCOPE-1, BEH-SCOPE-2): what is built and what is not is recorded as `contract built (tester-feature-access)` and `contract not built (tester-feature-access)` lines on those cards.
- **Pipeline PRs (BEH-PR-1/2, bundle pipeline-raises-pull-requests):** raising a pull request after a build is already shipped as BEH-ORCH-3 (`Pipeline.OpenPullRequestAsync`), and merge-off is BEH-ORCH-10 with `KIT_PIPELINE_MERGE` off by default. BEH-PR-2's "merge only with a corpus line" conflicts with BEH-ORCH-10's "merge on green checks when the switch is on". Raised as a review request on BEH-PR-2 in kit.beh; the built reading is BEH-ORCH-10 as shipped. Owner's answer on "complete means merge" is still open.
- **Running the C# suite here:** the SDK is at `/tmp/dotnet`. libicu and `kill` are absent from the host, but Debian's arm64 debs can be fetched from deb.debian.org (`libicu72_72.1-3+deb12u1_arm64.deb`, `procps_4.0.2-3_arm64.deb`) and unpacked with `dpkg -x` into `/tmp/localpkgs/root`. Then `PATH=/tmp/localpkgs/root/bin:/tmp/dotnet:$PATH LD_LIBRARY_PATH=/tmp/localpkgs/root/usr/lib/aarch64-linux-gnu` and no invariant flag: all 2099 backend tests pass. Without them, `ClaimerSidecarTests` and the ICU-ordering tests fail for environmental reasons, not code reasons.
- **Tester reads by URL (kit#795, 2026-11-14):** BEH-USERS-8 refuses another user's per-project read with 403 not-yours, even for a public repo. Older tests and the tester bundle asked for a public read by URL; that was superseded, and the test now asserts the 403. Recorded as a contract on BEH-FLAGS-3.
- **Stage-0 gate:** `dotnet run --project backend/Balenthiran.Kit.Cli --no-build -- check kit --repo . --dir prototypes/behaviour-ast/behaviours`. `--configuration Release` has no build output here, so leave it out.

## Running the tests in this sandbox
- `dotnet` is not on PATH here; a copy lives under /tmp/dotnet (SDK 10.0.404).
- .NET aborts without ICU. Set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` to run at all. With it on, the two `ReportConformanceTests` (locale order) fail; that is the sandbox, not the code. Install libicu to get a clean run.
- UI: `cd prototypes/behaviour-ast/ui && npm ci && npx vitest run`.

## Spec conflicts (BEH-CONFLICT-1 to 3)
- The branch carried the conflict code and tests already (`ConflictModeTests.cs`, `ConflictsDirective.cs`, `agent.md` "When two specs conflict", Settings "When specs conflict"). A build of this item is verification, not new code, unless a test in the plan is missing.

## Session cost (BEH-COST-1)

- A run's dollar cost is the `total_cost_usd` of its `claude -p --output-format json` result. kit-run writes it to `KIT_COST_FILE`, pool.sh sends it as `costUsd` in the finished report, and the server records it in the `session_cost` table before it answers.
- The list-price estimate the proxy stores (`AddUsage`) is a different number. Never store it as a session cost.

## Tickets and the pipeline
- The blocked tag is `BlockedBecause` on the ticket row. A question for him is an escalated review; its note may end with `Silence: <default>`, which triage acts on if he stays silent (BEH-TICKET-3).
- `kit.beh` cards may not tag `in FEAT-*` for a feature that lives in `kit-ui.beh`; the parser refuses it (CommittedArtefactTests).
- The board's Tickets section (pipeline tickets, BEH-ORCH-16) is `prototypes/behaviour-ast/ui/src/components/Tickets.tsx`, with ordering in `ui/src/tickets.ts`. Cards (behaviours) on the board are `ui/src/board.ts`. They are different things.
- The UI test suite is `npm test` in `prototypes/behaviour-ast/ui` (vitest, 571 tests). The C# backend tests need `dotnet`, which is not installed on the build runner, so they have not run here.
- BEH-UI-6 (kit.beh) builds on tickets, not cards, and conflicts with the built BEH-ORCH-16 blocked-first ordering. It is under review; see its `review requested` line.
- **BEH-UI-5 is taken by the agents page** (kit-ui.beh, renamed BEH-AGENTPAGE-1). `Agents.test.tsx` fails if `BEH-UI-5` appears in kit.beh, so the ticket-board card is BEH-UI-6 in kit.beh (merge of kit/work/ticket-board-priority with dev, 2026-11-20). The `.kit/bundles/ticket-board-priority.md` bundle still says BEH-UI-5.
- **BEH-UI-7 (pipeline runs, kit.beh):** kit/work/pipeline-runs-recent-first took BEH-UI-5 before dev reserved it for the agents page, and dev's ticket card took BEH-UI-6. The runs card is BEH-UI-7 after the merge of dev on 2026-11-20 (its C# tests are BEH_UI_7_*).
