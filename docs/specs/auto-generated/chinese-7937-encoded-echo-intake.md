# Chinese7937 encoded Echo intake

## Identity and scope

Revision2, 2026-10-29, complete fresh data decode before new adapter implementation. Full reference
version.dat is7937. Selected encoded files are ini/luacfg/XuanBaoEcho.dat9114B
and XuanBaoInfo.dat47345B. Existing explicit-key ChineseLuaCatalogTransform
and strict decoded Echo schema are established independent components.

Current plain ini/XuanBaoInfo.lua is40923B, strictGBK-compatible/UTF8-incompatible,
and contains none of the selected Echo export/accessor/cost symbol strings.
Its filename supplies no current Echo binding. No Lua program is executed.

## Packet contract

Not applicable: pure data adapter only. No2132/2350 handler/profile/startup change.

## Catalog/table contract

Encoded identity, a supplied key, decoded identity and strict complete schema
must be validated in one dataflow. Hash/filename equality alone is not a decode.
Use only an explicit caller-owned12-byte key and existing bounded byte transform;
no key default/discovery, EXE reads or key logging. A preexisting privately
Root-prepared key is available for separate metadata-only qualification, not
bundled into source or promoted to a runtime key owner.

The existing7930/7931 four-field literal module parser remains unchanged. Actual
fresh full-reference Echo decode SHA256 is
`753c1ebc56459c089819fd8ba8a8acaa53b276c18f6a0a60115a9baacf8511ff`;
the complete strict7931 literal parser accepts85rows/4named fields, strictUTF8
roundtrip true, NUL0. An observed client7937 marker does not invent a new literal
schema version. This is a direct fresh transform+schema result, not filename or
hash-only inference. The privately existing key/key hash was never printed.

Info's fresh decoded SHA256 is
`f917bfa6554eb2575eb06f1224b9122ec74cc57661549bfdaf954ebca82e7d08`;
strictGBK roundtrip true/UTF8 false, NUL0. It correctly fails the Echo parser's
UTF8 gate. Selected accessor names occur8/5/1times; occurrence alone is not a
selected-binding proof. Existing complete selected literal/direct-lookup evidence
for these exact decoded bytes remains separate and never accepts a whole Lua
program or current cache state. Info is not an attribute-table fallback.

## Validation and transaction rules

Capture input/key/pins, verify exact encoded identity, decode using the explicit
key, compare expected decoded identity and consume the entire existing schema.
Failure returns no new snapshot and cannot alter an earlier successful result.
Never print/store keys or row payloads. Temporary key copies are cleared.
No arbitrary callback, live cache owner, fallback table or accepting handler.
Info can remain byte-decoded/opaque to semantics until its selected binding is
proved. A text-compatible payload is not complete Lua program acceptance.

## Expected wire behavior and fixtures

None. Synthetic tests need explicit key/pin/bounds/version/syntax refusal and
ownership controls; any positive encrypted module fixture must be synthetic.
Actual private data qualification emits only hashes/counts/schema status.

## Evidence alignment

| Requirement | Evidence | Boundary |
| --- | --- | --- |
| Byte transform | Existing ChineseLuaCatalogTransform and published-vector tests | Explicit key only |
| Complete Echo schema | Existing strict C# decoded catalog parser | Declared7930/7931; no runtime binding |
| Selected Info lookup | Saved literal/direct-accessor investigation | Selected relationship, not full Lua/current owner |
| Plain full-reference similarly named Lua | Current bounded symbol/encoding check | No Echo relationship established |

## Implementation and qualification

The actual approved metadata-only probe built0errors/0warnings and ran terminal0,
referencing only an existing compiled Core DLL while the full Windows job remained
untouched. No raw/decoded rows, key/key hash or executable bytes were exported.
Root qualified the encoded-to-schema adapter with 135/135 focused C# tests
(zero failures/skips). A separate actual 7937 API probe decoded all 85 rows,
retained ExplicitKeyTransform provenance, preserved the original snapshot and
refused the different encoded catalog. Its selected inputs were unchanged.
The adapter and tests are integrated; the combined full Windows gate is pending.
Current key/cache/contribution/item ownership, permissions/cost meanings,
operations 13/14, durable activation/save, profile transport/startup and live
newer-client acceptance remain open.
