using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyItemViewPacketTests
{
    [Fact]
    public void LegacyItemViewOpcodeAndModesMatchLegacySource()
    {
        Assert.Equal((ushort)2275, WorldPacketOpcodes.LegacyItemView);
        Assert.Equal(WorldPacketOpcodes.LegacyItemView, LegacyItemViewPacket.Opcode);
        Assert.Equal((ushort)1, LegacyItemViewPacket.GoldMode);
        Assert.Equal((ushort)3, LegacyItemViewPacket.CPsMode);
        Assert.Equal((ushort)4, LegacyItemViewPacket.ViewEquipMode);
        Assert.Equal((ushort)7, LegacyItemViewPacket.RuneMode);
    }

    [Fact]
    public void LegacyItemViewViewEquipPacketWritesLegacyShapeAndRoundTrips()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1002,
            ItemId = 500109,
            Durability = 120,
            MaximumDurability = 150,
            Position = 4,
            SocketProgress = 42,
            SocketOne = 13,
            SocketTwo = 3,
            Effect = 200,
            Plus = 12,
            Bless = 7,
            Bound = 1,
            Enchant = 5,
            Suspicious = 1,
            Locked = 2,
            PlusProgress = 333,
            Activate = 1,
            StackSize = 1,
            PurificationItemId = 800001,
            RuneExperience = 777,
            Attribute1 = 1001,
            Attribute2 = 1002,
            Attribute3 = 1003,
            Attribute4 = 1004,
            Attribute5 = 1005,
            AnimaItemId = 3005360,
            MythSoul = 12,
            MythSoulPoints = 34,
            PerfectionLevel = 9,
            PerfectionProgress = 123,
            OwnerUid = 1_293_600,
            OwnerName = "HeroOne",
            Signature = "ready",
        };

        var packet = LegacyItemViewPacket.FromSnapshotItem(
            1_293_600,
            0,
            item,
            LegacyItemViewPacket.ViewEquipMode);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(LegacyItemViewPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyItemViewPacket.FrameLengthWithActivate, frame.Header.Length);
        Assert.Equal(LegacyItemViewPacket.PayloadLengthWithActivate, frame.Payload.Length);
        Assert.Equal(1002U, reader.ReadUInt32(0));
        Assert.Equal(1_293_600U, reader.ReadUInt32(4));
        Assert.Equal(0L, reader.ReadInt64(8));
        Assert.Equal(500109U, reader.ReadUInt32(16));
        Assert.Equal((ushort)120, reader.ReadUInt16(20));
        Assert.Equal((ushort)150, reader.ReadUInt16(22));
        Assert.Equal(LegacyItemViewPacket.ViewEquipMode, reader.ReadUInt16(24));
        Assert.Equal((byte)4, reader.Payload[26]);
        Assert.Equal(42U, reader.ReadUInt32(27));
        Assert.Equal((byte)13, reader.Payload[31]);
        Assert.Equal((byte)3, reader.Payload[32]);
        Assert.Equal(0U, reader.ReadUInt32(33));
        Assert.Equal((byte)200, reader.Payload[37]);
        Assert.Equal((byte)12, reader.Payload[38]);
        Assert.Equal((byte)7, reader.Payload[39]);
        Assert.Equal((byte)1, reader.Payload[40]);
        Assert.Equal((byte)5, reader.Payload[41]);
        Assert.Equal(0U, reader.ReadUInt32(42));
        Assert.Equal((byte)1, reader.Payload[46]);
        Assert.Equal((byte)0, reader.Payload[47]);
        Assert.Equal((byte)2, reader.Payload[48]);
        Assert.Equal((byte)0, reader.Payload[49]);
        Assert.Equal(333U, reader.ReadUInt32(50));
        Assert.Equal(1U, reader.ReadUInt32(54));
        Assert.Equal(0U, reader.ReadUInt32(58));
        Assert.Equal((ushort)1, reader.ReadUInt16(62));
        Assert.Equal(800001U, reader.ReadUInt32(64));
        Assert.Equal(800001U, reader.ReadUInt32(68));
        Assert.Equal(9U, reader.ReadUInt32(72));
        Assert.Equal(123U, reader.ReadUInt32(76));
        Assert.Equal(1_293_600U, reader.ReadUInt32(80));
        Assert.Equal("HeroOne", reader.ReadCString(84, 32, System.Text.Encoding.UTF8));
        Assert.Equal("ready", reader.ReadCString(116, 64, System.Text.Encoding.UTF8));
        Assert.Equal(777U, reader.ReadUInt32(180));
        Assert.Equal(1001U, reader.ReadUInt32(184));
        Assert.Equal(1002U, reader.ReadUInt32(188));
        Assert.Equal(1003U, reader.ReadUInt32(192));
        Assert.Equal(1004U, reader.ReadUInt32(196));
        Assert.Equal(1005U, reader.ReadUInt32(200));
        Assert.Equal(3005360U, reader.ReadUInt32(204));
        Assert.Equal(0U, reader.ReadUInt32(208));
        Assert.Equal(12U, reader.ReadUInt32(212));
        Assert.Equal(34U, reader.ReadUInt32(216));

        Assert.True(LegacyItemViewPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyItemViewRunePacketWritesOverrideAndRoundTrips()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1003,
            ItemId = 4010123,
            Durability = 1,
            MaximumDurability = 1,
            Position = 211,
            Effect = 200,
            StackSize = 1,
            RuneExperience = 5000,
            Attribute1 = 11,
            Attribute2 = 22,
            Attribute3 = 33,
            Attribute4 = 44,
            Attribute5 = 55,
        };
        var overrideSpell = LegacyItemViewPacket.CreateRuneSpellOverride(
            itemId: 4010123,
            runeSpellId: 4010123);

        var packet = LegacyItemViewPacket.FromSnapshotItem(
            1_293_600,
            0,
            item,
            LegacyItemViewPacket.RuneMode,
            overrideSpell);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(LegacyItemViewPacket.RuneMode, reader.ReadUInt16(24));
        Assert.Equal(4010101U, reader.ReadUInt32(33));
        Assert.Equal(LegacyItemViewPacket.RuneEffect, reader.Payload[37]);
        Assert.Equal(5000U, reader.ReadUInt32(180));

        Assert.True(LegacyItemViewPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyItemViewPacketPreservesRawMaxEnchantByte()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1006,
            ItemId = 500109,
            Durability = 1,
            MaximumDurability = 1,
            Position = 4,
            Enchant = byte.MaxValue,
            StackSize = 1,
        };
        var bytes = LegacyItemViewPacket
            .FromSnapshotItem(1_293_600, 0, item, LegacyItemViewPacket.ViewEquipMode)
            .ToFrameBytes();

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(byte.MaxValue, reader.Payload[41]);

        Assert.True(LegacyItemViewPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(byte.MaxValue, parsed.Enchant);
    }

    [Fact]
    public void LegacyItemViewFixedUtf8FieldsPreserveNonAsciiNames()
    {
        const string ownerName = "\u96f7\u594f";
        const string signature = "\u706b\u9f99";
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1005,
            ItemId = 500109,
            Durability = 1,
            MaximumDurability = 1,
            Position = 4,
            StackSize = 1,
            OwnerName = ownerName,
            Signature = signature,
        };
        var bytes = LegacyItemViewPacket
            .FromSnapshotItem(1_293_600, 0, item, LegacyItemViewPacket.ViewEquipMode)
            .ToFrameBytes();

        Assert.True(LegacyItemViewPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(ownerName, parsed.OwnerName);
        Assert.Equal(signature, parsed.Signature);
    }

    [Theory]
    [InlineData(47)]
    [InlineData(49)]
    [InlineData(208)]
    public void LegacyItemViewTryParseRejectsNonZeroReservedFields(int payloadOffset)
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1004,
            ItemId = 500109,
            Durability = 1,
            MaximumDurability = 1,
            Position = 4,
            StackSize = 1,
        };
        var bytes = LegacyItemViewPacket
            .FromSnapshotItem(1_293_600, 0, item, LegacyItemViewPacket.ViewEquipMode)
            .ToFrameBytes();
        bytes[PacketHeader.Size + payloadOffset] = 1;

        Assert.False(LegacyItemViewPacket.TryParse(ReadFrame(bytes), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
