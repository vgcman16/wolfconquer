# FSB-226: Complete native item-file loading before owner publication

## Identity and scope

Revision 1, 2026-10-29. Baseline public source 84611bb1. This milestone repairs
the complete existing native item loader and its raw equipment observation.
The confirmed live profile remains old7899 / VisibleDx8D13. It does not activate
new7937 handlers, change SQL, routes, original saves, or executable trust holds.

## Packet contract

Not applicable: this is login item-file hydration. Loading must not send packets,
consume a packet rental, mark FullLoading, or authorize gameplay implicitly.

## Catalog/table contract

The existing native record and save owners establish the file envelope:

| Part | Representation | Evidence |
| --- | --- | --- |
| Root count | nonnegative Int32LE | native ClientItems save / current owned save writer |
| Root item | actual 184-byte LegacyNativeClientItemRecord | existing independent field-offset/layout tests |
| Deposits | the root's declared immediate records, same 184-byte layout | actual save/load loops; preserve root separately from child |
| Perfection count | nonnegative Int32LE | actual postlude writer |
| Perfection row | actual 80-byte LegacyNativeClientItemPerfectionRecord | existing native fixed-buffer layout and writer |

Every count, row and tail must fit actual supplied bytes before allocation or
native item materialization. Preserve opaque fields and record bytes. A missing
file is distinct from a truncated/corrupt present file. Do not reinterpret EOF as
zero-filled rows/counts. No trailing or nested extension grammar is invented.

Parser API shared by the assigned modules: internal
`LegacyNativeItemLoadImage.Parse(ReadOnlySpan<byte>)`, with read-only `Roots`
and `Perfections`; each root has value-copy `Record` and read-only value-copy
`Deposits`. The parser does not normalize stats, route, instantiate item owners,
query metadata, or mutate the input. Domain validation remains a separate phase.

## Validation and transaction rules

1. Capture required real client/player and actual container ownership at the
   lifecycle where they are needed. Do not fabricate missing owners or accepting
   callbacks. Missing/empty-file behavior must avoid unused dependencies.
2. Parse the complete physical file into a detached image. Check counts before
   allocating, keep each root's route fields independent of children, and close
   every owned file handle before publishing runtime state.
3. Materialize genuine native items and copied extras, prepare all routes and
   perfection hydration before any actual owner/map/item/quest-marker mutation.
   Canonical UID membership, duplicate identities, actual load admission and required metadata
   must be validated through the actual owner rules.
4. The deposited quest item's marker is its UID, not ITEM_ID. Parent routing uses
   the parent UID/location, never the last child's reused record. Correct the
   rune fallback on the actual prepared item when the real job rule requires it.
5. Prepare all potentially throwing collection/comparer/allocation work detached.
   Revalidate the captured owners, membership, versions and metadata under real
   cooperating gates, then commit using nonthrowing field/state publication.
   Preserve real item aliases, including legitimate wardrobe/equipment overlap.
6. Ordinary refusal leaves prior runtime owners, maps, items, marker, queues and
   input file unchanged. A lock alone is not crash-safe persistence or globally
   atomic ownership. Public noncooperating writers and raw observers must remain
   explicit limitations until their complete lifecycle is wired.

## Expected wire behavior and fixtures

No new wire output. Keep independent record/perfection goldens. Replace the old
tests requiring parent/child key corruption and accepted partial postlude counts
with safe invariants. Exercise complete valid mixed containers, late corruption,
duplicate identities, real missing/foreign owners, rune/wardrobe admission,
perfection failures and repair/retry through the actual loader using owned files.
No unsafe old native-buffer baseline or original save is executed/rewritten.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Root overwritten by deposited row | LoadClientItems reused dBItem and current ItemLoader test explicitly accepts mismatched key/value | confirmed source defect |
| Short reads publish partial state | ignored ReadCount/ReadRecord/ReadPerfection bool results and accepted truncated-postlude test | confirmed source defect |
| Quest marker uses ITEM_ID for child | actual child branch and existing defect assertion | confirmed source defect |
| Raw equipment projection copies corrupted routing | LegacyNativeEquipmentRuntime.DecodeRawLoad | must align with corrected root image |
| Existing legacy filters/normalization | native save/load rules and current catalog metadata | inspect before changing; unknown client rules stay labeled unknown |
| SQL/new-client/live/crash acceptance | unavailable here | open; no substitute claim |

## Implementation and qualification

Root owns the loader orchestration, existing integration
test adaptation, ledger, integration and all builds/tests/commits. Assigned helpers
own parser, raw observation and actual owner-publication modules in nonoverlapping
scopes of this isolated candidate. No helper executes code or changes actual
checkouts. Root records actual focused and broader outcomes after review.

The resolved source-format boundaries are recorded below. Managed qualification
is pending; no implementation or full-client completion is claimed by this spec.

Current design rulings (source-backed; qualification pending): native root/child
UIDs are nonzero/globally unique. Perfection UIDs are nonzero, unique and refer
to serialized roots/children. Only immediate deposits are representable. A205901
root without children/perfections retains its explicitly unverified legacy skip;
children or perfection targeting that filtered root refuse rather than vanish.
Pure Wardrobe/Rune/Relic/Gear main perfections have no supported old save/load
policy and refuse; genuine child and Inventory/Equipment/Warehouse main targets
hydrate the newly materialized references. No repair/migration rewrites inputs.
Existing outer stat normalization is provisional legacy policy, not client truth.

The public loader requires a real UID/owner and !Fake/!FullLoading both initially
and immediately before publication, matching original login timing/current prefix
guards. Nonempty valid prelogin graphs are checked for global identity collisions;
this is not active-world hydration authority or whole Frame lifecycle proof.
Every actual item container must exist for that global collision proof whenever
anything is published. Complete empty/only-valid-filtered images need no unused
item owner. Gameplay Inventory40/Warehouse60/Sash queries are not invented load
caps; actual load AddDBItem/bank paths have none. Rune uses its genuine storage
admission checks. Whole-load publication is only cooperating in-memory safety,
not SQL/crash safety or a fence for all public raw writers/observers.

Root local qualification for the combined40-path candidate: Release build01
passed with0errors/157existingwarnings and no new changed-path warnings. Actual
focused01 passed423 with7existing skips; native-broad01 passed13278/13278 with
0failures/skips. Selections overlap. Receipts are in root-native-item-load-next.
The old public7bb fullrun failed one timing-sensitive startup test; its actual
failedTRXs/logs remain. Exact-commit successor fullCI, SQL, deployment and live
new-client/fullparity acceptance are pending. This note supersedes authored/unrun
labels in this spec/entry without expanding the verification scope.
