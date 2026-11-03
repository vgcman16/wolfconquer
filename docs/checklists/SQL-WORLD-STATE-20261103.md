# SQL world-state integration queue

Bounded component qualification on e82916b9: all29 actual provider Facts passed,
0failed/skipped, on fresh owned MariaDB12.2.2 at explicit port34385. Root read all
actual TRX rows and verified settled children/commands, absent temporary data and
private client options. No live backend, native handler or original schema/data
changed. The existing native gameplay path remains file-backed.

- [x] Immutable complete character/item images, raw physical-UID and save-graph
  validation, bounded revisions and complete operation descriptors are authored.
- [x] Real MySqlConnector schema/read/create/single/pair transaction source is
  authored, including global SQL UID constraints and durable exact replay.
- [x] Include explicit schema/version/engine/FK validation, bounded SQL blob/UID
  reads, commit-ambiguity outcomes and primary/cleanup preservation.
- [x] Include nine opt-in SQL Facts plus isolated model controls. The existing
  twenty SQL Facts remain unchanged; the required dynamic provider family is29.
  Controls cover real second-write rollback/retry, concurrent CAS, cold reads,
  historical replay, state/UID/identity refusals and four actual schema faults.
- [x] Replace a confounded malformed-UID test input with an otherwise complete
  image; add actual Create collision for a new account reusing a character UID.
- [x] Preserve original collision errors if diagnostic lookups also fail; no
  deterministic combined-provider-fault control is claimed.
- [x] Refuse partial owned schemas before DDL; verify complete schemas without
  marker writes. Two new real-provider Facts cover four partial cases and
  complete-schema reinitialization with a real marker-update refusal trigger.
- [x] Compile the source and inspect the combined focused284P/1F/0skip run:
  all8 core models,59 PIN and26 local-tool cases passed. Retain the earlier
  CS8752 compile failure and the one wrong empty-item golden as history.
- [x] Correct the literal8-byte empty item fixture without changing production
  code or weakening byte/offset goldens; all15 bundle cases then passed/0skip.
- [x] Run `Invoke-SqlGameplayCheck.ps1 -Port 34385`: all29 actual Facts passed,
  no failures/skips and owned cleanup verified. The prior SQL20 result was not
  substituted for these new component tests.
- [x] Qualify schema2 six-image SQL persistence on3534b2b2:285/285 affected normal
  tests and all40 actual SQL Facts passed,0failed/skipped, with owned cleanup.
  [Exact component scope](SQL-COMPLETE-PERSISTENCE-20261103.md). This does not
  connect native gameplay or migrate an existing backend.
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

The completed fullb6 gate failed a separate PIN-trade positive fixture; its
successor passed the focused family but a new full gate remains required.
Respect operator-provided execution holds and preserve original data, backups,
routes, sessions and unrelated work. Source/provider checks do not establish
host trust or actual7937 gameplay support.

[Implementation contract](../engineering/SQL-WORLD-STATE.md).
