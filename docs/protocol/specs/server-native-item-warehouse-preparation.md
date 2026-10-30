# FSB-219: Prepare real item and warehouse outputs before publication

## Identity and scope
- Revision1, 2026-10-29; Root-authorized offline native source batch.
- Base: public91040398 plus the Root overlays in DEPENDENCY.json; use existing PrepareFrame, PrepareWriteAt and PrepareAppend.
- Target: existing native7899/VisibleDx8D13 component wire fixtures. No new7937 profile, host activation or SQL authority is inferred.
- Actual owners: GameItemRuntime.ItemCreate/SendItemLocked, ItemViewPacket, WarehousePacket and WarehouseRuntime.Show, with five focused snapshot/preparation/publication modules.

## Packet contract
| Packet | Contract retained |
| --- | --- |
| Item2116 | Existing field order/widths and actual variable relic count; timer words come from the actual ItemCreate receiver, other fields from its supplied item |
| ItemView2275 | Existing cost/player/item fields, duplicate purification ID, two actual RemainingTime reads and conditional Activate word, actual variable relic count |
| Warehouse2080 | Incremental header at24, existing row layout and four-item batches; create -> append rows -> finalize remains valid |
| Lock2323 | Actual UID, UnlockDate type, zero mode/reserved word and original timer |
| Extra2134 | Actual copied purification/refinery value structs with existing field/type/effect mapping and packet order |

Preserve independent successful goldens, unknown enum bits, native fixed-string UTF8 truncation, header/seal rules, optional data and actual item/array aliases. Reject unrepresentable warehouse UInt16 count/capacity rather than wrapping; existing equivalent valid header bytes remain testable with representable inputs.

The retained fixed-string bytes are existing component fixtures. Correct Chinese
encoding and truncation for the selected client remain unconfirmed; these tests
do not establish that the old fixed-string algorithm is a valid client rule.

## Catalog/table contract
Not applicable: no new catalog roles, costs or item-type meanings.

## Validation and transaction rules
- Immutable wire snapshots capture only actual supplied items, names, raw scalars, copied relic values and actual timer reads. Do not create replacement domain items or owners. Leave source item/array references unchanged.
- Whole ItemCreate/ItemView frames prepare in genuine scratch storage before target publication. Incremental warehouse header/rows prepare complete segments and preserve valid cursor/Size ownership.
- SendItemLocked retains optional early returns for nonlocked/foreign-server cases. Required real client/player/server identity, date and complete output must validate before changing Locked/Mode. Prepare an update frame with local override values; only a successful preparation commits the real item transition. Keep the timer and unrelated data unchanged.
- Show retains missing-bank/already-shown early returns without inventing owners. An active show binds the actual User/Player.Owner/Warehouse, map/bank references and canonical actual key/UID/item/WH_ID relationships.
- Capture the actual bank order and prepare all four-item warehouse, lock/update and extra frames before changing item fields, extra structs, shown membership, target packet or queues. Evaluate mutable extra expiry getters on local value copies. Preserve warehouse -> item locks -> extra order for each batch.
- Revalidate actual owner/map/membership and observed item state before commitment. This is bounded cooperating/exclusive-caller safety; public field writes have no global transaction/lifetime lock.
- Retain the first actual item/server/player identity witnesses across preparation; never recapture a newer matching server ID pair as the original admission. Preserve Source item/relic-array aliases. The bank UID>0/key rule follows the existing native save-graph membership invariant; no donation OwnerUID field is treated as inventory ownership.
- Prepare marker capacity and final target output before no-throw model/marker commits, then call the actual existing Send. The real native sender caps frames at1032 and may filter/catch failures. Refuse an undeliverable source batch before state mutation; do not shrink, drop rows, mark shown or call that successful delivery.
- After commitment, sender failure is not an uncommitted rejection and must not trigger a fictional rollback. No crash, SQL, delivery-ack or complete multi-object atomicity claim is made.
- Concrete unresolved delivery frontier: the unchanged four-row warehouse encoding with64 relics per row produces1864 bytes, beyond the existing1032 sender cap. It is refused before mutations. This is source-format evidence, not full persistence/client acceptance of that relic count; no smaller replacement batch is invented.

## Expected wire behavior and fixtures
- Keep genuine default/full item, distinct receiver timer, conditional view timer, fixed-name encoding, sealed, row and warehouse order controls.
- Replace null-prefix/oversized relic, unlock-before-failure and shown-marker-before-failure requirements with unchanged real item fields, array identity, whole target bytes/cursor/Size, shown membership and empty queue.
- Owned regression fixtures cover late invalid rows/extras/lock output, canonical bank ownership, exact/short capacities, no replay on refused show, and repair/retry after refusal. No unsafe old native/OOB execution is authorized.
- Three focused test partials author25 unrun outcomes:8 item/lock,4 view and13 warehouse controls. Existing independent valid frames and the prior five test-file case counts remain; malformed warehouse wrap inputs are replaced with equivalent representable inputs for the exact same golden bytes.

## Evidence alignment
| Requirement | Existing evidence | Status |
| --- | --- | --- |
| No partial item/view output | Current builders write before null names/relic/capacity failure | Confirmed defect |
| No premature unlock | SendLocked assigns Locked/Mode before ItemCreate | Confirmed defect |
| No poisoned shown marker | Show adds IsShow before serialization; current test requires no retry | Confirmed defect |
| Real identity and sender limits | Actual warehouse AddItem/save owner checks; actual GameClient/Socket Send | Reuse actual evidence, not placeholder authority |
| Durable transactions/delivery | Public native fields and swallowed/filtered Send failures | Still unproved |

## Implementation and qualification
- Root approved five existing production files, five focused new modules, five existing test files, three new test partials, this spec and FSB-219 fragment.
- Source-only: no restore/build/test/dotnet/native/client/process/SQL/live configuration/save/route or Git mutation. Root reviews and qualifies the combined source.
- This package does not activate native warehouse handlers, replace a persistence owner, claim full client acceptance or complete parity.
