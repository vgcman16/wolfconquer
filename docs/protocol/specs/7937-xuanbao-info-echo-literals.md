# 7937 XuanBaoInfo Echo literal relationship

## Identity and scope

Revision 1, 2026-10-29, offline source/data intake requested by Root. Authoring
base is public `a4380ba1c66621ef7e2a5c646035697789988dd4`; no working-tree dirt is
an implementation input. Working client authority remains Chinese7899 /
VisibleDx8D13. This separate selected catalog contract covers declared7931 and
7937 decoded Info data, not a default protocol/profile or runtime activation.

The retained7931 decoded file is47345bytes, SHA256
`f917bfa6554eb2575eb06f1224b9122ec74cc57661549bfdaf954ebca82e7d08`.
Historical `decode-7931.json` pins it. The fresh7937
`actual-encoded-catalog-metadata.json` records an independent decode of the exact
current47345byte Info image to that same decoded digest, strict GBK roundtrip,
UTF8 false and NUL0. No new decode, key access, Lua/native execution or table
export is needed to inspect the identical retained decoded bytes.

Intended result: an immutable ordered string-to-UInt32 literal snapshot and exact
case direct lookup after a complete finite selected relationship check. No
currency identification, affordability, permissions, target/slot admission,
resource mutation, owner, queue, handler or startup binding is added.

## Packet contract

Not applicable. This parser accepts already decoded data only. Packet2132 and
its UI selection+1 observation remain separate evidence. Configuration names,
field counts and values do not define packet fields or authorize operations.

## Catalog/table contract

Decode with the existing strict CodePage936/GBK encoding and require byte
roundtrip; no UTF8 fallback/BOM repair. Reject NUL and Unicode BOM prefixes.
Maximum65536 input bytes,32768 lexical tokens,128-character identifiers/selected
field names and1024 selected fields are finite offline allocation guards.
They are not native/gameplay limits. Declared version and supplied file digest
are comparison metadata, not current loaded state or execution authority.

Observed selected syntax, with whitespace and Lua comments as trivia:

```lua
local XuanBaoEchoInfo = {
    ["field_name"] = 123,
}
local function GetXuanBaoEchoInfo(key)
    return XuanBaoEchoInfo[key]
end
local function Unload()
    XuanBaoEchoInfo = nil
    -- Unrelated reset statements remain opaque.
end
local XuanBao = {
    -- Unrelated members remain opaque.
    GetXuanBaoEchoInfo = GetXuanBaoEchoInfo,
}
XuanBao_GetXuanBaoEchoInfo = XuanBao.GetXuanBaoEchoInfo
```

The example value/name is synthetic. Actual selected table lines1324–1389 contain
59 distinct fields, source ordered, with double-quoted unescaped ASCII identifier
keys, canonical unsigned decimal UInt32 values and a comma after every member.
Actual getter is at1391–1393, reset at1413–1430, module export at1432–1453 and
global alias at1457. Comments occur inside the selected literal. Preserve all
named fields without inventing meanings or publishing the proprietary table.

Accept that finite selected grammar, with at least one field; do not assert that
an arbitrary59-field count establishes the complete current schema. Reject
duplicate exact-case fields, nondecimal/signed/overflow values, alternate key
syntax, expressions/calls, nested selected values and missing commas. Ordinal
case-sensitive names retain distinct case variants. Missing lookup is explicit
`TryGetValue=false`, never a synthesized owner, value or successful operation.

Locate selected constructs outside strings/comments with a bounded lexical
scanner. Strings and comments support ordinary quotes/escapes and Lua long
brackets solely to avoid false selected matches. Track lexical block/delimiter
depth to reject selected declarations nested/shadowed inside unrelated functions
or tables. Unrelated expression/statement semantics stay opaque; this is not a
whole Lua validator/interpreter. Unsupported or unbalanced lexical boundaries
refuse because selected scope can no longer be determined safely.

Require one top-level literal, one exact top-level local getter, one top-level
module literal with one direct selected export, one exact top-level global alias,
and one direct nil reset inside the unique top-level local Unload function.
Require that source order: literal, getter, Unload, module, global alias, so no
forward declaration silently changes the local closure/binding relationship.
Reject additional selected identifier uses/reassignments, duplicate constructs,
computed/quoted alternative selected exports, module reassignment, function-body
extensions and selected expression continuations. Other module members and reset
statements are not evaluated or converted to data. Module members require bare
identifier left-hand names; computed/escaped keys could ambiguously overwrite the
selected export and refuse. Unrelated right-hand expressions remain opaque.

