using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyShopCapacityTests
{
    [Theory]
    [InlineData(3U, 40, 1U, false)]
    [InlineData(4U, 40, 1U, false)]
    [InlineData(5U, 40, 1U, false)]
    [InlineData(6000U, 40, 1U, false)]
    [InlineData(6001U, 40, 1U, false)]
    [InlineData(6003U, 40, 1U, false)]
    [InlineData(3U, 41, 1U, false)]
    [InlineData(4U, 41, 1U, false)]
    [InlineData(5U, 41, 1U, false)]
    [InlineData(6000U, 41, 1U, false)]
    [InlineData(6001U, 41, 1U, false)]
    [InlineData(6003U, 41, 1U, false)]
    [InlineData(3U, 39, 1U, true)]
    [InlineData(4U, 39, 1U, true)]
    [InlineData(5U, 39, 1U, true)]
    [InlineData(6000U, 39, 1U, true)]
    [InlineData(6001U, 39, 1U, true)]
    [InlineData(6003U, 39, 1U, true)]
    [InlineData(3U, 38, 2U, true)]
    [InlineData(4U, 38, 2U, true)]
    [InlineData(5U, 38, 2U, true)]
    [InlineData(3U, 39, 2U, false)]
    [InlineData(4U, 39, 2U, false)]
    [InlineData(5U, 39, 2U, false)]
    [InlineData(3U, 39, 0U, true)]
    [InlineData(4U, 39, 0U, true)]
    [InlineData(5U, 39, 0U, true)]
    [InlineData(3U, 0, 100U, false)]
    [InlineData(4U, 0, 100U, false)]
    [InlineData(5U, 0, 100U, false)]
    [InlineData(6000U, 39, 100U, true)]
    [InlineData(6001U, 39, 100U, true)]
    [InlineData(6003U, 39, 100U, true)]
    public async Task RegisteredBuyRequiresCompleteModeledInventoryCapacity(
        uint shopId, int inventoryCount, uint requestedCount, bool admitted)
    {
        using var fixture = new Fixture(inventoryCount);
        await fixture.BuyAsync(shopId, requestedCount);
        if (!admitted)
        {
            fixture.AssertRefused();
            return;
        }

        var count = shopId < 6000 ? checked((int)(requestedCount == 0 ? 1 : requestedCount)) : 1;
        var added = fixture.Current.PlayerItems.InventoryItems.Skip(inventoryCount).ToArray();
        Assert.Equal(count, added.Length);
        Assert.Equal(count, added.Select(item => item.Uid).Distinct().Count());
        foreach (var item in added)
        {
            Assert.True(item.Uid > 0);
            Assert.DoesNotContain(fixture.Original.PlayerItems.InventoryItems, old => old.Uid == item.Uid);
            var expected = new PlayerItemSnapshotItem
            {
                Uid = item.Uid, ItemId = RewardId, Position = 0, StackSize = 1,
                Durability = 1, MaximumDurability = 1,
                Bound = shopId is 5 or 6003 ? (byte)1 : (byte)0,
            };
            Assert.Equal(
                PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = [expected] }),
                PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = [item] }));
        }
        fixture.AssertCommittedItems(added);
        AssertWalletAndWire(fixture, shopId, count, added);
    }

    private static void AssertWalletAndWire(Fixture fixture, uint shopId, int count,
        IReadOnlyList<PlayerItemSnapshotItem> added)
    {
        var original = fixture.Original.Character;
        var current = fixture.Current.Character;
        var cost = shopId == 3 ? 5L * count : shopId is 4 or 5 ? 7L * count : 10L;
        var expected = shopId switch
        {
            3 => original with { Money = original.Money - cost },
            4 => original with { ConquerPoints = original.ConquerPoints - cost },
            5 => original with { BoundConquerPoints = original.BoundConquerPoints - cost },
            _ => original,
        };
        Assert.Equal(expected with { Attributes = current.Attributes }, current);
        var attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase);
        var attribute = shopId switch
        {
            6000 => "ArenaCurrentHonor",
            6001 => "RacePoints",
            6003 => "ChampionPoints",
            _ => null,
        };
        if (attribute is not null)
            attributes[attribute] = (1000 - cost).ToString(CultureInfo.InvariantCulture);
        Assert.Equal(attributes.Count, current.Attributes.Count);
        foreach (var pair in attributes)
            Assert.Equal(pair.Value, current.Attributes[pair.Key]);

        var reloaded = fixture.ReloadCharacter();
        Assert.Equal(expected.Money, reloaded.Money);
        Assert.Equal(expected.ConquerPoints, reloaded.ConquerPoints);
        Assert.Equal(expected.BoundConquerPoints, reloaded.BoundConquerPoints);
        Assert.Equal(original.BankConquerPoints, reloaded.BankConquerPoints);
        Assert.Equal(original.Level, reloaded.Level);
        Assert.Equal(original.MinHitPoints, reloaded.MinHitPoints);
        foreach (var key in new[] { "RacePoints", "ArenaCurrentHonor", "ChampionPoints", "Sentinel" })
            Assert.Equal(attributes[key], reloaded.Attributes[key]);

        var cursor = 0;
        if (shopId == 6001)
            Assert.Equal(Scalar(LegacyMsgUpdateDataType.RaceShopPoints, 990), fixture.Sent[cursor++]);
        foreach (var item in added)
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(), fixture.Sent[cursor++]);
        if (shopId == 3)
            Assert.Equal(Scalar(LegacyMsgUpdateDataType.Money, checked((ulong)(1000 - cost))), fixture.Sent[cursor++]);
        if (shopId == 6000)
            Assert.Equal(new MsgArenaInfoPacket(0, MsgArenaInfoStatus.NotSignedUp,
                0, 0, 0, 0, 0, 990, ArenaStatisticsProvider.DefaultArenaPoints).ToFrameBytes(), fixture.Sent[cursor++]);
        if (shopId == 6003)
            Assert.Equal(new MsgGoldLeaguePointPacket(990, HistoryPoints: 0).ToFrameBytes(), fixture.Sent[cursor++]);
        Assert.Equal(cursor, fixture.Sent.Count);
    }

    private static byte[] Scalar(LegacyMsgUpdateDataType type, ulong value) =>
        new LegacyMsgUpdatePacket(OwnerUid, [new LegacyMsgUpdateEntry(type, value)]).ToFrameBytes();
}
