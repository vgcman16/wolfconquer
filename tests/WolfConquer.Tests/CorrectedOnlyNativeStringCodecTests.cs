using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using StringId = WolfConquer.Core.World.LegacyNativeMsgStringPacket.StringID;

namespace WolfConquer.Tests;

// Corrected code only: do not execute small-allocation or malformed cases
// against the original native implementation, which had unsafe bounds.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class CorrectedOnlyNativeStringCodecTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public Task TruncatedFixedPrefixPreservesOutputCursorAndInput(int bodyLength) => NativePacketTestThread.Run(() =>
    {
        byte[] input = [0, 0, 0, 0, .. new byte[] { 0x78, 0x56, 0x34, 0x12, 0x0A }[..bodyLength]];
        using var packet = new LegacyNativePacket(input);
        packet.Seek(4);
        var before = Existing(); var result = before;
        Assert.Throws<EndOfStreamException>(() => packet.GetStringPacket(out result));
        AssertExisting(before, result);
        Assert.Equal(4, packet.Position); Assert.Equal(input.Length, packet.Size);
        Assert.Equal(input, NativeStringPacketTestScope.Bytes(packet));
    });

    [Fact]
    public Task MalformedLaterStringDoesNotPublishEarlierFieldsOrRows() => NativePacketTestThread.Run(() =>
    {
        byte[] input = Convert.FromHexString("00000000785634120A02010061030062");
        using var packet = new LegacyNativePacket(input);
        packet.Seek(4);
        var before = Existing(); var result = before;
        Assert.Throws<EndOfStreamException>(() => packet.GetStringPacket(out result));
        AssertExisting(before, result);
        Assert.Equal(4, packet.Position); Assert.Equal(input.Length, packet.Size);
        Assert.Equal(input, NativeStringPacketTestScope.Bytes(packet));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task WholeFrameFitsExactActualCapacityAndOneByteLessPreservesTarget(bool sealedFrame) => NativePacketTestThread.Run(() =>
    {
        using var sealScope = new NativeStringPacketTestScope(sealedFrame);
        byte[] expected = Convert.FromHexString("0E00F407785634120A0102006678" +
            (sealedFrame ? "5451536572766572" : ""));
        var message = new LegacyNativeMsgStringPacket { UID = 0x12345678, ID = StringId.Effect, Strings = ["fx"] };
        using var exact = new LegacyNativePacket(expected.Length);
        Fill(exact); exact.Size = 3; exact.Seek(2);
        Assert.Same(exact, exact.StringPacketCreate(message));
        Assert.Equal(expected, NativeStringPacketTestScope.Bytes(exact));
        Assert.Equal(expected.Length, exact.Size); Assert.Equal(4, exact.Position);

        using var shortPacket = new LegacyNativePacket(expected.Length - 1);
        Fill(shortPacket); shortPacket.Size = 3; shortPacket.Seek(2);
        Refuses<InvalidOperationException>(shortPacket, () => shortPacket.StringPacketCreate(message));
        Assert.Equal(0x12345678u, message.UID); Assert.Equal(new[] { "fx" }, message.Strings);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task UnrepresentableCountOrEntryPreservesExistingWholeFrame(bool oversizedCount) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope(true);
        var packet = scope.Packet;
        Fill(packet); packet.Size = 17; packet.Seek(11);
        string[] strings = oversizedCount ? new string[256] : [new string('A', 65_536)];
        var message = new LegacyNativeMsgStringPacket { UID = 91, ID = StringId.Sound, X = 13, Y = 14,
            StringsLength = 73, Strings = strings };
        Refuses<ArgumentOutOfRangeException>(packet, () => packet.StringPacketCreate(message));
        Assert.Same(strings, message.Strings); Assert.Equal(91u, message.UID);
        Assert.Equal((ushort)13, message.X); Assert.Equal((ushort)14, message.Y);
        Assert.Equal((byte)73, message.StringsLength); Assert.Equal(StringId.Sound, message.ID);
    });

    [Fact]
    public Task DisposedReaderAndBuilderRefuseWithoutOutputPublication() => NativePacketTestThread.Run(() =>
    {
        using var packet = new LegacyNativePacket([0, 0, 0, 0, 1, 0, 0, 0, 10, 0]);
        packet.Seek(4); packet.Dispose();
        var before = Existing(); var result = before;
        Assert.Throws<ObjectDisposedException>(() => packet.GetStringPacket(out result));
        AssertExisting(before, result);
        Assert.Throws<ObjectDisposedException>(() => packet.StringPacketCreate(Existing()));
        Assert.Equal(10, packet.Size);
    });

    [Fact]
    public Task NullBuilderStreamRefusesClearlyWithoutChangingTheMessage() => NativePacketTestThread.Run(() =>
    {
        LegacyNativePacket packet = null!;
        var message = Existing();
        Assert.Throws<ArgumentNullException>(() => packet.StringPacketCreate(message));
        AssertExisting(message, message);
    });

    private static LegacyNativeMsgStringPacket Existing() => new()
    {
        UID = 123, ID = StringId.GuildName, X = 17, Y = 18,
        StringsLength = 19, Strings = ["preserved"]
    };

    private static void AssertExisting(LegacyNativeMsgStringPacket before, LegacyNativeMsgStringPacket result)
    {
        Assert.Same(before, result); Assert.Equal(123u, result.UID);
        Assert.Equal(StringId.GuildName, result.ID); Assert.Equal((ushort)17, result.X);
        Assert.Equal((ushort)18, result.Y); Assert.Equal((byte)19, result.StringsLength);
        Assert.Equal(new[] { "preserved" }, result.Strings);
    }

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray();
        Marshal.Copy(bytes, 0, (nint)packet.Memory, bytes.Length);
    }

    private static void Refuses<T>(LegacyNativePacket packet, Action action) where T : Exception
    {
        byte[] before = NativeStringPacketTestScope.Bytes(packet, packet.Capacity);
        int position = packet.Position, size = packet.Size;
        Assert.Throws<T>(action);
        Assert.Equal(before, NativeStringPacketTestScope.Bytes(packet, packet.Capacity));
        Assert.Equal(position, packet.Position); Assert.Equal(size, packet.Size);
    }
}
