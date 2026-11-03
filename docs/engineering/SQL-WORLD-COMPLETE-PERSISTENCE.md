# Complete bundles in the existing SQL world-state domain

Source base `e82916b9`. This extends the existing SQL storage owner, not gameplay,
authentication, native admission, file routing, UID issuance or an active backend.
No original schema is migrated. Source3534b2b2 passed285/285 affected normal tests
and all40 actual provider Facts,0failed/skipped, on a fresh owned MariaDB12.2.2.
Root read every actual result and verified child/data cleanup; prior29/15 passes
were not substituted for this batch. The full integration/public gate remains.

## Explicit schema2 and record formats

The same four InnoDB tables, account keys, global SQL item UID constraint and
durable operation ledger are retained. Schema2 adds these columns to the fixed
state table after its original five: `record_format TINYINT UNSIGNED NOT NULL`,
then nullable LONGBLOB `spell_image`, `proficiency_image`, `quest_image` and
`combat_gear_image`. Columns, indexes, foreign keys, engines and marker2 are
fully verified. There is no ALTER/migration/default-sidecar path.

Zero owned names permits explicit creation. Four existing tables must already
verify completely and return without DDL/DML. Any other nonzero presence refuses
before mutation. Existing schema1 refuses unchanged; interrupted initialization
requires explicit operator repair. Concurrent external DDL is not made atomic.

Format1 is core-only with all four SQL sidecars NULL. Format2 is a complete bundle
whose four NULLs explicitly mean Missing; non-NULL values are Present, including
valid count-zero books. Format2/four NULLs is distinct from format1 unknowns.
Unknown formats and format1 with any non-NULL sidecar are invalid stored state.
Typed reads reject the other supported format with finite StateFormatConflict;
neither silently upgrades, projects away sidecars or invents absence. Existing
v1 methods keep format1 behavior; B's writer explicitly writes its format/NULLs.

## Typed complete reads and immutable outcomes

`SqlWorldStateRepository.ReadBundleAsync(accountId)` uses the same real provider
connection and RepeatableRead transaction. Mutation calls share the private
`ReadBundleHeldAsync(connection, transaction, accountId, forUpdate, token)` and
the actual account-row locks. Both read families validate exact current item UID
index rows against the complete immutable Core UID set.
The shared SQL UID query captures that set, then binds checked(count+1) as its
LIMIT before optional FOR UPDATE. This bounds the provider result and still
detects the first extra membership row; an empty expected set uses LIMIT1.
No forced-drain or underlying database/network IO measurement is claimed.

Complete SQL SELECTs project lengths independently from raw bytes. Each of six
blob CASE projections requires format2, valid nonempty core bytes, each present
image1..16MiB and combined image size at most32MiB. Oversized/empty/malformed
presence cannot transfer blob payloads before those server-side bounds. C# checks
the same lengths/presence, then obtains bounded exact bytes and uses the existing
Core/bundle factories. No loader normalization overwrites raw padding/order.

`SqlWorldStateBundleRecord.TryCreate` accepts only a real immutable bundle and
revision1..Int64.MaxValue. Read/commit result classes reuse existing status and
failure enums, private/internal validated construction, defensive participant
storage and identity/UID disjointness. Returned records are historical storage
outcomes; they confer no current native custody or success-publication authority.

## Shared commit and ledger contract

B's CreateBundleAsync/SaveBundleAsync/SavePairBundleAsync consume only complete
requests. They use the same operation ledger and SQL UID index, account-sorted
real row locks, revision/identity checks, whole UID closures, and one database
transaction for all six exact images, format2, revisions, UID rows and receipt.
Missing writes SQL NULL. Complete Save requires an already complete valid row.

The existing bounded ledger stores complete WCSQLW01 or WCSQLW02 descriptor plus
hash. Replay requires exact full equality and returns historical records even
after later revisions. Cross-version descriptors with the same operation ID
conflict. Unknown-commit/committed-cleanup outcomes and primary plus cleanup
exceptions remain distinct; rollback failure is not a clean refusal.

## Acceptance and remaining boundary

C's owned real SQL controls cover exact complete cold read/save/pair/replay,
presence and padding, wrong-format/old-schema/stale/identity/UID refusals,
malformed/oversized stored data, concurrent CAS and a real second-row trigger
that observes the first full afterimage then forces whole rollback/retry.
Existing29 provider controls remain; their marker-corruption literal must use99
because2 is now the valid schema marker. Root alone runs checks and confirms
actual required TRX outcomes and owned cleanup. Root confirmed all40 required
Facts passed on3534b2b2, including the original29 and new11. The owned child alone
used a128MiB packet limit for contained large-data controls; no existing service
setting changed. Double-fault cleanup remains source-reviewed, not injected
provider evidence. Native/Store/transport integration, migration/recovery and
current7937 client acceptance remain separate unfinished requirements.
