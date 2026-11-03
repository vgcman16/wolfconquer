# Complete SQL persistence qualification queue

Base e82916b9 separately passed15 bundle model cases and29 actual provider Facts.
The schema2 successor on3534b2b2 passed285/285 affected normal tests and all40
actual SQL Facts,0failed/skipped. Root read the completed TRX rows and verified
owned child/command cleanup and removal of temporary data/private options. These
are bounded component/provider results; the full Windows/public gate remains.
Root composed23 helper paths plus one owned-fixture startup argument.

- [x] Use the same four tables, UID constraint and durable operation ledger.
- [x] Add explicit schema2 and record_format1/2 barriers; refuse old schemas
  unchanged instead of automatically migrating or inventing missing sidecars.
- [x] Prepare actual six-image read/create/single/pair operations, exact v2 replay,
  account ordering, revision CAS and whole-transaction rollback in source.
- [x] Bound all blob projections before transfer and UID queries to expected+1.
- [x] Retain caught collisions if diagnostic lookup or rollback also fails.
  Double-fault provider branches remain source-reviewed, not exercised.
- [x] Include eleven new actual-provider Facts and preserve all29 earlier ones.
  The earlier marker-corruption input changes2 to99 because2 is now valid.
- [x] Scope max_allowed_packet128MiB to the next disposable MariaDB child only,
  supporting the bounded descriptor and server-built oversized-data controls.
- [x] Build passed161.502s; affected models/parsers, PIN/trade and local-tool
  neighbors passed285/285,0failed/skipped in99.511s.
- [x] Closed fresh SQL run at explicit port34386 passed all40 actual Facts,
  0failed/skipped on MariaDB12.2.2, with settled owned cleanup. Root verified
  temporary data and private client options absent after the handle exited0.
- [ ] Inspect changed-path diagnostics and complete the full Windows gate.
- [ ] Verify exact public source/checks before publication.

Controls include exact six-image cold reads, padding/presence, full pair/UID/
currency transfer, historical and cross-version replay, real first-afterimage/
second-write failure and same-op retry, CAS, malformed/oversized data, extra UID
index rows and old-schema refusal without DDL or data repair. Root read all40
actual result rows; no skip or earlier29-pass substitution qualified this batch.

No native gameplay backend, current7937 profile, client login/rendering or live
migration is activated. Authenticated native ownership, global UID allocation,
all enabled gameplay writers and matching-client acceptance remain required.
Preserve original data, routes, sessions, backups and operator execution holds.

[Storage contract](../engineering/SQL-WORLD-COMPLETE-PERSISTENCE.md) and
[larger horizon](SQL-WORLD-STATE-20261103.md).
