using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemRemainingTimeTests
{
    [Theory]
    [InlineData(-3600L)]
    [InlineData(4294971392L)]
    public Task RealItemCreateReadsTheReceiverTimerAndPreservesTheExistingWireItemIdentity(long receiverSeconds) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); DateTime end = EndAfter(receiverSeconds);
        var receiver = new LegacyNativeGameItemRuntime { Uid=999,Activate=1,EndDate=end,TimeLeftInMinutes=123 };
        var supplied = new LegacyNativeGameItemRuntime { Uid=17,ITEM_ID=1000000,Activate=1,EndDate=DateTime.Now.AddHours(2) };
        DateTime before = DateTime.Now; receiver.ItemCreate(scope.Packet, supplied); DateTime after = DateTime.Now;
        byte[] bytes = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.Equal(206, bytes.Length); Assert.Equal((ushort)2116, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        Assert.Equal(17u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(1000000u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        AssertClockWindow(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(46)), end, before, after);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(50)));
        Assert.Equal(999u, receiver.Uid); Assert.Equal(end, receiver.EndDate); Assert.Equal(4, scope.Packet.Position);
        Assert.Empty(scope.Native.Queue);
    });

    [Theory]
    [InlineData(-3600L)]
    [InlineData(4294971392L)]
    [InlineData(-4294971392L)]
    public Task WholeItemViewRetainsTheNativeNonzeroWrappedTimerAndOmitsItsConditionalActivationWord(long seconds) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); DateTime end = EndAfter(seconds);
        var item = new LegacyNativeGameItemRuntime { Uid=17,ITEM_ID=1000000,Activate=1,EndDate=end,StackSize=7 };
        DateTime before = DateTime.Now;
        scope.Packet.ItemViewCreate(99, 123, item, LegacyNativeMsgItemView.ActionMode.ViewEquip);
        DateTime after = DateTime.Now;
        byte[] bytes = NativeSpellTestFixture.Bytes(scope.Packet);
        // Ordinary expired timers wrap nonzero in the original runtime; treating
        // them as zero adds a source-incorrect four-byte field to this packet.
        Assert.Equal(220, bytes.Length); Assert.Equal((ushort)2275, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        uint remaining = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(58));
        Assert.NotEqual(0u, remaining); AssertClockWindow(remaining, end, before, after);
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(62)));
        Assert.Equal(1u, item.Activate); Assert.Equal(end, item.EndDate); Assert.Equal(4, scope.Packet.Position);
        Assert.Empty(scope.Native.Queue);
    });
}
