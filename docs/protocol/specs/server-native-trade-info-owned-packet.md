# Native TradeInfo bounded, owned packet preparation

## Identity and scope

Revision 1, 2026-10-29; FSB-205. Clean public source
`731d8e54e435a6411361869a8a361f6731634705`, generated
`910403989ca8326f3bf4fa1f8c22c99e3b791634`. Production scope is
`LegacyNativeMsgTrade.Contracts.cs:TradeInfo.ToArray`. The native receiver remains
held and unregistered. No trade admission, SQL, transport, profile or handler
activation is included.

The initial premature pool-release premise is not established. Current
`LegacyNativePacketRecycle.Reuse` only seeks to 0; it neither enqueues nor frees.
The caller's strong packet reference retains its unmanaged storage. Disposing
the existing using wrapper explains the returned cursor 0.

The actual defect is unchecked `ProtoBufferSerialize` memcpy into the rented
16,384-byte allocation, with an unbounded public Name. The body and optional seal
can exceed that allocation before return. Retain this static counterexample;
never execute an oversized old/native write as a red baseline.

## Packet contract

Retain source-derived opcode 2396 and twenty required fields in ascending tag
order. This is source/golden evidence, not a new client capture or receiver binding.

| Field | Wire contract | Existing field or evidence |
| --- | --- | --- |
| 1 | bool varint | un1 remains opaque |
| 2, 3 | UInt32 varints | un2 / Sender |
| 4 | length-delimited UTF8 | required Name; empty retained |
| 5, 6, 7 | UInt32 varints | Receiver / Level / BattlePower |
| 8, 9 | UInt32 varints | un8 / un9 remain opaque |
| 10 through 17 | bool varints | Spouse / Friend / TradePartner / Mentor / Apprentice / Teammate / GuildMember / Enemy |
| 18, 19 | UInt32 varints | un18 / un19 remain opaque |
| 20 | signed Int64 varint | un20 remains opaque; negatives use ten bytes |

Keep protobuf-net's existing runtime-object serializer/model, ProtoMember
attributes, required defaults and order. The header contains little-endian
UInt16 declared length and UInt16 opcode. Declared length includes the four-byte
header and body and excludes the seal. Native Size includes the seal. Preserve
returned cursor 0, matching current wrapper disposal.

Capture the current seal once. Null means no seal; the legitimate setter requires
eight characters. Native `Write(string,8)` copies the first eight UTF8 bytes,
including for non-ASCII seals. Validate the captured nonnull string before native
allocation; never reread mutable global seal size/content during preparation.

Retain the independent 50-byte no-seal golden with Name=A from the existing
TeleportLifecycle packet test. Expected bytes must not come from the writer
under test.

## Catalog/table contract

Not applicable. No currency, catalog, item ownership or trade permission rule
changes. Actual client Name admission and capacity remain unknown.

## Validation and transaction rules

- Capture the required Name and twenty fields in a private snapshot. Reject null
  Name before serialization; retain empty Name. Add no gameplay character limit.
- First bound UTF8 Name bytes by the real allocation minus header and captured
  seal. This necessary envelope check bounds managed serialization allocation.
  Then validate the complete serialized body, including scalar/tag/default and
  variable string-prefix bytes, before native allocation or copy.
- Serialize once to managed bytes using the existing protobuf-net model. Prepare
  the complete header/body/seal in managed memory. Reject frames exceeding the
  allocation or UInt16 envelope; never truncate or substitute defaults.
- Allocate a real caller-owned LegacyNativePacket only after preparation. Copy
  within its capacity, publish exact Size and cursor 0, and dispose the allocation
  if copy/publication preparation throws. The caller disposes a successful result
  after its last use. No recycled wrapper escapes.
- The snapshot prevents later DTO mutation from changing serialized length or
  copied bytes. Concurrent updates to several fields are not made atomic.
- Native GameClient.Send separately diagnoses frames above 1,032 bytes. A valid
  storage envelope does not establish delivery. No installed production native
  TradeInfo construction was found; the separate LegacyTradeInfoPacket lifecycle
  and all send policy remain unchanged.

## Expected behavior and fixtures

Corrected-source-only controls use real packet allocations and real Take/Reuse
behavior, with no injected pool, transport, queue or owner. Retain independent
50-byte and UTF8/signed goldens, required defaults, empty Name, captured ASCII
and non-ASCII seals, exact Size/cursor and independent returned storage. Mutating
the DTO or another packet, interleaving a real rental, and disposing one result
must leave another result intact.

Exercise the exact 16,384-byte envelope with sealed/unsealed ASCII and multibyte
Names. Reject the next byte, excessive scalar-plus-Name totals and null required
Name while preserving prior results and source DTOs. Run large controls only on
the corrected source, never the unsafe baseline.

## Evidence alignment and qualification

| Requirement | Evidence | Qualification |
| --- | --- | --- |
| Premature pool release | Reuse only seeks to 0; Dispose/finalizer owns FreeHGlobal | Not an established defect |
| Oversize copy | ProtoBufferSerialize memcpy lacks capacity checks | Confirmed static defect; unsafe old write not run |
| Opcode / required fields | Contracts and retained independent 50-byte golden | Source-derived wire contract |
| Seal and cursor | Finalize, WriteSeal and wrapper Dispose | Observable behavior preserved in candidate |
| Actual client consumption | Native receiver held; no installed construction found | Unproved; no registration |

Only ToArray changes in production. Add focused corrected-only controls and a
separate FSB-205 entry. Shared full Windows run 40958745483 owns the host lane;
the helper does not build, test, restore or run SQL/native/client/process actions.
Root reviews, integrates, compiles and qualifies afterward. No executed C# result,
full gate, transport/live-client proof or parity increase is claimed here.
