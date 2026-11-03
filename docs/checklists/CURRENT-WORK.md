# Current reconstruction checklist

Updated 2026-11-03 UTC. The full English client/server objective remains active.
Root alone reviews, integrates, qualifies, stages, commits and pushes.

## Verified

- [x] Public Windows CI on `c476ca7c`: all eight stages passed; **35,723 passed,
  0 failed, 195 skipped** across Core, Updates and Launcher. All 664 Chinese7937
  rows passed. Skips are 43 opt-in SQL and 152 external-fixture cases.
  [Actual result and limits](FULL-WINDOWS-20261103-C476.md).
- [x] Complete six-image SQL admission data, native preparation, DH safety and
  atomic internal ticket redemption: 229 focused tests and 43 actual MariaDB
  provider Facts passed, with owned cleanup checked. The public run above also
  qualifies the pushed source. Native gameplay still saves files.
  [Provider results and limits](SQL-COMPLETE-ADMISSION-RESULTS-20261103.md).
- [x] New-client catalogs/codecs/Echo response consumer: 836 focused tests passed,
  0 failed/skipped, including 664 Chinese7937 rows. Main 57,093 and sub-item 1,693
  rows parse and round-trip all 67 fields. Complete semantic/runtime mapping
  remains open. [7937 status](7937-STATUS-20261103.md).
- [x] Private English proposal: **2,788 keys / 2,812 physical rows**, with prior
  translations, all unselected bytes and formatting preserved. Separate UStrRes
  proposal: 1,191 keys. Unapplied and unrendered; no whole-client percentage.
  [English results and limits](NAMED-UI-ENGLISH-20261103.md).
- [x] Exact arrow and fixed-percentage text-tool corrections: all 45 Python
  controls passed, 0 failed/skipped, including actual CLI byte goldens and
  malformed/mismatch refusals. FSB-340/343 are text-tool qualifications.
- [x] Default-network owned read-cancellation correction: 81/81 focused controls
  and public full CI passed. NoDelay was removed; no useful speedup was proven.
  The [earlier full failure](FULL-WINDOWS-20261103-B6.md) remains in history.

## Current qualification

- [x] Focused connection-lifetime correction (FSB-342): reviewed production and 13 new
  regression rows built and all 13 passed in the first affected run: 382 passed,
  2 failed, 0 skipped. Two older reply expectations were repaired with stronger
  ticket/state-preservation checks; run `20261103T170628834Z-d3c61f93` passed
  all 19 cases, 0 failed/skipped. A new public full gate remains required.
  It prevents a second ticket from replacing a bound
  connection's identity. [Contract](../protocol/specs/internal-handoff-connection-lifetime.md).
- [x] Genuine SQL lost-COMMIT-ACK control: all 44 actual provider Facts passed,
  0 failed/skipped, including real ACK loss, complete cold reads and exact replay.
  Owned MariaDB12.2.2, commands, data/private options and listener cleanup were
  independently checked. [Actual results](HANDOFF-SQL-RECOVERY-20261103.md).
  [Control and limits](../engineering/SQL-COMMIT-ACK-LOSS.md).
- [ ] Verify this reviewed batch's new public Windows gate after publication.
  Focused/provider qualification is complete; the earlier public c476 run does
  not contain this batch's connection-lifetime and ACK-loss changes.

## Remaining end-to-end work

- [ ] Genuine target authentication and complete SQL/native account, revision,
  Store and UID custody; complete native/sidecar/startup publication and every
  later gameplay writer, without shadow files or silent file fallback.
  [SQL scope](SQL-WORLD-STATE-20261103.md).
- [ ] Full 7937 profile/session activation, item-field meanings, Echo actions,
  costs and saving, and packet2350 gameplay integration.
- [ ] Remaining gameplay/social/events, actionable static defects, complete
  English resources, rendering checks and client distribution.
- [ ] After the operator's execution hold clears: actual target login, gear/mail
  actions and stability, then migrate one local endpoint at a time with a login
  check and single-file rollback after each change.

Preserve original sessions, saves, routes, backups, restore ZIP, `D:\cqclient`
and unrelated held edits. No AV changes or legacy executable launches while the
hold remains. Green source/provider checks do not establish host trust, live
acceptance, deployment, complete gameplay parity or that every bug is fixed.
