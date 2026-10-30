# FSB-226 parser: detached native item-file image

## Identity and scope

2026-10-29, public baseline 84611bb1. This parser implements the physical-file
phase of [the item-load milestone](server-native-item-load-publication.md).
It does not create native items, normalize fields, query metadata, filter IDs,
route records, change UIDs or publish runtime state. The old7899 /
VisibleDx8D13 profile remains unchanged; no client or network execution occurs.

## Packet contract

Not applicable: this is the native login item-file envelope, not a packet.

## Catalog/table contract

On the supported little-endian layout, the complete file is:

1. Nonnegative signed Int32LE root count.
2. For each root, one actual 184-byte `LegacyNativeClientItemRecord`, followed
   by exactly its unsigned `DepositeCount` immediate records of the same layout.
3. Nonnegative signed Int32LE perfection count, followed by exactly that many
   actual 80-byte `LegacyNativeClientItemPerfectionRecord` rows.
4. End of supplied input; no extension or trailing-byte grammar is established.

Both records are existing unmanaged sequential layouts. `MemoryMarshal.Read`
copies their complete storage; no string conversion or field rewriting is part
of parsing. Existing independent layout/offset goldens remain the authority.
Unsupported host byte order is refused rather than silently reinterpreted.

The native `ServerDatabase.SaveClientItems` (1354-1408) writes each root and one
loop over its immediate deposits. `ClientItems.DBItem.GetDBItem` stores each
item's deposit count, but the native writer has no grandchildren loop. The
current owned `LegacyNativeClientItemSave.Membership.CaptureDeposits` explicitly
rejects children with deposits, and `.Image.PrepareImage` writes that same
one-level envelope. Therefore a child declaring nonzero `DepositeCount` is
unsupported and rejected; it is neither recursively decoded nor silently lost.

## Validation and transaction rules

`LegacyNativeItemLoadImage.Parse(ReadOnlySpan<byte>)` returns a detached image
only after the complete envelope succeeds. `Roots` is a read-only list of
immutable root nodes; each `Record` is the real struct returned by value and
`Deposits` is a read-only list of struct values. `Perfections` is likewise a
read-only list of actual struct values. No backing mutable array is exposed.

Counts are checked against actual remaining bytes, including unparsed roots
and the mandatory perfection-count field, before their corresponding array
allocation. Division bounds avoid count multiplication overflow. Every row is
complete before it is copied. Missing counts/rows and physically impossible
claimed counts throw `EndOfStreamException`; negative signed counts, unsupported
nested deposits and trailing bytes throw `InvalidDataException`. No EOF becomes
zero and no prior record is reused. Failure exposes no partial image and never
writes the supplied span. Allocation/resource exhaustion is not promised to be
recoverable; memory use remains bounded by the caller-supplied physical input.
The caller must keep its owned supplied bytes stable during parsing; this API
does not synchronize unrelated concurrent writers. A successful image retains
no span or input-array reference, so later input changes cannot change its rows.

The actual owned save graph requires nonzero item UIDs, global item-UID uniqueness
across roots and children, and writes a legitimate wardrobe/equipment alias once.
The parser validates these same canonical identity rules, without changing a UID.
Zero or duplicate item UIDs throw `InvalidDataException`. The safe writer also
emits perfection rows only from its disjoint sets of serialized roots and
children. Each perfection UID must therefore be nonzero, name a serialized item,
and occur at most once in the perfection postlude. Zero, duplicate or orphan
perfection references throw `InvalidDataException`, including late corrupt rows;
no reference is repaired, dropped or used to overwrite an earlier row. Old
concurrent-writer corruption is refused rather than treated as a migration rule.
All record fields remain unchanged, including the validated UIDs, item 205901
and its children, raw enum/opaque/date bits and perfection text buffers. Domain
ownership, metadata and legacy filter policy belong to the later prepared loader.
An unsupported filtered-container migration must be rejected by that owner,
never silently dropped or reparented by this parser.

## Expected wire behavior and fixtures

No wire output. Focused tests construct owned byte arrays with independent
Int32LE counts and the established 184/80 layouts. They cover mixed roots and
children, raw storage, signed/unsigned count extremes, all truncated prefixes of
a complete mixed image, late count/tail corruption, trailing bytes, unsupported
depth, canonical item UIDs, perfection zero/duplicate/orphan refusal, genuine
root/child perfection references, filtered records, value-copy/read-only behavior
and unchanged input.
Original saves and unsafe old readers are not executed.

## Evidence alignment

| Requirement | Evidence | Limit |
| --- | --- | --- |
| One-level root/deposit envelope | native save loops and current safe writer | no recursive extension established |
| 184-byte root/deposit rows | `LegacyNativeClientItemRecordTests` independent field offsets | no domain conversion in parser |
| 80-byte perfection rows | `LegacyNativeClientItemPerfectionRecordTests` independent fixed buffers | no text decoding in parser |
| Canonical perfection references | safe `.Image` selects from globally unique serialized roots/children | old corrupt images refused without repair |
| Parse before publication | shared item-load milestone | loader transaction is Root-owned |

## Implementation and qualification

Implementation: `World/LegacyNativeItemLoadImage.cs`. Focused authored tests:
`LegacyNativeItemLoadImageTests` partials: 41 outcomes (12 facts and 29 inline
theory cases), authored and unrun. The helper performs source review
only: no build, test, native call, client, process, SQL or Git execution. Root
must review and run the integrated managed gates. This parser alone is not
runtime loading, crash-safe persistence, live-client acceptance or full parity.

Root local qualification for the combined40-path candidate: Release build01
passed with0errors/157existingwarnings and no new changed-path warnings. Actual
focused01 passed423 with7existing skips; native-broad01 passed13278/13278 with
0failures/skips. Selections overlap. Receipts are in root-native-item-load-next.
The old public7bb fullrun failed one timing-sensitive startup test; its actual
failedTRXs/logs remain. Exact-commit successor fullCI, SQL, deployment and live
new-client/fullparity acceptance are pending. This note supersedes authored/unrun
labels in this spec/entry without expanding the verification scope.
