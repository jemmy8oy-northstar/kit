# Kit repo memory

- `depends-on` in a `.beh` file takes decision ids only (`ADR-<PART>-<N>`). A behaviour id there fails `CommittedArtefactTests.Every_committed_corpus_parses` and so the whole backend suite. Write a dependency on a behaviour as a `contract` line instead.
- Running `dotnet test ./backend` on a host without ICU fails with "Couldn't find a valid ICU package". `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` avoids the crash but then `ReportConformanceTests` fails on purpose. Install libicu, or extract `libicu72` from Debian into a local directory and set `LD_LIBRARY_PATH`.
- Mapping entries in `kit.tests.json` and `kit-ui.tests.json` are a human's claim that the named test asserts the behaviour. The `kit-ui` mapping can name C# tests (see the BEH-FLAGS-1 entries). `verified` is optional and nothing reads it.
- Tester scope (BEH-FLAGS-3, BEH-SCOPE-1, BEH-SCOPE-2): what is built and what is not is recorded as `contract built (tester-feature-access)` and `contract not built (tester-feature-access)` lines on those cards.
