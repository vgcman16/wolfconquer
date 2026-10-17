# Publication validation

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
