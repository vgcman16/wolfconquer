# Chinese7937 Echo typed requirements and runtime catalog

## Identity and scope

Revision1, 2026-11-02. Dedicated newer-client implementation island against Root's
clean2a237a624542c2ae2bbab498f7c383060959f916 source. Root alone integrates and
qualifies it. No builds/tests/restores/Git/native/client jobs are authorized here.
The local client/updater execution hold remains ACTIVE.

Target is the separately selected official7937 reference and the7931 DX8 engine
retained by that reference (SHA256 a7e1aac88a999b1f4723e6a1f197b96924e24022fd681fb2c9cf79c1bb413422).
Existing VisibleDx8D13/default profiles remain separate. This milestone closes
the decoded selected Info literals -> complete typed requirements -> exact
current main/Sub item identity lookup -> operation6 request preparation path.
It creates a real immutable runtime catalog used by that request preparer.
It does not manufacture player/item activation state or a financial transaction.

## Packet contract

Observed operation6 callback009D72DF and builder011AF9D2 emit opcode2132 with
required field1=6 and optional UInt32 field4=UI selection+1; response fields are
absent. Independent selection0 full frame is0800540808062001. Existing codec is
reused unchanged. Prepared requests retain exactly that known field presence.
The returned request is outbound request data, never an authorization/success.
No receiving dispatcher is registered; operations13/14 and item2350 action
semantics remain unresolved. Unknown fields of existing catalogs remain owned
by their complete underlying documents and are not discarded or reinterpreted.

## Catalog/table contract

Sources are declared7937 main/Sub strict67-field envelopes and the selected
Info literal/direct getter relationship already implemented by
Chinese7937DecodedXuanbaoInfo. Main57093/Sub1693 rows reconstruct exactly; only
column0 identity is known. Current decoded pins are main875cc337245f102b15302e230972d0d92db95dcaade600a869a73a9ef623fe2c,
Sub72ae7536d232011443cff432f2ea8752f00998250020dcbe7868778ac78090df.
Info decoded47345B SHA256 f917bfa6554eb2575eb06f1224b9122ec74cc57661549bfdaf954ebca82e7d08
matches the retained7931 decoded image and fresh7937 transform result. All input
bytes, plaintext tables, game assets and private records remain outside source.

The additional typed projection consumes the COMPLETE selected59-field known
shape: max_hole_num, cost_num, three cost families indexed1..7 and six
open_hole families indexed2..7. Those family names are observed literal data,
not inferred prices/currency/experience rules. Preserve the original snapshot
and exact source hashes. Require all observed members, exact ordinal names and
no unrecognized member for this explicitly versioned projection. A future shape
must receive a separate supported schema instead of silent partial projection.

| Named field family | Type/index | Projection | Evidence |
| --- | --- | --- | --- |
| max_hole_num | UInt32 | CatalogMaximumHoleNumber | Complete selected literal; UI accessor00A09685 |
| cost_num | UInt32 | CostNumberField | Complete literal; gameplay role unresolved |
| cost_money_,cost_itemtype_,cost_itemnum_ | UInt32;1..7 | MoneyField,ItemTypeField,ItemNumberField | Complete literal and009D5415..009D55 cost accessors |
| open_holeN_cost_num | UInt32;N2..7 | CostNumberField | Complete literal; operational meaning unresolved |
| open_holeN_itemtype_1/2 | UInt32;N2..7 | ItemType1Field,ItemType2Field | Complete literal; operational meaning unresolved |
| open_holeN_exp_1/2,open_holeN_maxexp | UInt32;N2..7 | Experience1Field,Experience2Field,MaximumExperienceField | Complete literal; no server progression formula claimed |

Runtime lookup builds immutable separate main/Sub ID indexes without combining
overlapping IDs or guessing precedence. A reference found in either/both retains
its exact source location; absent lookup is explicit. Identity zero remains
ordinary catalog data; no absent-value interpretation is invented. Every typed
itemtype reference must resolve before publishing the coherent runtime catalog.
Cross-table existence is a derived consistency condition, not native semantic
authority over a particular item stat, currency or inventory object.

## Validation and transaction rules

