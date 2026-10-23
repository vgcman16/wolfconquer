namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Fact]
    public async Task PlumeCurrentLegacyCreatorOverflowDoesNotAdvanceCollection()
    {
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(
            objective0: 2, occupiedUid: uint.MaxValue);
        AssertPlumeRefusal(result.UpdatedSession, result.Sent, result.Log, [2U]);
        Assert.Contains("itemRewardPreparationOverflow", result.Log);
        Assert.Equal(uint.MaxValue, Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems).Uid);
    }
}
