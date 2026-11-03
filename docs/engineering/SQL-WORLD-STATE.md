# SQL character and item state contract, version 1

## Scope

Source base `b6aa0c1f0394832639b5f26acf57ce732fa21c2f`. This repository is a real
SQL storage transaction foundation and is initially **unwired**. It does not
authorize gameplay, switch world startup/loading/saving, migrate files, supply
native custody, emit responses or persist all sidecars. The complete horizon
still includes those real owners, recovery, authentication and matching-client
acceptance. Existing default storage and original data remain unchanged. UID
uniqueness here covers this SQL storage domain; the existing file-backed global
issuer/registry is not switched or merged by these models or repository.

Models and repository use `WolfConquer.Core.Data.SqlWorld`. The account ID is a
positive signed Int64 supplied by the eventual authenticated caller; it is not
inferred from the character UID or accounts.EntityID. Character UID is nonzero
UInt32, independently bound to the actual parsed Character UID. Storage identity
is not proof that the caller owns either identity.

## Immutable images

`SqlWorldStateImage.TryCreate(accountId, characterUid, characterImage, itemImage,
out image, out failure)` bounds each image to 1..16 MiB, captures both byte spans
and validates only those captured copies. It preserves complete bytes, including
Character encoding/BOM/comments/unknown fields and physical item record order.
The public image exposes identities, sorted complete `ItemUids` as a read-only
span and defensive `CopyCharacterBytes`/`CopyItemBytes`; no mutable parsed DTO
or caller-owned collection is retained or exposed.

Character validation reuses `CharacterSnapshotFileEncoding.Decode`,
`CharacterSnapshotParser.ParseText` and `LegacyOrdinaryAdmissionValidation.ValidateCharacter`.
Funds also require the real native CPS representation (CP UInt32, bound CP
nonnegative Int32, bank CP nonnegative Int64), nonnegative Money and the actual
warehouse alias/canonical trade-wallet parsers. A valid existing escrow claim
is preserved as data, not consumed or authorized. Invalid data refuses without
echoing input values. Decode retains the existing UTF8/BOM/Windows1252 policy;
no Chinese item67 semantics or new encoding assumption is introduced.

Item validation first uses `LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids`
on the complete original image, then `PlayerItemSnapshotLoader.Parse` and
`LegacyItemSaveGraph.TryCapture`. The existing `ItemSpellRawRecordsMatch` verifies
complete physical records/perfection records and deposited parent identity
against the model, permitting its established group ordering differences.
This prevents normalized/deduplicated/hidden items from becoming accepted
storage images. Zero/duplicate UID, malformed tails, grandchildren, invalid
placement or unmodeled/lost physical record bytes refuse. Every physical root
and deposited UID belongs to the retained sorted set, not just inventory roots.
This validates the existing save representation; item metadata/permissions and
runtime ownership remain actual gameplay caller prerequisites.

## Requests, revisions and canonical descriptor

`SqlWorldStateRequest.TryCreate(Guid operationId, SqlWorldStateOperationKind kind,
ReadOnlySpan<SqlWorldStateChange> changes, out request, out failure)` owns the
bounded changes and retains only immutable images. Kinds are Create=1, Save=2,
SavePair=3. Create has one participant and expected revision0. Save has one and
SavePair exactly two, with expected revisions1..Int64.MaxValue-1. Participants
are sorted by account ID; their account/character identities are distinct and
their complete new physical UID sets must be disjoint. Empty operation ID and
unsupported kinds refuse. Result revisions are1 for create, otherwise expected+1.
Read records permit revision1..Int64.MaxValue; exhausted records cannot be saved.

Descriptor maximum is65 MiB, checked before allocation. All integers below are
little endian. No padding, normalized image, semantic field substitution or
caller formatter participates:

1. Eight ASCII bytes `WCSQLW01`, UInt32 schema1, one-byte kind, one-byte count,
   then the16 bytes returned by .NET `Guid.ToByteArray()` (explicit mixed-endian
   .NET Guid representation, not an assumed RFC network order).
2. For each account-sorted participant: signed Int64 account ID, UInt32 character
   UID, signed Int64 expected revision, signed Int32 character byte length and
   exact image bytes, signed Int32 item byte length and exact image bytes, signed
   Int32 physical UID count and every ascending UInt32 UID.

