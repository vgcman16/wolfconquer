# visible-dx8-d13-ordinary-native-hydration: Saved physical identity before owner adoption

## Identity and scope

- Revision 1, 2026-10-27; requirement owner: Root's ordinary-admission milestone.
- Target: existing `VisibleDx8D13` managed admission/startup route. This slice adds
  no client contract. Its wire evidence is the retained typed codecs and existing
  startup sequence, not a new client capture or native execution.
- Source checkpoint supplied by Root: `9e779d5d`. Full Windows run40555473262 is
  in progress on that checkpoint; it does not qualify these proposed postimages.
- **P0 implementation:** hydrate saved Class/Level on the genuine cold physical
  Player in the existing committed prefix. Fence complete before/after identity
  and reject a fresh ordinary startup Level that its emitted codec cannot hold.
- **P1 design only:** detached saved Ninja/spell construction, admission ownership,
  coherent session/native spell state and managed publication. P0 does not assign
  MyNinja, MySpells, physical Equipment, native Socket, Fake or FullLoading.

## Packet contract

P0 changes no opcode, field, encoding, message order or successful frame. The
ordinary route commits before AnswerOk and then uses the actual connection's
encrypted `SendAsync`; a direct prefix commit emits no startup frames.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| LoaderHeroInfo | opcode22210 | existing frame | Existing startup hero | Existing sequence | Unchanged | `WorldPacketOpcodes.cs`, `LegacyLoaderHeroInfoPacket.cs` |
| LoaderHeroInfo.Level | payload+10 | byte | Saved physical level | Required | Fresh startup1..255 | `FromSnapshot` calls CheckedByte |
| LoaderHeroInfo.Class | payload+13 | uint32 little endian | Saved class code | Required | 0..UInt32.MaxValue | Same codec's CheckedUInt32 |
| LoaderHeroInfo.Name | payload+20 length, +22 bytes | UInt16 byte length, UTF8 | Existing name | Required | Existing codec checks | Same codec; untouched |
| ProtoHeroInfo | opcode2221, fields21/22 | retained schema | Level/Class | Existing sequence | Existing encoder rules | `LegacyProtoHeroInfoPacket.cs` |
| Saved learned spells | opcode2101, client Action78 phase | retained typed codec | Existing spell membership | Existing requested sequence | No added resend | `LegacySpellPacket.cs`, `WorldServerFrameHandler.Startup.cs` |
| Ninja component | source constants2393/2395 | existing component models | Component user/items | **Not activated** | Client request/order not inferred | `LegacyNativeMsgNinja*`, `NEXT-HOST-SEAM.md` |

The LoaderHeroInfo mesh can read the actual Player; Class/Level are still taken
from the saved snapshot. Matching these fields physically is not new dispatch or
cast authority. The physical Player Level is UInt16, distinct from this packet's
byte. Direct non-emitting prefix commits preserve the full0..65535 domain.

This is deliberately **not equivalent to the original whole Level setter**.
Root's original-source read of
`GameServer/MrRayzo.Role/Player.cs:1650..2475` found FullLoading-gated reward work
followed by unconditional `_level = value` and a clamp of values at least 150 to 150 that
also zeroes Experience. `GameServer/MrRayzo.Database/ServerDatabase.cs:813` calls
that setter and line 817 subsequently restores saved Experience. P0 mirrors the
already-committed managed snapshot without invoking that reward/cap transition.
Current managed saved-level preservation, the existing XP-gain policy's 140 cap,
the physical UInt16 domain and LoaderHero's byte width are distinct evidence and
policies. Target maximum-level/cap behavior remains unresolved. P0 introduces no
guessed 140/150 global cap or destructive normalization, and does not classify
that unresolved policy as a fixed source bug.

## Catalog/table contract

P0 uses the exact existing Character snapshot and native INI reader. It introduces
no table, class resolver, balance cap, replacement parser or encoding conversion.
The enclosing prefix verifies the snapshot against the saved file, read-leases
that file during real INI conversion, and prepares its existing exact Online image.

