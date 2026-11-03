# Complete SQL persistence qualification queue

Base e82916b9 separately passed15 bundle model cases and29 actual provider Facts.
The schema2 successor is reviewed/intaken, **unbuilt and unrun**. Root composed
23 helper paths plus one owned-fixture startup argument. Forty SQL Facts are
authored and dynamically discoverable; this is not a40-pass result.

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
- [ ] Build and run affected models/parsers, PIN/trade and local-tool neighbors.
- [ ] Run the closed SQL family at fresh explicit port34386; require every one
  of the40 actual Facts passed, no skips/failures, and settled owned cleanup.
- [ ] Inspect changed-path diagnostics and complete the full Windows gate.
- [ ] Verify exact public source/checks before publication.

Controls include exact six-image cold reads, padding/presence, full pair/UID/
currency transfer, historical and cross-version replay, real first-afterimage/
second-write failure and same-op retry, CAS, malformed/oversized data, extra UID
index rows and old-schema refusal without DDL or data repair. These remain test
source until Root records actual execution.

No native gameplay backend, current7937 profile, client login/rendering or live
migration is activated. Authenticated native ownership, global UID allocation,
all enabled gameplay writers and matching-client acceptance remain required.
Preserve original data, routes, sessions, backups and operator execution holds.

[Storage contract](../engineering/SQL-WORLD-COMPLETE-PERSISTENCE.md) and
[larger horizon](SQL-WORLD-STATE-20261103.md).
