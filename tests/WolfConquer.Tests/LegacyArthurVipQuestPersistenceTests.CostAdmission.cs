using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyArthurVipQuestPersistenceTests
{
    [Theory]
    [InlineData((byte)1, 0)]
    [InlineData((byte)1, 1)]
    [InlineData((byte)1, 2)]
    [InlineData((byte)1, 3)]
    [InlineData((byte)2, 0)]
    [InlineData((byte)2, 1)]
    [InlineData((byte)2, 2)]
    [InlineData((byte)2, 3)]
    [InlineData((byte)3, 0)]
    [InlineData((byte)3, 1)]
    [InlineData((byte)3, 2)]
    [InlineData((byte)3, 3)]
    [InlineData((byte)4, 0)]
    [InlineData((byte)4, 1)]
    [InlineData((byte)4, 2)]
    [InlineData((byte)4, 3)]
    public async Task EveryChainUsesOnlyPositiveUnboundCostsInLowestUidOrder(byte optionId, int shape)
    {
        var costs = GetCosts(optionId);
        var rows = new List<PlayerItemSnapshotItem>();
        var consumedUids = new List<uint>();
        for (var index = 0; index < costs.Count; index++)
        {
            var cost = costs[index];
            var firstUid = checked((uint)(30 + index * 100));
            consumedUids.Add(firstUid);
            if (shape == 0)
            {
                // The higher eligible UID is physically first; selection must
                // retain the existing lowest-UID policy, not list order.
                rows.Add(CreateCostControlItem(firstUid + 10, cost.ItemId, (ushort)(cost.Count + 5), 0));
                rows.Add(CreateCostControlItem(firstUid - 20, cost.ItemId, cost.Count, 1));
                rows.Add(CreateCostControlItem(firstUid - 10, cost.ItemId, 0, 0));
                rows.Add(CreateCostControlItem(firstUid, cost.ItemId, cost.Count, 0));
            }
            else
                rows.Add(CreateCostControlItem(firstUid, cost.ItemId,
                    shape == 2 ? (ushort)0 : cost.Count, shape == 2 ? (byte)0 : shape == 3 ? (byte)255 : (byte)1));
        }
        var originalItems = CreatePlayerItems(rows.ToArray());
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalItems);
        var rewardItemId = optionId switch
        {
            1 => GhostHornItemId, 2 => EvilToothItemId, 3 => ImpureVigorItemId, 4 => VipTokenItemId,
            _ => throw new ArgumentOutOfRangeException(nameof(optionId)),
        };
        var successText = optionId switch
        {
            1 => "You successfully exchange the (Moss,SoulAroma and DreamGras) for an GhostHorn!",
            2 => "You successfully exchange the GhostHorn for an EvilTooth!",
            3 => "You successfully exchange the EvilTooth for an ImpureVigor!",
            _ => null,
        };
        var playerItemsRoot = CreatePlayerItemsRoot();
        var result = await InvokeAsync(originalItems,
            CreateWorldDataCatalog(CreateItemType(rewardItemId, "ArthurReward", durability: 321)),
            playerItemsRoot, optionId);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalItems));
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        if (shape != 0)
        {
            AssertMutationFree(result, originalItems);
            AssertNoItemMutationFrames(result.Sent);
            Assert.Empty(result.ObserverSent);
            Assert.Contains("reason=missing-materials", result.Log);
            Assert.DoesNotContain("WorldServer applied LegacyNpc Arthur", result.Log);
            Assert.Equal(originalBytes, File.ReadAllBytes(Path.Combine(playerItemsRoot, CharacterUid + ".bin")));
            return;
        }
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == rewardItemId);
        Assert.Equal(321, reward.Durability);
        Assert.Equal(321, reward.MaximumDurability);
        Assert.Equal(1, reward.StackSize);
        Assert.Equal(0, reward.Bound);
        Assert.InRange(reward.Color, 3U, 8U);
        var expected = CreatePlayerItems(rows.Where(item => !consumedUids.Contains(item.Uid))
            .Append(reward).OrderBy(item => item.Uid).ToArray());
        AssertPlayerItemsEqual(expected, result.UpdatedSession.PlayerItems);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            File.ReadAllBytes(Path.Combine(playerItemsRoot, CharacterUid + ".bin")));
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        AssertSourceOrderedSuccessFrames(result, costs, reward, successText, optionId == 4, consumedUids);
    }

    private static PlayerItemSnapshotItem CreateCostControlItem(uint uid, uint itemId, ushort stackSize, byte bound) => new()
    {
        Uid = uid, ItemId = itemId, Durability = 71, MaximumDurability = 99,
        Position = 0, StackSize = stackSize, Bound = bound, Plus = 3,
        Bless = 5, Enchant = 7, Color = 6, SocketOne = 1, SocketTwo = 2,
    };

}
