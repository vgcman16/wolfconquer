# FSB-211: Complete native codec read publication

## Identity and scope
- Revision 1, 2026-10-29; Root-owned offline native packet qualification batch.
- Dependency: public `910403989ca8326f3bf4fa1f8c22c99e3b791634` plus exactly the 40 Root overlays retained in `DEPENDENCY.json`, including FSB-204 through FSB-208.
- Target: existing native component and retained 7899/VisibleDx8D13 wire evidence. This adds no new 7937 profile, opcode, enum validation or receiver activation.
- Owners: `GetAchievement`, `NpcDialog` and `GetArenaSignup`. Their creator/builder methods remain unchanged.

## Packet contract
Each reader starts at the caller's current cursor inside its actual supplied input window; none invents a header or opcode validation rule.

| Reader | Ordered fields | Successful consumption |
| --- | --- | --- |
| GetAchievement | UInt32 action, UInt32 UID, discarded UInt32, UInt32 flag | 16 bytes |
| NpcDialog | UInt32 NPC ID, UInt16 mesh, byte option, byte type, byte mode, UInt16 UTF8 byte length, complete CString field | 11 plus declared field length |
| GetArenaSignup | UInt32 dialog, UInt32 option | 8 bytes |

All scalars retain little-endian bits and unknown enum values. NPC text retains the existing UTF8 replacement and decoded-NUL truncation behavior while consuming the complete declared field. Its byte mode/unaligned length remain intentionally distinct from the existing NpcServerCreate layout. No producer/consumer round-trip layout is invented.

## Catalog/table contract
Not applicable: these codecs use no catalogs or new field meanings.

## Validation and transaction rules
- Required packet arguments reject explicitly before output assignment.
- Use the shared FSB-209 `LegacyNativePacket.ReadPrepared<T>` operation for the actual bounded read sequence. It validates actual storage/input state and restores the initial raw cursor on ordinary preparation failure.
- Parse into local tuple values; assign every caller `out` parameter only after the complete read returns successfully. Rejection retains the caller's original outputs, packet cursor, Size and input bytes.
- Missing fields retain FSB-208 EndOfStreamException rather than fabricating zero/empty values. Invalid physical/input state and disposed storage retain the shared validation failures.
- Mutable Size is caller metadata, not independent capture provenance or authentication authority. The caller must still establish a genuine initialized input extent.
- Public pointer/Size changes, Seek and Dispose do not share a lifetime gate. This is an exclusive-caller guarantee, not concurrent or foreign-pointer safety.
- No gameplay, player state, resource accounting, queues or transport is added or authorized by parsing.

## Expected wire behavior and fixtures
- Preserve existing independent valid Achievement, NPC and Arena goldens, field widths, unknown enum behavior and successful end cursors.
- Retain existing malformed/physical-boundary cases but replace assertions requiring partial caller outputs/consumed prefixes with unchanged complete caller state.
- New corrected-only cases use genuinely copied exact input with partial final fields, tiny capacities, null packets and disposed packets. No old unsafe binary/OOB baseline execution is authorized.
- Ordinary refusal must preserve full owned allocation bytes, initial raw cursor, Size and initialized caller values. NPC success continues to return the same supplied packet object.

## Evidence alignment
| Requirement | Existing evidence | Status |
| --- | --- | --- |
| Exact layouts and valid outputs | Three current codec source files and existing independent tests | Preserve |
| Missing tails must not publish partial fields | Sequential output assignments currently precede later reads | Confirmed source defect |
| Whole read failure must retain original cursor | Current FSB-208 scalar/list failure is bounded but outer readers already consumed earlier fields | Confirmed source defect |
| Actual input ownership and client acceptance | Supplied Size metadata and component fixtures only | Still unproved |

## Implementation and qualification
- Implementation calls shared `ReadPrepared<T>` authored in the independent FSB-209 package; it does not edit or duplicate that shared helper.
- Scoped production paths: World/LegacyNativeAchievementPacket.cs, LegacyNativeNpcServerReplay.Codec.cs and LegacyNativeArenaSignupPacket.cs (reader methods only).
- Scoped existing tests: LegacyNativeAchievementTests.Codecs.cs, LegacyNativeNpcServerReplayTests.Reader.cs and LegacyNativeArenaMatchPacketTests.Signup.cs. New controls are in CorrectedOnlyNativeCodecReadPublicationTests.cs.
- Source-only package: no restore, build, test, native/client execution, SQL, live configuration, process or Git mutation. Root must integrate the shared helper and qualify the combined source before any fixed/runtime claim.
- FSB-211 ledger fragment and HANDOFF record actual qualification limits. Unrelated builders and broader protocol/gameplay gaps remain open.
