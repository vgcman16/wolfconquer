# Development checklist

Updated2026-11-02. Full English client/server reconstruction remains active.

## Local full checkpoint - 2026-11-02

The reviewed Chinese7937 increment passed3507 focused checks with0failures/skips,
including231 new cases and3276 existing neighbors. The complete local Windows
gate then passed on source commit38e300c441b25da789c99a1ff73da860a1607bc6:
Core34085, Updates446 and Launcher57 passed,0failed and152Core NotExecuted.
All8build/test/script stages passed in121m37.774s; run
20261102T154534056Z-e1748d43 finished2026-11-02T17:47:11Z.
This is a local result; a newer public Windows CI result is not yet claimed.

- Complete59-field Echo requirements, immutable main/Sub ID lookup and observed
  operation6 request preparation. Per-player activation and persistence remain open.
- Explicit pinned relic table loading before native publication and real drop
  planner use of selected gold-limit rules; default protocol profile is unchanged.
- Lossless bounded item2350 extension retention around the strict43-field codec,
  preserving opaque bytes without inventing field2/3 semantics or a handler.
- Protected mandatory monster primaries now reject before cast costs; seven
  saved/native/real-wire controls pass. A runtime fixture log race is repaired.

These are managed source/test results. Actual7937 gameplay, English rendering,
full SQL/economy and live acceptance remain unfinished. The legacy executable
hold remains active. Specs and limits: [7937 ingestion](specs/auto-generated/7937/).

## Earlier published checkpoint

- [x] Sanitized C#/.NET9 source and updater/launcher tooling published.
- [x] Exact publicc271eec1 full Windows41243428459 passed all8stages:
  Core32,810/152existingNotExecuted, Updates446 and Launcher57,0failures.
  Actual receipt/logs/3TRXs/six complete managed outputs are retained.
- [x] Authenticated internal creation, completed-history protection and registered
  Rune composition/chance/decomposition have bounded ownership/recovery proof.
- [x] Reviewed RuneBag/WindWalker128, stamina75 and admission26 controls pass in
  overlapping sets; initial failures remain retained. Builds have0errors and no
  changed-path warnings. These are source/fixture results, not client acceptance.
- [x] Repair the later full-b8 stamina/query/Caprice regressions: combined
  build0errors/no changed-path warnings and3364/3364 focused,0failures/skips.
  Genuine malformed-admission refusals and successful gameplay/wire controls stay.
- [x] Verify this repair's exact-public full Windows gate: c271eec1 passed
  Windows41243428459; this is source/tooling, not live-client acceptance. Originalb8 full
  failed88Core cases; the focused result does not make that old run green.
- [ ] Finish native authentication/loader/shared ownership and real SQL/load-save.
- [ ] Complete native Action/auto-attack, remaining status/passive/resource owners.
- [ ] Complete economy, trade/social/events/housing and broader durable recovery.
- [ ] Triage actionable CodeFactor findings without weakening behavior coverage.
- [ ] Verify current source with a controlled actual client when the hold clears.
- [ ] Finish complete English,7937 runtime/profile activation and full-client feed.

The separate official7937 reference/stage and selected catalog/packet codecs do
not establish live feature support or a completion percentage. The older55%
planning estimate concerns legacy parity. Original saves/routes/restore points
remain preserved; legacy client/updater execution stays on hold.

See [status](RECONSTRUCTION-STATUS.md), [validation](../VALIDATION.md) and the
unchanged low-confidence [planning estimate](PARITY.md).