No economy/player transaction belongs to this immutable read owner. Inputs are
real complete parsed documents and the exact captured encoded Info, not
callbacks, queues, fabricated owners or acceptance delegates. Require declared
captured7937 state, an explicit12-byte transform key and expected decoded Info
pin. Compare the encoded Info against that captured configuration finding,
transform using the existing byte codec, compare the decoded pin and invoke the
complete strict selected Info7937 parser before its additional typed projection.
No key discovery/default, file access or Lua execution occurs. The temporary key
copy is cleared. Preserve the caller's key and retain no key in the catalog.
Capture/version/pins/shape/all references are validated before returning a new
catalog. Failure returns null and leaves earlier instances usable.

Preparation accepts zero-based UI selection, uses checked+1 and the observed
max_hole_num as a catalog/UI bound. Keys6/7 remain retained data when the actual
maximum is5. This is a request-preparation bound, not a claim that five activations
are permitted or persisted. Unknown permission, balance, ownership, cooldown and
activation state cannot become successful server mutations from this API.

## Expected wire behavior and fixtures

Synthetic owned fixtures cover complete59-field source, all7 cost/6 progression
rows, UInt32 edges, missing/extra/duplicate grammar members, exact case/version,
strict selected relationship and input ownership. Runtime controls cover main-only,
Sub-only, both and missing identities; all typed item references; declared version,
pin mismatch; existing instance preservation; opaque67 fields preservation;
selection boundaries and the independent2132 frame golden. No real catalog rows
or game assets are included. Tests are authored only; Root runs qualification.

## Evidence alignment

| Requirement | Catalog/reference | Bounded wire | Status |
| --- | --- | --- | --- |
| Complete typed59 fields | Retained7931/fresh7937 identical Info | Not applicable | Observed names/UInt32 shape |
| One-based cost index | Complete literal/accessor reports | Existing operation6 field4 golden | Observed request mapping |
| Item identity references | Strict7937 column0; complete family values | No item activation sequence | Derived referential consistency only |
| Request response/result | No transaction/current player owner | field10 zero-success consumer only | Unresolved; no accepting handler |
| Permissions/debit/save/profile startup | Missing actual owner/contract | No current7937 session | Unresolved; separate milestone |

## Implementation and qualification

Helper scope: new Chinese7937EchoRequirements and its synthetic fixtures, plus
metadata-only additional-record inspection under this island. Coordinator scope:
new Chinese7937EchoRuntimeCatalog, direct lookup/request preparer and component
integration fixtures. No existing tracked source preimage is modified. All new
paths are checked absent in the exact2a237 candidate before handoff.

Implementation now authored: complete typed requirements plus encoded Info
factory, immutable catalog lookup and catalog-backed operation6 request preparer.
Additional data-only inspection rechecked all three decoded pins and parsed the
complete36 opening fields:12 nonzero item references,2 distinct types,all12main,
0Sub/0missing. Together with7cost references this is19references/3distinct types,
all main,0zero/0missing. This supports the derived referential consistency rule;
it does not establish an inventory owner or consumption behavior.

The two owned encoded Info fixtures use the same independent public-domain
zero-key3-Way reference as the existing Echo fixture generator; the published
zero-key known answer is checked before authoring. Neither fixture is a client
table. Complete fixture is1898B (decoded SHA256
eed227795e14b60daceb91e37445aaa5cbedd91ca203373172a79dff4bf5c58c),
incomplete selected module305B. Initial helper authoring did not run C# checks.

Root component qualification2026-11-02: actual build passed, then combined
run20261102T153347176Z-a0ec3a6d passed3507/3507 with0failed/skipped. This includes
78 Echo requirements and22 runtime catalog controls (100 new rows), plus existing
Chinese/catalog/transform/packet/relic/magic/Flag6 neighbors. Build161.643s,
tests202.895s. Full integration gate remains required; no live proof follows.
The11 paths were new/ABSENT in exact2a237. Host
startup/profile wiring remains absent, and the catalog is instantiated through
its explicit factory only. Actual-data C# execution is not established by these
synthetic controls; the retained data alignment above is separate evidence.
Next milestone is the real per-player Echo state and
durable activation transaction after its currently missing action/state evidence,
not a claim that this catalog/request join completes newer-client gameplay.
