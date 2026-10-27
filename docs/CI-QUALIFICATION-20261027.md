# CI qualification: 2026-10-27

## Latest full result and corrected plan assertions

[Windows40537900359](https://github.com/vgcman16/wolfconquer/actions/runs/40537900359)
tested exact source4b9762ccbcd362a1af208b3bb5b627e11dd7a90f and finished FAILED
at16:58:36Z. Restore/build passed. Actual Core results:28,528 Passed,164 existing
excluded cases and2 Failed; Updater434/434 passed. The only failures were the
Plan/DryRun argument-index assertions after adding the two serial test flags.
Earlier native-launch, poison and1057 cases passed. Launcher/parsers were not
reached. The real failed receipt, two TRXs and stdout/stderr are retained.

Core finished16:56:31.857Z; Updater started16:56:39.110Z. This verifies no overlap
for that run, without proving the earlier timing-failure causes. The public test
now checks the exact configuration pairs, serial flags, logger/results positions
and all retained spacing/probe/no-write controls. All15 local-tool cases pass in
the1,082-case public focused run. No script logic or deadline was relaxed.

The composed Ninja/caller selection also passed1,067/1,067 in the private snapshot;
see [qualification and limits](NINJA-SPELL-SAFETY.md). The new publication batch
requires its own exact-public full gate. Prior failed receipts remain failed.
No game/client/service restart, SQL, route/save/restore-ZIP change or live-client
acceptance follows from these checks.

## Earlier repair checkpoint

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
