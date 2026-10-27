# visible-dx8-d13-performer-durable-learning: Expected-session dance commitment

## Identity and scope

Revision 1, 2026-10-27; Root-requested correction to the already registered
Performer NPC7776 dance choices. Target is retained VisibleDx8D13 managed codecs;
no new capture, handler, opcode or native owner is introduced. Source baseline is
the current canonical public tree. Full CI40555473262 is independent of this
unqualified proposal. Root alone integrates and executes qualification.

The current method saves PlayerSpells before calling unconditional Store.Set.
A stale context can therefore overwrite a newer saved and live spell list.
Replace that ordering with real expected-session TryCommitSession: all required
preparation first, real file persistence under its actual gate, then Store publish.
Extract the method into focused partials rather than growing SubclassMasters.cs.
FullAccount, books, runes, native/session adoption and reverse writes are excluded.

## Packet contract

Preserve all eight choices and existing dialogs, phase/duplicate checks, typed
factories, UTF8 encodings, fields and packet order. No new byte golden is authority.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| NPC / option |7776 /12,11,10,8,7,6,5,4 | Existing request | Dance choice | Existing dispatch | Table below | SubclassMasters.cs |
| SpellCreate |2101 | Existing LegacySpellPacket | Newly learned zero-progress spell | First success frame | Unchanged | LegacySpellPacket.cs |
| PlayerEffect |2036, Effect10 | Existing LegacyStringPacket | zf2-e300, real UID | Second frame | Existing checked UInt32 UID | LegacyStringPacket.cs |
| System talk |2179 | Existing message codec | Existing choice-specific text | Third frame | Unchanged | LegacyMsgMessagePacket.cs |
| Dialog / option / avatar / finish |2255 | Existing NPC reply codec | Existing success dialog | Final four frames | Avatar110, option255 | NpcEntrypoints.Responses.cs |

Seven successful frames are serialized before durable file work. Phase, duplicate
and unavailable paths retain their existing dialog-only sequences. A stale or
retired expected Store owner produces no frames. Postcommit send failure is not
rollback or retry permission; stale lifetime stops the remaining notifications.

## Catalog/table contract

The existing private static definition table remains unchanged; there is no new
catalog or metadata provider. SubProfInfo is read by the actual subclass parser.

| Option | Spell | Required phase | Display name |
| --- | --- | --- | --- |
|12 |1420 |3 |Phoenix |
|11 |1421 |6 |Melody |
|10 |1422 |9 |Tipsy |
|8 |1415 |1 |Battle Dance |
|7 |1416 |3 |Triumph |
|6 |1417 |5 |Step Stomp |
|5 |1418 |7 |Moonlight |
|4 |1419 |9 |Snow Wind |

PlayerSpellSnapshotLoader stores Int32 count plus12-byte little-endian records:
ID/Level UInt16, XP Int32, Previous/Soul/SelectedSoul bytes and opaque row byte11.
The model serializes only11 bytes and zeroes that last byte. Preserve all modeled
prior rows/order and append the actual zero-progress row through the existing
helper, but do not overwrite saved opaque bytes. Under the actual Store+file gate,
read the bounded prior image, require its modeled rows to equal the frozen current
session, copy all original bytes, increment count and append the genuine learned
record. A conflict refuses as savedSpellsChanged. Missing file retains the existing
explicit first-save policy: serialize the complete frozen real session membership.
No meaning is assigned to padding or native selected-soul values.

## Validation and transaction rules

1. Capture the real Store holder identity before caller collection/attribute
   reads. When Store exists, require exact expected record, stable account/UID,
   and matching connection/session native binding. A genuinely absent native
   binding stays absent; a present client requires its real reciprocal Player
   and matching positive UID. No constructor or fake queue is supplied.
2. Preserve the original phase then duplicate gates and missing Store/root
   unavailable dialog. A stale Store owner is refused before any dialog output.
3. Capture actual array/exact List storage or this producer's privately owned
   frozen rows; unsupported caller collections refuse before I/O. Prepare private stable prior/learned membership, complete persisted bytes,
   checked real UID effect and all seven frames. Positive unique UInt16 IDs bound
   a learned membership to65535 rows; reject malformed complete source membership
   before allocation/writing, without inventing metadata or gameplay limits.
