# CI qualification: 2026-10-27

[Windows40523086178](https://github.com/vgcman16/wolfconquer/actions/runs/40523086178)
failed on exact source71fd1df9 after54 minutes. Restore/build passed. Actual TRXs
report Core28,527 Passed/164 NotExecuted/3 Failed and Updater432 Passed/2 Failed.
Launcher and parser stages were not reached. Prior full source252bb950 remains
separate accepted evidence; this run is not a full pass.

One failure was a stale assertion that class1057 should not receive lineage spell
1110. The target-class repair correctly grants it; the test now checks the full
zero-level spell record while retaining every stat and persistence/repeat control.
The poison fixture now fires the actual global Task.Delay registration through a
manual TimeProvider, awaits real tick rearm and joins real workers before cleanup.
Its four aliases and all flag/attribute/frame/log assertions remain.

Actual Core/Updater host intervals overlapped for300.076 seconds. The local gate
now requests one MSBuild node and disables parallel TFM dispatch, preserving all
eight steps, tests, result layout, receipts and parser criteria. This proves the
intended plan; the next full run must verify actual host intervals. The workflow
budget is75 minutes to allow serial suites plus launcher/parser stages.

Three test-only diagnostic changes retain the native probe report when its child
marker is missing and bounded actual updater phase timings on failure. Original
process guards, deadlines and success assertions remain. The archive does not
prove which startup/phase delay caused those three failures; diagnostics alone
are not a timing fix.

Owned public focused qualification passed47 Core cases (including all four poison
aliases, native-launch controls and the corrected login case) and3 updater cases,
zero failures/skips. The existing eight-command plan is not-run and contains both
scheduling flags. Full exact-source qualification remains pending. No Ninja
proposal, game/service restart, live capture, route/save/restore-ZIP change or
full-client/parity claim is included.
