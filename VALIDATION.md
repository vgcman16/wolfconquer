# Publication validation

## Follow-up: exact GitHub check and offline staging

Published commit `03d9e1e1cd0c7b8631fdeab0e5430a7467a32f03` passed
[Windows local checks run 39797202093](https://github.com/vgcman16/wolfconquer/actions/runs/39797202093).
Its actual eight-stage receipt finished at `2026-10-22T14:40:35.1647385Z`:
15,089 server/tool tests passed with 166 skips, 219 updater tests passed, and
43 separate launcher tests passed. This is exact-commit CI, not deployment.

After that commit, the display-only working-directory update passed the full
launcher presentation suite: **49 passed, zero failed/skipped** on both local
private and sanitized public trees. The application's own XAML preview was
rendered and inspected locally, with no game or desktop input.

The exact-list offline staging tool passed **ten grouped synthetic cases** on
both trees. They exercise copied bytes, rebased plans, ordinary path containment,
actual junction/file-symbolic-link refusal and a real second-file writer conflict
that leaves explicit partial staging data with no publisher plan. No real client
was copied or published by these tests. These focused follow-up results do not
claim a new full solution run for later changes.

The parity renderer passed **17 tests, zero failures/skips** locally. Actual
outputs passed deterministic byte comparison: a 24-frame, 1.92-second GIF
(62,548 bytes for the initial status), static PNG and full generated Markdown.
The PNG was visually inspected. Tests verify the fixed fill across every frame,
matching assessment metadata, strict schema/date/range parsing, read-only check
behavior and actual input/output link refusal. The tests validate presentation
and safety behavior, not the subjective 55% planning estimate. GitHub automatic
refresh still requires its own successful run after this source is published.

## 2026-10-22 launcher and updater intake

The sanitized public working tree passed the full sequential Windows local check
`20261022T142329063Z-05ad322c`, finished at `2026-10-22T14:26:09.9030331Z`.
This is local evidence for the reviewed source, not a GitHub check, deployment,
full-client download, or real-game login result.

| Check | Result |
|---|---|
| Solution restore and Release build | Passed; 74 warnings; zero errors |
| Server/tool tests | 15,089 passed; zero failed; 166 skipped |
| Update engine and CLI tests | 219 passed; zero failed; zero skipped |
| Separate Windows launcher tests | 43 passed; zero failed; zero skipped |
| Dashboard status parser | 20 synthetic cases passed; no network/service probes |
| Saved updater-test result parser | 13 synthetic cases passed |
| Explicit client-file planner | Nine synthetic cases passed separately |
| Launcher-only ZIP packager | 62 synthetic assertions passed separately |

The eight-stage `Invoke-LocalCheck.ps1` restores/builds/tests the solution, then
the Windows-only launcher test project, and finally both receipt parsers.
Updater tests execute once in the solution run. Opt-in per-project/framework TRX
directories prevent two assemblies finishing in the same second from overwriting
each other's report. Receipts are derived from actual terminal test results.

Initial runs found four outdated local-tool fixture assumptions and one old
default-brand assertion, then a test-only immediate process-module identity race
and a TRX filename collision. These were genuine failed runs. Fixtures and result
isolation were repaired; the owned command probe now checks identity using its
retained process handle and waits for natural exit. No test was skipped or killed
to manufacture success. The 166 external-fixture skips remain excluded coverage.

Working-directory tests launch only an explicitly attributed copy of the Windows
command executable, not the game. Packaging tests use synthetic files and never
execute their contents. The planner's filename quarantine is not a content or
redistribution-rights audit. No official full-client package/feed is published.
The original launcher artwork is newly created, not extracted game artwork.

## Earlier 2026-10-22 server/tool snapshot (historical)

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

The latest successful sequential run, including the test-harness repairs described
below, finished at `2026-10-22T11:55:08.9381842Z`:

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

A later CI repeat at `939dbf8a` failed two tests (15,082 passed, 166 skipped):
another scheduling-sensitive snapshot contention check and a child process that
printed the expected profile-rejection error but did not exit before its timeout.
A local repeat after those fixes exposed the same timing pattern in the item-save
test (15,083 passed, one failed, 166 skipped). These are recorded failures, not
passing retries. The snapshot contention family now shares a small test helper
that owns the real file lock, observes actual failed-attempt temporary-file
cleanup, checks unchanged bytes while locked, then drains the writer after
release. The scalar cleanup assertion also uses the real transaction prefix.
Profile probes retain nonzero-exit, error-text, and no-listener assertions;
Windows child launches inherit crash-report suppression while the testhost's
previous error mode is immediately restored. There is no permanent OS setting
change or kill-as-success path.

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
