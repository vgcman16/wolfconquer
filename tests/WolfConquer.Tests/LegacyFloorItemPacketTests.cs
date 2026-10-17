using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFloorItemPacketTests
{
    [Fact]
    public void LegacyFloorItemOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2164, WorldPacketOpcodes.LegacyFloorItem);
        Assert.Equal(WorldPacketOpcodes.LegacyFloorItem, LegacyFloorItemPacket.Opcode);
    }

    [Fact]
    public void LegacyFloorItemPacketWritesVisibleItemShape()
    {
        var packet = new LegacyFloorItemPacket(
            Uid: 900028,
            ItemId: 111003,
            X: 577,
            Y: 586,
            MaxLife: 40,
            LegacyFloorItemDropType.Visible,
            Life: 0,
            PlusOrColor: 3,
            ItemOwnerUid: 1_293_600,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 0,
            OwnerY: 0,
            Name: "");

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyFloorItemPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyFloorItemPacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyFloorItemPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal((byte)LegacyFloorItemDropType.Visible, frame.Payload.ToArray()[14]);
        Assert.Equal(3, frame.Payload.ToArray()[19]);
    }

    [Fact]
    public void LegacyFloorItemPacketBuildsFromDroppedInventoryItem()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 2001,
            ItemId = 111003,
            Plus = 4,
        };
        var character = new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "user02",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 15,
            FirstClass: 15,
            SecondClass: 15,
            Reborn: 0,
            Level: 1,
            Experience: 0,
            MapId: 1002,
            X: 577,
            Y: 586,
            PreviousMapId: 1002,
            PreviousMapX: 577,
            PreviousMapY: 586,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 1,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "1293600.ini",
            Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>());

        var packet = LegacyFloorItemPacket.FromDroppedItem(item, character, floorUid: 900028);

        Assert.Equal(900028U, packet.Uid);
        Assert.Equal(111003U, packet.ItemId);
        Assert.Equal(577, packet.X);
        Assert.Equal(586, packet.Y);
        Assert.Equal(40, packet.MaxLife);
        Assert.Equal(LegacyFloorItemDropType.Visible, packet.DropType);
        Assert.Equal(4, packet.PlusOrColor);
        Assert.Equal(1_293_600U, packet.ItemOwnerUid);
    }

    [Fact]
    public void LegacyFloorItemPacketBuildsFromMonsterDrop()
    {
        var monster = new LegacyMonsterInstance(
            Uid: 401_000,
            MapId: 1015,
            X: 841,
            Y: 573,
            Mesh: 200,
            Level: 87,
            Hitpoints: 0,
            MaxLife: 8_925,
            MonsterId: 18,
            Name: "Birdman",
            Boss: false,
            GeneratorId: 1,
            SpawnIndex: 0);
        var character = new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "user02",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 15,
            FirstClass: 15,
            SecondClass: 15,
            Reborn: 0,
            Level: 1,
            Experience: 0,
            MapId: 1015,
            X: 841,
            Y: 573,
            PreviousMapId: 1015,
            PreviousMapX: 841,
            PreviousMapY: 573,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 1,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "1293600.ini",
            Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>());

        var packet = LegacyFloorItemPacket.FromMonsterDrop(
            itemId: 1_002_000,
            monster,
            character,
            floorUid: 900_100);

        Assert.Equal(900_100U, packet.Uid);
        Assert.Equal(1_002_000U, packet.ItemId);
        Assert.Equal(841, packet.X);
        Assert.Equal(573, packet.Y);
        Assert.Equal(40, packet.MaxLife);
        Assert.Equal(LegacyFloorItemDropType.Visible, packet.DropType);
        Assert.Equal(0, packet.PlusOrColor);
        Assert.Equal(1_293_600U, packet.ItemOwnerUid);

        var scattered = LegacyFloorItemPacket.FromMonsterDrop(
            itemId: 1_090_000,
            x: 840,
            y: 572,
            character,
            floorUid: 900_101);
        Assert.Equal(900_101U, scattered.Uid);
        Assert.Equal(1_090_000U, scattered.ItemId);
        Assert.Equal(840, scattered.X);
        Assert.Equal(572, scattered.Y);
        Assert.Equal(1_293_600U, scattered.ItemOwnerUid);
    }

    [Fact]
    public void LegacyFloorItemPacketParsesPickupRequestUid()
    {
        var writer = new PacketWriter(LegacyFloorItemPacket.Opcode, payloadLength: sizeof(uint));
        writer.WriteUInt32(0, 900_100);
        var frame = ReadFrame(writer.ToArray());

        Assert.True(LegacyFloorItemPacket.TryParsePickupRequest(frame, out var floorUid));
        Assert.Equal(900_100U, floorUid);
    }

    [Fact]
    public void LegacyFloorItemPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyFloorItemPacket.Opcode + 1, payloadLength: LegacyFloorItemPacket.PayloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyFloorItemPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
