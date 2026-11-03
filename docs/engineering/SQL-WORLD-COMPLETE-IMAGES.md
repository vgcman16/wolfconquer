# Complete SQL image models, descriptor version 2

## Scope and authority

Source base `6b9d1366`. These immutable models represent the required complete
Character, Items, Spells, Proficiencies, Quests and CombatGear storage images.
They are unwired: no authentication, file access, native admission, backend
selection, UID issuance, gameplay mutation or response follows from validation.
They do not claim complete sidecar/runtime migration or live7937 acceptance.
The existing v1 models and SQL provider remain unchanged. Distinct bundle and
request types prevent passing a complete bundle to that provider implicitly.

There is no new client packet or catalog contract. All sidecar formats and
validation rules below come from installed server loaders and ordinary admission,
not from inferred current67 item semantics.

## Owned bundle API

Namespace `WolfConquer.Core.Data.SqlWorld`:

- `SqlWorldStateSidecarKind`: Spells1, Proficiencies2, Quests3, CombatGear4.
- `SqlWorldStateSidecarPresence`: Missing0, Present1.
- `SqlWorldStateSidecarInput(Kind, Presence, ReadOnlyMemory<byte> Bytes)` is
  caller-owned DATA until the bundle factory captures it.
- `SqlWorldStateBundle.TryCreate(core, sidecars, out bundle, out failure)` requires
  a valid immutable `SqlWorldStateImage` and exactly four distinct known kinds.
  It exposes immutable Core, AccountId, CharacterUid, complete sorted ItemUids,
  kind-sorted Sidecars and TotalImageBytes. Sidecars expose Kind, Presence,
  ByteCount and a defensive CopyBytes method. No parsed mutable DTO is exposed.

Missing requires zero bytes. Present requires a nonempty complete file image;
a valid count-zero book is present, distinct from missing. No missing image is
replaced with a serialized empty book. Every image is at most16MiB and the whole
bundle, including Core's exact Character/Items bytes, is at most32MiB.

The factory first captures the four input descriptors in local values, then
checks kinds, presence and all byte bounds before image copying/allocation.
It copies every sidecar before any payload parser runs, validates only captured
bytes, retains their exact order/padding, and clears the output on refusal.
Finite failure values identify invalid Core, count/kind/duplicate/presence,
presence-byte mismatch, per-image/aggregate bounds and the four malformed
payload categories. No private values or paths enter those failures.

## Exact installed validation

- Spells: `PlayerSpellSnapshotLoader.Parse` consumes the complete count/12-byte
  records and tail. Ordinary admission additionally refuses ID0, negative XP
  and duplicate IDs. The otherwise opaque record padding is retained verbatim;
  it is not erased by serializing parsed rows.
- Proficiencies: `PlayerProficiencySnapshotLoader.Parse` consumes complete
  count/20-byte records and tail; ordinary admission refuses duplicate IDs.
  No additional ID/status rule is invented. Padding/order remain exact.
- Quests: `PlayerQuestSnapshotLoader.Parse` performs its existing full structural
  walk and allocation bounds. Existing UID/status/intention semantics remain;
  this component does not impose new membership or gameplay rules.
- CombatGear: `LegacyTradeJournal.TryReadCompleteMailboxGear` is the genuine
  complete-image admission guard. It requires its established canonical complete
  loader/writer image. Its accepted input bytes are retained, not replaced with
  a newly serialized projection; noncanonical images remain refused.

Core retains its existing Character funds/identity and complete physical item
graph validation. All of these checks validate storage DATA, not current owner
or gameplay permission.

## Distinct complete request and descriptor

B's `SqlWorldStateBundleChange` and `SqlWorldStateBundleRequest` retain the same
operation kind, participant, revision and complete UID-disjointness rules as v1.
Inputs are account-sorted. Create uses revision0; save/pair use revisions1 through
Int64.MaxValue-1. The bounded descriptor is at most65MiB, checked before allocation.

Descriptor fields are little endian: ASCII `WCSQLW02`, UInt32 schema2, one byte
kind, one byte participant count, then16 `.NET Guid.ToByteArray()` bytes. For each
account-sorted participant: Int64 account, UInt32 CharacterUID, Int64 expected
revision, Int32 Character length and exact bytes, Int32 Items length and exact
bytes, Int32 physical UID count and ascending UInt32 UIDs. Then exactly four
kind-sorted entries: one byte kind, one byte presence, Int32 retained length and
every exact byte. Missing is explicitly present in the descriptor with length0.
Copies of descriptor and SHA256 are defensive; hash alone is not replay authority.

No v1 descriptor or schema is changed and no complete-bundle provider is added.
Future SQL integration must persist all complete images and a full exact ledger
descriptor atomically before any real native/gameplay publication.

## Qualification boundary

Root read every source/test path and qualified all15 bundle/request cases on
e82916b9,0failed/skipped. Actual parsers, independent literal sidecar/descriptor
goldens, presence, malformed/bounds, captured padding/order and request identity
rules are covered. The first combined run retained one wrong4-byte empty-item
golden; it was corrected to the actual two-count/8-byte format without changing
production serialization or weakening its independent byte/offset checks.
The separate29-Fact base SQL gate passed for Character/Items. The later schema2
provider on3534b2b2 also passed all40 actual SQL Facts, storing all six declared
images atomically in the same UID/ledger domain. See the complete-persistence
contract; native custody, full recovery and actual client rendering remain open.
