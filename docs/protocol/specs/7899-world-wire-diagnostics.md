# 7899-world-wire-diagnostics: opt-in structural packet observations

## Identity and scope

- Revision 1, 2026-10-27; user-requested diagnostic tooling, authored offline.
- Current world profiles remain `VisibleDx8D13` and `RootD11_7899`. This tool adds
  no profile, packet handler, catalog, gameplay response or acceptance decision.
- Input is a managed decoder-produced inbound world `PacketFrame`. Unknown
  opcodes may be observed before world admission, with metadata only.
- The sole structural contract is the existing D13 daily-task codec, whose source
  records target PE SHA256
  `D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5`.
  No new binary inspection or traffic observation was performed for this slice.

## Packet contract

The existing four-byte little-endian header remains unchanged. Output is private
UTF-8 NDJSON, not a protocol response. Each version-1 record contains `timeUtc`,
`profile`, `opcode`, `headerLength`, `payloadLength`, `unknownOpcode`,
`frameStatus`, `schema`, `scanStatus`, `fields`, `rawPayloadHex` and
`rawPayloadTruncated`. Default structural metadata contains no names,
account/character IDs, endpoints or decoded scalar values. Raw bytes require the
separate bounded opt-in below.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| D13 daily-task | opcode 2419 | Protobuf | Structural schema match only | Explicit scan opt-in | Exact D13 profile | `D13RandomDailyTaskPacket.cs` |
| Known outer fields | 1–7 | wire 0 | Known number/wire pair | Original scalar presence remains codec-owned | Never emit values | `D13RandomDailyTaskPacket.Parsing.cs` |
| Known outer repeated field | 8 | wire 0 or 2 | Known number/wire pair | Repeated, packed or unpacked | Never recurse into LEN | Same parser |
| Observed field descriptor | flat outer stream | number, wire, offsets, lengths | No semantic type/name inference | Ordered, duplicates retained | 64 fields / 1024 scan bytes by default | Official Protobuf encoding guide |
| Raw payload prefix | payload offset 0 | hex bytes | Separate operator opt-in | Only D13 2419 is allowlisted | 64 bytes by default | Explicit non-sensitive numeric daily-task contract |

`encodedLength` counts the available whole field, from `tagOffset`, including its
tag and any LEN prefix. `valueOffset` follows the tag. `dataLength` is the LEN
body's declared byte length; it is null for other wire types. A truncated value
retains its partial descriptor. An incomplete/invalid tag does not invent one.

Unknown packets without a matching explicitly enabled contract remain opaque.
Statuses distinguish `not_requested`, `opaque_profile_or_opcode`, `complete`,
`scan_limit`, `field_limit`, `truncated`, `malformed` and `unsupported_group`.
Groups are not recursively parsed. Wire types 6/7 and impossible tags are malformed.
The scanner does not validate required-field presence, signed interpretation,
UInt32 semantic ranges, packed element contents or gameplay eligibility.

The separate Chinese7935 2132 codec has fields 1–10; the older D13 contract does
not gain those fields because an opcode matches. Neither 2132 nor 2350 is enabled
by this diagnostic implementation. There is no Chinese7935 runtime profile here.

## Catalog/table contract

Not applicable: no table is loaded or changed. Existing closed codec source is
the explicit schema reference; no guessed descriptor/catalog is generated.

## Validation and transaction rules

- Default off. Enabled creation requires an absolute existing non-reparse parent.
  A new GUID child gets a protected current-user-only Windows ACL, or Unix 0700;
  its exclusive `packets.ndjson` file uses Unix 0600 where applicable.
- Scan and raw opcode lists are separate, bounded defensive copies. Both currently
  accept only 2419. The exact D13 profile is checked again at observation time.
- Raw data additionally requires a coherent frame and an existing positive current
  `WorldSessionStore` session for the actual connection; its reference is rechecked
  after copying the prefix. No accepting authorization callback or boolean exists.
- Auth/login/handoff, PIN, character naming, chat/broadcast/string and NPC reply
  opcodes are hard-excluded. NPC reply includes account/PIN/email text consumers.
- The two existing default frame formatters now emit opcode and payload length
  only. Enabling this option never enables legacy wire tracing.
- Prepare diagnostics only. No resources, permissions, sessions, inventories,
  rank state or response queues are committed by the observer.
- Record/file/rate/signature/scan/field/raw caps bound retained diagnostic work.
  Metadata-only dedupe uses structural shape, excluding timestamp and payload
  values/bytes. Explicit authorized raw records also distinguish their captured
  prefix internally: different captured bytes survive at the same shape/length,
  while exact observed prefix/shape repeats dedupe. Raw capture/dedupe makes no
  additional reads beyond the prefix. A value change solely beyond that cap
  dedupes when structural evidence is also identical; a separately opted scan can
  still distinguish changed tags/spans within its independent scan limit. Only fixed-size
  internal digests are retained; none are emitted. A full lifetime signature set
  stops further records rather than evicting.
- Creation failure leaves diagnostics unavailable. Observation/write/disposal
  failures disable/close the diagnostic sink without changing gameplay outcome.
  A failed disk write can leave a partial final NDJSON line; this is not a journal.

## Expected wire behavior and fixtures

No added network frames. Existing successful protocol bytes stay unchanged.
Independent structural examples are `08 00` (field 1/wire 0) and
`4A 03 AA BB CC` (unknown field 9/wire 2, opaque three-byte body).
`08 80` is truncated; `00` is malformed; groups stop with an explicit status.

The paired test package uses owned files and genuine session/lifecycle fixtures.
Its existing-API baseline checks the privacy change through both old formatter
methods. New logger API tests require the actual implementation; an absent API is
not behavioral RED evidence. Root alone performs builds and qualification; helpers author isolated changes.

## Evidence alignment

| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| No tag-based semantics | Flat descriptors | Not applicable | Literal tag streams | Official wire-format rule |
| D13 exact schema | 2419 only | Existing codec source | Independent outer tags/wires | Source-backed; not new capture |
| Keep 7935 separate | No 2132/2350 binding | `CHINESE-7935-PROTOCOL.md` | No automatic parse | Profile mismatch fenced |
| Preserve gameplay | Observer only | Not applicable | No-response/state witnesses | Focused229 pass per checkout; live unproved |

## Implementation and qualification

The implementation is `src/WolfConquer.Core/World/UnknownPacketAndFieldLogger*`,
with last optional typed FrameHandler constructor arguments, two dispatch hook
sites and WorldServer-owned sink lifetime. The paired C# tests are the focused
`UnknownPacketAndFieldLogger*`, `WorldWireFrameDiagnosticTests` and
`WorldServerFrameHandlerTests.WireDiagnostics.*` files, plus retained dispatch
and achievement controls.

Public focused qualification passed 229/229, including all 138 baseline names
and 91 new-API cases. The tests-only baseline was 16 Failed/122 Passed. Private focused qualification also passed 229/229, zero failures/skips.
Exact-source full CI remains pending. A mechanical ACL-test
loop cleanup follows the focused pass with the same assertions. No schema
registry expansion, unknown handler, server restart, traffic capture, packet
injection or client asset change was performed. See
[operator guidance](../../WORLD-WIRE-DIAGNOSTICS.md) for activation and limits.
