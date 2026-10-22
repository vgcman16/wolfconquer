# Publication validation

## 2026-10-22 update

Validated on Windows x64, SDK 9.0.314, Release, with a measured PowerShell 7
executable for native child probes. These results apply to the sanitized public
tree, not the private repository or a deployed game server.

| Check | Result |
|---|---|
| Restore | Seven projects; passed |
| Release build | Passed; zero errors; warnings remain |
| Full unfiltered solution tests | 15,084 passed; 0 failed; 166 skipped; 15,250 total |
| Local development tools | 15 synthetic cases included in the full run |
| Dashboard | HTTP/health/status passed; four explicitly selected service processes observed; POST 405 and unknown route 404; browser rendering inspected |

The successful sequential run finished at `2026-10-22T11:26:15.0945758Z`:

```powershell
pwsh -NoProfile -File ./tools/dev/Invoke-LocalCheck.ps1 -PowerShellPath <explicit-PS7-executable>
```

The script performs restore, build with `--no-restore`, then tests with
`--no-build --no-restore` and no filter. Small receipts and logs stay local under
ignored `.artifacts/local-dev/`. No input-hash proof ceremony is required.

An initial integrated run found missing owned Beast metadata in two test fixtures,
stale absent-method/provider-call assertions, and a scheduling-sensitive lock test.
The fixtures now supply real synthetic startup input and restore the prior cache;
assertions match implemented methods; lock release observes genuine failed-attempt
cleanup. Production retry limits and gameplay behavior were not weakened.

The first build emitted 74 warnings; the subsequent incremental build emitted 73
(unchanged Core warning not re-emitted). Warnings include NU1701 for legacy
MySql.Data, an unused interop field, nullable diagnostics, and xUnit suggestions.
This is not warning-free. Restore emitted no dependency audit warnings; that is
not a security guarantee. See [dependencies](DEPENDENCIES.md) for provider limits.

The 166 skips are [excluded coverage](docs/EXTERNAL-FIXTURE-TESTS.md), not passes.
Synthetic codec roundtrips are not a native-client oracle. These checks do not
establish SQL execution, complete English translation, current-source live-client
compatibility, deployment, full parity, or production readiness. GitHub workflow
and CodeFactor results apply independently to the commit they actually inspect.

## Earlier public commit 498e7c83 (historical only)

The edited publication snapshot was validated locally on Windows x64 with .NET SDK 9.0.314 and Release configuration on 2026-10-17. These results apply to the sanitized source in this repository.

| Check | Result |
|---|---|
| Restore | All seven projects restored successfully |
| Release build | Passed; zero warnings and zero errors |
| Full test suite | 10,531 passed; zero failed; 165 skipped; 10,696 total |

The full run used no test filter and finished at `2026-10-17T16:21:51.9282145+00:00`. The 165 skips are the [documented external-fixture methods](docs/EXTERNAL-FIXTURE-TESTS.md). They are excluded coverage, not passing tests. Active tests include synthetic packet/layout fixtures and local HTTP, socket, and child-process integration checks. The snapshot does not establish compatibility with a complete external game client or omitted game datasets.

Commands used:

```sh
dotnet restore WolfConquer.sln --ignore-failed-sources -p:NuGetAudit=false
dotnet build WolfConquer.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test tests/WolfConquer.Tests/WolfConquer.Tests.csproj -c Release --no-build --no-restore
```

Restore used the available local package cache and disabled NuGet vulnerability auditing for that operation; this is not a dependency vulnerability assessment. The test run also enabled a TRX logger for the private validation receipt. No GitHub CI result is claimed.

The 2,586 source/project/solution/SDK inputs were frozen before the build and checked again after testing. Their aggregate SHA-256 is `bd256bae28ad40aadeeb7ca51efb350bb7ca711745168dec6d57a5cebe6e9da5` (SHA-256 of the sorted compact JSON map from relative path to each file's SHA-256). Public documentation was finalized after the run. The staged source bytes were checked against those frozen inputs; build outputs, logs, fixture data, and original history are excluded.
