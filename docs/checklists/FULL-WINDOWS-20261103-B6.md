# Full local Windows result: trade metadata checkpoint

Source `b6aa0c1f0394832639b5f26acf57ce732fa21c2f`. Root ran the complete
`tools/dev/Invoke-LocalCheck.ps1` command in a frozen isolated checkout.
Run `20261103T045521230Z-409984b4` started04:55:21.3522274UTC and finished
07:04:00.7324044UTC on2026-11-03, exit1. The handle terminated and no test process
remained before the next check started. The run was not restarted or edited.

| Stage | Actual result |
| --- | --- |
| Restore | Passed |
| Build | Passed |
| Solution tests | Failed: Core34,945 passed /1 failed /172 skipped; Updates446 passed /0 failed /0 skipped |
| Launcher restore/build/tests, updater status and smoke parser | Not run after solution-test failure |

Root counted actual UnitTestResult rows in both completed TRX files. Core had
35,118 rows and Updates446. Combined executed report:35,391 passed,1 failed,
172 skipped, no unknown outcomes. Aggregate notExecuted counters were not used
as skip evidence. The twenty opt-in SQL tests were skipped in this ordinary
run; their separate earlier actual-provider qualification remains separate.

The only failed test was
`LegacySecondaryPasswordRuntimeTests.VerifiedProtectedOwnersTradeAndRetainVerificationAcrossLawfulRecordCopies`:
expected received item UID20, actual original UID10 at its original line66.
Its genuinely native-bound positive fixture supplied no WorldData catalog.
The current metadata gate correctly refused the offered items; PIN verification
alone is not item-transfer authority. The isolated successor supplies the
existing coherent66-field catalog through actual handler construction, keeping
all swap, wallet, saved-item and PIN assertions and every production guard.
That repair needs its own qualification; it does not turn this run into a pass.

The next SQL candidate also contains new storage models/provider work and a
timing-reader enhancement. Its first focused build on source6b9d1366 failed
CS8752 in a new test helper's single target-typed params argument; no tests ran.
The successor uses an explicit SqlWorldStateChange constructor and batches the
reviewed complete-image models/controls into the next compilation. The failed
build log and source remain retained. New SQL provider Facts still require a
fresh closed database run with every dynamically discovered Fact executed.

No public push, new Windows Actions pass, backend migration or live-client
acceptance follows from this result. Original data, routes and sessions remain
unchanged. See [current work](CURRENT-WORK.md) and
[SQL qualification queue](SQL-WORLD-STATE-20261103.md).
