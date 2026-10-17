using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyItemLockPacketTests
{
    [Fact]
    public void LegacyItemLockOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2323, WorldPacketOpcodes.LegacyItemLock);
        Assert.Equal(WorldPacketOpcodes.LegacyItemLock, LegacyItemLockPacket.Opcode);
    }

    [Fact]
    public void LegacyItemLockPacketWritesUnlockDateShape()
    {
        var packet = new LegacyItemLockPacket(
            1002,
            LegacyItemLockPacket.UnlockDateType,
            LegacyItemLockPacket.UnlockDateLockedMode,
            20991231);
        var bytes = packet.ToFrameBytes();

        Assert.Equal(LegacyItemLockPacket.FrameLength, bytes.Length);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        Assert.Equal(LegacyItemLockPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyItemLockPacket.PayloadLength, frame.Payload.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(1002U, reader.ReadUInt32(0));
        Assert.Equal(LegacyItemLockPacket.UnlockDateType, reader.Payload[4]);
        Assert.Equal(LegacyItemLockPacket.UnlockDateLockedMode, reader.Payload[5]);
        Assert.Equal((ushort)0, reader.ReadUInt16(6));
        Assert.Equal(20991231U, reader.ReadUInt32(8));
    }

    [Fact]
    public void LegacyItemLockTryCreateUsesLegacyUnlockDateRule()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1002,
            Locked = 2,
            UnlockTimer = 20991231,
        };

        Assert.True(
            LegacyItemLockPacket.TryCreateUnlockDate(item, new DateTime(2026, 6, 4, 12, 0, 0), out var packet));
        Assert.Equal(new LegacyItemLockPacket(1002, 2, 0, 20991231), packet);
    }

    [Fact]
    public void LegacyItemLockTryCreateRejectsUnlockedOrExpiredItems()
    {
        var unlocked = new PlayerItemSnapshotItem { Uid = 1002, Locked = 1, UnlockTimer = 20991231 };
        var expired = new PlayerItemSnapshotItem { Uid = 1003, Locked = 2, UnlockTimer = 20200101 };
        var invalidDate = new PlayerItemSnapshotItem { Uid = 1004, Locked = 2, UnlockTimer = 20261340 };

        Assert.False(
            LegacyItemLockPacket.TryCreateUnlockDate(unlocked, new DateTime(2026, 6, 4), out _));
        Assert.False(
            LegacyItemLockPacket.TryCreateUnlockDate(expired, new DateTime(2026, 6, 4), out _));
        Assert.False(
            LegacyItemLockPacket.TryCreateUnlockDate(invalidDate, new DateTime(2026, 6, 4), out _));
    }

    [Fact]
    public void LegacyItemLockPacketRoundTrips()
    {
        var packet = new LegacyItemLockPacket(1002, 2, 0, 20991231);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyItemLockPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
