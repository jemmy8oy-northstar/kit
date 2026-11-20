# Repo memory (kit)

## Building the C# tests in this sandbox
- `dotnet` is not installed. Install the .NET 10 SDK with Microsoft's `dotnet-install.sh --channel 10.0` into a temp directory, then set `DOTNET_ROOT` and `PATH` to it.
- libicu is absent, so `dotnet` aborts unless `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` is set. With it set, `ReportConformanceTests.Globalization_is_ICU_so_locale_order_is_reachable` and two report tests fail, and `ClaimerSidecarTests` fails because there is no `kill` binary. These three fail on an unchanged tree too.
- Baseline on an unchanged tree: 2014 tests, 2011 passed, 3 failed (those three).

## Pipeline routing (BEH-ROUTE-*)
- A flag's level is read by `FlagLevels.Of`: low and medium go to the review-low model (sonnet by default), high or unreadable goes to opus. Medium's model is an assumption until the owner confirms it.
