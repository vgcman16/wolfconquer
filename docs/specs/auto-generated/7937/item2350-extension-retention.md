# Chinese7937 item2350 extension-preserving envelope

## Identity and scope

Revision1,2026-11-02. Third changed-only island on exact2a237. First11-path Echo
and second18-path relic-consumer proposals remain immutable. Target selected7937
reference retains7931 DX8 engine A7E1AAC88A999B1F4723E6A1F197B96924E24022FD681FB2C9CF79C1BB413422.
No builds/tests/Git/native/client execution; Root alone qualifies and integrates.

Close the documented native unknown-field retention gap without guessing any
additional item field semantics. Preserve all existing43known-field validation,
required presence and profile separation. This is a packet decoder/serializer
prerequisite; it does not register a receiving handler or create a player owner.

## Packet contract

Opcode2350, little-endian UInt16 whole-frame length/opcode header, known payload
schema Chinese7935ItemInfoView. Required1/2/3 retain presence including zero;
field2/3 meanings stay opaque. UInt32/Int32/UInt64/raw27/28/repeated31 contracts
and explicit UID/category7/signedposition40 projection remain unchanged.

Existing reader remains intentionally known-schema-only. New explicitly declared
client7937 envelope scans complete bounded wire records, extracts top-level1..43
records for the existing strict reader and retains every unknown fieldnumber>43
as an opaque complete encoded record. Unknowns are never treated as item state,
strings, nested known messages, permissions or executable instructions.

Unknown protobuf wire types0varint,1fixed64,2length-delimited,3/4balanced group,
5fixed32 are structurally delimited;6/7,field0,overflow/truncation and top-level
end-group refuse. A group must close with the same number. Nested content remains
opaque even when its field numbers equal required1..3. No required fields can be
satisfied inside an extension. Lengths are bounded by remaining payload before
slicing/allocation. Tag≤UInt32 gives the protobuf fieldnumber≤536870911 bound;
varints retain the existing10-byte/UInt64 overflow checks.

Finite guards:65531payloadbytes,1024top-level extensions,16nested group levels.
These are sanitized codec allocation/stack guards, not observed gameplay limits.
Known wrong-wire records still refuse through the existing reader; they are not
laundered into extensions. Unknown ordering, duplicates, byte spellings and all
known record spellings are retained by exact original-payload serialization.

Frame entry checks actual bytes/header/payload coherence, explicit7937 declaration
and exact2350 before decoding. It accepts no forged disagreement between separate
PacketFrame fields. Serializer emits a fresh exact4-byte header plus owned payload.

## Catalog/table contract

No new item columns are inferred. Current7937 main/Sub67-field data remains
separate; field2350/2 is NOT equated to catalog item ID. This milestone follows
additional595score-limit/20flag decoding in the preceding island and addresses
the independently documented wire-extension gap rather than relabeling columns.

## Validation and transaction rules

Capture caller bytes once, scan/validate every record, strict-parse required known
schema and only then return one immutable envelope. Failure returns null; no partial
unknown fields or known view escape. Copy payload/extension bytes at public output
boundaries. Retain an immutable known view; no patch/mutate-forward API exists.
No callbacks/queues/owner fabrication, resources, persistence or gameplay effects.

## Expected wire behavior and fixtures

Independent synthetic literals cover interleaved extensions of each supported
wire form, duplicate unknowns, nonminimal but valid spellings, group matching,
nested required-looking tags staying opaque, and full original-payload/frame
identity. Keep existing known-only2350 controls/goldens. Refusals cover malformed
tags/varints/lengths, truncated fixed fields, mismatched/unclosed/excessive groups,
missing required known fields, wrong known types, extension/payload bounds,
wrong version/opcode and coherent-header/payload disagreement. Test authorship only.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
|43known schema/required mask7 | Retained item-parser dispatch045/required046; existing C# codec/goldens | Established bounded contract |
| Native extension retention | Retained state README; writer043 at01216D27..39 writes retained bytes | Observed source/byte storage, not live capture |
| Opaque balanced wire records | Protobuf wire grammar plus existing LegacyProtoWire widths | Derived structural support; no extension meanings |
| Current7937 profile/gameplay | No current session/owner evidence | Unresolved; no activation |

## Implementation and qualification

Helper owns internal finite wire scanner/ranges and isolated scanner controls.
Coordinator owns new immutable Chinese7937ItemInfoEnvelope/frame API and public
integration controls. No existing source preimage is modified. Root component
qualification2026-11-02 passed the actual build and combined
run20261102T153347176Z-a0ec3a6d:3507/3507,0failed/skipped. It includes31 public
envelope/frame and37 scanner controls (68 new rows), plus existing strict known
item/schema/packet and wider Chinese/relic/magic/Flag6 neighbors. Build161.643s,
tests202.895s. Full integration gate remains required; this is synthetic codec
proof, not native/live acceptance. Execution hold remains. Next actual evidence
frontier is a current item2350 producer/consumer action mapping or2132 owned state;
unknown retention supplies no permission to guess those contracts.
