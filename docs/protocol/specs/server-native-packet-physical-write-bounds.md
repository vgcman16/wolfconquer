# Native packet physical writes and frame publication

## Identity and scope

Revision 1, 2026-10-29; Root-assigned FSB-207. Base: public
`731d8e54e435a6411361869a8a361f6731634705`, generated
`910403989ca8326f3bf4fa1f8c22c99e3b791634`. This is native packet storage safety,
not evidence for a newly selected client/profile. Production scope is the IO
writers, Finalize/WriteSeal, and one focused private write-helper partial. Reads,
general memcpy, protobuf codecs, TradeInfo, receivers and send policy are outside
this change. No SQL, client, process, route or runtime activation is included.

## Packet contract

Retain all valid native little-endian scalar bytes, signed bit patterns, UTF8
encoding and existing field ordering. Physical allocation Capacity is immutable;
mutable Size and the default MAX_SIZE constant do not authorize memory access.

| API | Existing representation | Supported bound |
| --- | --- | --- |
| bool / signed / unsigned scalar writes | 1 / 2 / 4 / 8 native little-endian bytes | Entire width fits remaining real allocation |
| Write(string, length) | First min(UTF16 character count, length) bytes of the whole UTF8 representation, then zeros | Nonnull string; nonnegative width; complete field fits |
| Write(string[]) | Byte count; UInt16 UTF8 byte length and content per entry; null/empty entry has zero length | At most 255 entries and 65,535 bytes per entry; complete list fits |
| WriteUnsafe(pointer, length) | Caller-owned source bytes | Nonnegative length; nonnull source for positive length; destination span fits |
| ZeroFill | Zero bytes | Nonnegative count; complete span fits |
| CreateProtocolBuffer | Historical byte 8, uint varints and one cast byte for each trailing next-field token | Preserve existing opaque byte construction; complete prepared output fits |
| Finalize(type) | UInt16 length, UInt16 type; body; optional seal | Cursor at least 4, declared cursor at most 65,535, complete frame fits |
| WriteSeal | First eight UTF8 bytes of captured eight-character seal | A seal is present; complete eight-byte span fits |

The fixed-string rule can split UTF8 code points. Preserve it, including a UTF16
surrogate pair crossing the bounded prefix; do not introduce client Name policy.
CreateProtocolBuffer is not promoted to a general protobuf encoder or interpreted
as a new schema. Preserve its unusual trailing next-field token and existing
unchecked low-byte token construction.

Finalize captures the optional seal once. Null means no seal. A present seal has
eight characters; encoding/truncation follows native Write(string,8). The UInt16
declared length is the body-end cursor, including the four-byte header and
excluding the seal. Size includes the seal. Successful Finalize leaves cursor 4.
WriteSeal appends a captured seal without changing Size; explicit absent seal
requests reject clearly. No other caller of WriteSeal was found.

## Catalog/table contract

Not applicable. This storage component derives no gameplay, catalog, Name,
permission or ownership policy. Actual client admission limits remain unknown.

## Validation and transaction rules

- Validate nonnegative counts, actual live allocation and full-width cursor
  addresses before any destination unmanaged access. Check addresses without
  narrowing through the legacy int Position. Reject negative, beyond-end, null
  or wrapped cursors; exact allocation end accepts a zero-byte operation only.
- Validate complete width against immutable Capacity with subtraction-based
  checks. Exact physical end is valid. Invalid writes throw before changing
  bytes, cursor or Size instead of silently dropping all or part of a value.
- Prepare composite string/list/protocol/source-copy bytes in managed owned
  storage before committing them. Snapshot mutable input arrays after necessary
  finite allocation bounds. Representation/count/encoding/allocation failures
  leave the destination unchanged.
- Bound fixed-string encoding work to the prefix needed to retain the original
  bytes; include the next UTF16 character when needed for surrogate pairing.
- WriteUnsafe first validates destination bounds, then snapshots source bytes.
  Source extent/lifetime remains the caller's unsafe responsibility. This change
  does not prove arbitrary foreign pointers valid or coordinate concurrent
  foreign disposal/cursor/buffer mutation. Snapshotting supports valid overlapping
  source/destination spans; it does not authorize invalid source reads.
- Finalize validates liveness, full header/body/optional seal, declared UInt16
  length and physical capacity, and prepares header/seal before writing either.
  On ordinary preparation/refusal, bytes, Size and cursor remain unchanged.
  Publish exact Size and cursor 4 after the validated copies. Existing payload
  bytes remain untouched; initialization of caller-created gaps is not proved.

## Expected wire behavior and fixtures

Use real allocations, real public cursor state and owned synthetic inputs.
Retain independent primitive, fixed-string, list, opaque protocol-helper and
sealed/unsealed header goldens. Cover exact physical ends for default and custom
allocations, allocations above MAX_SIZE, positive/zero/negative counts, null
inputs, representation overflows, disposed state and full-width invalid cursors.
Corrected-only controls must preserve all bytes, cursor and Size on refusal.
Never execute old out-of-bounds writes as a red baseline.

Adapt only the two storage tests that explicitly require exact-end write loss.
Keep their independent read behavior separate: readers retain their existing
strict MAX_SIZE checks and are not made safe by this write correction. Do not
edit generic protobuf tests or Teleport packet tests.

## Evidence alignment

| Finding | Evidence | Qualification |
| --- | --- | --- |
| Last physically fitting byte silently lost | IO scalar/unsafe/zero-fill guards use strict less-than MAX_SIZE | Confirmed static defect; corrected exact-end controls required |
| Custom allocation can be exceeded | IO writers use default constant, not actual Capacity | Confirmed static defect; unsafe old writes not executed |
| Partial composite output | String/list/protocol helper writes several independently checked chunks | Confirmed static refusal gap; prepare complete value first |
| Incoherent finalization | Finalize mutates seal/body/header/Size with repeated global seal reads and unchecked UInt16 cast | Confirmed static bounds/publication gap |
| Fixed UTF8 truncation and trailing token | Retained storage goldens and IO source | Preserve valid byte compatibility |
| Live client / broad native IO safety | No execution in this scope; reads/memcpy/foreign lifetimes separate | Unproved |

## Implementation and qualification

Implement focused helpers under Networking and corrected-only synthetic tests.
No new accepting owner, fake capacity, transport or receiver registration is
allowed. Root reviews and composes with FSB-205/206, then qualifies the combined
source after the shared lane becomes available. Full Windows run 40958745483 owns
that lane; this helper performs no build, test, restore or native execution.
Static checks and authored controls are not executed C# tests, deployment,
live-client acceptance or a parity increase. Record FSB-207 separately until Root
qualification. No broader caller change is currently required by inspection.
