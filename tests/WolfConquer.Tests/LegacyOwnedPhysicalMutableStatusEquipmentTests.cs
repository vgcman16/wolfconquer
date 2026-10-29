using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedPhysicalMutableStatusEquipmentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectOverlayChangedStatEquipKeepsBothOldFieldsUntilItsRealQueryPoint(bool admitted)
    {
        var result = await DirectOverlayEquipmentRunAsync("distinct", admitted);
        Assert.True(result.ItemCallbackRan);
        Assert.True(result.AcceptedItemsObservedBeforeQuery);
        Assert.Equal(60U, result.BeforeQueryStatus.Breakthrough);
        Assert.Equal(50U, result.BeforeQueryStatus.Counteraction);
        Assert.Equal(10_000, result.BeforeQueryStamp); // Neither materialization nor first item send renews it.
        Assert.Equal(80U, result.RawAfterMutation[LegacyPlayerStatusPacket.BreakthroughIndex]);
        Assert.Equal(70U, result.RawAfterMutation[LegacyPlayerStatusPacket.CounteractionIndex]);
        Assert.Equal(admitted ? 80U : 60U, result.AfterMutationStatus.Breakthrough);
        Assert.Equal(admitted ? 70U : 50U, result.AfterMutationStatus.Counteraction);
        Assert.Equal(admitted ? 16_000 : 10_000, result.AfterMutationStamp);
        Assert.Equal(admitted ? 0 : 1, result.OperationWaitMessages);
        Assert.Equal(80U, result.FinalStatus.Breakthrough);
        Assert.Equal(70U, result.FinalStatus.Counteraction);
        Assert.Equal(16_000, result.FinalStamp);
        Assert.Equal(91_003U, Assert.Single(result.Owner.PlayerItems.EquipmentItems, item => item.Position == 1).Uid);
        Assert.Contains(result.Owner.PlayerItems.InventoryItems, item => item.Uid == 91_001);
    }

    [Theory]
    [InlineData("normal-equip-while-alternate", 60U, 50U, true)]
    [InlineData("inactive-alternate-equip", 60U, 75U, false)]
    [InlineData("active-alternate-equip", 80U, 70U, true)]
    [InlineData("inactive-alternate-unequip", 60U, 50U, true)]
    [InlineData("normal-unequip-while-alternate", 60U, 50U, true)]
    public async Task DirectOverlayEquipmentQueryPredicatesRetainTheSourceEquipUnequipAsymmetry(
        string scenario, uint expectedBreakthrough, uint expectedCounteraction, bool queried)
    {
        var result = await DirectOverlayEquipmentRunAsync(scenario, admitted: true);
        Assert.True(result.ItemCallbackRan);
        Assert.True(result.AcceptedItemsObservedBeforeQuery);
        Assert.Equal(60U, result.BeforeQueryStatus.Breakthrough);
        Assert.Equal(75U, result.BeforeQueryStatus.Counteraction); // Earned by an actual prior Imp receive.
        Assert.Equal(10_000, result.BeforeQueryStamp);
        Assert.Equal(expectedBreakthrough, result.AfterMutationStatus.Breakthrough);
        Assert.Equal(expectedCounteraction, result.AfterMutationStatus.Counteraction);
        Assert.Equal(queried ? 16_000 : 10_000, result.AfterMutationStamp);
        Assert.Equal(0, result.OperationWaitMessages);
        var equipment = result.Owner.PlayerItems.EquipmentItems;
        switch (scenario)
        {
            case "normal-equip-while-alternate":
                Assert.Equal(91_003U, Assert.Single(equipment, item => item.Position == 1).Uid);
                Assert.Equal(91_002U, Assert.Single(equipment, item => item.Position == 51).Uid);
                break;
            case "inactive-alternate-equip":
            case "active-alternate-equip":
                Assert.Equal(91_003U, Assert.Single(equipment, item => item.Position == 51).Uid);
                break;
            case "inactive-alternate-unequip":
                Assert.DoesNotContain(equipment, item => item.Position == 51);
                Assert.Contains(result.Owner.PlayerItems.InventoryItems, item => item.Uid == 91_002);
                break;
            case "normal-unequip-while-alternate":
                Assert.DoesNotContain(equipment, item => item.Position == 1);
                Assert.Contains(result.Owner.PlayerItems.InventoryItems, item => item.Uid == 91_001);
                break;
        }
        // Inactive alternate EQUIP alone skips QueryEquipment. Normal Equip
        // still queries while alternate is selected, and BOTH Unequips query.
    }

}
