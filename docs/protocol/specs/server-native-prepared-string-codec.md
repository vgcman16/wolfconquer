# FSB-209: prepared native packet codecs and whole StringPacket failure safety

## Identity and scope

- Revision: 2026-10-29; Root-authorized offline implementation.
- Target: existing native source-shaped codecs, with historical old7899 /
  VisibleDx8D13 live evidence elsewhere. No7937 client acceptance is implied.
- Direction: StringPacket2036 construction and parsing on caller-owned native
  packets; no dispatcher, authorization, transport or persistence change.
- Dependencies: reviewed FSB206–208 packet/protobuf physical-bound fixes in
  Root's40-path candidate based on public91040398. Managed qualification pending.

## Packet contract

Existing successful StringPacket byte contract remains unchanged:

| Field | Offset/width | Rule/evidence |
| --- | --- | --- |
| Declared length | 0/UInt16LE | Excludes optional eight-byte seal; existing goldens |
| Opcode | 2/UInt16LE | 2036, existing source and independent fixtures |
| UID or X/Y | 4/UInt32LE or two UInt16LE | LocationEffect writes X/Y; reader retains existing combined-UID behavior |
| StringID | 8/byte | Preserve unknown byte values; no invented enum validation |
| Strings | 9/byte count plus UInt16LE UTF8 lengths | Byte count and each length must fit; null entry is empty |
| Null Strings | 9/three zero bytes | Distinct from empty array's one count byte |
| Optional seal | Declared length/8 bytes | Existing first-eight-UTF8-byte semantics preserved |

CString conversion preserves existing NUL truncation while consuming the complete
declared entry. No new header validation is imposed by this body parser; its caller
owns frame selection and genuine input Size. Mutable Size is range-checked input
metadata, not proof of receive provenance or authorization.

## Catalog/table contract

Not applicable: this codec does not select catalog rows or infer gameplay rules.

## Validation and transaction rules

- Required stream/message/parser/body delegates reject null explicitly.
- Shared `ReadPrepared<T>` checks actual allocation liveness, full-width cursor
  bounds and supplied Size before invoking a controlled internal parser. It keeps
  the original raw cursor and restores it on ordinary parse/allocation/encoding
  failure while the same allocation remains live. Return/output publication occurs
  only after the complete parse and unchanged-allocation/Size validation.
- Shared `PrepareFrame` snapshots target allocation/cursor/Size and seal; writes
  a complete actual scratch packet with the target's actual Capacity using
  InitWriter/body/Finalize; snapshots its complete frame into managed bytes;
  validates captured target/seal state and the full prepared frame, then copies
  header/body/seal and publishes Size/cursor4. No allocation or encoding follows
  the first target write.
- StringPacket snapshots mutable UID/ID/X/Y and a cloned Strings array before
  scratch preparation. Rejection must preserve all target bytes, cursor, Size,
  original message fields and the caller's pre-existing reader output reference.
- No clamping, partial lists, silently fabricated owners or accepting callbacks.
  Internal delegates compose known codecs; they provide no permission authority.
- Caller-exclusive packet lifetime remains required. Public pointer/Size writes,
  malicious delegate behavior, concurrent disposal/mutation, process failure and
  foreign-pointer extents are not made atomic or safe by this composition helper.

## Expected wire behavior and fixtures

- Retain exact independent2036 valid unsealed/sealed, UTF8/NUL/null-entry,
  null-versus-empty, LocationEffect and unknown-StringID fixtures.
- Correct earlier null-reader publication and partial-field/cursor refusal tests
  to require unchanged existing output references and complete cursor rollback.
- Oversized count/entry, small actual Capacity, null/disposed/invalid-state and
  truncated later-entry fixtures must reject before target/output publication.
- Exercise shared prepare callbacks with real owned scratch/native packets and
  deliberate ordinary exceptions after actual reads/writes, without OS sends.
- Do not execute unsafe older native code as a failure baseline.

## Evidence alignment

| Requirement | Source/fixture | Status |
| --- | --- | --- |
| 2036 layout and successful cursor/seal behavior | LegacyNativeStringPacket + independent LegacyNativeStringPacketTests goldens | Retained source/fixture evidence |
| Physical read/write bounds | Root FSB207/208 candidate | Source dependency; managed gate pending |
| Whole-codec rejection safety | Existing partial-builder/null-reader/partial-model tests | Defect identified; corrected-only controls required |
| New client, dispatch and live packet sequence | No new capture | Unproven; not activated here |

## Implementation and qualification

- `Networking/LegacyNativePacket.PreparedCodecs.cs`: shared controlled codec
  composition helpers.
- `World/LegacyNativeStringPacket.cs`: prepared reader and builder.
- Existing StringPacket tests plus focused corrected-only prepared-codec tests.
- Helper performs no restore/build/test/live execution. Root reviews, integrates
  and records actual managed outcomes. No fixed/live claim before those results.
- FSB209 fragment records the source defect and remaining boundary separately.
