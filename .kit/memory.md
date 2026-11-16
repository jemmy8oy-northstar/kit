# Repo memory

## Running the tests in this sandbox
- `dotnet` is not on PATH here; a copy lives under /tmp/dotnet (SDK 10.0.404).
- .NET aborts without ICU. Set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` to run at all. With it on, the two `ReportConformanceTests` (locale order) fail; that is the sandbox, not the code. Install libicu to get a clean run.
- UI: `cd prototypes/behaviour-ast/ui && npm ci && npx vitest run`.

## Spec conflicts (BEH-CONFLICT-1 to 3)
- The branch carried the conflict code and tests already (`ConflictModeTests.cs`, `ConflictsDirective.cs`, `agent.md` "When two specs conflict", Settings "When specs conflict"). A build of this item is verification, not new code, unless a test in the plan is missing.
