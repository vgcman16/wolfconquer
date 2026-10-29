# RUNTIME-INSPECT-2: selected runtime container validation

## Identity and scope

Revision 2, 2026-10-29. Reviewed validation for the default-off
owned runtime diagnostic. The actual bound D13 owner/issuance/session/actors and
v1 raw-writer/ABA limitations remain mandatory. No gameplay writer, protocol
profile, WCM2, DataTool behavior, saved file, host configuration or live route is
changed. Both atomicNativeTupleProven and liveStoreSynchronized remain false.

## Packet contract

No Conquer packet changes. WRI1 explicitly represents unavailable native item
roots with null rows, a typed reason and null selected comparison. Existing bounded detached rows and
typed refusal are retained. This tightens the source projection that may produce
an Observed response; it adds no acceptance state or remote authority.

## Catalog/table contract

No new catalog schema. Existing loader/save container routing is authority for
membership ranges, not a guessed enumeration of gameplay-eligible equip slots.

| Selected root | Required shape | Existing source evidence |
| --- | --- | --- |
| Inventory | Position 0, warehouse 0 | PlayerItemSnapshotLoader.AddVisibleItem; LegacyItemSaveGraph.ValidPlacement group 0 |
| Ordinary equipment | Position 1..63, warehouse 0 | Same loader; ValidPlacement group 3; native nonwarehouse routing |
| Equipped wardrobe alias | Position 1..59, warehouse 100, native family 9/17 bucket | Same loader; native warehouse routing; native save Membership.IsWardrobeEquipmentAlias/IsWardrobeBucket |
| All selected equipment | Distinct position per managed/native side; normal and alternate positions distinct | Equipment.Plan duplicate-position admission |

## Validation and transaction rules

Apply those shape checks independently to actual managed and native roots before
copying a row. Retain selected UID uniqueness, key/UID integrity, current owners,
row/value/reference witnesses and existing bounds. Ordinary equipment does not
gain new item-family, gender, class, level or equip-action restrictions.

Each selected native warehouse-100 alias requires bounded direct lookups in the
actual constructor-bound wardrobe owner's two distinct default-comparer maps.
The correct family bucket must contain the exact UID and same native item object;
the other bucket must not contain that UID. Recheck owner, map-array/map references
and repeat these selected alias witnesses with the existing native projection.
Missing, foreign, malformed or detected-changing witness refuses. No full wardrobe
enumeration, invented owner, save or silent omission. Uncollected containers and
wardrobe rows remain outside coverage; selected alias validation does not bless
their complete graphs or prove atomicity against raw writers.

## Expected wire behavior and fixtures

The existing genuine operator fixture gains only an optional catalog-input
argument with unchanged defaults. New tests prepare owned equipment input files
before actual registry bootstrap/reconciliation and ordinary login, with real
garment/mount metadata. Ordinary Frame does not create native item owners. Native-positive tests then
explicitly compose real member phases and Core/Data LoadClientItems on those
valid owned bytes, preserving the issued ordinary binding/Spells/Profs/Player.
This is isolated component composition, not live Frame wiring. These controls
check same-object equipment/wardrobe aliases at positions 59/17 and ordinary equipment at 4/54.
Controlled negatives temporarily alter actual selected managed arrays/native rows
or actual wardrobe membership/owner fields, restore them before close, and require
typed refusal, no snapshot and unchanged bytes, UID high-water, queue and binding.
Cover inventory placement, equipment zero/out-of-range/warehouse/family, duplicate
positions, missing/changed alias owner, wrong bucket/key/object and duplicate
membership. No fake owner, accepting saver or reflected HP seed is used.

The existing Mana test invokes the actual setter only as setup, reads both actual
post-write sources and computes the returned equality expectation. It does not
require stale managed state or disagreement. A later coordinated setter may
produce equality; if its legal writer entry changes, migrate setup to that entry.
A pure codec DTO separately covers explicit Mana disagreement and false equality.
No test may require restoring the unsafe uncoordinated writer behavior.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Malformed rows refuse | Frozen projection lacked placement/slot checks; binding checks do not provide them | Source gap confirmed; candidate supplied |
| Preserve genuine wardrobe aliases | Existing loader/native save and existing actual native save alias tests | Canonical exception retained |
| Reject wrong selected alias identity | Actual constructor-owner and same-item map rule | Narrow direct witnesses added |
| Do not encode Mana bug as desired behavior | Frozen Owners test had mandatory false equality after raw setter | Assertion replaced with observed-value comparison |

## Implementation and qualification

Projection and focused Containers partial; Owners test correction; three new test
parts and minimal optional genuine-fixture catalog input. Exact preimages and
repository-relative manifests are retained in helper-runtime-inspection-validation-next.
Root integrated the reviewed sources and genuine native-component fixture.
Release build08 has zero errors; the combined selection passed 104/104, zero
skips. Broader and full checks are separate pending gates. No live deployment
or client operation qualifies from these component tests. Raw-writer/ABA, synchronous-lock cancellation and
file/runtime equality limits remain open exactly as in v1.

When only one native container exists, validate and repeatedly witness every
present selected row; invalid state still refuses. Missing native roots are not
empty roots, and no complete selected comparison is available until both owners
exist. Malformed native files are never executed by these fixtures.

Root neighboring account/mail/save/tool selection also passed 194/194, zero
skips, in 3m31s. It overlaps the 104-case selection. Full successor CI and live
acceptance remain separate pending gates.
