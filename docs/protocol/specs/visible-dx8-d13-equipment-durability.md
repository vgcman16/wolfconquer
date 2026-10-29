# VisibleDx8D13: durable ordinary equipment moves

## Identity and scope
- Revision 3, 2026-10-28; isolated Sanitized Parity implementation with focused qualification. Public/full equipment qualification and client acceptance remain pending.
- Chinese7899 / VisibleDx8D13; current committed ItemUsage packet codec, equipment handler and shared decoded item catalog are source evidence. No new client observation is claimed.
- Ordinary inventory-to-equipment and equipment-to-inventory moves, including existing normal/alternate selection and displacement rules. No new item allocation, forge, trade, SQL, native item-storage or global stat-query transaction claim.

## Packet contract
- Existing opcode2137, Equip action4, Unequip action6. Preserve existing fixed87-byte payload and successful frame order.
| Field | Offset | Width | Meaning | Rule | Evidence |
| --- | --- | --- | --- | --- | --- |
| Id | 0 | uint32 LE | Inventory item UID for Equip | Actual current physical membership | MsgItemUsagePacket |
| DwParam | 4 | uint64 LE | Requested equip position / unequip slot | <=ushort.MaxValue; zero equip uses existing natural-slot resolver | Equipment.cs |
| Action | 12 | uint16 LE | 4 or6 | Existing parser only | MsgItemUsagePacket |
- Equip: remove-inventory, displaced unequip/item rows, alternate notification when applicable, equipped item row/update, existing equipment query. Unequip: unequip reply, inventory item row, existing query. No success frame precedes the real durable move.
- Query status2301, HP2176, perfection2128 and existing spawn/equipment phases retain their own current-owner checks and source order.

## Catalog/table contract
- Actual loaded WorldData ItemType row and existing ItemType required field indices2/4/5/6/7 (class/level/gender/strength/agility), decoded seed items.json.
- Missing/ambiguous catalog metadata refuses ordinary equip instead of skipping requirements. Preserve current natural/alternate/weapon rules; class1000 is not granted class1001 dual-wield authority.
- Bound and item-locked gear remains equippable where current position/class rules permit; trading restrictions are not invented as equip restrictions. Preserve durability and all other item fields, including zero/broken durability rather than inventing a repair requirement.
- First live candidate120003 LightNecklace, slot2: level7, other listed requirements0; body1008/class1000/level120 qualifies by source only.

## Validation and transaction rules
- Capture the actual current Store record, retained native issuance, physical client/player when present, actual configured pair-ready Recovery, real Users/account.ini and Items/characterUID.bin. No fake native/spell/ranking owner and no new registry opening.
- A real shared world lease preserves existing action4 consumable/non-equipment behavior. Ordinary equipment moves acquire Exclusive only after releasing Shared; then Trades, Store writer and actual file locks in that order. Never await transport under the exclusive/gates.
- Require current PIN admission, no ended connection, active trade/escrow or native trade, and full unique physical UID graph with unambiguous equipped positions. Inventory capacity is the existing40 physical slots: final inventory must be<=40, including unequip and all displaced rows.
- Freeze complete input item/attribute state before projecting. Preserve unrelated containers, balances, character fields, bound/lock state and raw item bytes. No authorization is consumed.
- Reuse genuine journal v2 SingleOwner: unchanged exact Character before/after at AccountId, changed Items before/after at character UID. Retain exact opaque Character and item bytes; alter only moved top-level item Position fields. Validate entire raw catalog and canonical row equivalence, not an invented empty saved image.
- Journal and verify actual afterimages before TryCommitSession publishes prepared native spell/proficiency maps and final Store record. Those maps are unchanged participants only; no optional spell/rank file root is required for an item-only move.
- Definite writer refusal proves both exact preimages; no memory or output publication. Pending/uncertain outcome fences the captured actual Recovery. Durable success followed by publication fault is a committed failure, never false unchanged rejection. No rollback/refund/replay on failed notification.
- Retain original issuance and accepted item graph across every output and query; never recapture authority from a replacement holder. Remove the late ordinary item save so query reentry cannot overwrite newer item state.

## Expected wire behavior and fixtures
- Real ordinary-admission/runtime fixture, genuine pre-bootstrap saved gear and registry/PIN ownership, actual connection sender/close and physical roots. This does not claim a live mailbox-to-equip observation. Current v7 ordinary admission requires AccountId==CharacterUID, so genuine Frame positives retain that constraint. A lower-writer distinct-ID component control can check Character/account versus Items/UID addressing separately; distinct-ID admitted acceptance remains pending and is not implemented by this batch.
- Positive equip/unequip must show exact saved item transition before first real connection output, with successful existing wire sequence. Cover displacement, alternate, class/gender/level/stat requirements and raw metadata preservation.
- Two-hand right-slot replacement clears both existing right and left rows in right-then-left notification order, including when the right slot was occupied. Final capacity includes both displaced rows.
- Refusals: no PIN/current owner/catalog/room; duplicate UID/position; stale external Character or Items; missing/wrong roots; real OS write denial. Assert no resource/file/holder/success change for definite refusal.
- Existing failure-only journal interruption points may test AfterPending/Character/Items; no callback may grant persistence acceptance. Recovery/replay and postcommit send failure retain truthful committed outcome.

