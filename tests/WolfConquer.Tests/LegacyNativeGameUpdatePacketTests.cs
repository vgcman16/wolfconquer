using System.Buffers.Binary;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using GameType = WolfConquer.Core.World.LegacyNativeMsgGameUpdate.DataType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeGameUpdatePacketTests
{
    [Theory]
    [InlineData(52u, true, 4u, 4u)]
    [InlineData(53u, true, 0u, uint.MaxValue)]
    [InlineData(53u, false, 4u, 4294967291u)]
    [InlineData(53u, false, uint.MaxValue, 0u)]
    [InlineData(111u, false, 4u, 4u)]
    [InlineData(uint.MaxValue, true, uint.MaxValue, uint.MaxValue)]
    public Task GameUpdateWritesAllSevenNativeUInt32Fields(uint id, bool active, uint amount, uint expectedAmount) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Packet.Seek(99);
        Assert.Same(f.Packet, f.Packet.GameUpdateCreate(uint.MaxValue, (GameType)id, active, 2, 3, amount));
        var bytes = NativeSpellTestFixture.Bytes(f.Packet);
        Assert.Equal(32, bytes.Length);
        Assert.Equal((ushort)32, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal((ushort)2215, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        uint[] expected = [uint.MaxValue, 1, id, active ? 256u : 1u, 2, 3, expectedAmount];
        for (int index = 0; index < expected.Length; index++)
            Assert.Equal(expected[index], BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 + index * 4)));
        Assert.Equal(4, f.Packet.Position);
    });

    [Fact]
    public Task CompletePrimitiveFrameMatchesIndependentGoldenAndNativeSeal() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Packet.GameUpdateCreate(1, GameType.Accelerated, true, 2, 3, 4);
        const string frame = "2000A70801000000010000003400000000010000020000000300000004000000";
        Assert.Equal(Convert.FromHexString(frame), NativeSpellTestFixture.Bytes(f.Packet));
        LegacyNativePacket.SealString = "TQServer";
        f.Packet.GameUpdateCreate(1, GameType.Accelerated, true, 2, 3, 4);
        Assert.Equal(Convert.FromHexString(frame + "5451536572766572"), NativeSpellTestFixture.Bytes(f.Packet));
        Assert.Equal(40, f.Packet.Size);
        Assert.Equal(4, f.Packet.Position);
    });

    [Fact]
    public void EnumKeepsLiteralUInt32ValuesAndNoFlagsMarker()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(GameType)));
        Assert.False(typeof(GameType).IsDefined(typeof(FlagsAttribute), false));
        Assert.Equal(new uint[] { 52, 53, 54, 55, 57, 58, 59, 60, 93, 111 },
            Enum.GetValues<GameType>().Select(value => (uint)value));
        Assert.Equal(111u, (uint)GameType.SoulShacle);
        Assert.Throws<NullReferenceException>(() => LegacyNativeGameUpdatePacket.GameUpdateCreate(null!, 0, 0, false, 0, 0));
    }
}
