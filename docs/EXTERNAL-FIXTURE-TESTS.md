# External fixture coverage

165 test methods (163 Facts and 2 Theories) require external game/seed data that is absent from this source snapshot. Of these, 150 previously returned without assertions when files were missing; another 15 explicitly required the external seed catalog. This snapshot marks all of them explicitly skipped, so a passing test report cannot count them as verified behavior. Theory case totals may differ from method totals.

The methods are retained for future fixture reconstruction. Re-enable a method only after replacing or supplying its required data and confirming that its assertions run. The source snapshot does not include the original external fixtures. Synthetic tests for the same subsystems remain active where they are independent of external data.

| Test file | Methods explicitly skipped |
|---|---:|
| `LegacyConsumableHealthPersistenceParityTests.cs` | 1 |
| `LegacyConsumableManaPersistenceParityTests.cs` | 2 |
| `LegacyConsumableStackPersistenceParityTests.cs` | 1 |
| `LegacyDaoQiPacketTests.cs` | 1 |
| `LegacyExchangeShopPacketTests.cs` | 4 |
| `LegacyHeroRewardPacketTests.cs` | 2 |
| `LegacyMonsterInstanceTests.cs` | 1 |
| `LegacyNpcPresenceParityTests.cs` | 6 |
| `MsgItemUsageNonEquipmentItemUseTests.cs` | 5 |
| `VisibleDx8D13TrainingVitalityBuyPointsTests.cs` | 5 |
| `VisibleDx8D13TrainingVitalityScoreTests.cs` | 3 |
| `VisibleDx8D13TrainingVitalityStudyTests.cs` | 3 |
| `WorldDataLoaderTests.cs` | 1 |
| `WorldServerFrameHandlerCombatGearGetPassiveTests.cs` | 3 |
| `WorldServerFrameHandlerTests.cs` | 127 |
