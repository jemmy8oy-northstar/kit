# Repo memory

## Running the backend tests in a bare sandbox
- No `dotnet` on PATH: the SDK is net10.0. Install with `dotnet-install.sh --channel 10.0` into a temp dir.
- ICU is missing and nobody can `apt-get install` without root. Without it the SDK aborts, and `ReportConformanceTests` expects ICU collation. Fix without root: `apt-get download` `libicu72` and `procps` from a private apt cache (`-o Dir::State::Lists=... -o Dir::Cache=...`), `dpkg -x` them into a local dir, then set `LD_LIBRARY_PATH` to its `usr/lib/aarch64-linux-gnu` and put its `bin` on `PATH`.
- `ClaimerSidecarTests` starts `kill` by name, so `procps` must be on PATH.
- Setting `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` avoids the ICU abort but makes `ReportConformanceTests` fail. Do not use it.
- Full run: `dotnet test backend/Balenthiran.Kit.slnx`, about 30 s, 1997 tests.

## Sign-in gates
- The sign-in gate answers before routing, for every method. A signed-out request to any path, even one that does not exist, gets 401 `not-signed-in`. A signed-in non-operator gets 403 `not-yours` on any write. Only a signed-in operator reaches the 404 `no-such-route`. See BEH-GATE-H1 and BEH-USERS-8.
