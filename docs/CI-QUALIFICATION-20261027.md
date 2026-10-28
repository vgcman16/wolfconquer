# CI qualification: updated through 2026-10-28

## Full CI boundary

- [x] Exact public `224f1eb5dfc1ed148ccd53252f833339b5622f7c` passed
  [run 40646084998](https://github.com/vgcman16/wolfconquer/actions/runs/40646084998),
  job `122675116449`. Receipt `20261028T065007663Z-6d4fda5e` ran from
  2026-10-28T06:50:07.8007529Z through 07:32:27.2033388Z.
- [x] Restore/build and all eight steps exited 0: Core 29,039 Passed /
  164 existing NotExecuted, Updater 434, Launcher 57, synthetic parsers 20+13.
- [x] Actual terminal receipt, logs and three TRXs are retained in
  `.artifacts/root-fullaccount-durable-next/full-224f1eb5`.
- [x] Prior exact `8e8ff83f` run `40569779073` also passed. Historical `9e779d5d`
  run `40555473262` remains failed evidence and is not retroactively repaired.

The parsers did not perform network/client probes. Full CI does not establish
current-build live-client acceptance, a real-client feed or full parity.

## FullAccount qualification

- FullAccount class reset passed 354/354 focused cases in each public/private
  snapshot, zero failures/skips; all 52 baseline names remain.
- The corrected-fixture old-source baseline was 39 passed/13 failed. Earlier
  37 passed/15 fixture-setup failures are retained separately as setup evidence.
- Two final public staging controls passed 2/2 after an equivalent assertion
  cleanup. The private 354 includes that final assertion; these sets overlap.
- Exact public source `224f1eb5dfc1ed148ccd53252f833339b5622f7c` passed full
  [Windows run 40646084998](https://github.com/vgcman16/wolfconquer/actions/runs/40646084998):
  Core 29,039 Passed / 164 existing NotExecuted, Updater 434, Launcher 57,
  synthetic receipt parsers 20+13. All eight steps exited 0.
- This is source qualification, not deployment, native graph adoption, SQL,
  power-loss/multi-process safety or live-client acceptance. See
  `docs/FULLACCOUNT-CLASS-TRANSACTION.md` and FSB-158.

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
