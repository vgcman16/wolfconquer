# Two-owner SQL pair transactions

## Identity and scope

Revision 1, 2026-11-03. Extend the real
[bundle custody owner](sql-world-bundle-custody.md) to save two retained accounts
atomically. This is a prerequisite for SQL-backed trade and native admission;
authentication, native/Store publication, UID issuance and backend activation
remain separate unfinished work.

## Packet and catalog contracts

Not applicable: no client opcode, schema, catalog mapping or route changes.
Use the existing version-2 six-image SQL tables, UID constraints, CAS and exact
operation ledger. No game-file shadow, schema change or silent fallback.

## Ownership and transaction rules

- Each real repository acquisition retains its account lock, a private random
  192-bit nonce lock, actual selected database and physical connection ID. The
  nonce name is `wolf:sql:claim:` plus 48 uppercase hex characters, 63 ASCII in
  total. It is not returned or logged. Identity data alone cannot construct an
  owner or authorize native publication.
- `SavePairBundleAsync(other, request, token)` requires two distinct actual
  owners/accounts, exact SavePair participants, character UIDs and revisions.
  Take both async gates by account order; cancellation releases any acquired
  prefix. Never await SQL while holding a thread-affine Monitor.
- The lower account's retained connection performs one real SQL transaction.
  Both local connections are checked before/after; actual account and private
  nonce ownership for both is checked on the transaction server. Match database
  names ordinally. Names, numeric IDs and connection strings alone prove no
  common domain. Healthy invalid/domain refusals preserve owners and all rows.
- The held-commit proof has a closed shape: absent identities use the existing
  raw account-lock path; one identity requires exactly its single participant;
  two require their exact distinct accounts. Partial/mismatched proof cannot
  fall back to raw mode. Recheck before ledger insertion, mutation and COMMIT.
- After exact ledger replay and all current locked-row/CAS checks, allow UIDs
  already in the union of both current bundles to move or be removed. An absent
  UID requires a real issuer and is refused before mutation. Preserve deletion
  of both old memberships before insertion, all six images and SQL constraints.
- Current-owner acquisition/read locks the actual world/UID rows. A new claimant
  after peer connection loss must wait for an older pair's transaction before
  receiving current state; task incompletion alone is not proof of that wait.
- A known Committed/Replayed result followed by custody/cleanup failure becomes
  `SqlWorldCustodyOutcomeException`, retaining the exact Request, Result and
  actual cause. Unknown COMMIT remains the existing unknown-outcome type. Retire
  both operational owners and retain the same final request/exception on both.
  Cleanup attempts both actual connections, preserves errors and retains failed
  close custody for cleanup only. Historical replay never grants current native
  authority or reissues a removed UID through a new operation.

## Actual-provider acceptance

Keep all 50 single-owner/previous provider controls and add nine real controls:
opposite-order concurrent pair calls with one commit and exact replay; complete
invalid/domain/new-UID refusal preservation; genuine second-write rollback and
same-request retry; actual lost COMMIT ACK on both owners; actual held ACK plus
peer death preserving known commitment; historical replay after later UID removal;
and a held actual COMMIT request while a replacement claimant demonstrably waits
behind the older row transaction and subsequently receives revision 2. Two
further controls cancel a pair waiting behind a real second-owner single COMMIT
and require first-owner reuse before release; and start both real owner disposals
during a held pair COMMIT, requiring commitment, retirement and exact cold replay.
Started operation/disposal tasks must actually settle; an observation timeout
does not count as completion, and cleanup must preserve primary failures.

The final control must observe real waiter/blocker thread IDs, owned schema and
the requested PRIMARY row through MariaDB metadata. Absence, insufficient
privilege or hidden lock data fails the bounded control. Proxy gates forward
only original request/OK bytes, preserve strict default drop behavior and accept
clean EOF only at an actual packet boundary after successful ACK forwarding.
No synthetic reply, accepting authorization callback or fake owner is permitted.

MariaDB documents connection-scoped
[advisory locks](https://mariadb.com/docs/server/reference/sql-functions/secondary-functions/miscellaneous-functions/get_lock),
transaction-scoped [row locks](https://mariadb.com/docs/server/reference/sql-statements/data-manipulation/selecting-data/for-update)
and [lock-wait metadata](https://mariadb.com/docs/server/reference/system-tables/information-schema/information-schema-tables/information-schema-innodb-tables/information-schema-innodb_lock_waits-table).
These contracts guide implementation; actual owned-provider execution must
establish its behavior. Participating writers are coordinated, not arbitrary
privileged external database writes.

## Implementation and qualification

The single/pair implementation passed59 actual provider controls,0failed/skipped.
The earlier queued50-Fact batch was superseded without a standalone
pass claim; all its controls remain in the combined gate. Preserve failed attempts,
repair actual defects, and keep the required full integration/publication gate
separate. Affected normal data/event neighbors passed217/217; selected-SQL tools
passed9/9. These selections do not activate native admission or establish full parity.
See the [qualification queue](../../checklists/SQL-BUNDLE-CUSTODY-20261103.md).
No full SQL/native migration, 7937 action acceptance or host-trust clearance follows.
