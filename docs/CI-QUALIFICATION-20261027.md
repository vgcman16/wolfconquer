# CI qualification: 2026-10-27

## Current exact-public full success

[Windows 40569779073](https://github.com/vgcman16/wolfconquer/actions/runs/40569779073),
job `122420603994`, tested exact source
`8e8ff83f119cc9c65c0753ba025a164f1a402aa7` and completed **SUCCESS**.
Receipt `20261027T193258424Z-a66534bf` started
2026-10-27T19:32:58.5563157Z and finished 2026-10-27T20:20:09.2926996Z.
Restore/build and all eight steps exited 0. Actual results: Core 28,974 Passed /
164 existing NotExecuted, Updater 434 Passed and Launcher 57 Passed. The receipt
parsers passed 20+13 synthetic cases; they did not perform network or client probes.

The exact terminal receipt, logs and three TRXs are retained in
`.artifacts/root-fullaccount-durable-next/full-8e8ff83f`. This supersedes the
pending full-gate statement for the accepted public source. It does not repair the
historical `9e779d5d` failure, prove deployment or establish live-client acceptance.

## FullAccount successor: focused-qualified, new full gate pending

- FullAccount class reset is integrated after354/354 focused cases in each
  public/private snapshot, zero failures/skips; all52 baseline names remain.
- A fixture-only baseline correction preserves the production account=UID loader
  invariant. The meaningful old-source baseline was39passed/13failed; an earlier
  37passed/15setup-failed result is retained separately.
- The two final public staging cases passed2/2 after an equivalent assertion
  cleanup. The private354 includes the final assertion. These totals overlap.
- This is focused source qualification, not deployment, native graph adoption,
  power-loss/SQL safety or live-client acceptance. A new exact-public full gate
  must qualify this publication batch.
- See `docs/FULLACCOUNT-CLASS-TRANSACTION.md` and FSB-158.

## Historical qualification entries

The dated evidence and earlier pending language below are superseded by the current exact-public result above.

## Ordinary player persistence checkpoint

Full Windows run [40555473262](https://github.com/vgcman16/wolfconquer/actions/runs/40555473262)
tested source `9e779d5d1d6233aee934a5c98c7b4e5b3852bf78`; terminal outcome FAILED,
not timeout. Receipt `20261027T175957217Z-4b2511f0` ran from17:59:57Z to18:52:16Z.
Restore/build passed. Core TRX:28,861 passed/164 NotExecuted/1 failed; Updater:434
passed. Launcher/parser stages were not reached. Actual failed artifacts are retained.

The only failure was `LegacyNativeNinjaWireContractTests` item-null: the historical
successful empty frame conflicted with the new required-envelope rejection.
The same case now checks ArgumentNullException, complete native storage, cursor,
size and queue preservation. All nonnull literal goldens are unchanged.

The newer composed batch passes524/524 focused public and524/524 private cases,
zero failures/skips; exact name sets match. Baseline508 was102 failed/406 passed;
the3 missing-PS7 controls separately passed3/3 with the measured runtime bound,
and one existing aborted high-login cleanup separately passed1/1 unchanged.
The98 intended differences remain actual old-source failures. All508 names are
retained;16 unsafe old reader/native bounds ran only after correction.

This is focused qualification, not a repaired historical receipt, new full gate,
deployment or live-client proof. See [scope and limits](ORDINARY-PLAYER-PERSISTENCE.md).

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
