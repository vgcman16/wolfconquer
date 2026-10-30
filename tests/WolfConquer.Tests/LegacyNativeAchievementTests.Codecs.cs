using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Action = WolfConquer.Core.World.LegacyNativeClientAchievement.TypeAchievement;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAchievementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ScalarCodecKeepsAllWidthsReservedWordsAndNativeSeal(bool seal) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(seal); var packet = scope.Packet;
        var action = (Action)uint.MaxValue;
        Assert.Same(packet, packet.AchievementCreate(action, 0x80000000, uint.MaxValue));
        byte[] expected = ScalarFrame(action, 0x80000000, uint.MaxValue);
        if (seal) expected = expected.Concat(Encoding.ASCII.GetBytes("TQServer")).ToArray();
        Assert.Equal(expected, Bytes(packet)); Assert.Equal(4, packet.Position);
        Assert.Equal(seal ? 32 : 24, packet.Size);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(16)]
    public Task ArrayCodecKeepsActualLengthNotFourteenAndDoesNotMutateCaller(int length) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var words = Enumerable.Range(0, length).Select(i => (uint)(0x80000000u + i)).ToArray(); var before = words.ToArray();
        Assert.Same(scope.Packet, scope.Packet.AchievementCreate(Action.CheakView, uint.MaxValue, words));
        Assert.Equal(ArrayFrame(Action.CheakView, uint.MaxValue, words), Bytes(scope.Packet));
        Assert.Equal(before, words); Assert.Equal(4, scope.Packet.Position);
        scope.Packet.GetAchievement(out var action, out var uid, out var flag);
        Assert.Equal(Action.CheakView, action); Assert.Equal(uint.MaxValue, uid);
        Assert.Equal((uint)length, flag); Assert.Equal(20, scope.Packet.Position); // Reader sees length, not word0.
    });

    [Fact]
    public Task NullArrayFaultRetainsWrittenPrefixOldSizeAndNoFinalization() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var packet = scope.Packet;
        Assert.Throws<NullReferenceException>(() => packet.AchievementCreate(Action.SendQueue, 73, (uint[])null!));
        Assert.Equal(16, packet.Position); Assert.Equal(321, packet.Size);
        byte[] prefix = Bytes(packet, 20);
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, prefix[..4]);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(4)));
        Assert.Equal(73u, BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(8)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(12)));
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, prefix[16..20]);
        Assert.Throws<NullReferenceException>(() => ((LegacyNativePacket)null!).AchievementCreate(Action.BigHash, 1, 9u));
        Assert.Throws<NullReferenceException>(() => ((LegacyNativePacket)null!).AchievementCreate(Action.BigHash, 1, Array.Empty<uint>()));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(237)]
    public Task ReaderUsesCapturedInputAtCurrentCursorAndDiscardsTheReservedWord(int start) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var packet = scope.Packet;
        packet.Seek(start); packet.Write(uint.MaxValue); packet.Write(0x80000000u); packet.Write(0x12345678u); packet.Write(0xF0000001u);
        int inputEnd = packet.Position; packet.Size = inputEnd; packet.Seek(start);
        packet.GetAchievement(out var action, out var uid, out var flag);
        Assert.Equal(uint.MaxValue, (uint)action); Assert.Equal(0x80000000u, uid); Assert.Equal(0xF0000001u, flag);
        Assert.Equal(start + 16, packet.Position); Assert.Equal(inputEnd, packet.Size);
    });

    [Theory]
    [InlineData(16380, true)]
    [InlineData(16379, true)]
    [InlineData(16375, true)]
    [InlineData(16367, false)]
    public Task ReaderPublishesAllFieldsOnlyAfterTheCompleteCapturedRecord(int start, bool rejected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var packet = scope.Packet;
        // All physical writes remain within our MAX_SIZE allocation, including rejected final reads.
        byte[] buffer = new byte[LegacyNativePacket.MAX_SIZE];
        uint[] words = [0x11223344, 0xAABBCCDD, 0xDEADBEEF, 0x87654321];
        for (int i = 0; i < words.Length && start + 4 * (i + 1) <= buffer.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(start + 4 * i), words[i]);
        Copy(packet, buffer); packet.Seek(start);
        var action = (Action)91; uint uid = 92, flag = 93;
        if (rejected) Assert.Throws<EndOfStreamException>(() => packet.GetAchievement(out action, out uid, out flag));
        else packet.GetAchievement(out action, out uid, out flag);
        Assert.Equal(rejected ? 91u : 0x11223344u, (uint)action);
        Assert.Equal(rejected ? 92u : 0xAABBCCDDu, uid);
        Assert.Equal(rejected ? 93u : 0x87654321u, flag);
        Assert.Equal(rejected ? start : start + 16, packet.Position); Assert.Equal(buffer.Length, packet.Size);
        Assert.Equal(buffer, Bytes(packet));
    });
}
