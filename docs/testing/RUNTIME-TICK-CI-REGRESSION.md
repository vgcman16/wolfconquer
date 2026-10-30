# Runtime startup tick CI regression

Actual public7bb9156c fullWindows40983513888 failed one core test after a successful
restore/build: HandlerRuntimeLoopSendsLegacyXpAndStaminaAfterStartupWhenEnabled.
Core30694passed/1failed/152existingNotExecuted; Updates446passed. Launcher and
remaining parser phases did not run. Failed actual logs/TRXs are retained under
root-native-trade-packet-next/full-7bb9156c-failed. This is not a qualified fullgate.
The exact compiled failing test passed its one isolated rerun; its timing-dependent
count/tail assertion remains a defect rather than proof the full suite passed.

The test formerly waited for Count>=64, cancelled, then parsed the moving last two
frames as XP1/Stamina5. Startup/global frames and collection changes are not that
completion condition. Its focused partial now waits for both actual parsed updates
in one thread-safe capture snapshot, then checks their exact UID, individual type/
value and XP-before-stamina ordering. No production timing, runtime packet, timeout,
skip, expected count or eligibility rule is weakened to obtain a pass. The method
was moved out of the existing monolithic test file. Qualification is pending.

Root local qualification for the combined40-path candidate: Release build01
passed with0errors/157existingwarnings and no new changed-path warnings. Actual
focused01 passed423 with7existing skips; native-broad01 passed13278/13278 with
0failures/skips. Selections overlap. Receipts are in root-native-item-load-next.
The old public7bb fullrun failed one timing-sensitive startup test; its actual
failedTRXs/logs remain. Exact-commit successor fullCI, SQL, deployment and live
new-client/fullparity acceptance are pending. This note supersedes authored/unrun
labels in this spec/entry without expanding the verification scope.
