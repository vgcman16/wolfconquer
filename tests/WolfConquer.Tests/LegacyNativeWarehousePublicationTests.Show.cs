using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeWarehousePublicationTests
{
    [Fact]
    public Task FullShowMissingBankReturnsBeforeMarkersUserOrStreamAndEmptyBankSendsOneHeader() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); scope.Warehouse.Show(8,LegacyNativeMsgWarehouse.DepositActionID.Show,null!);
        Assert.Empty(scope.Warehouse.IsShow); Assert.Empty(scope.Queue); Assert.Equal(237,scope.Packet.Position);
        scope.Warehouse.ClientItems[8]=new(); scope.Warehouse.Show(8,LegacyNativeMsgWarehouse.DepositActionID.Show,scope.Packet);
        Assert.Equal(Frame(8,60,LegacyNativeMsgWarehouse.DepositActionID.Show),Assert.Single(scope.Queue));
        Assert.Equal(new uint[] {8},scope.Warehouse.IsShow);
        scope.Warehouse.Show(8,LegacyNativeMsgWarehouse.DepositActionID.Show,null!); Assert.Single(scope.Queue);
    });

    [Theory]
    [InlineData(1)] [InlineData(4)] [InlineData(5)] [InlineData(9)]
    public Task FullShowPublishesEveryActualItemInFourItemBatchesWithTheSourceSnapshotOrder(int count) => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); for(uint uid=1;uid<=count;uid++) scope.Warehouse.AddItem(Item(uid),8);
        uint[] order=scope.Warehouse.ClientItems[8].Values.Select(item=>item.Uid).ToArray();
        scope.Warehouse.Show(8,LegacyNativeMsgWarehouse.DepositActionID.Show,scope.Packet);
        var frames=scope.Queue.ToArray(); Assert.Equal((count+3)/4,frames.Length);
        for(int group=0;group<frames.Length;group++)
            Assert.Equal(Frame(8,60,LegacyNativeMsgWarehouse.DepositActionID.Show,order.Skip(group*4).Take(4).ToArray()),frames[group]);
        Assert.All(frames,bytes=>Assert.True(bytes.Length<=920)); Assert.Equal(count,scope.Warehouse.ClientItems[8].Count);
    });

    [Fact]
    public Task FullShowUsesTheNativePhysicalSashCapacityWithoutClampingToTheDeclaredMaximum() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); Assert.Equal((ushort)300,LegacyNativePlayerRuntime.MaxInventorySashCount);
        Assert.Equal((ushort)0,scope.Client.Player.InventorySashCount); scope.Client.Player.InventorySashCount=301;
        scope.Warehouse.AddItem(Item(1),scope.Client.Player.UID);
        scope.Warehouse.Show(scope.Client.Player.UID,LegacyNativeMsgWarehouse.DepositActionID.ShowInventorySash,scope.Packet);
        Assert.Equal(Frame(17,301,LegacyNativeMsgWarehouse.DepositActionID.ShowInventorySash,1),Assert.Single(scope.Queue));
        Assert.Equal((ushort)301,scope.Client.Player.InventorySashCount);
    });

    [Fact]
    public Task FullShowRetainsWarehouseThenUnlockThenExtraPacketOrderAndTrueMutableExtraTypes() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); var item=Item(1); item.Locked=2; item.UnLockTimer=0;
        item.Refinary=new LegacyNativeItemExtra.Refinery {ItemUID=1,EffectID=724440,EffectDuration=0};
        item.Purification=new LegacyNativeItemExtra.Purification {ItemUID=1,PurificationItemID=800001,PurificationDuration=0};
        scope.Warehouse.AddItem(item,8); scope.Warehouse.Show(8,LegacyNativeMsgWarehouse.DepositActionID.Show,scope.Packet);
        byte[][] frames=scope.Queue.ToArray(); Assert.Equal(3,frames.Length);
        Assert.Equal(new ushort[] {2080,2116,2134},frames.Select(bytes=>BitConverter.ToUInt16(bytes,2)));
        Assert.Equal((byte)2,frames[0][24+26]); Assert.Equal((byte)0,item.Locked);
        Assert.Equal(LegacyNativeGameItemFlags.ItemMode.Update,item.Mode);
        Assert.Equal(LegacyNativeItemExtra.Typing.PermanentRefinery,item.Refinary.Typ);
        Assert.Equal(LegacyNativeItemExtra.Typing.PurificationAdding,item.Purification.Typ);
        Assert.Same(item,scope.Warehouse.ClientItems[8][1]);
    });

    [Fact]
    public Task FullShowFaultKeepsTheNativeShownMarkerAndDoesNotPublishOrRetryTheBadBank() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); var item=Item(1); item.OwnerName=null!; scope.Warehouse.AddItem(item,8);
        Assert.Throws<NullReferenceException>(()=>scope.Warehouse.Show(8,LegacyNativeMsgWarehouse.DepositActionID.Show,scope.Packet));
        Assert.Empty(scope.Queue); Assert.Equal(new uint[] {8},scope.Warehouse.IsShow);
        Assert.Equal(88,scope.Packet.Position); Assert.Equal(321,scope.Packet.Size);
        item.OwnerName="Repaired fixture name";
        scope.Warehouse.Show(8,LegacyNativeMsgWarehouse.DepositActionID.Show,null!); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task FullShowStillMutatesMarkersAndExtraTypesWhenActualNativeSendFiltersAFakeClient() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); scope.Client.Fake=true; var item=Item(1);
        item.Purification=new LegacyNativeItemExtra.Purification {ItemUID=1,PurificationItemID=800001,PurificationDuration=0};
        scope.Warehouse.AddItem(item,8); scope.Warehouse.Show(8,LegacyNativeMsgWarehouse.DepositActionID.Show,scope.Packet);
        Assert.Empty(scope.Queue); Assert.Equal(new uint[] {8},scope.Warehouse.IsShow);
        Assert.Equal(LegacyNativeItemExtra.Typing.PurificationAdding,item.Purification.Typ);
        Assert.Equal((ushort)2134,BitConverter.ToUInt16(NativeSpellTestFixture.Bytes(scope.Packet),2));
    });
}
