# Owned item repair transaction

## Identity and scope
Revision2, 2026-10-30. Authorized sanitized-parity gameplay milestone.
Target current server VisibleDx8D13/old7899; shared groundwork for7937 without
claiming new-client mapping/activation/live acceptance. The source-only successor
is rebased onto exact public `2ff18f1282d7f969d0533a8566ee5c08a28613eb`, the
three-file parity child of graph/GM source `133513eb6f9ccd1c0fa90618b791783e7bc9e2cc`.
Root's actual full Windows run41061642002 owns qualification of that base; it
does not execute this repair delta. All70 new repair outcomes and the one
migrated trade scenario remain unrun until Root's separate repair gates.
Both paid and zero-durability meteor RepairItem are required end to end. Keep
actual costs and public packet order; remove generic best-effort Set/postludes
from these two successful repair branches. Full SQL remains separate.

## Packet contract
Use existing MsgItemUsagePacket RepairItem action14 and existing codecs/goldens.
No new opcode/layout/encoding. Paid successful sequence remains two Money updates
then target item update. Zero-durability success retains genuine physical meteor/
tear debit frames in existing distribution order, then the repaired item update.
No success/resource output before actual persisted Store outcome. Delivery uses
actual canonical outfinal and occurs after Exclusive/Trades/Store/file gates.
A transport failure after commitment is notification failure, never refund/retry.

## Catalog/table contract
Use actual WorldDataCatalog/ItemType row and existing TryReadGoldWorth formula
inputs for paid repair. The current `ItemType.GoldWorthFieldIndex` is11, read as
one actual uint field from the unique matching row. Preserve its quality-based
cost formula; missing, ambiguous or malformed required row data refuses before
charging. Record exact required field/current row evidence and
reject malformed/missing required metadata before charging. Do not invent NPC,
range, maximum inventory or new cost policy. Meteor repair costs existing5 units
of1088001/1088002, preserving genuine physical distribution across stacks.
Unknown new7937 column meanings remain unknown and inactive.

## Validation and transaction rules
Capture the actual Store holder/native identity/connection/Player/Recovery/PIN
under existing WorldOperationBarrier Exclusive and Trades gate; obey current
native actor/order. Bound item graph and canonical session remain exact owners,
not client UID equality or a caller-provided acceptance function.
Prepare a detached repair result and all wire frames before writer. Validate
complete real saved graph, target ownership, durability/max state, funds, complete
actual material stacks, metadata, roots and current admission. Do not consume a
repair target as its own material; refuse destructive material roots with deposits.
Rejected operation leaves wallet/material/target/native/holder/files/success unchanged.
Use existing Store.TryCommitSession and genuine closed repair persistence. Native
plans precede writer. Keep exact character/item raw preimages under real ordered
file locks and use actual LegacyTradeJournal single-owner WAL. Paid repair changes
only allowed Money/durability; meteor changes only target durability and genuine
material stacks/removals, with unchanged character participant for identity.
Preserve raw184/80 record order/padding/opaque bytes of unrelated retained items;
do not use equipment position-only validator as repair authority or serialize a
whole normalized graph over a byte-preserved save. A real journal interruption/
postimage uncertainty retains actual pending/recovery identity. Prove preimages
before ordinary write refusal; committed/uncertain outcomes block actual Recovery,
retire actual owner where required and never masquerade as unchanged/refundable.
Generic other actions' best-effort persistence remains open. This is recoverable
owned-file persistence, not SQL/distributed/power-loss atomicity.

## Expected wire behavior and fixtures
Owned actual Frame/Store/native/container/catalog and saved fixtures; no fake
queues, accepting writer/validator, global metadata substitute or guessed UID
producer. Pre-issuer fixture data is permitted. Cover paid/meteor inventory and
equipment success; first response sees actual canonical native/holder and exact
character/item files; original5-stack distribution and two-Money goldens.
Refusal controls: insufficient funds/material, target-as-cost/deposits, metadata,
malformed tail, foreign/retired owner, raw native drift and changed saved inputs.
Real IO/journal interruption windows, duplicate/concurrent requests, recovery,
committed owner loss and transport failure must preserve actual outcome truth.
Do not weaken existing meaningful tests to accommodate an unsafe path.

## Evidence alignment
| Requirement | Source evidence | Status |
| --- | --- | --- |
| Existing repair costs/order | ItemUsage.InventoryActions + existing repair tests | observed source; preserve valid behavior |
| Native graph prewriter join | exact2ff18f12 PreparedSessionSpellPublication and genuine binding.Items | base fullCI pending; repair delta unrun |
| Existing durability gap | ItemUsage.InventoryActions repair sends/manual postlude | observed; must close for both repair branches |
| Raw complete owned files/journal | EquipmentMove/Health persistence and actual LegacyTradeJournal | reuse real mechanisms, distinct allowed mutation proof |
|7937/SQL/live client | matching future observations | unproven; no activation claim |

## Implementation and qualification
Three isolated author scopes: A actual repair persistence/raw delta proof + raw
owned tests; B actual capture/plan/transaction/delivery and exclusive dispatch;
C actual joined integration/failure controls. Send finite API/file proposals for
Root alignment before code. Root alone executes/builds/tests/intakes/commits/pushes.
No helper execution, SQL, service, client/updater, routing or original-data changes.
Preserve59held Guild/Union entries, original backups/restoreZIP and active host
execution hold. No new monoliths or duplicate builds/tests/CI. Root's current
full Windows run qualifies the published base only. This finite24-path delta
stays unqualified until Root reviews, compiles and runs its meaningful repair
controls and appropriate actual-commit gate after that run is terminal.

## Startup recovery custody limit

These raw WAL interruption controls are not proof of a complete owned-runtime
restart. Existing `LegacyOwnedAdmissionRuntime.TryOpen` and UID registry
producer admission refuse a pending game journal, while WorldServer opens the
runtime before Frame recovery. Real quiescent custody/recovery ownership remains
an open startup prerequisite. This rebase adds no `allowPending` option, fake
producer, alternate owner or accepting recovery callback to bypass that gap.
