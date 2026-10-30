# FSB-210: Prepare native Clan packets before publication

## Identity and scope
- Revision1, 2026-10-29; Root requirement owner.
- Existing native component/wire profile7899/VisibleDx8D13. No7937 profile or live activation is inferred.
- Source baseline91040398 plus exactly40 reviewed Root overlays recorded in the helper dependency files.
- Scope is `World/LegacyNativeClanPacket.cs`, associated Clan packet tests and focused safety tests. Shared `LegacyNativePacket.PrepareFrame`/`ReadPrepared` APIs are separately authored; this consumer does not edit Networking.
- Correct ordinary preparation/parse failure publication while retaining successful2049 bytes and current-cursor decoding.

## Packet contract
- All builders finalize opcode2049. Native header is little-endian UInt16 declared length/opcode; declared length excludes the optional existing8-byte seal, physical Size includes it, final cursor4.
- Member body: UInt32 mode4, reserved0, reserved0, rowcount, then52 bytes per row.

| Field | Offset within member row | Width/encoding | Rule |
| --- | --- | --- | --- |
| Name | 0 | Fixed32, existing character-count UTF8 byte prefix then zero padding | Required nonnull string; empty allowed; preserve odd native byte truncation |
| Level | 32 | UInt32 LE from byte value | Preserve bits/value |
| Rank | 36 | UInt16 LE | Preserve unknown enum bits |
| Online | 38 | UInt16 LE | true1/false0 |
| Class | 40 | UInt32 LE | Preserve bits/value |
| Fixed value | 44 | UInt32 LE1 | Preserve |
| Donation | 48 | UInt32 LE | Preserve bits/value |

- Bulletin body: UInt32 mode25/player.ClanUID/reserved0 followed by a one-entry native string list. The current optional null bulletin remains an empty entry. It is not the clan.ID field.
- Relation/single body: UInt32 mode/clanID/reserved0 then required nonnull name/leader strings (relation2entries, single1). Empty strings remain valid. String lists use bytecount/UInt16 LE UTF8 byte lengths; null optional callback list omits the list entirely, whereas an empty callback list emits count0. Callback nonnull arrays are cloned; existing null individual entries remain encoded empty.
- GetClan parses UInt32 mode/UID/parameter plus the entire string list at the current cursor, preserving unknown mode bits. It does not add framing/opcode interpretation or make absent callback-list bodies valid GetClan inputs.

## Catalog/table contract
Not applicable: no table or catalog lookup changes.

## Validation and transaction rules
- Reject null stream, required owners/arrays/rows/member names/relation names/single name before touching the target. Bulletin requires an actual client.Player whose Owner is the supplied client; no surrogate owner or UID-only repair is permitted. Capture player.ClanUID and optional bulletin value; no clan membership mutation/permission is conferred.
- Clone mutable array/list references and capture every row scalar/name into private immutable snapshots before entering PrepareFrame. The callback may only consume those snapshots and immutable value arguments.
- Inputs/target remain caller-exclusively owned during this operation. A coherent concurrent source-graph snapshot, noncooperating mutation or native disposal is not established by these copies or by the owner-reference check.
- PrepareFrame owns actual scratch packet creation, matching Capacity/current seal, finalized bytes and single target commit. Any validation/serialization/physical-capacity/finalization failure leaves complete target bytes, raw cursor and Size unchanged. A successful builder returns the same target with the original bytes/layout and ordinary final cursor4/Size.
- GetClan uses ReadPrepared to parse a complete tuple under guarded cursor rollback. Assign all caller out variables only after the complete tuple succeeds. Refusal preserves every caller output and original raw cursor/Size/bytes, including a malformed late string after valid earlier words. The shared helper uses controlled reads on the same packet; it is not a separate input allocation or concurrent lifetime gate.
- No inventory/resource/persistence mutation occurs in these codecs. Packet transaction preparation does not prove trade, database atomicity or concurrent foreign mutation/lifetime safety.

## Expected wire behavior and fixtures
- Retain independent existing member72-byte, empty member20-byte, relation25-byte, single23-byte and callback16/17-byte goldens.
- Retain null/empty bulletin behavior and player.ClanUID selection. Replace null/late-row/owner tests that formerly required partial output with unchanged-target assertions.
- Corrected-only controls use fully initialized owned allocations and genuine NativeSpellTestFixture owners. Cover malformed last member/name/list, missing/foreign player, capacity/refusal at all builders, list count/entry-length bounds, disposed/invalid cursor state, exact capacity/seal outcomes and unknown enum bits.
- GetClan controls use genuine captured bytes and sentinel outputs; truncated scalars/count/length/late bodies must retain all outputs, even if earlier fields were valid. No unsafe old-code red baseline, arbitrary foreign pointers or invented positive owners.

## Evidence alignment
| Requirement | Source/wire evidence | Status |
| --- | --- | --- |
| Successful2049 contract | Existing independent LegacyNativeClanTests.Packets goldens and source Clan packet layout | Preserve |
| Target changes before owner/row validation | Builders InitWriter/Write before dereferencing later null fields | Confirmed source defect |
| Parser publishes early words before late list fails | GetClan sequential out assignments | Confirmed source defect |
| Capacity/cursor/seal commit | Separately owned Packet PrepareFrame/ReadPrepared API | Required dependency; Root must qualify composed source |
| New profile/server/gameplay authority | Unavailable in this slice | No claim |

## Implementation and qualification
- C# builder/parser: `World/LegacyNativeClanPacket.cs`; no dispatcher/profile activation.
- Tests: retained Clan packets plus new corrected-only preparation tests; FSB-210 ledger fragment retained separately.
- No restore/build/test/dotnet/native/client/SQL/Git/actual checkout mutations in helper scope. Root alone reviews, composes and executes combined managed qualification.
- Source candidate is unbuilt and unqualified. Full client/gameplay/host trust/parity and shared API concurrency limits remain separate.
