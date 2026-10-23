using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyLotteryExchangeMutationTests
{
    [Theory]
    [InlineData((ushort)2, uint.MaxValue, "inventory-full-after-planned-ticket-removal")]
    [InlineData((ushort)1, uint.MaxValue, "item-uid-exhausted")]
    [InlineData((ushort)2, 1_000U, "inventory-full-after-planned-ticket-removal")]
    public async Task MarketChecksCapacityAfterExactPlannedDebitBeforeCreatingReward(
        ushort ticketStack,
        uint unrelatedUid,
        string expectedReason)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var usersRoot = CreateUsersRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, LotteryTicketItemId, ticketStack),
            CreateInventoryItem(unrelatedUid, 720_001),
        };
        inventory.AddRange(Enumerable.Range(20, 38).Select(index =>
            CreateInventoryItem((uint)index, checked(820_000U + (uint)index))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var originalRows = originalItems.InventoryItems.ToArray();
        var originalImage = PlayerItemSnapshotLoader.ToLegacyBytes(originalItems);
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        WriteUserSnapshot(usersRoot, CharacterUid, conquerPoints: 25);
        var itemPath = Path.Combine(
            playerItemsRoot,
            CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var originalItemFile = File.ReadAllBytes(itemPath);
        var originalCharacterFile = File.ReadAllBytes(GetUserSnapshotPath(usersRoot));

        Assert.Equal(40, originalItems.InventoryItems.Count);
        Assert.Equal(ticketStack, originalItems.InventoryItems[0].StackSize);
        Assert.Equal((byte)0, originalItems.InventoryItems[0].Bound);
        Assert.Equal(originalImage, originalItemFile);

        var result = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket", durability: 77)),
            playerItemsRoot,
            usersRoot,
            conquerPoints: 25,
            seedPlayerItems: false);

        AssertMutationFree(result, originalItems, conquerPoints: 25);
        Assert.Same(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(0, result.UpdatedSession.Character.Money);
        Assert.Single(result.UpdatedSession.Character.Attributes);
        Assert.Equal("25", result.UpdatedSession.Character.Attributes["ConquerPoints"]);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        for (var index = 0; index < originalRows.Length; index++)
        {
            Assert.Same(originalRows[index], result.UpdatedSession.PlayerItems.InventoryItems[index]);
        }

        Assert.Equal(originalImage, PlayerItemSnapshotLoader.ToLegacyBytes(originalItems));
        Assert.Equal(originalImage, PlayerItemSnapshotLoader.ToLegacyBytes(result.UpdatedSession.PlayerItems));
        Assert.Equal(originalItemFile, File.ReadAllBytes(itemPath));
        Assert.Equal(originalCharacterFile, File.ReadAllBytes(GetUserSnapshotPath(usersRoot)));
        AssertPersistedEquals(playerItemsRoot, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("source=NpcHandler.MarketLadyLuck", result.Log);
        Assert.Contains("sourceLines=40295-40338", result.Log);
        Assert.Contains("reason=" + expectedReason + ";", result.Log);
        Assert.DoesNotContain("WorldServer applied", result.Log);
        if (ticketStack > 1)
        {
            Assert.Contains("no ticket was consumed and no reward was granted", result.Log);
            Assert.DoesNotContain("reason=item-uid-exhausted", result.Log);
        }
        else
        {
            Assert.Contains("no ticket was consumed and no mutation packet was sent", result.Log);
            Assert.DoesNotContain("reason=inventory-full-after-planned-ticket-removal", result.Log);
        }
    }
}