| Column/offset | Name | Type/width/encoding | Validation | Packet/behavior relation | Evidence |
| --- | --- | --- | --- | --- | --- |
| Character.Class | Class | saved integer -> physical uint | 0..UInt32.MaxValue; retain cold current-class conflict refusal | Physical class and existing startup Class agree | `ExistingPrefixLoad`, `CharacterSnapshot` |
| Character.Level | Level | saved integer -> physical ushort | Direct0..65535; fresh ordinary1..255 | Physical level and emitted byte agree | Same prefix and fresh admission validation |
| Character.MsgNinja | MsgNinja | persisted slash-delimited text | P1 requires exact complete format; no derived Score read | Detached candidate only until lifecycle settled | `Ninja.Persistence*`, `MailboxRecoveryLine` |
| PlayerSpell header | Count | Int32 little endian | Complete exact record count; positive unique spell IDs, nonnegative XP | Existing ordinary snapshot | `PlayerSpellSnapshotLoader` |
| PlayerSpell row | ID/Level/XP/PreviousLevel/SoulLevel/UseSpellSoul | UInt16/UInt16/Int32/byte/byte/byte plus opaque padding | P1 must preserve representability and padding | Actual native MsgSpell conversion | Same loader, `LegacyNativeMsgSpell.cs` |

Saved spells and Ninja data are not metadata authority. Unknown learned IDs are
membership, not permission to cast. Nonzero derived Ninja scoring requires the
actual matching metadata and six genuine rank owners; modern catalog adjacency
does not establish that binding. No missing table is replaced by an accepting
provider. P0 makes no saved Ninja absence/default decision.

## Validation and transaction rules

### P0: bounded physical identity correction

1. Retain actual connection/client/Player reciprocal identity, cold phase,
   FullLoading/Fake exclusions and existing UID/current-class checks. Capture the
   complete native before-values, including Class/Level, before any caller
   dictionary or genuine INI converter can run. Capture Transformation separately.
2. Prepare saved Class/Level using checked physical conversions. Reject a direct
   Level outside0..65535 without altering any participant. Do not impose an
   initial-level equality rule on a stable cold owner.
3. Keep `ValidateCharacter` unchanged: retained journal validation also calls it.
   A new `ValidateStartupCharacter` invokes it then rejects Level>255 because
   fresh admission will emit LoaderHeroInfo. Only the raw and normalized fresh
   `LegacyOrdinaryAdmissionPlan.TryPrepare` call sites use this narrower gate.
4. Keep the actual Beast/Hairface participants and their preimage/catalog checks.
   Beast prefix calculation uses activation, points, flag and real metadata;
   Hairface prefix copies persisted ownership rows and real catalog data. Neither
   preparation reads Class, Player Level or Transformation. Hairface `CanEquip`
   does read class/body, but prefix loading does not call it or equip anything.
   No derived prepared value therefore needs recomputation for this P0 change.
5. The existing final current-owner checks include the complete before-record
   plus unchanged Transformation. Direct commit uses its verified exact file
   replacement; ordinary admission uses the genuine joint journal/lease. Only
   after that commit, publish saved `_class`/`_level` with the existing plain
   Player field stores, then existing Hairface/Beast stores. Invoke no live
   setters, allocators, metadata, packets or callbacks in that publication.
6. Published ordinary proof requires the exact physical Player, reciprocal owner,
   cold phase, complete prepared after-values including Class/Level, unchanged
   Transformation, and the genuine published Beast/Hairface slots. A later
   class/level/transformation change makes the admission proof stale.

No resources or new durable fields are consumed. A precommit refusal leaves
the saved file, native before-state, Store admission and success outputs unchanged.
After durable commit, interruption is a committed/recovery boundary, not rollback
or permission to retry a grant. Existing per-holder, actor and file/journal locks
remain; this is not a global concurrent snapshot or crash-atomic runtime claim.

### P1: explicit proposed participant, not P0 implementation

The first candidate API is a sealed `LegacyOrdinaryNativeHydrationPlan` with
TryPrepare(actual connection, normalized CharacterSnapshot, normalized
PlayerSpellSnapshot), IsPreparedCurrent, MatchesCandidate(WorldSession),
PublishCommittedNative and IsPublishedCurrent. This is a design boundary, not a
new accepting callback or currently callable implementation.

It would prepare actual `new LegacyNativeSpellStorage(client)` and native spell
rows detached from normalized committed membership, plus an actual constructor-
bound Ninja loaded from an explicit complete saved line. It must refuse existing
foreign/nonempty owner slots rather than temporarily install candidates, and
must recheck the captured physical owner and inputs before plain slot publication.
MailboxRecoveryOwner is mailbox-debt-only and cannot grant ordinary authority.
Null/absent Ninja policy and roundtrip of UseSpellSoul bytes outside0..4 remain
explicit questions; the native setter otherwise silently maps them to None.

Do not integrate this participant until these real joins are specified:

