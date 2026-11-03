# Captured native loading checkpoint

Updated 2026-11-03 UTC. This advances the complete SQL backend objective without
temporary Character INI files. It does not enable a new login or switch backends.

## Implemented and component/provider qualified

- [x] Root reviewed and integrated separate helper source for captured Character
  parsing, exact Online-only bytes and actual Player/Beasts/Hairface preparation.
- [x] The new wrapper exposes immutable data and preparation checks; it has no
  commit/publication API. Actual observed transport EOF denies preparation.
- [x] The existing file loader uses concrete bounded INI reads and fixed typed
  parsing. Missing optional defaults and valid Win32 quotes remain; malformed,
  overflowing, truncated or hidden-control values refuse before effects.
- [x] Authored controls cover supported encodings, independent account/player
  IDs, unchanged bytes/native state, metadata/owner changes, real peer EOF,
  full Hairface values, strict file inputs and converter independence.
- [x] Build the integrated batch. First affected execution finished
  700 passed / 10 failed / 0 skipped; old unsafe accepted-default/truncation
  assertions require correction with meaningful rejection coverage.
- [x] Corrected repair/component suite passed221/221; all42 actual provider
  Facts passed without failures/skips, with owned cleanup independently checked.
- [x] Record [actual results and limits](SQL-NATIVE-PREPARATION-RESULTS-20261103.md).
- [x] Complete the captured-prefix full Windows gate on3bb:35,660passed/0failed/
  194skipped, all8stages. Exact-public successor checks remain separate/pending.

The captured-prefix full gate passed on clean `3bb73465`, following its221/42
bounded qualification. Later complete-data source passed229combined focused
cases and all43 actual provider Facts. Its successor full public CI remains
pending; earlier storage or parent results do not replace that new-source gate.
See the [engineering contract](../engineering/SQL-CAPTURED-NATIVE-PREFIX.md).

## Still required for actual SQL-native admission

- [x] Qualify the separately integrated complete six-image typed reader and
  explicit native-prefix consumer, including quest hydration and defensive
  nested data exposure. All23 new component cases and all43 actual provider
  Facts passed,0failed/skipped. Native publication/custody remains open.
  [Data contract](../engineering/SQL-COMPLETE-ADMISSION-DATA.md).
- Genuine client-compatible authentication, actual Store/connection lifetime,
  SQL revision and shared item-UID custody.
- Complete sidecar, startup, gameplay writer, durable commit and recovery joins.
- Target dispatcher/profile activation, actual client login and stability.
- Resolve explicitly refused quoted/overlong captured-import cases from evidence.

No original client/server/data/route is changed. Existing execution holds remain.
