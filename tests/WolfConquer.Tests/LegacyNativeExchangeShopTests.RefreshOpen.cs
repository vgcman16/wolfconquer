using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Theory]
    [InlineData("null-tail")] [InlineData("wrong-key")] [InlineData("row-alias")]
    public Task RefreshOpenRefusesCompleteCorruptBookBeforeCallerPacketOrPriorFrameWrites(string malformed)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); var old = ReadyRefreshPrefix(scope);
        if (malformed == "null-tail") scope.Shop.Items.Add(101, null!);
        else if (malformed == "wrong-key") scope.Shop.Items[19].goods_rec_id = 777;
        else scope.Shop.Items.Add(101, old);
        var before = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryOpen(scope.Packet, 77, 7, out var reason));
        Assert.StartsWith("exchangeShop", reason); AssertRefreshRejected(scope, before);
        scope.Shop.Open(scope.Packet, 77, 7); AssertRefreshRejected(scope, before);
        Assert.Contains("exchangeShop", scope.State.ConsoleText.ToString());
    });

    [Fact]
    public unsafe Task RefreshOpenChecksActualAllocationCapacityBeforeUnsafeCopy()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        using var small = new LegacyNativePacket(8);
        Marshal.Copy(Enumerable.Repeat((byte)0xB7, 8).ToArray(), 0, (IntPtr)small.Memory, 8);
        small.Size = 8; small.Seek(4);
        var smallBytes = NativeSpellTestFixture.Bytes(small); var before = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryOpen(small, 77, 7, out var reason));
        Assert.StartsWith("exchangeShop", reason);
        Assert.Equal(smallBytes, NativeSpellTestFixture.Bytes(small)); Assert.Equal(8, small.Size); Assert.Equal(4, small.Position);
        AssertRefreshRejected(scope, before);
    });

    [Fact]
    public Task RefreshOpenRejectsOversizedWholePublicationRatherThanSendingAProperPrefix()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        for (uint id = 1001; id <= 1500; id++)
            scope.Shop.Items.Add(id, new() { shopid = 7, goods_rec_id = id, times = uint.MaxValue });
        var before = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryOpen(scope.Packet, 77, 7, out var reason));
        Assert.StartsWith("exchangeShop", reason); AssertRefreshRejected(scope, before);
    });

    [Fact]
    public Task RefreshOpenRefusesDisposedRealPacketWithoutTouchingTheValidCallerOrQueue()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        using var disposed = new LegacyNativePacket(64); disposed.Dispose();
        var before = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryOpen(disposed, 77, 7, out var reason));
        Assert.StartsWith("exchangeShop", reason); AssertRefreshRejected(scope, before);
    });

    [Fact]
    public Task RefreshOpenPreservesTheRealEightByteSealAndExisting2317HeaderLength()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        scope.Shop.Items.Add(17, new() { shopid = 7, goods_rec_id = 17, times = 11 });
        LegacyNativePacket.SealString = "TQServer";
        DateTime before = DateTime.Now;
        Assert.True(scope.Shop.TryOpen(scope.Packet, 77, 7, out var reason), reason);
        DateTime after = DateTime.Now;
        byte[] actual = Assert.Single(scope.Queue);
        int cursor = 4;
        Assert.Equal((byte)8, actual[cursor++]); Assert.Equal(77u, Read(actual, ref cursor));
        Assert.Equal((byte)16, actual[cursor++]); Assert.Equal(7u, Read(actual, ref cursor));
        Assert.Equal((byte)24, actual[cursor++]); Assert.Equal(scope.Client.Player.UID, Read(actual, ref cursor));
        Assert.Equal((byte)32, actual[cursor++]); uint time = Read(actual, ref cursor);
        TimeBetween(time, before, after);
        byte[] expected = [.. Frame(77, 7, scope.Client.Player.UID, time, (17u, 11u)), .. Encoding.ASCII.GetBytes("TQServer")];
        Assert.Equal(expected, actual);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(expected.Length, scope.Packet.Size);
    });

    [Fact]
    public Task RefreshPreparedOpenTransportLossDoesNotBecomeAnUncommittedRefusal()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var before = CaptureRefresh(scope);
        var queueField = typeof(LegacyNativeSecuritySocket)
            .GetField("SendQueue", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var queue = queueField.GetValue(scope.Client.Socket);
        queueField.SetValue(scope.Client.Socket, null);
        try
        {
            Assert.True(scope.Shop.TryOpen(scope.Packet, 91, 7, out var reason), reason);
            AssertRefreshStorageUnchanged(before, scope.Shop);
            Assert.Equal(before.Frames.Length, scope.Queue.Count);
            for (int index = 0; index < before.Frames.Length; index++)
            {
                Assert.Same(before.Frames[index].Reference, scope.Queue.ElementAt(index));
                Assert.Equal(before.Frames[index].Bytes, scope.Queue.ElementAt(index));
            }
            Assert.Equal((ushort)2317, BitConverter.ToUInt16(NativeSpellTestFixture.Bytes(scope.Packet), 2));
        }
        finally { queueField.SetValue(scope.Client.Socket, queue); }
    });
}