## Evidence alignment
| Requirement | Catalog/source | Wire fixture | Status |
| --- | --- | --- | --- |
| Slots and basic kit admission | ItemType, Requirements, ClassRules, decoded seed rows | Existing Frame equipment goldens | Source-supported, new live proof pending |
| Durability before publication | Existing v2 journal/Store transaction | Genuine owned Frame tests in test07 | Focused pass; full/client pending |
| Raw bytes survive movement | Actual DBItem position offset12 and raw validator | Nonzero opaque-byte/UID controls in test07 | Focused pass; broader boundaries below |

## Serialized callback and equipment actions
- Actual equipment mutations require an exclusive world operation. Existing spell/query tails retain shared operation ownership; a sender callback cannot await a nested exclusive equipment mutation while that outer shared operation is still held. No shared-to-exclusive upgrade or guard waiver is introduced.
- Reachable follow-on tests queue a typed item intent until the outer Frame completes, or signal an independently created component producer which remains pending until Shared releases, then join its real operation. The qualified reentry/RootMagic fixtures replace old handmade nested-exclusive orderings with these serialized actions and retain their separate before/after arithmetic and wire observations.
- The actual fresh80fa53c9 candidate build05 has zero errors; the corrected callback method names and225/168 damage inputs were confirmed in the built test list. Actual equipment-reentry-rootmagic-03.trx passes all9 cases. The two-player XP case retains the real admitted workers, joins them on disposal and checks exact ordered full flag vectors256 then257, caused by Shield then the first hostile-cast FlashingName. The peer has legal HP-only gear and actual current/saved100000HP, without a fake alive owner. These tests do not prove same-connection TCP concurrency, the impossible old nested effects, or client rendering.

## Implementation and qualification
- The current candidate is a fresh public80fa53c9 Git archive at `.artifacts/root-equipment-integrated-next/candidate`, with72 explicit equipment/spec/test paths. This source batch has focused qualification only; FSB-178 remains OPEN pending its own full gate and live acceptance. No equipment deployment is claimed.
- Actual build05: zero errors/100 historical warnings. Actual `results/equipment-combined-04.trx`:161 passed,0 failed,0 skipped (4m3s). This includes the preserved owned durability/requirements/weapon/alternate/cache families, corrected9 serialized callback/RootMagic cases and the four genuine Store/v2 component controls. The9-case run and older127 selection overlap this result; do not add their counts.
- Earlier failures remain retained. New test01=3pass/6fail; actual fixtures exposed a peer whose real startup HP clamped to zero and an unreachable nested-equip critical/refinery expectation. Legitimate HP-only gear and source-derived serialized attack inputs corrected these. Test02=6pass/3fail used a stale incrementally reused callback binary; an explicit rebuild and built-name check prevented treating it as qualification. Its XP mismatch was independently traced to the real first hostile-cast FlashingName vector, retaining full ordered wire assertions.
- Earlier broad diagnostics in the old8e candidate had250 passed/34 Main gear failures/19 existing skips, then41 passed/21 direct failures. Actual `equipment-preserved-neighbors-05.trx` now records281 passed,0 failed and11 existing skips (1m1s) across surviving affected methods and neighboring fixture classes. The two private-seed-wide stat cases and nine existing consumable cases remain skipped. This overlaps other selections; do not add counts.
- The four passing `ActualStoreBoundaryEquipmentJournalRetainsDurableMoveWhenRawPublicationProofFails` rows use the real current Store, v2 writer and real recovery. Two controls deliberately invalidate a raw map only after actual durable success; two retain the valid graph. They establish the component's durable-history/fence/retirement boundary, not an injected failure through the actual Frame equipment writer/catch/log path. **Actual Frame postwriter fault injection remains OPEN.** Transport-failure cases are a separate boundary.
- Published testing tools are public80fa53c9. Tools-only full Windows40790881796 excluded FSB-178 and endedcancelled after the worker disappeared; its incomplete output cannot qualify equipment. A successor full gate is still required. The older qualified livebe2740ae World/client fixture is unchanged. No new live equip/unequip or Chinese7899 rendering acceptance is inferred from these tests.
- Stat-query sidecars and later derived-spell changes remain separate transactions. File journal v2 supports its existing process-restart reconciliation, not multi-process or machine-power-loss atomicity. SQL, native public item-storage closure, uncooperative external writers and distinct-ID ordinary admission remain outside this increment.