4. Capture the consumed SubProfInfo string and modeled spell list. Under the real
   expected-session TryCommitSession gate, revalidate those inputs and phase/
   duplicate eligibility, captured holder identity, optional physical owner and
   cancellation before file work. Attribute/comparer-capable reads are followed
   by a callback-free comparison of the actual known row backing before the final
   owner proof. Current real actor gates are used only when a
   native client exists; retain Store->client->Player order.
5. Reconcile/prepare the raw preserved image and persist it under SnapshotFileLockCoordinator using the
   exact configured root + invariant UID + .bin path and existing
   SnapshotFilePersistence `.player-spell-file-` writer. This is the same actual
   per-file primitive as PlayerSpellSnapshotLoader; no accepting saver is injected.
   TryCommitSession publishes only after that write returns true and does not
   invoke the ordinary duplicate auto-save callback.
6. Serialization/validation/I/O refusal leaves current session/spells and success
   outputs unchanged. Stale context cannot touch the file. The existing per-file
   replacement/restore contract remains; no SQL, crash-safe file+memory commit or
   new journal guarantee is claimed. An actual uncertain filesystem failure does
   not prove the file was untouched merely because memory stayed unchanged.
7. Send prepared frames only after commitment and outside held gates. Check the
   same holder lifetime and committed session/physical binding before/after awaits.
   A later replacement drops stale notifications without rolling back the saved
   learned spell. Postcommit send/log errors must escape this refusal catch.

Caller-owned raw dictionaries/lists can be mutated outside Store coordination;
complete input rereads and final plain owner checks detect observed divergence,
but this is not a universal concurrent snapshot or arbitrary raw-writer guarantee.
No new native MySpells authority is issued by this operation.

## Expected wire behavior and fixtures

- Retain existing LegacyPerformerDanceLearningTests eight success, eight phase,
  eight duplicate and missing-wiring/I/O controls and independent seven-frame
  codec assertions. Keep existing positive text and numeric values unchanged.
- Root's test helper adds stale expected record, removed holder, same-UID lifetime
  retirement and genuine changed eligibility/membership during preparation. Owned
  prior spell bytes, current record/rows and outbound list must remain unchanged.
- Use a real configured owned spell file whose actual share policy refuses save;
  no accepting persistence callback. Verify no memory/frame prefix on failure.
- A valid transaction saves the full prepared list before actual send capture and
  publishes once; postcommit transport failure retains committed learning.
- All tests remain offline owned fixtures. No native tiny-buffer or live account
  invocation is needed. Tests/qualification are authored and run separately.

## Evidence alignment

| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Eight real choices | Catalog table | Existing private definitions | Existing dance tests | Retained source/codec contract |
| File before memory under expected owner | Transaction4..5 | Existing configured route | Actual Store + owned file tests | New implementation, unexecuted |
| No stale success | Transaction1/6/7 | Not applicable | New stale-lifetime tests | Required invariant, not a live claim |
| Whole durability | Transaction6 | Existing per-file writer | No crash test supplied | Explicitly limited |

## Implementation and qualification

Production scope under `src/WolfConquer.Core/World/`:

- `WorldPacketHandlers.NpcEntrypoints.SubclassMasters.cs`: remove only the old
  dance method; existing definition table and call site stay exact.
- New `WorldPacketHandlers.NpcEntrypoints.Performer.Dance.cs`: existing outward
  gates, actual transaction invocation and committed sequence publication.
- New `WorldPacketHandlers.NpcEntrypoints.Performer.DancePlan.cs`: stable source
  image, prepared frames/bytes, real ownership and consumed-input checks/write.
- New `WorldPacketHandlers.NpcEntrypoints.Performer.DanceImage.cs`: bounded actual
  file read, modeled equality and opaque-byte-preserving append under file gate.
- New `WorldPacketHandlers.NpcEntrypoints.Performer.DanceSource.cs`: actual known
  row-storage witness and privately owned frozen output, with no callback reads
  after the final attribute lookup.

No central Store, native owner, other producer or schema changes are required.
Paired tests are owned separately in helper-performer-durable-learning-tests-next.
This proposal has no executed qualification. Root records actual results and a
scoped source-bug entry after intake. See SANITIZED-PARITY.md and
PROTOCOL-WORKFLOW.md; complete spell/native adoption remains open.
