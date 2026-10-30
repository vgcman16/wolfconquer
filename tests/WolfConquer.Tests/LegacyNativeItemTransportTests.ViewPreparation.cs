using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed unsafe partial class LegacyNativeItemTransportTests
{
    [Theory]
    [InlineData(211, false)] [InlineData(212, true)]
    public Task ItemViewUsesActualExactCapacityOrRetainsTheCompleteOldTarget(int capacity, bool fits) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var target = InitializedViewPacket(capacity);
        var item = ViewItem(); var relics = item.RelicAttributes; byte[] before = ItemViewAllocation(target);
        if (!fits)
        {
            Assert.Throws<InvalidOperationException>(() => target.ItemViewCreate(0x88776655, -1234567890123L, item, LegacyNativeMsgItemView.ActionMode.ViewEquip));
            Assert.Equal(before, ItemViewAllocation(target)); Assert.Equal(capacity - 1, target.Position); Assert.Equal(13, target.Size);
        }
        else
        {
            target.ItemViewCreate(0x88776655, -1234567890123L, item, LegacyNativeMsgItemView.ActionMode.ViewEquip);
            Assert.Equal(ViewBytes(), ItemViewAllocation(target)); Assert.Equal(212, target.Size); Assert.Equal(4, target.Position);
        }
        Assert.Same(relics, item.RelicAttributes); Assert.Equal((ushort)0x1234, item.Position); Assert.Equal(2u, item.Activate);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task OversizedViewRelicsRefuseWithoutChangingTheItemOrTarget() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = ViewItem(); item.RelicAttributes = new LegacyNativeRelicAttribute[5000];
        var relics = item.RelicAttributes; byte[] before = ItemViewAllocation(scope.Packet);
        Assert.Throws<InvalidOperationException>(() => scope.Packet.ItemViewCreate(1, 0, item, LegacyNativeMsgItemView.ActionMode.ViewEquip));
        Assert.Equal(before, ItemViewAllocation(scope.Packet)); Assert.Equal(211, scope.Packet.Position); Assert.Equal(333, scope.Packet.Size);
        Assert.Same(relics, item.RelicAttributes); Assert.Equal((ushort)0x1234, item.Position); Assert.Equal(2u, item.Activate);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task DisposedViewTargetCannotMutateTheRealSourceItem() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var target = InitializedViewPacket(212); var item = ViewItem();
        var relics = item.RelicAttributes; nuint cursor = (nuint)target.stream; target.Dispose();
        Assert.Throws<ObjectDisposedException>(() => target.ItemViewCreate(1, 0, item, LegacyNativeMsgItemView.ActionMode.ViewEquip));
        Assert.Equal(cursor, (nuint)target.stream); Assert.Equal(13, target.Size); Assert.Equal((nuint)0, (nuint)target.Memory);
        Assert.Same(relics, item.RelicAttributes); Assert.Equal((ushort)0x1234, item.Position); Assert.Empty(scope.Queue);
    });

    private static LegacyNativePacket InitializedViewPacket(int capacity)
    {
        var packet = new LegacyNativePacket(capacity);
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, capacity).ToArray(), 0, (nint)packet.Memory, capacity);
        packet.Size = 13; packet.Seek(capacity - 1); return packet;
    }

    private static byte[] ItemViewAllocation(LegacyNativePacket packet)
    {
        var bytes = new byte[packet.Capacity]; Marshal.Copy((nint)packet.Memory, bytes, 0, bytes.Length); return bytes;
    }
}
