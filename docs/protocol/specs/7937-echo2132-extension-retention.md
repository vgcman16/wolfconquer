# 7937 Echo2132 extension retention

## Identity and scope

Revision 1, 2026-11-02; Root-approved offline decoder prerequisite. Baseline is
the complete candidate at fe3e79a29ff6f377981465de0d78785d7105bf92. Explicitly
declared Chinese7937 only; this does not select/register a protocol profile.
Retained same-engine text supports this decoder. It is not a matching7937 wire
capture or proof of live gameplay, persistence or client acceptance.

## Packet contract

Opcode2132, existing four-byte little-endian length/opcode framing. The existing
closed known codec remains authoritative for field widths and presence:

| Scope | Field | Wire | Presence/behavior | Evidence |
| --- | --- | --- | --- | --- |
| Parent | 1 | uint32 varint | Required raw operation; last duplicate wins | Existing inspected codec |
| Parent | 2..7,10 | uint32 varint | Optional; last duplicate wins | Existing inspected codec |
| Parent | 8 | bytes | Optional; last duplicate wins, absent differs from empty | Existing inspected codec |
| Parent | 9 | message | Ordered repeated child, maximum1024 | Existing inspected codec |
| Child9 | 1,2 | uint32 varint | Optional; last duplicate wins, empty child valid | Existing inspected codec |
| Parent/Child9 | >10/>2 respectively | Opaque complete record | Retain original order, duplicates and exact encoded bytes | Retained unknown-buffer writers |

Retained text only: `.artifacts/helper-chinese-7935-patch-next/7931-xuanbao-child-and-output-008.txt`
parent writer011D15CE..011D15E0 and
`7931-xuanbao-string-child-writers-016.txt` child writer00E0EB2E..00E0EB41
append the unknown buffer through01270F30. Both bind engine SHA256
a7e1aac88a999b1f4723e6a1f197b96924e24022fd681fb2c9cf79c1bb413422,
length30085432, carried by the selected7937 reference. Native writers append
unknown buffers after known fields; exact original replay is an envelope
guarantee, not a claim that native reserialization preserves original ordering.

Structurally delimit varint/fixed64/bytes/balanced-group/fixed32 records. Unknown
length-delimited contents and group contents stay opaque. Parse only actual
parent field9 as the established child schema. Known fields with wrong wire
types, uint32 overflow, malformed/truncated records, tag/varint overflow or
missing required operation refuse with no envelope. Valid nonminimal varints
remain accepted and byte-exact. A tenth varint byte may contain only bit0.

Finite application guards: payload1..65531bytes, total opaque records1024 across
parent and all child9 scopes, group depth16, existing child count1024. These are
decoder safety limits, not native/gameplay capacity claims. An opaque group is
one record; its nested tags cannot satisfy required known fields.

## Catalog/table contract

Not applicable. No catalog row or unresolved Addition/Resolve/2350 meaning is
needed to delimit2132. This does not interpret operation8/10 or other actions.

## Validation and transaction rules

Capture one owned source payload before parsing. Validate every scope and then
use the unchanged strict codec on a filtered known-schema projection. Preserve
all known duplicate last-value and child append behavior. The public envelope
owns exact source bytes and opaque records; returned byte arrays are copies.
The explicitly named `KnownProjection` is not extension-preserving serialization.
Only envelope `ToPayloadBytes`/`ToFrameBytes` provide exact replay.

Frame entry requires explicit7937, actual opcode2132 and agreement of actual
complete header/frame and separate payload, including segmented decoder input.
No accepting runtime owner, resources, persistence, output queue or handler is
added. Existing generic2132 and response8/10 readers remain strict: extensions
refuse there, so existing consumers cannot silently accept and drop them.

## Examples and controls

Independent synthetic ordered byte goldens cover interleaved parent and child
extensions, repeated unknown numbers, known scalar duplicates, child append
order, absent/present-zero and exact replay independent of canonical known
serialization. Controls cover every wire form, nested opacity, full malformed
tails, group/extension/child/payload edges, wrong version, coherent and forged
frames, defensive ownership and the unchanged2350 scanner API/goldens.

## Unresolved behavior and next seam

Unknown values, actions and state meanings remain unknown. There is no matching
7937 request/reply plus authenticated pre/post Echo state, persistence owner or
activation evidence. This explicit envelope is not bound to existing runtime
consumers. A future consumer must carry this envelope through its complete wire
path and own any effects; serializing only KnownProjection would lose data.

## Verification and source-bug status

Source-only proposal, unbuilt/unrun. Root alone composes and qualifies. Existing
strict refusal was explicit supported behavior, not a newly fixed server bug.
Shared structural scanner extraction must preserve2350 behavior; existing
Chinese7937ItemInfoWireScannerTests and envelope suites are required neighbors.
