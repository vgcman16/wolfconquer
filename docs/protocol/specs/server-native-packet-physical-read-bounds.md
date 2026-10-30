# FSB-208: Native packet physical read and EOF bounds

## Identity and scope
- Revision2, 2026-10-29; Root source-only component milestone.
- Base is committed public `910403989ca8326f3bf4fa1f8c22c99e3b791634` plus exactly the 24 reviewed paths captured by `root-native-trade-packet-next/candidate.json`. Source pin `731d8e54e435a6411361869a8a361f6731634705`; those dependency paths/hashes are preserved separately.
- Target remains the existing native component and pinned wire evidence (7899/DX8D13); no new 7937 client, frame authority, receiver or transport admission is inferred.
- Production scope: only the read region in Networking/LegacyNativePacket.IO.cs and one focused private read-helper partial. Preserve the composed FSB-204..207 trade, write, Finalize and protobuf code byte-for-byte.

## Packet contract
| API | Representation | Complete physical/input bound |
| --- | --- | --- |
| Unsigned/signed scalar readers | Native little-endian 1/2/4/8-byte bit patterns | Full width fits actual caller-supplied Size and immutable Capacity |
| ReadBytes(count) | Exact bytes | Nonnegative count; full count fits before allocation/native copy |
| ReadCString(count) | Decode all count bytes with existing UTF8 replacement behavior, then truncate at first decoded NUL | Nonnegative count; full field fits; even a NUL prefix does not authorize a truncated tail |
| ReadStringList() | Byte count; UInt16 little-endian byte lengths; UTF8/CString behavior for each entry | Count byte, every length header and complete entry must fit before any cursor/output publication |
| ReadUnsafe(destination,count) | Exact bytes into caller-owned foreign destination | Validate packet source first; positive count requires nonnull destination; foreign destination extent/lifetime remains caller-owned |

Exact input/allocation end is valid for a complete field; a zero-byte read is valid at that end. Missing bytes raise EndOfStreamException, rather than returning fabricated zero/empty data or partially advancing a composite cursor. Invalid physical cursor/Size raises InvalidOperationException; disposed storage raises ObjectDisposedException; negative counts and null positive-count foreign destination reject explicitly.

## Catalog/table contract
Not applicable: no catalog/schema changes.

## Validation and transaction rules
- Validate liveness, full-width cursor addresses, nonnegative complete requested counts, Size in0..Capacity and cursor/count within Size before native access. Spare allocation and MAX_SIZE never supply an input window.
- Size is mutable caller-provided metadata. A genuine constructor(byte[]) copy, finalized writer or explicitly captured initialized byte extent must establish it. Range checking is not independent capture provenance, authentication or receive authority. Do not derive a fallback from allocation length, cursor, untrusted header or requested count.
- Scalar/byte/string results prepare before cursor publication. String-list metadata and all fields validate before decoding/publishing any string array or consuming cursor; parse/copy/managed allocation failures preserve original packet cursor, Size and bytes.
- ReadUnsafe prepares an owned copy of the validated source before writing the foreign destination; valid overlaps use that snapshot. Its destination allocation cannot be established by this API. It neither invents an accepting owner nor proves arbitrary pointer writes safe; no invalid foreign-pointer execution control is authorized.
- No shared lifetime gate exists with public pointer/Size writes, Seek, Dispose or noncooperating native accesses. Rechecks are not a whole-Packet concurrency guarantee.
- Production map: native Read* calls reside in caller-supplied packet Get* codecs. The sole ReadUnsafe consumer is real TeamLeadership.GetTeamLeadership and its fixed sizeof(struct) destination. BinaryReader/file/managed PacketReader names are separate APIs. No internal production writer-to-native-reader path requiring new Size publication was found in this map; any new contrary evidence must be reported before caller broadening.
- Test-only writer/readback fixtures must capture their actual completed written extent, finalize normally, use a real copied initialized buffer, or inspect an already captured wire prefix directly. Never change writer Size semantics globally to satisfy a fixture.
- Primitive and complete string-list refusal safety does not make sequential caller codecs transactional. Achievement, NpcDialog and GetStringPacket still assign earlier fields before a later primitive/list failure; the adapted tests retain these prior assignments and mark whole-codec admission/rollback OPEN. Failed Clan and StringPacket builders also have separate partial-output defects; tests snapshot whatever incomplete state they leave without requiring particular dirty prefix bytes, counts or cursor positions.

## Expected wire behavior and fixtures
- Retain independent initialized byte goldens for all scalar widths/signed bits, exact bytes, UTF8 replacement/CString NUL behavior and string lists with null/empty source entries represented as zero lengths.
- Cover custom/default allocation ends, Size ending before spare allocation, actual EOF, zero/negative/oversize counts, invalid full-width cursor/Size and disposed state. Physically short controls run only against the corrected implementation.
- Composite malformed controls include absent count, truncated UInt16 length, a later overlong row after an earlier valid row, truncated NUL-containing field and declared-length tails; cursor/Size/native bytes and owned foreign destination remain unchanged on ordinary refusal.
- Never execute old unsafe OOB/header cases for a red baseline. New tests remain uncompiled/unexecuted pending Root's combined gate.

## Evidence alignment
| Requirement | Evidence | Status |
| --- | --- | --- |
| Existing little-endian/UTF8 successful bytes | Composed PacketStorage goldens and original IO implementation | Preserve |
| Actual physical/input bound | Readers compare to strict MAX_SIZE and ignore Capacity/Size | Confirmed source defect |
| Composite failure preservation | ReadBytes/List publish intermediate cursor changes | Confirmed source defect |
| Valid initialized Size | Packet(byte[]) and Finalize publish actual lengths; some tests bypass both | Capture/fixture correction required; metadata alone is not authority |
| Foreign destination and concurrent lifetime | No destination owner or common lifetime gate in native API | Explicitly unproved |

## Implementation and qualification
- Root alone integrates and runs the coherent native packet/trade batch. The preceding full Windows40958745483 gate is terminal/SUCCESS; that gate covers its committed source, not this unbuilt successor.
- Primary source-authored controls: CorrectedOnlyNativePacketPhysicalReadTests and exact post207 LegacyNativePacketStorageTests adaptations. Additional finite test-only readback adaptations follow the actual caller map and Root approval.
- No build, restore, test, dotnet, SQL, network/client/process or actual checkout/Git mutations in this helper scope. FSB-208 is a candidate entry, not a qualified fixed/live claim.
- Approved test-only adaptations are the 13 finite Storage, Chi, Clan, Achievement, ItemUsage, StringPacket, NpcReplay Fixture/Reader, ArenaSignup, Dialog Contracts/NpcReader and Gear Fixture/View paths retained in the package manifest. Existing outcomes and independent valid goldens are retained; represented string-list counts255 succeed, while256/257 now refuse the FSB-207 byte-count overflow instead of requiring wraparound.
- See FIXED-SOURCE-BUGS.md FSB-208 and owned HANDOFF.md for the finite paths and verification frontier.
