# FSB-206: Native generic protobuf allocation bounds

## Identity and scope
- Revision 1, 2026-10-29; Root-authorized offline source milestone.
- Clean public source `731d8e54e435a6411361869a8a361f6731634705`; generated `910403989ca8326f3bf4fa1f8c22c99e3b791634` changes tracking only.
- Existing native generic Packet protobuf API, pinned protobuf-net 2.0.0.668 net40 asset. Preserve existing native schemas and selected VisibleDx8D13 protocol evidence; this change supplies no new client evidence or 7937 schema authority.
- Production scope: `Networking/LegacyNativePacket.Protobuf.cs`. FSB-205 owns TradeInfo complete-frame preparation independently; do not change its owner or helper APIs.

## Packet contract
The instance decoder consumes an existing native frame from offset zero. The writer appends a managed protobuf payload at its current cursor and does not write a header, seal or Size. All 32 generic production serialization calls in this committed source subsequently call Finalize; examples are MsgMessage2179, spawn2227, NinjaInfo2394, and Trade2119/2396. Fresh InitWriter state has Size0/cursor4 and is valid.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Declared length | 0 | UInt16 little endian | Header plus protobuf body; excludes optional seal | Instance read only | 4 <= declared <= actual Size <= immutable Capacity | Packet.Finalize and existing parser/goldens |
| Opcode | 2 | UInt16 little endian | Caller-specific opcode | Instance decoder ignores value | Four header bytes must physically exist | Existing generic parser and opcode-independent positive |
| Body | 4 through declared-1 | Existing pinned protobuf schema | Existing caller type | Existing required/optional/repeated contracts | Decode only declared bytes | Actual registered Dict/Gear contracts and goldens |
| Optional seal | Following declared body | Existing 8 bytes | Later Finalize output | Existing global seal configuration | Serialization reserves currently configured SealSize against Capacity | Packet.Finalize/WriteSeal and all production serializer callers |

No protobuf field, ordering, encoding, default or unknown-field behavior changes. The separate byte[] decoder receives a wholly managed payload and retains its packet-independent behavior, including on a disposed packet.

## Catalog/table contract
Not applicable: no catalog or schema changes.

## Validation and transaction rules
- Reject disposed/missing storage before native access. Validate the public cursor with full pointer-width arithmetic, not the truncating Position property; reject before-allocation and beyond-allocation cursors.
- Mutable Size must be within 0..Capacity. Writers do not require cursor<=Size or a received-frame Size. Validate complete managed payload length plus current seal reservation before copying; preserve cursor, Size and native bytes on bounds refusal.
- Serialize wholly managed first, then revalidate captured allocation/cursor/Size/seal-size state before native copy. Successful append changes only payload bytes and cursor; Size remains unchanged, null remains a zero-byte payload.
- Instance decode validates actual Size>=4 before reading the header and validates declared>=4 and declared<=Size before allocating/copying payload. Spare physical allocation never substitutes for supplied input Size.
- Parse the bounded managed copy before committing the cursor to declared length. Header, bounds and managed protobuf failures preserve the original cursor, Size, supplied object and native bytes. Revalidate allocation/cursor/Size before the cursor commit.
- Invalid storage/cursor/Size raises InvalidOperationException (disposed raises ObjectDisposedException); invalid/truncated frame lengths raise InvalidDataException. Pinned managed protobuf exceptions remain unchanged.
- No files, balances, authorization, gameplay owners or transports are touched.

## Expected wire behavior and fixtures
- Existing Dict payload `08011011` decodes Type1/Done17. At cursor4, Finalize2465 produces header `0800A109`, that payload, and optionally ASCII `TQServer`; serializer alone leaves Size0 and appends no seal.
- Retain Gear payload `080010111A18080010001800200028003000380040004800500058006000` at arbitrary valid cursor and actual original object aliases.
- Retain empty/default, opcode-independent, trailing-field exclusion, table-gate independence and managed-array overload tests.
- New source-authored controls cover exact physical capacity (with/without seal), one-byte overflow, real large MsgTalkProto content, negative/excess Size, invalid full-width cursors, disposed state, truncated actual input/header, declared length beyond actual input/allocation, malformed varint rollback, and fully managed raw decode on disposed/corrupt packet state.
- Never run unsafe old oversize/truncated-pointer cases for a red baseline. All new cases await Root qualification after integration.

## Evidence alignment
| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Existing successful protobuf bytes | Packet contract | Not applicable | Existing Dict/Gear goldens plus finalized Dict literal | Existing source evidence; new assertions unexecuted |
| Actual input length owns instance decode | Validation | Not applicable | Short Size with physically backed bytes refuses | Sanitized source correction; old test deliberately ignored Size |
| Native append capacity | Validation | Not applicable | Owned exact/overflow allocations | New source controls, unexecuted |
| No arbitrary restriction on raw overload | Packet contract | Not applicable | Managed bytes decode on disposed/corrupt packet | New source control, unexecuted |

Size remains caller-supplied mutable metadata. These checks bound native access
by the real allocation and supplied input window; they do not independently prove
the provenance or initialized extent of an overreported in-allocation Size. The
receiving caller must bind Size to captured input. This is not receive, session or
authentication authority and does not make hostile public-field writes safe.

## Implementation and qualification
- Focused filter: `FullyQualifiedName~LegacyNativePacketProtobufTests`; Root may widen to real MsgMessage/Ninja/Trade/Spawn codec neighbors.
- No build, restore, test or unsafe baseline execution in this helper scope. Full Windows gate40958745483/job123745654477 owns the host slot.
- This API has no common lock/lifetime owner with Dispose, public pointer/Size mutation or general IO. A noncooperating concurrent dispose/write remains outside this bounded operation contract; no whole-Packet thread-safety claim.
- Seal reservation captures the current size only. A later global seal change or unsafe general Finalize/Write/Seek remains a separate boundary. No blanket MAX_SIZE/UInt16-envelope policy is invented for this payload append API; complete-frame owners must validate their own full framing. FSB-205 supplies such ownership for TradeInfo separately.
- See FSB-206 in FIXED-SOURCE-BUGS.md and the isolated HANDOFF.md for exact unqualified source/test paths.
