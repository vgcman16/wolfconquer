using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedVendorListingIncarnationTests
{
    [Fact]
    public Task SameNativeNodeSurvivesUnrelatedRealReprojectionAndCanStillBeShownAndSold() =>
        Run(childInSash: true, damaged: false, async f =>
        {
            var booth = await StartAndList(f);
            var oldRow = Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == Listed);
            var oldChild = Assert.Single(oldRow.DepositedItems);
            var native = f.Current().NativeGameClient!.Inventory!.ClientItems[Listed];
            await Move(f, Extra, LegacyWarehouseAction.DepositItem);
            await Move(f, Extra, LegacyWarehouseAction.WithdrawItem);
            var current = Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == Listed);
            Assert.NotSame(oldRow, current); Assert.NotSame(oldChild, Assert.Single(current.DepositedItems));
            Assert.Same(native, f.Current().NativeGameClient!.Inventory!.ClientItems[Listed]);
            oldRow.OwnerName = "detached-only"; oldChild.Signature = "detached-only";
            Assert.NotEqual(oldRow.OwnerName, current.OwnerName);
            Assert.NotEqual(oldChild.Signature, Assert.Single(current.DepositedItems).Signature);
            AssertActors(f);
            await AssertShown(f, booth);
            await AssertSold(f, booth);
        });

    [Fact]
    public Task RealWarehouseRoundTripReplacesNativeNodeAndOldListingRefusesUntilRemovedAndRelisted() =>
        Run(childInSash: true, damaged: false, async f =>
        {
            var booth = await StartAndList(f);
            var row = Assert.Single(f.Current().PlayerItems.InventoryItems, item => item.Uid == Listed);
            var native = f.Current().NativeGameClient!.Inventory!.ClientItems[Listed];
            var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = [row] });
            await Move(f, Listed, LegacyWarehouseAction.DepositItem);
            await Move(f, Listed, LegacyWarehouseAction.WithdrawItem);
            var returned = Assert.Single(f.Current().PlayerItems.InventoryItems, item => item.Uid == Listed);
            Assert.NotSame(row, returned);
            Assert.NotSame(native, f.Current().NativeGameClient!.Inventory!.ClientItems[Listed]);
            Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = [returned] }));
            AssertActors(f);
            await AssertStaleRefused(f, booth);

            var start = f.ActorFrames.Count;
            await f.SendAsync(Fixture.Actor, Remove().ToFrameBytes());
            Assert.Equal(Remove().ToFrameBytes(), Assert.Single(f.ActorFrames.Skip(start)));
            Assert.False(f.Current().LegacyVendorListings!.Contains(Listed));
            start = f.ActorFrames.Count;
            await f.SendAsync(Fixture.Actor, Listing().ToFrameBytes());
            Assert.Equal(Listing().ToFrameBytes(), Assert.Single(f.ActorFrames.Skip(start)));
            Assert.True(f.Current().LegacyVendorListings!.Contains(Listed));
            await AssertShown(f, booth);
            await AssertSold(f, booth);
        });

    [Fact]
    public Task RealSashDepositChangesListedChildClosureAndOldListingCannotShowOrSell() =>
        Run(childInSash: false, damaged: false, async f =>
        {
            var booth = await StartAndList(f);
            var beforeNode = f.Current().NativeGameClient!.Inventory!.ClientItems[Listed];
            await f.SendAsync(Fixture.Actor, new LegacyWarehousePacket(Listed, LegacyWarehouseAction.ShashDepositItem,
                Child, 0, []).ToFrameBytes());
            var sash = Assert.Single(f.Current().PlayerItems.InventoryItems, item => item.Uid == Listed);
            Assert.Equal(Child, Assert.Single(sash.DepositedItems).Uid);
            Assert.DoesNotContain(f.Current().PlayerItems.InventoryItems, item => item.Uid == Child);
            AssertActors(f);
            Assert.NotNull(beforeNode);
            await AssertStaleRefused(f, booth);
        });

    [Fact]
    public Task RealRepairChangesListedRootAndOldListingCannotShowOrSell() =>
        Run(childInSash: false, damaged: true, async f =>
        {
            var booth = await StartAndList(f);
            Assert.Equal((ushort)5, Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == Listed).Durability);
            await f.SendAsync(Fixture.Actor, new MsgItemUsagePacket(Listed, 0, MsgItemUsageAction.RepairItem, 1234, 0, 0, 0).ToFrameBytes());
            Assert.Equal((ushort)10, Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == Listed).Durability);
            Assert.Equal((ushort)10, f.Current().NativeGameClient!.Inventory!.ClientItems[Listed].Durability);
            AssertActors(f);
            await AssertStaleRefused(f, booth);
        });

    private static async Task AssertStaleRefused(Fixture f, uint booth)
    {
        var before = Before.Capture(f);
        Assert.True(before.Seller.LegacyVendorListings!.Contains(Listed));
        var logStart = f.Log.ToString().Length;
        await f.SendAsync(Fixture.Peer, Show(booth).ToFrameBytes());
        before.Unchanged(f);
        Assert.Contains("seller-listed-item-incarnation-changed", f.Log.ToString()[logStart..], StringComparison.Ordinal);
        logStart = f.Log.ToString().Length;
        await f.SendAsync(Fixture.Peer, Buy(booth).ToFrameBytes());
        before.Unchanged(f);
        Assert.Contains("seller-listed-item-incarnation-changed", f.Log.ToString()[logStart..], StringComparison.Ordinal);
        Assert.Same(before.Seller.LegacyVendorListings, f.Current().LegacyVendorListings);
        Assert.DoesNotContain(f.Current(Fixture.Peer).PlayerItems.InventoryItems, row => row.Uid == Listed);
    }

    private static async Task AssertSold(Fixture f, uint booth)
    {
        var before = Before.Capture(f);
        var item = Assert.Single(before.Seller.PlayerItems.InventoryItems, row => row.Uid == Listed);
        var request = Buy(booth);
        var delivered = new List<uint>();
        f.BeforeSend = (uid, _) =>
        {
            AssertActors(f);
            Assert.False(f.Current().LegacyVendorListings!.Contains(Listed));
            Assert.DoesNotContain(f.Current().PlayerItems.InventoryItems, row => row.Uid == Listed);
            Assert.Contains(f.Current(Fixture.Peer).PlayerItems.InventoryItems, row => row.Uid == Listed);
            delivered.Add(uid);
        };
        try { await f.SendAsync(Fixture.Peer, request.ToFrameBytes()); }
        finally { f.BeforeSend = null; }
        Assert.Equal(new[] { Fixture.Peer, Fixture.Peer, Fixture.Actor, Fixture.Actor, Fixture.Peer, Fixture.Actor }, delivered);
        Assert.False(f.Current().LegacyVendorListings!.Contains(Listed));
        Assert.DoesNotContain(f.Current().PlayerItems.InventoryItems, row => row.Uid == Listed);
        var received = Assert.Single(f.Current(Fixture.Peer).PlayerItems.InventoryItems, row => row.Uid == Listed);
        Assert.Equal(Child, Assert.Single(received.DepositedItems).Uid);
        Assert.Equal(before.Seller.Character.Money + 125, f.Current().Character.Money);
        Assert.Equal(before.Buyer.Character.Money - 125, f.Current(Fixture.Peer).Character.Money);
        var buyer = f.Frames(Fixture.Peer).Skip(before.BuyerFrames).ToArray();
        var seller = f.ActorFrames.Skip(before.SellerFrames).ToArray();
        Assert.Equal(3, buyer.Length); Assert.Equal(3, seller.Length);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(), buyer[0]);
        Assert.Equal(request.ToFrameBytes(), buyer[1]);
        Assert.Equal((request with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), seller[0]);
        Assert.Equal(new MsgItemUsagePacket(Listed, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), seller[1]);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Peer, [new(LegacyMsgUpdateDataType.Money,
            checked((ulong)f.Current(Fixture.Peer).Character.Money))]).ToFrameBytes(), buyer[2]);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Actor, [new(LegacyMsgUpdateDataType.Money,
            checked((ulong)f.Current().Character.Money))]).ToFrameBytes(), seller[2]);
        AssertActors(f);
        var committed = Before.Capture(f);
        await f.SendAsync(Fixture.Peer, request.ToFrameBytes());
        committed.Unchanged(f);
    }
}
