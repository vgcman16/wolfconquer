# D13 captured equipment query and cache permission boundary

## Identity and scope
- Revision1, 2026-10-31; approved Root repair of the public b8 full-run regression.
- Target: retained VisibleDx8D13 source-shaped equipment query; no new client activation or live acceptance claim.
- An actual query captures equipment and alternate selection before its AppendItems send. A reentrant legitimate toggle may change current selection during that send. Preserve the captured computation without treating its stale result as new WindWalker or Monk permission.

## Packet contract
- No layout/opcode changes. Existing AppendItems, status, HP and perfection ordering remains unchanged.
- The existing independent captured-alternate fixture retains its 60/50 breakthrough/counteraction and100000 HP oracles. No success is inferred from transport alone.

## Catalog/table contract
- No new table. Existing query catalog/selector and complete item snapshot remain authoritative calculation inputs.
- Captured computation is distinct from final effective equipped-weapon eligibility.

## Validation and transaction rules
- Before initializing a query epoch, retire the previous epoch/Monk capability as before.
- A captured source must match the actual current UID, account, native-client reference, class and10311 membership under the existing native identity gate.
- Only captured items/alternate selection may differ for this computation. Foreign source identity never initializes an epoch.
- WindWalker completion, Monk completion and final token checks retain exact current source-input and weapon-field equality. An intentionally stale captured computation grants neither capability.
- This repair does not make the legacy query's callback/reentrant phases globally atomic or undo earlier source-phase publication on a later failure.

## Expected wire behavior and fixtures
- Existing genuine component query toggles ALT to NORMAL inside its real AppendItems callback: outer status still derives captured ALT, current selection remains NORMAL, maxHP remains100000, and no stale194/cache permission is issued.
- Three negative API controls submit foreign captured UID/account/native-client reference under the actual fixture identity. Each refuses and retires the prior epoch without changing Store state or emitting packets.
- The component fixture has no admitted Monk binding: its Monk refusal is only an absence control. Existing actual admitted Monk drift tests retain the bound-owner coverage.

## Evidence alignment
| Requirement | Evidence | Status |
| --- | --- | --- |
| Capture before AppendItems | EquipmentQuery.Execution.CaptureEquipment and ExecuteLegacyEquipmentQueryAsync | Existing source contract |
| Same-source computation with later toggle | DirectQueryCapturedAlternateSurvivesActualToggleToNormalInsideReadonlyAppend | Independent existing success oracle retained |
| Current capability requires exact inputs | TryCompleteWindWalker, TryCompleteStaminaCache and final token proof | Retained unchanged |

## Implementation and qualification
- Production: LegacyNativeMagicQueryClient.cs and .WindWalker.cs; narrow computation-entry predicate only.
- Tests: LegacyEquipmentQueryCapturedAlternateTests.cs and existing DirectQueryStagesFixture.
- Source-only authored, UNBUILT/UNRUN. Root alone qualifies/integrates. Full query transaction, nativeFullLoading and7937 activation remain outside this repair.
