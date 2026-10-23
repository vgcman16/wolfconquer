using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task EarthForceTransactionValidAmountsAndCompleteWireOrderRemainUnchanged(ushort fragments)
    {
        var scene = await RunEarthForceTransactionAsync(fragments);
        Assert.Null(scene.Failure);
        Assert.NotNull(scene.Committed);
        Assert.Same(scene.Committed, scene.Current);
        AssertEarthForceTransactionOriginalAndFiles(scene);
        Assert.All(scene.PublicationOwners, owner => Assert.Same(scene.Committed, owner));
        var frames = scene.Sent.Where(IsEarthForceTransactionRewardFrame).ToArray();
        var expected = new List<byte[]>();
        if (fragments < 5)
            expected.Add(EarthForceTransactionItemGolden(fragments == 0 ? 10U : 1U,
                3_008_754, fragments == 0 ? (ushort)11 : (ushort)0,
                fragments == 0 ? (ushort)1 : (ushort)3, checked((ushort)(fragments + 1))));
        if (fragments is 4 or 5)
            expected.Add(new MsgItemUsagePacket(1, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes());
        if (fragments == 6)
            expected.Add(EarthForceTransactionItemGolden(1, 3_008_754, 0, 3, 1));
        if (fragments >= 4)
            expected.Add(EarthForceTransactionItemGolden(10, 3_008_745, 23, 1, 1));
        Assert.Equal(expected.Count, frames.Length);
        for (var index = 0; index < expected.Count; index++) Assert.Equal(expected[index], frames[index]);

        var items = scene.Current!.PlayerItems.InventoryItems;
        Assert.Same(scene.Original.PlayerItems.InventoryItems[0], items[0]);
        var remaining = items.Where(item => item.ItemId == 3_008_754).Sum(item => Math.Max(1, (int)item.StackSize));
        Assert.Equal(fragments < 4 ? fragments + 1 : fragments == 6 ? 1 : 0, remaining);
        if (fragments >= 4)
        {
            var earth = Assert.Single(items, item => item.ItemId == 3_008_745);
            Assert.Equal((ushort)1, earth.StackSize);
            Assert.Equal((byte)0, earth.Bound);
            Assert.Equal((ushort)23, earth.Durability);
            Assert.Equal((ushort)23, earth.MaximumDurability);
            Assert.Contains("converted=True", scene.Log);
        }
        else Assert.DoesNotContain(items, item => item.ItemId == 3_008_745);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task EarthForceTransactionConsumesExactlyFiveAcrossPhysicalRows(ushort fragments)
    {
        var scene = await RunEarthForceTransactionAsync(fragments, splitFragments: true);
        Assert.Null(scene.Failure);
        Assert.Same(scene.Committed, scene.Current);
        Assert.NotNull(scene.Committed);
        AssertEarthForceTransactionOriginalAndFiles(scene);
        var frames = scene.Sent.Where(IsEarthForceTransactionRewardFrame).ToArray();
        Assert.Equal(fragments == 4 ? 4 : 3, frames.Length);
        if (fragments == 4) Assert.Equal(EarthForceTransactionItemGolden(1, 3_008_754, 0, 3, 3), frames[0]);
        var firstRemoval = fragments == 4 ? 1 : 0;
        Assert.Equal(new MsgItemUsagePacket(1, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[firstRemoval]);
        if (fragments == 6) Assert.Equal(EarthForceTransactionItemGolden(2, 3_008_754, 0, 3, 1), frames[1]);
        else Assert.Equal(new MsgItemUsagePacket(2, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[firstRemoval + 1]);
        Assert.Equal(EarthForceTransactionItemGolden(10, 3_008_745, 23, 1, 1), frames[^1]);
        Assert.Single(scene.Current!.PlayerItems.InventoryItems, item => item.ItemId == 3_008_745);
        Assert.Equal(fragments == 6 ? 1 : 0, scene.Current.PlayerItems.InventoryItems
            .Where(item => item.ItemId == 3_008_754).Sum(item => (int)item.StackSize));
    }

    [Fact]
    public async Task EarthForceTransactionOneFreeSlotRetainsTheSourceConversionRule()
    {
        var scene = await RunEarthForceTransactionAsync(4, inventoryCount: 39);
        Assert.Null(scene.Failure);
        Assert.NotNull(scene.Committed);
        Assert.Equal(39, scene.Current!.PlayerItems.InventoryItems.Count);
        Assert.Single(scene.Current.PlayerItems.InventoryItems, item => item.ItemId == 3_008_745);
        Assert.Equal(3, scene.Sent.Count(IsEarthForceTransactionRewardFrame));
        AssertEarthForceTransactionOriginalAndFiles(scene);
    }

    [Fact]
    public async Task EarthForceTransactionFullBagKeepsFsb050TruthfulCapacityWording()
    {
        var scene = await RunEarthForceTransactionAsync(4, inventoryCount: 40);
        AssertEarthForceTransactionUnchanged(scene);
        Assert.Contains(ParseNpcReplies(scene.Sent), reply => reply.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=inventory-full", scene.Log);
    }

    [Fact]
    public async Task EarthForceTransactionRateMissDoesNotReadUnrelatedRewardMetadata()
    {
        var scene = await RunEarthForceTransactionAsync(4, "invalid-earth", rate: false);
        AssertEarthForceTransactionUnchanged(scene);
        Assert.Contains("Core.Rate(40)=false", scene.Log);
        Assert.DoesNotContain("reward-metadata-invalid-or-missing", scene.Log);
    }

    [Fact]
    public async Task EarthForceTransactionInactiveQuestDoesNotInventCapacityOrRewardOutput()
    {
        var scene = await RunEarthForceTransactionAsync(4, accepted: false, inventoryCount: 40);
        AssertEarthForceTransactionUnchanged(scene);
        Assert.DoesNotContain(ParseNpcReplies(scene.Sent), reply => reply.Text == "Please make 1 more space in your inventory.");
    }

    [Fact]
    public async Task EarthForceTransactionFragmentOnlyDoesNotRequireEarthMetadata()
    {
        var scene = await RunEarthForceTransactionAsync(3, "missing-earth");
        Assert.Null(scene.Failure);
        Assert.NotNull(scene.Committed);
        var frame = Assert.Single(scene.Sent, IsEarthForceTransactionRewardFrame);
        Assert.Equal(EarthForceTransactionItemGolden(1, 3_008_754, 0, 3, 4), frame);
        Assert.Equal((ushort)4, Assert.Single(scene.Current!.PlayerItems.InventoryItems, item => item.ItemId == 3_008_754).StackSize);
    }

    [Fact]
    public async Task EarthForceTransactionExistingFiveDoesNotRequireFragmentGrantMetadata()
    {
        var scene = await RunEarthForceTransactionAsync(5, "missing-fragment");
        Assert.Null(scene.Failure);
        Assert.NotNull(scene.Committed);
        Assert.Single(scene.Current!.PlayerItems.InventoryItems, item => item.ItemId == 3_008_745);
        Assert.Equal(2, scene.Sent.Count(IsEarthForceTransactionRewardFrame));
    }

    // Independent literal offsets from the existing full 2116 packet layout;
    // no projection through the production FromSnapshotItem/ToFrameBytes path.
    private static byte[] EarthForceTransactionItemGolden(uint uid, uint itemId,
        ushort durability, ushort mode, ushort stack)
    {
        var bytes = new byte[206];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0), 206);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2116);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), itemId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), durability);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), durability);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), mode);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), stack);
        return bytes;
    }
}
