# Chinese7937 packed compose/random metadata and real runtime consumers

## Identity and scope

Revision1,2026-11-02. Fourth isolated proposal, base2a237 plus explicitly declared
second-island source dependencies where extended. The frozen11/18/8 paths remain
unchanged. Root owns all jobs/integration; no build/test/Git/client/native execution.

Supported current inputs are official7937 ini/xuanbao_compose_attr_limit.dat and
ini/xuanbao_rand_attr.dat. Permitted data-only inspection applied the existing
stateless ClientItemCatalogCodec, verified complete numeric@@/CRLF/trailing
delimiters and exact inverse. Compose13 complete ordered numeric records equal
actual consumed seed metadata; random65 records equal the seed numeric multiset,
but source order differs. Source byte pins/complete metadata are retained beside
this scope; no private rows/assets/keys are bundled.

This implements explicit pinned current-client metadata loading into real server
compose-limit and random-relic consumers. It does not change rewards, active
protocols, SQL,2350field meanings or2132 activation. Resolve/Fuse UI conflicts
remain in the separate relic-metadata-frontier spec.

## Packet contract

Not applicable: existing item planner/compose helper consume typed metadata.
No receiving handler, wire layout, success sequence or new profile is introduced.

## Catalog/table contract

Reuse the established fixed byte codec only; no supplied/discovered game key or
alternate decryption. Explicit client7937, full32-byte packed pins, finite nonempty
captures and exact packed inverse are required. Decoded rows are ASCII numeric
fields, exactCRLF including final row, exact trailing@@, no header/BOM/NUL/empty
rows. All data remains source ordered/raw spelling intact.

| Table | Complete observed rows/layout | Semantic alignment |
| --- | --- | --- |
| Compose |13rows;4numeric fields plus trailing empty | ID and3values match existing XuanbaoComposeAttributeLimit numeric records in order |
| Random |65rows;4numeric fields plus trailing empty; widths10/3/10/10; zero-padded digits | ItemTypeId,AttributeType,Min,Max match all65 seed records by unique(item,type), NOT ordering |
| Addition |70rows;5numeric fields plus trailing empty | Not activated: all57 shared first-column IDs differ from oldseed; role continuity unresolved |

Parse unsigned decimal spellings with leading zeroes and require Int32 numeric
width for these two established server record types. Preserve original RawLine
and Fields in owned read-only collections; do not canonicalize/sort source data.
Refuse duplicate compose IDs/random(item,type) keys and unsupported full formats.
Keep each complete row and both packed/decoded SHA256 identities immutable.

## Validation and transaction rules

New runtime sidecar binds once to a genuine WorldDataCatalog. Prepare all data
before publication; deep-copy server comparison values/order and never retain
post-publication mutable input aliases as authoritative state. Validate the full
compose/random key sets and all numeric values against captured existing real
server metadata; a conflict refuses pending an updated behavior spec. Required
compose caps must be30..UInt16.MaxValue; random ranges must remain valid for the
existing normalized epic-maximum convention. Validate real item IDs/attribute
owners before returning a usable runtime source. No fabricated row/owner fallback.

Random SOURCE order remains available in the decoded immutable snapshot. Runtime
candidate order is the captured existing SERVER order, joined by unique(item,type)
to equivalent selected records. This explicitly preserves current server index/
selection behavior; it does not prove that client source order is equivalent.
No source sorting or hidden first/last winner is permitted.

Actual FromWorldData drop factories read the bound sidecar when present; all
existing callers retain old behavior without opt-in. Existing compose-limit
lookup uses the same once-bound owner. Optional startup requires all4 explicit
compose/random path+SHA256 settings together, validates before native publication
and before optional gold-limit binding, and fails catalog startup on configured
malformation/conflict. No wholesale catalog/loader replacement or hot reload.

These are detached metadata/planner operations. Inventory UID assignment, capacity,
debit/grant transaction, crash/retry and reward persistence remain caller ownership;
this milestone supplies no new accepting handler or durable transaction claim.

## Expected wire behavior and fixtures

No new wire behavior. Synthetic owned fixtures cover strict packed pins/inverse,
all row spellings/widths/endings/duplicates and immutable Fields/Values, ordinary
pinned files, complete failed-publication preservation, real world-owner binding,
numeric/key-set conflicts, preserved server candidate order when source order
differs, and actual public normal/max relic generators plus compose-cap lookup.
Legacy/default controls remain. Test source only; Root qualifies.

## Evidence alignment

| Requirement | Current data | Existing server | Status |
| --- | --- | --- | --- |
| Compose schema/values |13 complete decoded records | Actual13 consumed seed records, ordered equality | Independently aligned |
| Random schema/values |65 complete records/unique keys | Actual65 seed records, numeric multiset equality | Aligned values/keys only |
| Random selection order | Current source differs | Existing candidate order retained at binding | Explicit unresolved client-order meaning |
| Addition roles |70numeric envelopes |57shared IDs all changed | Unresolved; no typed semantic activation |
| Resolve rewards | Materialidentity agrees;two counts both conflict with9 | Real bGold owner/action/transfer missing | Conflicting; unchanged reward logic |

## Implementation and qualification

Helper owns focused packed compose/random decoder/snapshot/pinned file loader
and synthetic parser controls in new paths. Coordinator owns deep immutable runtime
binding, focused actual lookup/factory/startup joins and real owner/order controls.
Declared second-island dependencies use its exact frozen postimages, not rewritten
copies. No frozen path is edited. All C# UNBUILT/UNRUN; Root alone integrates and
runs appropriate focused/full gates. No current7937 client acceptance is inferred.