| Element | Representation | Validation | Evidence |
| --- | --- | --- | --- |
| Selected names | ASCII, ordinal | Exact literals above; unique relationship | Retained decoded7931 bytes and fresh7937 identity |
| Field key | Double-quoted ASCII identifier | Unescaped, nonempty, at most128 chars, exact-case unique | Complete selected literal |
| Field value | Canonical decimal UInt32 | 0..4294967295; no sign/expression | Complete selected literal |
| Selected rows | Ordered name/value records | Snapshot-owned array and frozen ordinal lookup | Literal order/direct index return |
| Other Lua | Lexically bounded opaque text | Never executed or treated as accepted runtime logic | Full Info contains unrelated systems |

## Validation and transaction rules

No gameplay transaction applies. Prepare the whole selected snapshot before
publication. Any refusal returns null snapshot and a finite failure enum; no
partial fields are published. Copy input bytes before decode/hash, copy snapshot
rows and expose only read-only rows plus ordinal lookup. No static/default catalog
or mutable dictionary escapes.

The optional already-decoded file loader requires explicit supported declared
version,32-byte SHA256, absolute ordinary local input and ordinary ancestors.
Reject UNC/device/reparse paths. Hold a read-only lease, check finite length,
read exactly, recheck EOF/length/path and compare the captured bytes' digest in
fixed time before parsing. This follows existing Echo intake's portable ordinary
file checks; it is not hostile concurrent path-replacement or runtime ownership
proof. No transform/key-loading API is part of this scope.

## Expected behavior and fixtures

All published fixtures are synthetic, including names/values, GBK comments,
opaque unrelated Lua and guarded temporary decoded files. Cover complete direct
relationship and source ordering; UInt32 edges; case/missing lookup; input/output
ownership; comment/string decoys; duplicate/missing/unsupported selected forms;
nested/shadowed declarations; extra selected writes; export/alias/getter/reset
changes; malformed long/quoted strings/comments and numeric syntax; bounded
input/token/field/name sizes; strict GBK/no BOM/NUL; path/pin/file refusal and
preservation of an already returned snapshot after a failed later load.

The immutable result does not prove Lua execution, module loading/Unload timing,
native cache/state, current server currency/permission/cost rules, legitimate
targets6/7, operation13/14, item/contribution ownership or durable activation.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Strict GBK input | Historical decoded pin + fresh7937 GBK roundtrip/UTF8 false | Observed byte identity/encoding |
|59 UInt32 named literals | Complete selected retained literal, not occurrence counts | Observed literal data; meanings unspecified |
| Direct getter/export/global alias | Exact selected statements above | Observed static relationship only |
| Unload resets selected table | Direct nil assignment in named function | Observed source; no loaded lifecycle proof |
| Server costs/permissions/state/transaction | No real owner supplied | Unresolved; no activation or decisions |

## Implementation and qualification

New isolated `Chinese7937DecodedXuanbaoInfo` partials and
`Chinese7937XuanbaoInfoSnapshot` live under Core/Client. Synthetic partial tests
live under `Chinese7937XuanbaoInfoTests`. This spec and exact base/path manifest
are prepared before implementation. No existing source preimage is modified.

Root Release build01 completed with0errors; combined direct556/556 passed,
including all41 Info outcomes (plus15 account and20 catalog-decoder outcomes).
The actual new C# API then accepted59 fields with declared7937 and SHA256
`f917bfa6554eb2575eb06f1224b9122ec74cc57661549bfdaf954ebca82e7d08`.
It used the retained7931 plaintext whose digest equals the independently decoded
fresh7937 input; the current raw7937 pin remained unchanged. The private
`info-probe-actual.json` records ValuesPrinted=false, LuaExecuted=false and
RuntimeActivated=false. No field values or keys are bundled or printed.

Initial neighbors01 completed2095passed/2failed and remains pre-fix evidence.
After the cache-test correction and separate Ninja FSB-203 repair, Release
build02 completed with0errors (157warnings). Combined02 passed2657/2657 with
0failures/skips: direct556 plus neighbors2101; targeted6/6 are a subset.
Integration is reviewed; the new full Windows gate remains pending at this qualification checkpoint. This
data/API acceptance does not establish current loaded state, native owners,
currency/cost/permission semantics, live behavior or a parity increase.
