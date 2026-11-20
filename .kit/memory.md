# Kit repo memory

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
