# SQL-native trade qualification checklist

Updated2026-11-03. Managed build completed; qualification incomplete, no public push.

- [x] Root reviewed/intaken A14 eligibility, B6 absent-Items recovery and C7 actual
  SQL/native trade controls, with independent cross-reviews. No accepting owner,
  saver, queue, active flag, new allocator or SQL character backend was introduced.
- [x] Included the2-path actual Plan import failure repair without changing the
  earlier53f checkout. FSB318-321 integration remains OPEN; source review alone
  is not verification. Actual managed/tool evidence follows below.
- [x] Nine new opt-in trade Facts join the original five. Dynamic source discovery
  and exact TRX acceptance remain; skipped/stale/missing results cannot pass.
- [x] Added a closed SQL gameplay command,6 authored tool controls and cooperative
  full-run host mutex. Their6 actual tool controls and16 existing Plan/status
  rows passed in the affected run; the actual provider callback is still unrun.
- [x] Managed bd49 build passed168.643s. Affected gate025707466Z-43900952 ran522
  rows:521passed/1failed/0skipped in544.752s. All metadata/eligibility/runtime/tool
  rows passed. [Result](TRADE-COMPONENT-20261103.md).
- [x] Reviewed the sole failed fixture setup: the real writer correctly refused
  its pending-journal mutation. Preserve that guard/failure. The repaired test
  asserts it, then injects only an owned external collision and requires a real
  noncollision recovery counterfactual at the same path. Production is unchanged.
- [ ] Execute that corrected case before actualSQL14; earlier521 passing rows
  are not relabeled as a clean522-row rerun.
- [ ] Run all14 declared Facts on a fresh owned MariaDB instance, including real
  trade outputs, leases/retry, EOF and cold recovery. Require owned cleanup.
  After terminal/managed qualification use
  `pwsh -NoProfile -File tools/dev/Invoke-SqlGameplayCheck.ps1 -Port 34383` from
  this reviewed checkout. It restores/builds fresh and accepts no partial filter.
- [ ] Inspect all current full53f outcomes, repair any remaining real defects,
  then run the appropriate full gate before reviewed publication.
- [ ] Continue the selected next booth milestone; its independent proposal is
  not part of this source checkpoint or any current qualification claim.

Contract: [SQL/native trade](../specs/auto-generated/sql-native-trade-roundtrip.md).
Earlier verified SQL5: [bounded result](OWNED-SQL-AUTH-20261103.md).
The last completed full local pass was35,124P/0F/152skip on older sourcefe3e;
the newer53f full terminated FAILED with34,814Core passed/1failed/157skipped and
446 updater passed. Later launcher/script stages were not run. Its sole Plan
import failure is repaired only in the next source and still needs qualification.
No live client was launched; hold, original data/routes/sessions and backups stay.