- `WorldSession.PlayerSpells` and native ClientSpells are distinct. Ordinary
  EquipmentQuery can mutate the former during startup; skill books, soul changes
  and other managed writers also exist. A native slot snapshot alone would become
  stale. Specify one coherent writer/publication boundary before adopting it.
- Physical Equipment, Rune, score metadata, prepared rank lifecycle and durable
  rank loading are separate real prerequisites for Ninja Loading. Raw equipment
  observation is not the constructor-owned physical equipment storage.
- Component `Send` has no ordinary native Socket adoption. Prepared frames must
  use an actual Store/connection-bound managed publisher with before/after await
  identity checks. No Fake queue or Socket substitution supplies authority.
- Existing saved spells belong Action78, while startup's admitted spell publisher
  sends only new normalized grants. Establish Ninja frame placement and actual
  client request/response contract before adding frames or handlers.

Existing `ordinary-admission-v1` recipes/afterimages are not silently reinterpreted.
P0 changes no recipe or business file image. Future grants or durable participant
changes need versioned recovery proof; volatile reconstruction uses committed
afterimages and the real current Store holder. Network delivery is not exactly-once.

## Expected wire behavior and fixtures

Root owns matching P0 tests in a separate artifact; no helper execution is allowed.
Use genuine owned Character files, native client/Player, actual INI conversion,
Beast/Hairface catalogs, ordinary encrypted connection/Store and journal fixtures.

- Direct valid Class UInt32.MaxValue/Level65535 publishes those exact physical
  widths without packet emission; Level0 remains the physical-domain control.
- Fresh ordinary saved class and Levels140/255 retain existing startup bytes and
  agree with the same physical Player. Level256 refuses before durable effects.
- Negative or >65535 direct Level refuses unchanged; current Level changed during
  a real INI converter or after preparation invalidates the complete before-proof.
- Preserve the physical Player and unrelated owners/flags/view, nonzero
  Transformation, prepared Beast calculations and Hairface catalog row results.
- Changing published Class, Level, Transformation or owner/slots invalidates the
  actual published admission proof. Retain before/after durable interruption tests.
- P1 controls must cover complete malformed saved rows, actual owner slots,
  selected-soul roundtrip, session/native writer coherence, action ordering and
  stale/disconnected managed publication. No placeholder owner positive controls.

## Evidence alignment

| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Physical saved Class/Level | P0 steps1..5 | Exact captured Character file | Existing startup codec | Source-supported correction; unexecuted proposal |
| Byte-level startup admission | Packet table, P0 step3 | Saved Level integer | LoaderHeroInfo payload+10 | Established codec width; not a gameplay cap |
| Preserve transformation/derived prefix | P0 steps4..6 | Actual Beast/Hairface tables | No added packet | Source trace: preparation does not depend on old class/level |
| Retained journal compatibility | P0 step3, replay paragraph | Existing v1 recipe/images | Not applicable | Shared validator remains wider |
| Ordinary Ninja/spell ownership | P1 prerequisites | Saved rows exist | No ordinary Ninja capture supplied | Unresolved coherence/lifecycle/transport; not activated |

## Implementation and qualification

P0 source paths, all under `src/WolfConquer.Core/World/`:
`LegacyNativePlayerRuntime.ExistingPrefixLoad.cs`,
`LegacyNativeExistingPrefixBefore.cs`,
`LegacyNativeExistingCharacterPrefix.Admission.cs`,
`LegacyOrdinaryAdmissionValidation.cs`,
`LegacyOrdinaryAdmissionPlan.Prepare.cs`.

Supporting source: `LegacyNativeExistingCharacterPrefix{,.Prepare}.cs`,
`LegacyOrdinaryAdmissionPlan.cs`, `WorldSessionStore.OrdinaryAdmission.cs`,
`LegacyTradeJournal.AdmissionValidation.cs`, `WorldServerFrameHandler.{OrdinaryAdmission,SessionStartup,Startup}.cs`,
`LegacySpellPublication{Plan,Owner}.cs`, `WorldPacketHandlers.EquipmentQuery.DerivedSpells.cs`.
The initial frontier is recorded in `.artifacts/root-ninja-loading-next/NEXT-HOST-SEAM.md`.

No tests, builds, native/live actions or Git operations were run for this proposal.
Root reviews, integrates, qualifies and records a source-bug entry only with the
actual outcome. P1, SQL adoption, complete gameplay, native sender adoption,
client rendering and crash safety remain unproven. The source policy and workflow
are `docs/engineering/SANITIZED-PARITY.md` and `PROTOCOL-WORKFLOW.md`.
