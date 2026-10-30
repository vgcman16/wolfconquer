using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

// Corrected-only real-bank controls. No old oversized/OOB native baseline.
public sealed unsafe partial class LegacyNativeWarehousePublicationTests
{
    [Fact]
    public void MissingBankDoesNotRequireAnUnrelatedShownListUserOrStream()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null) { IsShow = null! };
        warehouse.Show(8, LegacyNativeMsgWarehouse.DepositActionID.Show, null!);
        Assert.Null(warehouse.User); Assert.Null(warehouse.IsShow); Assert.Empty(warehouse.ClientItems);
    }

    [Theory]
    [InlineData("extra-date")] [InlineData("unlock-date")]
    public Task LaterBatchFailurePreservesEarlierPreparedUnlocksExtrasAndAllPublicationState(string fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint uid = 1; uid <= 5; uid++) scope.Warehouse.AddItem(Item(uid), 8);
        var rows = scope.Warehouse.ClientItems[8].Values.ToArray();
        var first = rows[0]; var bad = rows[^1];
        first.Locked = 2; first.Mode = Flags.ItemMode.View;
        first.Refinary = new() { ItemUID=first.Uid,EffectID=724440,EffectDuration=0,Typ=LegacyNativeItemExtra.Typing.ExpireTime };
        if (fault == "extra-date")
            bad.Refinary = new() { ItemUID=bad.Uid,EffectID=724440,EffectDuration=1,AddedOn=DateTime.MaxValue,Typ=LegacyNativeItemExtra.Typing.ExpireTime };
        else { bad.Locked = 2; bad.Mode = Flags.ItemMode.Trade; bad.UnLockTimer = -1; }
        var arrays = rows.Select(item => item.RelicAttributes).ToArray();
        var oldRefinery = first.Refinary; byte[] before = WarehouseAllocation(scope.Packet);

        Assert.Throws<ArgumentOutOfRangeException>(() => scope.Warehouse.Show(8, LegacyNativeMsgWarehouse.DepositActionID.Show, scope.Packet));

        Assert.Equal(before, WarehouseAllocation(scope.Packet)); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue); Assert.Empty(scope.Warehouse.IsShow);
        Assert.Equal((byte)2, first.Locked); Assert.Equal(Flags.ItemMode.View, first.Mode); Assert.Equal(0, first.UnLockTimer);
        Assert.Equal(oldRefinery, first.Refinary);
        for (int index = 0; index < rows.Length; index++) Assert.Same(arrays[index], rows[index].RelicAttributes);
        if (fault == "extra-date") bad.Refinary.EffectDuration = 0;
        else bad.UnLockTimer = 0;
        scope.Warehouse.Show(8, LegacyNativeMsgWarehouse.DepositActionID.Show, scope.Packet);
        Assert.Equal(new uint[] { 8 }, scope.Warehouse.IsShow); Assert.NotEmpty(scope.Queue);
        Assert.Equal((byte)0, first.Locked); Assert.Equal(Flags.ItemMode.Update, first.Mode);
        Assert.Equal(LegacyNativeItemExtra.Typing.PermanentRefinery, first.Refinary.Typ);
        Assert.All(rows, item => Assert.Same(item, scope.Warehouse.ClientItems[8][item.Uid]));
    });

    [Theory]
    [InlineData("key")] [InlineData("warehouse-id")] [InlineData("zero-uid")] [InlineData("owner")]
    public Task ForeignOrMalformedActualBankMembershipCannotAcquireAShownMarker(string fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = Item(1); scope.Warehouse.AddItem(item, 8);
        var bank = scope.Warehouse.ClientItems[8];
        switch (fault)
        {
            case "key": bank.TryRemove(1, out _); bank.TryAdd(99, item); break;
            case "warehouse-id": item.WH_ID = 9; break;
            case "zero-uid": bank.TryRemove(1, out _); item.Uid = 0; bank.TryAdd(0, item); break;
            case "owner": scope.Client.Warehouse = new LegacyNativeWarehouseRuntime(scope.Client); break;
        }
        byte[] before = WarehouseAllocation(scope.Packet);
        Assert.Throws<InvalidOperationException>(() => scope.Warehouse.Show(8, LegacyNativeMsgWarehouse.DepositActionID.Show, scope.Packet));
        Assert.Equal(before, WarehouseAllocation(scope.Packet)); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue); Assert.Empty(scope.Warehouse.IsShow); Assert.Equal((byte)0, item.Locked);
    });

    [Fact]
    public Task SourceFourItemBatchBeyondTheRealSenderLimitRefusesWithoutShrinkingOrMarkingShown() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint uid = 1; uid <= 4; uid++)
        {
            var item = Item(uid); item.RelicAttributes = new LegacyNativeRelicAttribute[64]; scope.Warehouse.AddItem(item, 8);
        }
        var rows = scope.Warehouse.ClientItems[8].Values.ToArray();
        using var encoded = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
        encoded.WarehouseCreate(8, LegacyNativeMsgWarehouse.DepositActionID.Show, 0, 60, 4);
        foreach (var item in rows) encoded.AddItemWarehouse(item);
        encoded.FinalizeWarehouse(); Assert.Equal(1864, encoded.Size); // Source geometry is still four rows.
        byte[] before = WarehouseAllocation(scope.Packet);
        var failure = Assert.Throws<InvalidOperationException>(() => scope.Warehouse.Show(8, LegacyNativeMsgWarehouse.DepositActionID.Show, scope.Packet));
        Assert.Contains("1032", failure.Message);
        Assert.Equal(before, WarehouseAllocation(scope.Packet)); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue); Assert.Empty(scope.Warehouse.IsShow); Assert.Equal(4, scope.Warehouse.ClientItems[8].Count);
        Assert.All(rows, item => Assert.Equal(64, item.RelicAttributes.Length));
    });

    [Fact]
    public Task RealPreparedShowCannotCommitAfterItsBankMembershipIsRemoved() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = Item(1); item.Locked = 2; item.Mode = Flags.ItemMode.View;
        scope.Warehouse.AddItem(item, 8); var bank = scope.Warehouse.ClientItems[8];
        object context = WarehouseInvoke("CaptureShow", scope.Warehouse, 8u, LegacyNativeMsgWarehouse.DepositActionID.Show,
            scope.Warehouse.ClientItems, bank, scope.Warehouse.IsShow)!;
        using var plan = (IDisposable)WarehouseInvoke("PrepareShow", null, context, scope.Packet)!;
        byte[] before = WarehouseAllocation(scope.Packet);
        Assert.True(bank.TryRemove(1, out var removed)); Assert.Same(item, removed);
        Assert.Throws<InvalidOperationException>(() => WarehouseInvoke("CommitShow", scope.Warehouse, plan, scope.Packet));
        Assert.Equal(before, WarehouseAllocation(scope.Packet)); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue); Assert.Empty(scope.Warehouse.IsShow);
        Assert.Equal((byte)2, item.Locked); Assert.Equal(Flags.ItemMode.View, item.Mode);
    });

    [Theory]
    [InlineData(-1, 1)] [InlineData(65536, 1)] [InlineData(60, -1)] [InlineData(60, 65536)]
    public Task UnrepresentableWarehouseHeaderFieldsRefuseWithoutAValueWrappingPrefix(int capacity, int count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); byte[] before = WarehouseAllocation(scope.Packet);
        Assert.Throws<ArgumentOutOfRangeException>(() => scope.Packet.WarehouseCreate(8, LegacyNativeMsgWarehouse.DepositActionID.Show, 0, capacity, count));
        Assert.Equal(before, WarehouseAllocation(scope.Packet)); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue);
    });

    private static object? WarehouseInvoke(string method, object? target, params object[] arguments)
    {
        var member = typeof(LegacyNativeWarehouseRuntime).GetMethod(method,
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)!;
        try { return member.Invoke(target, arguments); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw;
        }
    }

    private static byte[] WarehouseAllocation(LegacyNativePacket packet)
    {
        var bytes = new byte[packet.Capacity]; Marshal.Copy((nint)packet.Memory, bytes, 0, bytes.Length); return bytes;
    }
}
