using WolfConquer.Core.World;
namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public Task VendorBirthRetainsRealOwnerAndNullLifecycleSlots()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var vendor = new LegacyNativeVendorRuntime(scope.Client);
            Assert.Same(scope.Client, vendor.Owner); Assert.Empty(vendor.Items);
            Assert.Null(vendor.HalkMeesaje); Assert.Null(vendor.VendorNpc);
            Assert.False(vendor.InVending); Assert.Equal(0u, vendor.VendorUID);
            Assert.Equal(20, LegacyNativeVendorRuntime.MaxItems);
            var nullOwner = new LegacyNativeVendorRuntime(null!);
            Assert.Null(nullOwner.Owner); Assert.NotNull(nullOwner.Items);
            Assert.Null(scope.Client.MyVendor); Assert.False(scope.Client.IsVendor);
            scope.Client.MyVendor = vendor; Assert.False(scope.Client.IsVendor);
            vendor.InVending = true; Assert.True(scope.Client.IsVendor);
        });
    }

    [Theory]
    [InlineData(1, 0, 0, 410000u)]
    [InlineData(0, 1, 0, 410000u)]
    [InlineData(0, 0, 1, 410000u)]
    [InlineData(0, 0, 255, 410000u)]
    [InlineData(0, 0, 0, 750000u)]
    public Task VendorRejectsExactNativeRestriction(byte bound, ushort inscribed, byte locked, uint id)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var vendor = new LegacyNativeVendorRuntime(scope.Client);
            var item = Item(10, id); item.Bound = bound; item.Inscribed = inscribed; item.Locked = locked;
            Assert.False(vendor.AddItem(item, LegacyNativeMsgItemView.ActionMode.Gold, 10));
            Assert.Empty(vendor.Items);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(255)]
    public Task VendorRetainsSameItemAndRawPriceAndUnknownCostMode(byte bound)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var vendor = new LegacyNativeVendorRuntime(scope.Client);
            var item = Item(12); item.Bound = bound;
            Assert.True(vendor.AddItem(item, (LegacyNativeMsgItemView.ActionMode)255, long.MinValue));
            Assert.Same(item, vendor.Items[12].DataItem);
            Assert.Equal(long.MinValue, vendor.Items[12].AmountCost);
            Assert.Equal((LegacyNativeMsgItemView.ActionMode)255, vendor.Items[12].CostType);
            Assert.False(vendor.AddItem(Item(12), LegacyNativeMsgItemView.ActionMode.Gold, 2));
            Assert.Same(item, vendor.Items[12].DataItem);
        });
    }

    [Theory]
    [InlineData(19, true)]
    [InlineData(20, false)]
    [InlineData(21, true)]
    public Task VendorCapacityUsesNativeEqualityOnly(int count, bool expected)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var vendor = new LegacyNativeVendorRuntime(scope.Client);
            for (uint i = 0; i < count; i++)
                Assert.True(vendor.Items.TryAdd(i, new LegacyNativeVendorRuntime.VendorItem { DataItem = Item(i) }));
            Assert.Equal(expected, vendor.AddItem(Item(1000), LegacyNativeMsgItemView.ActionMode.Gold, 1));
            Assert.Equal(count + (expected ? 1 : 0), vendor.Items.Count);
        });
    }

    [Fact]
    public Task VendorStopRemovesRealMapMemberAndBroadcastsBeforeClearingOwner()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var map = scope.Map(); Scope.Admit(map, scope.Client);
            var peerQueue = new Queue<byte[]>(); var peer = scope.Peer(18, peerQueue); Scope.Admit(map, peer, 65, 64);
            var vendor = new LegacyNativeVendorRuntime(scope.Client) { InVending = true, VendorUID = 100007 };
            var npc = new LegacyNativeSobNpcRuntime { ObjType = LegacyNativeMapObjectType.SobNpc, UID = vendor.VendorUID,
                Map = map.ID, X = 65, Y = 64, OwnerVendor = scope.Client };
            vendor.VendorNpc = npc; scope.Client.MyVendor = vendor;
            Assert.True(map.View.EnterMap(npc));
            Assert.True(vendor.AddItem(Item(10), LegacyNativeMsgItemView.ActionMode.Gold, 5));
            vendor.StopVending(scope.Packet);
            Assert.Equal(RemoveBytes(vendor.VendorUID), Assert.Single(scope.Queue));
            Assert.Equal(RemoveBytes(vendor.VendorUID), Assert.Single(peerQueue));
            Assert.Empty(vendor.Items); Assert.False(vendor.InVending); Assert.Null(scope.Client.MyVendor);
            Assert.Same(npc, vendor.VendorNpc); Assert.Equal(100007u, vendor.VendorUID);
            Assert.DoesNotContain(map.View.Roles(LegacyNativeMapObjectType.SobNpc, 64, 64), obj => ReferenceEquals(obj, npc));
        });
    }

    [Fact]
    public Task VendorStopFaultAfterPacketAndItemClearDoesNotFalselyCloseSession()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var map = scope.Map(); Scope.Admit(map, scope.Client);
            var vendor = new LegacyNativeVendorRuntime(scope.Client) { InVending = true, VendorUID = 100003 };
            scope.Client.MyVendor = vendor; Assert.True(vendor.AddItem(Item(10), LegacyNativeMsgItemView.ActionMode.Gold, 5));
            Assert.Throws<NullReferenceException>(() => vendor.StopVending(scope.Packet));
            Assert.Equal(RemoveBytes(100003), Assert.Single(scope.Queue));
            Assert.Empty(vendor.Items); Assert.True(vendor.InVending); Assert.Same(vendor, scope.Client.MyVendor);
        });
    }

    [Fact]
    public void VendorStopColdBranchDoesNotTouchOwnerOrPacket()
    {
        var vendor = new LegacyNativeVendorRuntime(null!);
        vendor.StopVending(null!);
        Assert.False(vendor.InVending);
        Assert.Throws<NullReferenceException>(() => vendor.AddItem(null!, LegacyNativeMsgItemView.ActionMode.Gold, 1));
    }
}
