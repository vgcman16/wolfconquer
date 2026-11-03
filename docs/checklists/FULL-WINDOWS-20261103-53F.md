# Full Windows53f result

Source53f828174a170dcda1c4c59798feae4c1e215b60 stayed frozen in the separate
decompose-sql-7937 candidate. Root ran `pwsh -NoProfile -File tools/dev/Invoke-LocalCheck.ps1`.
Run20261103T003401400Z-06a79af7 started00:34:01.4942345UTC and terminated
02:47:02.2897565UTC, exit1. Its actual process/session are terminal.

| Stage | Actual result |
| --- | --- |
| Restore | Passed |
| Build | Passed |
| Solution tests: Core |34,814 passed,1failed,157NotExecuted |
| Solution tests: Updates |446 passed,0failed/skipped |
| Launcher restore/build/test | Not run after failing solution-test stage |
| Updater status/smoke parser | Not run |

Root inspected both actual TRX result-row sets, not just aggregate counters:
Core34,972 rows and Updates446 rows. Total completed evidence is35,260 passed,
1failed and157skipped. It is not a complete eight-stage pass or public CI result.
The five opt-in realSQL Facts skipped in this ordinary run; their separate earlier
actualSQL5 evidence must not be replaced by these skips.

Root also inspected the actual feature rows: all25 owned relic-decompose,14
decompose-request,39 decompose-preparation and14 AccountLoginService rows passed
without skips. The final-witness, bounded cancel output and visible-peer controls
are among those passes. This supports the bounded source-bug records while the
overall publication gate remains failed.

The only failure is
`LocalDevelopmentToolsTests.DefaultRootIsRelativeToScriptLocationRatherThanWorkingDirectory`.
The inert copied Plan script imported CheckLogPump.psm1 before its Plan return,
but that synthetic fixture correctly contains no execution-only pump. A later
source repair moves the import after Plan and adds a real missing-pump refusal
control. This does not change the old failure or establish a new test pass.

Private receipt/raw logs/two TRXs remain beneath that frozen candidate's
`.artifacts/local-dev/20261103T003401400Z-06a79af7*`. No client/native launch,
live7937 acceptance or host-cleanliness claim follows. Original data is unchanged.
Next: qualify the already-intaken trade/recovery/tool changes, actualSQL14 and
the appropriate complete gate before reviewed publication.
