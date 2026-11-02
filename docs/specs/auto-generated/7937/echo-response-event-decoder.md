# Echo operation 8/10 raw response-event decoder

## Identity and scope

Revision 1, 2026-11-02. Source baseline is Root's complete 192-path candidate on
public 5ccca124. This is an explicitly declared 7937, read-only decoder proposal.
Same-engine retained text supports the projection below: the selected official
7937 reference retains the 7931 DX8 engine with SHA256
`a7e1aac88a999b1f4723e6a1f197b96924e24022fd681fb2c9cf79c1bb413422`.
This is derived support from retained inspection text, not a matching 7937 wire
capture or current-client acceptance. No executable or client inspection runs.

The existing known-schema 2132 codec captures the wire. This addition captures
the original bytes and projects the exact raw argument order for two separately
observed UI response branches. It does not implement an event queue, UI owner,
player state, activation, costs, persistence, profile or receiving dispatcher.

## Packet contract

Opcode 2132 uses the existing four-byte little-endian UInt16 length/opcode header
and known `Chinese7935XuanbaoOperationPayload` schema. Required UInt32 field 1 is
the operation. Optional fields 2..7/10 are UInt32; 8 is uninterpreted bytes and 9
is an ordered repeated child with two optional UInt32 fields. No layout changes.

| Operation | Internal UI event | Ordered argument fields | Evidence |
| --- | --- | --- | --- |
| 8 | 0x36 | 7, 5 | retained response8-and-record-operations-001.txt, 011CBF8D..011CBFF2 |
| 10 | 0x37 | 2, 3, 4 | retained operation13-14-shared-001.txt, 011CC0F8..011CC187 |

The retained exact dispatch table is
`helper-chinese-7935-consumer-next/operation-dispatch-table-013.json`, table VA
011CC1B6, with rows 8 -> 011CBF6F and 10 -> 011CC0DA for the same pinned engine.
These tie the operation numbers to the argument-producing targets above.

The table's events are internal client events, not new network actions. The raw
receiver evidence uses field 5 and a subtraction for operation 8, and prior UI
state plus fields 2/3/4 for operation 10. No business meaning is established for
those values. The decoder performs no subtraction or state update and labels
arguments only with their wire field numbers and sequence positions.

`ui-owner-initial-state-008.txt`, 011D2B18..011D2B44, shows zero initialization
of scalar storage and separate clearing of presence. Each projected argument
therefore retains nullable `WireValue` / `IsPresent` and separately exposes
`NativeStorageValue` (absent maps to raw UInt32 zero). Absence is not rewritten
into present zero. Fields 8/9/10 and all unrelated known fields are retained by
the immutable complete payload; field 10 does not gate these two mappings.

Existing confirmed duplicate singular scalar/string/child-scalar last-value
behavior and repeated-child append order remain unchanged. The captured source
bytes retain original record order, duplicate spellings and noncanonical valid
varints exactly; no normalization is used as an exact round-trip claim.

Unknown fields, wrong wire types, malformed/truncated values, UInt32 overflow,
missing operation and malformed complete tails refuse through the existing
known-schema codec. Native unknown retention remains a separate open boundary.
Other valid operations refuse this specific projection without changing the old
generic codec. Finite payload/child bounds are unchanged offline codec guards.

Frame decoding checks explicit 7937, exact 2132, actual raw header length/opcode,
whole-frame length and separate payload coherence before payload decoding. A
caller-built disagreement is refusal, not a reconciled or fabricated frame.

## Catalog/table contract

No new catalog is parsed or bound. Retained 7937 Echo Info literals describe UI
maxima/cost/progression families but do not establish meanings for these response
arguments. Main/Sub item identity tables do not establish new 2350 field meanings.
No catalog value is used as a permission, state default or accepting owner.

## Validation and transaction rules

Capture caller bytes, fully parse the existing known schema, require operation 8
or 10, then publish one immutable response and its owned arguments. Refusal returns
null with a finite reason. Original input and earlier decoder objects remain
unchanged. Public byte copies and argument spans cannot mutate retained state.
No callbacks, funds, item UIDs, saves, outbound sends or successful action replies.

## Expected wire behavior and fixtures

Independent synthetic wire goldens establish ordered operation-8 and operation-10
mapping, including out-of-order records, absent/present-zero, UInt32 extremes and
duplicate last values with exact original-byte retention. Other known fields,
raw bytes and child order remain usable. Controls cover full malformed tails,
version/operation/bounds, coherent segmented frames, forged frame components and
unchanged default registry. Tests are authored only; no C# execution is claimed.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Existing known 2132 schema | integrated codec and retained native parser/writer text | Confirmed bounded schema |
| Operation-to-target selection | helper-chinese-7935-consumer-next/operation-dispatch-table-013.json | Exact retained table, same pinned engine |
| Raw operation-8 argument order | helper-chinese-echo-state-next/response8-and-record-operations-001.txt | Same-engine retained text; derived 7937 support |
| Raw operation-10 argument order | helper-chinese-echo-selection-next/operation13-14-shared-001.txt | Same-engine retained text; derived 7937 support |
| Actual UI receiver | response8-10-ui-state-014.txt and owner RTTI/registration reports | Bounded raw receiver evidence only |
| Absent scalar storage zero | helper-chinese-7935-echo-request-next/ui-owner-initial-state-008.txt | Observed storage separately from presence |
| Catalog/business/state ownership | no matching observation | Unknown; no inference |
| Matching 7937 wire sequence | no retained matching request/reply/state transition found | Missing |

Current bounded wire diagnostics only enable VisibleDx8D13 opcode 2419; retained
D13 item/skill captures filter other opcodes and cannot establish 2132/2350 state.
The exact missing next observation is a matching-profile request/reply sequence
with the real pre/post Echo item, selected position, activation and persistence
owners. Operations 13/14, 2350 extensions and costs remain opaque. Capturing it
requires a separately authorized future step; the execution hold remains active.

## Implementation and qualification

New modules: `Chinese7937EchoResponse.cs` and `.Parsing.cs` under Core/World.
New controls: `Chinese7937EchoResponseTests.cs` and `.Refusals.cs`.
No existing source or frozen proposal changes. Root reviews, integrates and runs
the composed build and new/neighbor codec controls after the sole host job closes.
No runtime binding, client display, transaction, SQL, deployment or full parity
claim follows from this decoder.
