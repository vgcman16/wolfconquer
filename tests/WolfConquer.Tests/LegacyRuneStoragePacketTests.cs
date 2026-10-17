using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRuneStoragePacketTests
{
    [Fact]
    public void LegacyRuneStorageOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2286, WorldPacketOpcodes.LegacyRuneStorage);
        Assert.Equal(WorldPacketOpcodes.LegacyRuneStorage, LegacyRuneStoragePacket.Opcode);
    }

    [Fact]
    public void LegacyRuneStorageStartupPacketWritesLegacyProtobufShape()
    {
        var bytes = LegacyRuneStoragePacket.Startup(hpAdd: 123).ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(LegacyRuneStoragePacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0x02,
            0x10, 0x00,
            0x18, 0x7B,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyRuneStorageStartupUsesFixedLegacyDefaults()
    {
        var packet = LegacyRuneStoragePacket.Startup();

        Assert.Equal(LegacyRuneStoragePacket.StartupType, packet.Type);
        Assert.Equal(LegacyRuneStoragePacket.StartupItemUID, packet.ItemUID);
        Assert.Equal(0U, packet.HPAdd);
    }

    [Fact]
    public void LegacyRuneStorageStartupComputesHpAddFromRuneStorageScore()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems:
            [
                new PlayerItemSnapshotItem { Uid = 1001, ItemId = 402005, Position = 101 },
            ]);
        var storageAttributes = new[]
        {
            RuneStorageAttribute(id: 1, requiredScore: 2_000, hpAdd: 123),
            RuneStorageAttribute(id: 2, requiredScore: 2_200, hpAdd: 777),
        };

        var packet = LegacyRuneStoragePacket.Startup(playerItems, storageAttributes);

        Assert.Equal(123U, packet.HPAdd);
    }

    [Fact]
    public void LegacyRuneStorageStartupIgnoresUnequippedStoragePositionForHpAdd()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems:
            [
                new PlayerItemSnapshotItem { Uid = 1001, ItemId = 402005, Position = 211 },
            ]);
        var storageAttributes = new[]
        {
            RuneStorageAttribute(id: 1, requiredScore: 1, hpAdd: 123),
        };

        var packet = LegacyRuneStoragePacket.Startup(playerItems, storageAttributes);

        Assert.Equal(0U, packet.HPAdd);
    }

    [Fact]
    public void LegacyRuneStoragePacketRoundTrips()
    {
        var packet = new LegacyRuneStoragePacket(7, 4_294_967_295, 987_654_321);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyRuneStoragePacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyRuneStoragePacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyAutoHunt, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x02;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRuneStoragePacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRuneStoragePacketRejectsMissingRequiredItemUID()
    {
        var writer = new PacketWriter(LegacyRuneStoragePacket.Opcode, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x02;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRuneStoragePacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRuneStoragePacketRejectsMalformedProtobuf()
    {
        var writer = new PacketWriter(LegacyRuneStoragePacket.Opcode, payloadLength: 1);
        writer.Payload[0] = 0x80;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRuneStoragePacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static RuneStorageAttribute RuneStorageAttribute(int id, int requiredScore, int hpAdd)
    {
        return new RuneStorageAttribute(
            id,
            requiredScore,
            Bonuses:
            [
                new RuneStorageAttributeBonus(1, hpAdd),
                new RuneStorageAttributeBonus(2, 999),
                new RuneStorageAttributeBonus(0, 0),
            ],
            SourceFile: "rune_storage_attr.txt",
            LineNumber: id,
            RawLine: "",
            Fields: []);
    }
}
