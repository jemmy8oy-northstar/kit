# Memory for this repo

- Running the backend suite here: the build agent's sandbox had no .NET SDK. Installing it with `dotnet-install.sh --channel 10.0 --install-dir /tmp/dotnet` works. The runtime also needs `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (no ICU). With that set, three tests fail for environment reasons, not code: `ClaimerSidecarTests` (no `kill` binary), and two `ReportConformanceTests` (ICU ordering). Run them on a machine with ICU and procps to confirm.
- The UI suite runs with `npm ci && npx vitest run` in `prototypes/behaviour-ast/ui`.
- `FEAT-*` ids are resolved within one `.beh` file only. A card in `kit.beh` cannot name a feature defined in `kit-ui.beh`; `CommittedArtefactTests.Every_committed_corpus_parses` catches it.
- Project detection (`backend/Balenthiran.Kit.Database/UserProjects.cs`) lists any `.beh` on a repo's default branch. That already covers `.kit/` and the BEH-USERS-3 `behaviours/<name>.beh` fallback, so the fallback must stay until BEH-USERS-3's starter file moves into `.kit/`.
