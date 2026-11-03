# SQL world bundle custody

## Identity and scope

Revision 1, 2026-11-03. This is a server persistence prerequisite, independent
of the selected client wire profile. It does not activate Chinese7937, authenticate
a player, publish native/Store state, issue item UIDs or switch native saves to SQL.
The full English client/server objective and those remaining joins stay open.

## Packet contract

Not applicable: this API neither accepts client packets nor sends responses.
Existing client-confirmed packet contracts remain unchanged.

## Catalog/table contract

Use the existing version-2 SQL world schema, six-image bundle decoder, account
and character identity constraints, global item-UID membership and operation
ledger. No schema migration, file shadow or fallback is introduced. An actual
validated format-2 Found read is required; a missing/format-1/invalid record
cannot create an owner. Account ID and character UID remain distinct.

## Validation and transaction rules

- `TryAcquireBundleCustodyAsync` opens the repository's own nonpooled connection,
  acquires the account lock and a private random 192-bit claim lock, then reads
  a complete validated bundle under actual row locks. Only that
  issuance path can construct `SqlWorldBundleCustody`; callers cannot supply a
  connection, detached record or purported grant. Check cancellation immediately
  before returning the acquired owner.
- Each read/save verifies the actual selected database, physical connection ID
  and retained account/claim locks. An async gate coordinates use and disposal; it is not the SQL
  ownership proof. Provider loss retires operational access before cleanup.
  A failed close retains only cleanup custody for another disposal attempt.
- Bound save accepts only single-account Save operations for the acquired
  account/character and current revision. New operations cannot introduce item
  UIDs absent from the current committed membership; a real UID issuer remains
  required. Exact operation replay is checked before new-operation validation.
  A historical receipt is storage data, never a current native authority grant.
- Preserve the immutable operation and original exception when commit outcome is
  unknown or commitment succeeded before cleanup failed, including aggregates.
  Later use of that retired owner rethrows that outcome. A new actual owner and
  exact-operation replay must resolve the ledger; do not invent rollback, an
  ordinary refusal or a replacement operation ID.
- All six raw core/bundle Create/Save/SavePair entry points must cooperate with
  custody. Acquire account locks before transaction or operation insertion, in
  sorted/deduplicated order. Exact GET_LOCK result 1 alone grants ownership;
  result 0 is `SessionCustodyConflict`. NULL, errors or lost locks never grant.
  Verify every participant remains held, including before ledger/mutation/commit.
- Raw connections are nonpooled. Closing the actual connection releases any
  partially acquired prefix when a later account conflicts. Do not recursively
  acquire the retained owner's lock for its own save or return it to a pool.
- Derive names from actual `SELECT DATABASE()`:
  `wolf:sql:<first 24 uppercase SHA256 hex characters of UTF8(lowercase database)>:a:<positive invariant account ID>`.
  Maximum length is 55 ASCII characters. Hash/case collisions can only serialize
  unrelated accounts. The deployment must provide one physical ownership domain;
  explicit comma-separated server lists are rejected, while DNS/proxy topology
  remains an operator prerequisite rather than a source-proven property.

## Expected behavior and fixtures

Actual owned MariaDB controls must cover complete acquisition/read/save; exclusion
through a second repository; an independent account's positive acquisition; all
six raw paths refusing without changing images, UID membership or operations;
both pair paths releasing the first account when the second remains held;
identity/kind/revision/new-UID refusals; historical replay after later removal;
cancellation, disposal, genuine server connection loss and real lost COMMIT ACK.

After a real close, tests may retry only typed custody conflict for a bounded
three-second server release/reacquisition interval. Initial busy checks remain
immediate. Read-only server observations must verify the second claim still
exists. A timeout or any other failure fails the control, rather than granting
or substituting a fake owner. Owned KILL controls verify schema and nonce marker,
actual database and the exact account-lock holder before addressing a connection.

Existing concurrent CAS controls retain both requests: exactly one commits;
the initial loser may have revision or custody conflict. Once both finish,
retry the exact loser to revision conflict without an operation and replay the
exact winner without further effects. Keep existing crash/ACK controls intact.

## Evidence alignment

MariaDB documents connection-scoped cooperative advisory locks, their lifetime
beyond transaction commit, and actual holder queries:
[GET_LOCK](https://mariadb.com/docs/server/reference/sql-functions/secondary-functions/miscellaneous-functions/get_lock),
[IS_USED_LOCK](https://mariadb.com/docs/server/reference/sql-functions/secondary-functions/miscellaneous-functions/is_used_lock),
[RELEASE_LOCK](https://mariadb.com/docs/server/reference/sql-functions/secondary-functions/miscellaneous-functions/release_lock).
Actual provider qualification must establish the implemented behavior on the
owned server. These locks coordinate participating repository writers; they do
not protect against arbitrary privileged external database writes.

## Implementation and qualification

Implementation lives in `Data/SqlWorld/SqlWorldBundleCustody*`, repository custody
partials, `AccountLocks`, both commit partials and the existing nonpooled
connection factory. Tests are the `BundleCustody` partials of
`ModernSqlAuthFileRoundtripTests`, with owned database and ACK-proxy helpers.

The reviewed single/pair batch passed59 actual provider controls,0failed/skipped,
with actual source names, results and owned cleanup checked. Earlier44-provider
results and failed attempts remain separate. A new full Windows gate remains
required. Serialize focused/provider/full checks and preserve failed runs.
See [current work](../../checklists/CURRENT-WORK.md) for actual results and limits.

The [two-owner pair contract](sql-world-pair-custody.md) extends bound storage
save without activating native admission. Real
authentication/connection-bound native publication, UID issuance, startup,
sidecar ownership and every later gameplay writer still need an end-to-end join.