`CopyDescriptorBytes` and SHA256 `CopyDescriptorHash` return defensive copies.
The ledger stores the full bounded descriptor plus hash, and replay requires
exact complete descriptor equality. Hash equality alone is not authority.
The operation ID is part of the descriptor, as are both participants and all
original bytes. Reversed caller participant order has the same canonical form.

## Fixed InnoDB schema and transaction owner

The original provider used schema1. The reviewed successor uses explicit schema2
and format1 for these core-only requests, sharing the same four fixed tables:
`wolf_sql_world_schema`, `wolf_sql_world_state`, `wolf_sql_world_item_uid` and
`wolf_sql_world_operation`. Format2 and its complete APIs retain all six images;
see [complete persistence](SQL-WORLD-COMPLETE-PERSISTENCE.md). Neither API projects
away the other format or automatically migrates a schema1 database. The image/
request descriptor contract above remains version1; new provider qualification
is pending independently from the retained e82916b9 results.
State is keyed by signed account ID with unique unsigned character UID and
positive bounded revision plus complete Character/Items blobs. Item ownership
has globally unique unsigned item UID, account index and real state foreign key.
The operation ledger has a binary16 operation key, binary32 digest and full
bounded request descriptor. That exact descriptor determines the historical
result images and revisions (expected+1), so no mutable current-row lookup or
separate accepting result payload defines a replay. Schema initialization is
explicit; constructors and reads do no DDL. The initializer verifies existing columns, types, indexes,
InnoDB engines and schema revision. IF NOT EXISTS alone is insufficient proof.
DDL is not claimed transactional; partial initialization failure remains explicit.
Before any DDL, the initializer counts existing owned table/view names. Zero
permits explicit creation; all four require full shape/version verification and
return without DDL or marker writes. One to three refuse before mutation with
sqlWorldSchemaPartial. Interrupted initial DDL requires explicit operator repair;
this does not claim protection against arbitrary concurrent external DDL.

`SqlWorldStateRepository.InitializeSchemaAsync`, `ReadAsync`, `CreateAsync`,
`SaveAsync` and `SavePairAsync` use installed MySqlConnector with a scoped supplied
connection string. It is never logged. No arbitrary SQL identifiers, file fallback
or successful default state is accepted. DB reads revalidate complete images,
identities, revisions and the actual UID ownership rows before returning Found.

Create/saves lock/record the operation and validate the complete descriptor in
the same real transaction. Pair state locks use ascending account order. Both
expected revisions and existing character identities are rechecked under locks.
Both old UID sets are removed before inserting either new set; the unique UID
constraint protects other actors. Both images/revisions, all UID ownership and
the durable operation receipt commit together. Rejection rolls back all changes,
including the operation row. A local lock is not this atomicity mechanism.

## Results and errors

`SqlWorldStateReadResult.Status` is Found/Missing/Invalid, with `Record` only on
Found and a finite `Failure` otherwise. Missing is distinct from malformed or
conflicting SQL state. `SqlWorldStateRecord` owns an immutable Image and Revision.
`SqlWorldStateCommitResult.Status` is Committed/Replayed/Refused; successful results
carry one or two historical committed Records and no failure; refused results
carry no records and a finite failure. A replay returns the original committed
images/revisions even if later operations have advanced current SQL state.

Provider and cancellation exceptions remain exceptions. A failure after commit
may have an ambiguous transport outcome; it must not be labeled a proved refusal
or invite a refund. Retrying the same operation ID/complete descriptor resolves
through the durable ledger. Cleanup must preserve the primary failure if rollback
or disposal also fails. If collision classification itself fails after a real
duplicate-key exception, both the original provider exception and subsequent
lookup/cleanup failure are retained. That combined-fault branch remains untested.
Repository success is storage proof only, not a native
publication receipt or permission to issue gameplay success.

## Qualification

Source e82916b9 passed the closed fresh owned SQL gate: all29 actual Facts,
0failed/skipped, including the20 existing auth/native/file cases and9 new storage
component/schema cases. Root read every result and verified owned cleanup.
Controls cover cold reads, create/CAS/pair, exact images/UIDs/revisions, stale/
conflicting/historical replay, concurrent ownership, real second-write failure/
whole rollback and partial/invalid schema refusal. Eight core-model cases passed
in the combined focused run; the distinct complete-bundle family passed15/15
after correcting its empty-item golden. Cleanup-double-fault paths remain source
review only; no forced provider/disposal-fault evidence is claimed. These checks
do not complete SQL native loading, gameplay writers, sidecar migration or actual
client acceptance. No live backend changed.
