using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyExchangeMaterialTests
{
    [Theory]
    [InlineData(1u, 5)]
    [InlineData(2u, 10)]
    [InlineData(3u, 15)]
    public async Task ExactSummedStockUsesOriginalPerDefinitionUpdateRemoveOrder(uint count, ushort available)
    {
        var original = Stock(101, ExchangeMaterialA, available);
        using var fixture = new Fixture(ExchangeRow(materials: [(ExchangeMaterialA, 2), (ExchangeMaterialA, 3)]), [original]);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile);
        await fixture.Buy(count);
        Assert.Equal(4, fixture.Sent.Count);
        var afterFirst = original.WithStackSize(checked((ushort)(available - (2 * count))));
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(afterFirst, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), fixture.Sent[0]);
        Assert.Equal(RemovalFrame(101), fixture.Sent[1]);
        var current = fixture.Current();
        var reward = Assert.Single(current.PlayerItems.InventoryItems);
        AssertRewardAndEcho(fixture, reward, count, frameOffset: 2);
        Assert.Equal(count, current.LegacyExchangeShopBuyCounts![444]);
        Assert.Equal(available, original.StackSize); Assert.Same(fixture.Original.Character, current.Character);
        Assert.Equal(character, File.ReadAllBytes(fixture.CharacterFile));
        var persisted = new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(11);
        Assert.DoesNotContain(persisted.InventoryItems, item => item.ItemId == ExchangeMaterialA);
        Assert.Equal(reward.Uid, Assert.Single(persisted.InventoryItems).Uid);
        Assert.Equal((ushort)count, persisted.InventoryItems[0].StackSize);
    }

    [Fact]
    public async Task AggregationDoesNotGroupOrReorderInterleavedDefinitionAndPhysicalStackDebits()
    {
        var a1 = Stock(101, ExchangeMaterialA, 3, bound: 1);
        var b = Stock(201, ExchangeMaterialB, 2);
        var a2 = Stock(102, ExchangeMaterialA, 5);
        using var fixture = new Fixture(ExchangeRow(materials:
            [(ExchangeMaterialA, 2), (ExchangeMaterialB, 1), (ExchangeMaterialA, 4)]), [a1, b, a2]);
        await fixture.Buy();
        Assert.Equal(6, fixture.Sent.Count);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(a1.WithStackSize(1), mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), fixture.Sent[0]);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(b.WithStackSize(1), mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), fixture.Sent[1]);
        Assert.Equal(RemovalFrame(101), fixture.Sent[2]);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(a2.WithStackSize(2), mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), fixture.Sent[3]);
        var current = fixture.Current();
        Assert.Equal((ushort)1, current.PlayerItems.InventoryItems[0].StackSize);
        Assert.Equal(201u, current.PlayerItems.InventoryItems[0].Uid);
        Assert.Equal((ushort)2, current.PlayerItems.InventoryItems[1].StackSize);
        Assert.Equal(102u, current.PlayerItems.InventoryItems[1].Uid);
        AssertRewardAndEcho(fixture, current.PlayerItems.InventoryItems[2], 1, 4);
        Assert.Equal((ushort)3, a1.StackSize); Assert.Equal((ushort)2, b.StackSize); Assert.Equal((ushort)5, a2.StackSize);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public async Task ExistingIdOnlyBoundAndZeroStackAsOneSemanticsArePreserved(byte firstBound)
    {
        var one = Stock(101, ExchangeMaterialA, 0, firstBound);
        var two = Stock(102, ExchangeMaterialA, 1, firstBound == 0 ? (byte)1 : (byte)0);
        using var fixture = new Fixture(ExchangeRow(materials: [(ExchangeMaterialA, 1), (ExchangeMaterialA, 1)]), [one, two]);
        await fixture.Buy();
        Assert.Equal(4, fixture.Sent.Count); Assert.Equal(RemovalFrame(101), fixture.Sent[0]); Assert.Equal(RemovalFrame(102), fixture.Sent[1]);
        AssertRewardAndEcho(fixture, Assert.Single(fixture.Current().PlayerItems.InventoryItems), 1, 2);
        Assert.Equal((ushort)0, one.StackSize); Assert.Equal((ushort)1, two.StackSize);
    }

    [Fact]
    public async Task DisabledZeroIdAndZeroAmountSlotsRemainNonRequirements()
    {
        using var fixture = new Fixture(ExchangeRow(monopoly: 0,
            materials: [(0, uint.MaxValue), (ExchangeMaterialA, 0)]), []);
        await fixture.Buy(count: 2);
        Assert.Equal(2, fixture.Sent.Count);
        var reward = Assert.Single(fixture.Current().PlayerItems.InventoryItems);
        AssertRewardAndEcho(fixture, reward, 2, 0, bound: 0);
    }

    private static byte[] RemovalFrame(uint uid) => new MsgItemUsagePacket(uid, 0,
        MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes();

    private static void AssertRewardAndEcho(Fixture fixture, PlayerItemSnapshotItem reward, uint count, int frameOffset, byte bound = 1)
    {
        Assert.NotEqual(0u, reward.Uid); Assert.Equal(ExchangeRewardId, reward.ItemId);
        Assert.Equal((ushort)321, reward.Durability); Assert.Equal((ushort)321, reward.MaximumDurability);
        Assert.Equal((ushort)0, reward.Position); Assert.Equal((ushort)count, reward.StackSize); Assert.Equal(bound, reward.Bound);
        // UID issuance is outside this material proof. Preserve every remaining
        // complete valid reward wire byte; the future Host has its own controls.
        var expected = new PlayerItemSnapshotItem { Uid = reward.Uid, ItemId = ExchangeRewardId,
            Durability = 321, MaximumDurability = 321, Position = 0, StackSize = (ushort)count, Bound = bound };
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(expected).ToFrameBytes(), fixture.Sent[frameOffset]);
        Assert.Equal(new LegacyExchangeShopBuyPacket(LegacyExchangeShopBuyType.Goods, 24702, 23, count, 444, []).ToFrameBytes(),
            fixture.Sent[frameOffset + 1]);
        var expectedImage = PlayerItemSnapshotLoader.ToLegacyBytes(fixture.Current().PlayerItems);
        Assert.Equal(expectedImage, File.ReadAllBytes(fixture.ItemFile));
        var reloaded = new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(11);
        Assert.Equal(expectedImage, PlayerItemSnapshotLoader.ToLegacyBytes(reloaded));
    }
}
