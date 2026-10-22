using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    // Exact native ItemType.unabletradeitem table (ItemType.cs328), not a
    // general item-category rule or proof of Inventory.Update's other effects.
    [Theory]
    [InlineData(750_000U)]
    [InlineData(722_700U)]
    [InlineData(3_306_885U)]
    [InlineData(3_307_142U)]
    [InlineData(3_600_025U)]
    [InlineData(3_200_000U)]
    [InlineData(729_549U)]
    [InlineData(3_008_994U)]
    [InlineData(3_200_005U)]
    [InlineData(3_600_029U)]
    [InlineData(3_007_108U)]
    [InlineData(3_007_109U)]
    [InlineData(3_007_110U)]
    [InlineData(3_200_004U)]
    [InlineData(3_600_031U)]
    [InlineData(3_600_027U)]
    [InlineData(3_600_024U)]
    public async Task UnableTradeUnboundDropConsumesWholeStackUidWithoutGroundObject(uint itemId)
    {
        const uint uid = 1_299_501;
        SeedUnableTradeDrop(uid, itemId);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var dropped = Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        Assert.Equal(itemId, dropped.ItemId);
        Assert.Equal((byte)0, dropped.Bound);
        Assert.Equal((ushort)3, dropped.StackSize);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var expected = before.PlayerItems with
        { InventoryItems = before.PlayerItems.InventoryItems.Where(item => item.Uid != SoldUid).ToArray() };
        var first = player.Sent.Count;
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));

        await DropPinItem(owner, player, SoldUid);

        AssertWholeDropItemImage(uid, before, Current(owner, player), expected, characterBytes);
        AssertExactDropRemoval(Assert.Single(player.Sent.Skip(first)));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, Current(owner, player)));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));

        // Replaying this consumed UID cannot remove anything else or create a
        // delayed ground item after the first full-stack removal.
        var removed = Current(owner, player);
        var savedItems = File.ReadAllBytes(ItemPath(uid));
        first = player.Sent.Count;
        await DropPinItem(owner, player, SoldUid);
        Assert.Same(removed, Current(owner, player));
        Assert.Empty(player.Sent.Skip(first));
        Assert.Equal(savedItems, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData((byte)1, (ushort)0)]
    [InlineData((byte)0, (ushort)1)]
    public async Task LockedOrInscribedUnableTradeDropRetainsWholeItemBeforeConsumptionBranch(byte locked, ushort inscribed)
    {
        const uint uid = 1_299_511;
        SeedUnableTradeDrop(uid, 750_000, locked, inscribed);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var projectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
        var first = player.Sent.Count;

        await DropPinItem(owner, player, SoldUid);

        Assert.Same(before, Current(owner, player));
        Assert.Equal(projectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, player).PlayerItems));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal((ushort)3, Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == SoldUid).StackSize);
        Assert.Empty(player.Sent.Skip(first));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Fact]
    public async Task OrdinaryUnboundDropStillCreatesGroundObjectInsteadOfUnableTradeConsumption()
    {
        const uint uid = 1_299_521;
        SeedUnableTradeDrop(uid, ItemId);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var dropped = Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var expected = before.PlayerItems with
        { InventoryItems = before.PlayerItems.InventoryItems.Where(item => item.Uid != SoldUid).ToArray() };
        var first = player.Sent.Count;
        Assert.Equal((byte)0, dropped.Bound);
        Assert.Equal((ushort)3, dropped.StackSize);
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));

        await DropPinItem(owner, player, SoldUid);

        AssertWholeDropItemImage(uid, before, Current(owner, player), expected, characterBytes);
        var frames = player.Sent.Skip(first).ToArray();
        Assert.Equal(2, frames.Length);
        Assert.True(LegacyFloorItemPacket.TryParse(ReadFrame(frames[0]), out var floor));
        Assert.Equal(LegacyFloorItemDropType.Visible, floor.DropType);
        Assert.Equal(ItemId, floor.ItemId);
        Assert.Equal(uid, floor.ItemOwnerUid);
        AssertExactDropRemoval(frames[1]);
        var entry = Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal(before.Character.MapId, entry.MapId);
        Assert.Equal(floor, entry.Packet);
        Assert.NotNull(entry.Item);
        Assert.Same(dropped, entry.Item);
        Assert.Equal((ushort)3, entry.Item.StackSize);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private void SeedUnableTradeDrop(uint uid, uint itemId, byte locked = 0, ushort inscribed = 0)
    {
        Seed(uid);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var items = loader.LoadByCharacterUid(uid);
        var prior = Assert.Single(items.InventoryItems, item => item.Uid == SoldUid);
        var dropped = new PlayerItemSnapshotItem
        {
            Uid = prior.Uid, ItemId = itemId, Durability = prior.Durability,
            MaximumDurability = prior.MaximumDurability, Position = prior.Position,
            StackSize = 3, Bound = 0, Color = prior.Color, Plus = prior.Plus,
            Locked = locked, Inscribed = inscribed,
        };
        loader.SaveByCharacterUid(uid, items with
        { InventoryItems = items.InventoryItems.Select(item => item.Uid == SoldUid ? dropped : item).ToArray() });
    }

    private void AssertWholeDropItemImage(uint uid, WorldSession before, WorldSession after,
        PlayerItemSnapshot expected, byte[] characterBytes)
    {
        Assert.DoesNotContain(after.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        Assert.Equal(KeptUid, Assert.Single(after.PlayerItems.InventoryItems).Uid);
        Assert.Same(before.Character, after.Character);
        Assert.Equal(before with { PlayerItems = after.PlayerItems }, after);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(uid)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
    }

    private static void AssertExactDropRemoval(byte[] bytes) => Assert.Equal(
        new MsgItemUsagePacket(SoldUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), bytes);
}
