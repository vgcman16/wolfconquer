using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGameUpdatePacketTests
{
    [Fact]
    public void LegacyGameUpdateOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2215, WorldPacketOpcodes.LegacyGameUpdate);
        Assert.Equal(WorldPacketOpcodes.LegacyGameUpdate, LegacyGameUpdatePacket.Opcode);
    }

    [Fact]
    public void LegacyGameUpdateDataTypesMatchLegacyValues()
    {
        Assert.Equal((uint)52, (uint)LegacyGameUpdateDataType.Accelerated);
        Assert.Equal((uint)53, (uint)LegacyGameUpdateDataType.Decelerated);
        Assert.Equal((uint)54, (uint)LegacyGameUpdateDataType.Flustered);
        Assert.Equal((uint)58, (uint)LegacyGameUpdateDataType.Stun);
        Assert.Equal((uint)59, (uint)LegacyGameUpdateDataType.Freeze);
        Assert.Equal((uint)60, (uint)LegacyGameUpdateDataType.Dizzy);
        Assert.Equal((uint)93, (uint)LegacyGameUpdateDataType.AzureShield);
    }

    [Fact]
    public void LegacyGameUpdateFreezeWritesLegacyStructLayout()
    {
        var bytes = LegacyGameUpdatePacket.Freeze(0x01020304, seconds: 4).ToFrameBytes();

        Assert.Equal(
        [
            0x20, 0x00, 0xA7, 0x08,
            0x04, 0x03, 0x02, 0x01,
            0x01, 0x00, 0x00, 0x00,
            0x3B, 0x00, 0x00, 0x00,
            0x00, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ], bytes);
    }

    [Fact]
    public void LegacyGameUpdateActiveFrightenedWritesD13StructLayout()
    {
        var bytes = new LegacyGameUpdatePacket(
                0x01020304,
                LegacyGameUpdateDataType.Flustered,
                Active: true,
                ShownAmount: 0,
                Time: 6,
                Amount: 0)
            .ToFrameBytes();

        Assert.Equal(32, bytes.Length);
        Assert.Equal((ushort)2215, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.Equal(54U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12, 4)));
        Assert.Equal(0x00000100U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(6U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24, 4)));
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28, 4)));
        Assert.Equal(
        [
            0x20, 0x00, 0xA7, 0x08,
            0x04, 0x03, 0x02, 0x01,
            0x01, 0x00, 0x00, 0x00,
            0x36, 0x00, 0x00, 0x00,
            0x00, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x06, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ], bytes);
    }

    [Fact]
    public void LegacyGameUpdateInactiveFrightenedWritesD13StructLayout()
    {
        var bytes = new LegacyGameUpdatePacket(
                0x01020304,
                LegacyGameUpdateDataType.Flustered,
                Active: false,
                ShownAmount: 0,
                Time: 0,
                Amount: 0)
            .ToFrameBytes();

        Assert.Equal(32, bytes.Length);
        Assert.Equal((ushort)2215, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.Equal(54U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12, 4)));
        Assert.Equal(0x00000001U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24, 4)));
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28, 4)));
        Assert.Equal(
        [
            0x20, 0x00, 0xA7, 0x08,
            0x04, 0x03, 0x02, 0x01,
            0x01, 0x00, 0x00, 0x00,
            0x36, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ], bytes);
    }

    [Fact]
    public void LegacyGameUpdateInactiveStunWritesD13StructLayout()
    {
        var bytes = new LegacyGameUpdatePacket(
                0x01020304,
                LegacyGameUpdateDataType.Stun,
                Active: false,
                ShownAmount: 0,
                Time: 0,
                Amount: 0)
            .ToFrameBytes();

        Assert.Equal(
        [
            0x20, 0x00, 0xA7, 0x08,
            0x04, 0x03, 0x02, 0x01,
            0x01, 0x00, 0x00, 0x00,
            0x3A, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ], bytes);
    }

    [Fact]
    public void LegacyGameUpdateRoundTripsFreeze()
    {
        var frame = ReadFrame(LegacyGameUpdatePacket.Freeze(1_293_600, seconds: 4).ToFrameBytes());

        Assert.True(LegacyGameUpdatePacket.TryParse(frame, out var parsed));

        Assert.Equal(1_293_600U, parsed.Uid);
        Assert.Equal(LegacyGameUpdateDataType.Freeze, parsed.Type);
        Assert.True(parsed.Active);
        Assert.Equal(0U, parsed.ShownAmount);
        Assert.Equal(4U, parsed.Time);
        Assert.Equal(0U, parsed.Amount);
    }

    [Fact]
    public void LegacyGameUpdateRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyGameUpdatePacket.Opcode + 1, LegacyGameUpdatePacket.PayloadLength);

        Assert.False(LegacyGameUpdatePacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void LegacyGameUpdateRejectsUnexpectedEntryCount()
    {
        var bytes = LegacyGameUpdatePacket.Freeze(1_293_600, seconds: 4).ToFrameBytes();
        bytes[8] = 2;

        Assert.False(LegacyGameUpdatePacket.TryParse(ReadFrame(bytes), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
