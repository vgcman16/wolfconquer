# SQL world-state integration queue

Source-only batch based on b6aa0c1f. Root reviewed and composed24 source/test/spec
paths in a separate candidate. All new SQL models/repository/tests are UNBUILT
and UNRUN. No backend, native handler, schema or default configuration changed.

- [x] Immutable complete character/item images, raw physical-UID and save-graph
  validation, bounded revisions and complete operation descriptors are authored.
- [x] Real MySqlConnector schema/read/create/single/pair transaction source is
  authored, including global SQL UID constraints and durable exact replay.
- [x] Include explicit schema/version/engine/FK validation, bounded SQL blob/UID
  reads, commit-ambiguity outcomes and primary/cleanup preservation.
- [x] Include seven opt-in SQL Facts plus isolated model controls. The existing
  twenty SQL Facts remain unchanged; the required dynamic provider family is27.
  Controls cover real second-write rollback/retry, concurrent CAS, cold reads,
  historical replay, state/UID/identity refusals and four actual schema faults.
- [x] Replace a confounded malformed-UID test input with an otherwise complete
  image; add actual Create collision for a new account reusing a character UID.
- [ ] After the separate full33805 is terminal, compile and run model/parser/
  graph neighbors, inspect real failures and changed-path diagnostics.
- [ ] Run `Invoke-SqlGameplayCheck.ps1 -Port 34385` on a fresh owned instance;
  require all27 actual Facts passed/no skips and settled owned cleanup. A stale
  twenty-Fact assembly or the earlier SQL20 result cannot qualify this batch.
- [ ] Complete the appropriate integration/publication gate before publishing.

The full persistence horizon remains open:

- [ ] Connect actual SQL read/load to authenticated world/native admission with
  a genuine revision/ownership receipt; no shadow-file or accepting issuer.
- [ ] Route real gameplay save, inventory, trade and booth commits through the
  selected SQL owner and publish only after commitment. Handle committed replay
  as history, not current native authority; reconcile uncertain commit outcomes.
- [ ] Join the real global item UID/allocator namespace and migrate all remaining
  spell/proficiency/quest/gear/rune/other sidecar state coherently.
- [ ] Prove cold startup, interrupted transactions, disconnect/retry and full
  client acceptance on the selected backend. No live migration or fallback yet.

The running full gate remains on frozen b6aa in the OTHER trade-integration
candidate. Do not duplicate a host job. Preserve saves, routes, sessions,
restore ZIP,D:\cqclient,held59Guild/Union edits and9mail fixtures. Legacy client/
updater/native-inspection hold stays active; source checks do not establish
host cleanliness or current7937 gameplay support.

[Implementation contract](../engineering/SQL-WORLD-STATE.md).
