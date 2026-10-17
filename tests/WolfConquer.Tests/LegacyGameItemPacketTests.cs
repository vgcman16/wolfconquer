using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGameItemPacketTests
{
    [Fact]
    public void LegacyGameItemOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2116, WorldPacketOpcodes.LegacyGameItem);
        Assert.Equal(WorldPacketOpcodes.LegacyGameItem, LegacyGameItemPacket.Opcode);
    }

    [Fact]
    public void LegacyGameItemPacketWritesLegacyShape()
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
            StackSize = 1,
            Color = 5,
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

        var packet = LegacyGameItemPacket.FromSnapshotItem(item);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(LegacyGameItemPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyGameItemPacket.FrameLength, frame.Header.Length);
        Assert.Equal(LegacyGameItemPacket.PayloadLength, frame.Payload.Length);
        Assert.Equal(1002U, reader.ReadUInt32(0));
        Assert.Equal(500109U, reader.ReadUInt32(4));
        Assert.Equal((ushort)120, reader.ReadUInt16(8));
        Assert.Equal((ushort)150, reader.ReadUInt16(10));
        Assert.Equal(LegacyGameItemPacket.AddItemMode, reader.ReadUInt16(12));
        Assert.Equal((byte)4, reader.Payload[14]);
        Assert.Equal(42U, reader.ReadUInt32(15));
        Assert.Equal((byte)13, reader.Payload[19]);
        Assert.Equal((byte)3, reader.Payload[20]);
        Assert.Equal(0U, reader.ReadUInt32(21));
        Assert.Equal((byte)200, reader.Payload[25]);
        Assert.Equal((byte)12, reader.Payload[26]);
        Assert.Equal((byte)7, reader.Payload[27]);
        Assert.Equal((byte)1, reader.Payload[28]);
        Assert.Equal((byte)5, reader.Payload[29]);
        Assert.Equal(0U, reader.ReadUInt32(30));
        Assert.Equal((byte)1, reader.Payload[34]);
        Assert.Equal((byte)0, reader.Payload[35]);
        Assert.Equal((byte)2, reader.Payload[36]);
        Assert.Equal((byte)0, reader.Payload[37]);
        Assert.Equal(333U, reader.ReadUInt32(38));
        Assert.Equal(0U, reader.ReadUInt32(42));
        Assert.Equal(0U, reader.ReadUInt32(46));
        Assert.Equal((ushort)1, reader.ReadUInt16(50));
        Assert.Equal(9U, reader.ReadUInt32(52));
        Assert.Equal(123U, reader.ReadUInt32(56));
        Assert.Equal(1_293_600U, reader.ReadUInt32(60));
        Assert.Equal("HeroOne", reader.ReadCString(64, 32, System.Text.Encoding.UTF8));
        Assert.Equal("ready", reader.ReadCString(96, 64, System.Text.Encoding.UTF8));
        Assert.Equal((ushort)0, reader.ReadUInt16(160));
        Assert.Equal(777U, reader.ReadUInt32(162));
        Assert.Equal(1001U, reader.ReadUInt32(166));
        Assert.Equal(1002U, reader.ReadUInt32(170));
        Assert.Equal(1003U, reader.ReadUInt32(174));
        Assert.Equal(1004U, reader.ReadUInt32(178));
        Assert.Equal(1005U, reader.ReadUInt32(182));
        Assert.Equal(3005360U, reader.ReadUInt32(186));
        Assert.Equal(0U, reader.ReadUInt32(190));
        Assert.Equal(12U, reader.ReadUInt32(194));
        Assert.Equal(34U, reader.ReadUInt32(198));
    }

    [Fact]
    public void LegacyGameItemPacketRoundTrips()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1001,
            ItemId = 3324226,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = 1,
            Color = 6,
        };
        var packet = LegacyGameItemPacket.FromSnapshotItem(item);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyGameItemPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyGameItemPacketPreservesRawMaxEnchantByte()
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

        var packet = LegacyGameItemPacket.FromSnapshotItem(item);
        var bytes = packet.ToFrameBytes();

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(byte.MaxValue, reader.Payload[29]);

        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(byte.MaxValue, parsed.Enchant);
    }

    [Fact]
    public void LegacyGameItemPacketWritesRuneSpellOverride()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1003,
            ItemId = 3005360,
            Durability = 1,
            MaximumDurability = 1,
            Position = 4,
            Effect = 200,
            StackSize = 1,
            Color = 6,
        };

        var packet = LegacyGameItemPacket.FromSnapshotItem(
            item,
            new LegacyGameItemPacket.RuneSpellOverride(3005301, 17));
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(3005301U, reader.ReadUInt32(21));
        Assert.Equal((byte)17, reader.Payload[25]);

        Assert.True(LegacyGameItemPacket.TryParse(frame, out var parsed));
        Assert.Equal(3005301U, parsed.SpellId);
        Assert.Equal((byte)17, parsed.Effect);
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyGameItemPacketBuildsLegacyRuneSpellOverride()
    {
        var folded = LegacyGameItemPacket.CreateRuneSpellOverride(itemId: 4010123, runeSpellId: 4010123);
        var mapped = LegacyGameItemPacket.CreateRuneSpellOverride(itemId: 4020456, runeSpellId: 700001);
        var nonRune = LegacyGameItemPacket.CreateRuneSpellOverride(itemId: 500109, runeSpellId: 500109);

        Assert.Equal(new LegacyGameItemPacket.RuneSpellOverride(4010101, LegacyGameItemPacket.RuneEffect), folded);
        Assert.Equal(new LegacyGameItemPacket.RuneSpellOverride(700001, LegacyGameItemPacket.NoEffect), mapped);
        Assert.Null(nonRune);
    }

    [Fact]
    public void LegacyGameItemPacketBuildsRuneSpellOverrideFromItemTypeLookup()
    {
        var runeItem = new PlayerItemSnapshotItem { ItemId = 4036205 };
        var normalItem = new PlayerItemSnapshotItem { ItemId = 101000 };
        var itemTypesById = new Dictionary<int, IReadOnlyList<ItemType>>
        {
            [4036205] = [ItemTypeWithRuneSpellId(4036205, 4036205)],
            [101000] = [ItemTypeWithRuneSpellId(101000, 4036205)],
        };

        var runeOverride = LegacyGameItemPacket.CreateRuneSpellOverride(runeItem, itemTypesById);
        var normalOverride = LegacyGameItemPacket.CreateRuneSpellOverride(normalItem, itemTypesById);

        Assert.Equal(new LegacyGameItemPacket.RuneSpellOverride(4036201, LegacyGameItemPacket.RuneEffect), runeOverride);
        Assert.Null(normalOverride);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static ItemType ItemTypeWithRuneSpellId(int itemId, uint runeSpellId)
    {
        var fields = Enumerable.Repeat("0", 26).ToArray();
        fields[0] = itemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[1] = "Item";
        fields[ItemType.RuneSpellIdFieldIndex] = runeSpellId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new ItemType(itemId, "Item", fields);
    }
}
