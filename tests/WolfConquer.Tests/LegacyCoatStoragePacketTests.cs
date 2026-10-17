using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCoatStoragePacketTests
{
    [Fact]
    public void LegacyCoatStorageOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2220, WorldPacketOpcodes.LegacyCoatStorage);
        Assert.Equal((ushort)2220, LegacyCoatStoragePacket.Opcode);
    }

    [Fact]
    public void LegacyCoatStorageEmptyStartupWritesRequiredDefaultFields()
    {
        var bytes = LegacyCoatStoragePacket.CreateEmptyStartup().ToFrameBytes();

        Assert.Equal("0800ac0808002000", Convert.ToHexString(bytes).ToLowerInvariant());

        var frame = ReadFrame(bytes);

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(0U, parsed.ActionId);
        Assert.Equal(0U, parsed.DwParam1);
        Assert.Equal(0U, parsed.DwParam2);
        Assert.Equal(0U, parsed.DwParam3);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyCoatStorageRoundTripsWardrobeItemStorageFields()
    {
        var item = new LegacyCoatStorageItem(
            ItemUid: 1001,
            ItemId: 200614,
            DwParam3: 0,
            DwParam4: 0,
            DwParam5: 0,
            DwParam6: 0,
            DwParam7: 0,
            Plus: 8,
            Bless: 5,
            Type1: 3,
            DwParam11: 0,
            DwParam12: 0,
            DwParam13: 0,
            DwParam14: 0,
            Type2: 3,
            DwParam16: 0,
            DwParam17: 0,
            DwParam18: 0,
            TimeLeft: 0,
            DwParam20: 0,
            Stack: 1,
            MinDurability: 1234,
            MaxDurability: 1234);
        var packet = new LegacyCoatStoragePacket(
            ActionId: 0,
            DwParam1: 0,
            DwParam2: 0,
            DwParam3: 0,
            [item]);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        var parsedItem = Assert.Single(parsed.Items);
        Assert.Equal(1001U, parsedItem.ItemUid);
        Assert.Equal(200614U, parsedItem.ItemId);
        Assert.Equal(8U, parsedItem.Plus);
        Assert.Equal(5U, parsedItem.Bless);
        Assert.Equal(3U, parsedItem.Type1);
        Assert.Equal(3U, parsedItem.Type2);
        Assert.Equal(1U, parsedItem.Stack);
        Assert.Equal(1234U, parsedItem.MinDurability);
        Assert.Equal(1234U, parsedItem.MaxDurability);
    }

    [Fact]
    public void LegacyCoatStorageParsesInboundTakeOffRequestWithNoItems()
    {
        var frame = ReadPayloadFrame(CreatePayload((1, 5), (3, 9), (4, 0)));

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(5U, parsed.ActionId);
        Assert.Equal(0U, parsed.DwParam1);
        Assert.Equal(9U, parsed.DwParam2);
        Assert.Equal(0U, parsed.DwParam3);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyCoatStorageParsesInboundInventoryToWardrobeRequestWithNoItems()
    {
        var frame = ReadPayloadFrame(CreatePayload((1, 1), (2, 2001), (4, 0)));

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyCoatStoragePacket.InventoryToWardrobeActionId, parsed.ActionId);
        Assert.Equal(2001U, parsed.DwParam1);
        Assert.Equal(0U, parsed.DwParam2);
        Assert.Equal(0U, parsed.DwParam3);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyCoatStorageParsesInboundRetrieveRequestWithNoItems()
    {
        var frame = ReadPayloadFrame(CreatePayload((1, 2), (2, 2001), (4, 0)));

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyCoatStoragePacket.RetrieveActionId, parsed.ActionId);
        Assert.Equal(2001U, parsed.DwParam1);
        Assert.Equal(0U, parsed.DwParam2);
        Assert.Equal(0U, parsed.DwParam3);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyCoatStorageParsesInboundEquipRetrieveNoOpRequestWithNoItems()
    {
        var frame = ReadPayloadFrame(CreatePayload((1, 3), (2, 2001), (4, 0)));

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyCoatStoragePacket.EquipRetrieveNoOpActionId, parsed.ActionId);
        Assert.Equal(2001U, parsed.DwParam1);
        Assert.Equal(0U, parsed.DwParam2);
        Assert.Equal(0U, parsed.DwParam3);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyCoatStorageRoundTripsInboundNoItemSourceShape()
    {
        var inboundBytes = CreateFrameBytes(CreatePayload((1, 5), (3, 9), (4, 0)));
        var frame = ReadFrame(inboundBytes);

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));

        Assert.Equal(inboundBytes, parsed.ToFrameBytes());
    }

    [Fact]
    public void LegacyCoatStorageCreatesTakeOffResponseWithSourceFields()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 2001,
            ItemId = 181325,
            Position = 0,
            WarehouseId = 100,
        };

        var frame = ReadFrame(LegacyCoatStoragePacket.CreateTakeOffResponse(item).ToFrameBytes());

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyCoatStoragePacket.TakeOffActionId, parsed.ActionId);
        Assert.Equal(2001U, parsed.DwParam1);
        Assert.Equal(181325U, parsed.DwParam2);
        Assert.Equal(0U, parsed.DwParam3);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyCoatStorageCreatesInventoryToWardrobeResponseWithSourceFields()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 2001,
            ItemId = 181325,
            Position = 0,
            WarehouseId = 100,
        };

        var frame = ReadFrame(LegacyCoatStoragePacket.CreateInventoryToWardrobeResponse(item).ToFrameBytes());

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyCoatStoragePacket.InventoryToWardrobeActionId, parsed.ActionId);
        Assert.Equal(2001U, parsed.DwParam1);
        Assert.Equal(0U, parsed.DwParam2);
        Assert.Equal(0U, parsed.DwParam3);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyCoatStorageCreatesRetrieveResponseWithSourceFields()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 2001,
            ItemId = 181325,
            Position = 0,
            WarehouseId = 0,
        };

        var frame = ReadFrame(LegacyCoatStoragePacket.CreateRetrieveResponse(item).ToFrameBytes());

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyCoatStoragePacket.RetrieveActionId, parsed.ActionId);
        Assert.Equal(2001U, parsed.DwParam1);
        Assert.Equal(181325U, parsed.DwParam2);
        Assert.Equal(0U, parsed.DwParam3);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyCoatStorageCreatesEquipFromWardrobeResponseWithSourceFields()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 2001,
            ItemId = 181325,
            Position = 9,
            WarehouseId = 100,
        };

        var frame = ReadFrame(LegacyCoatStoragePacket.CreateEquipFromWardrobeResponse(item).ToFrameBytes());

        Assert.True(LegacyCoatStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyCoatStoragePacket.EquipFromWardrobeActionId, parsed.ActionId);
        Assert.Equal(2001U, parsed.DwParam1);
        Assert.Equal(181325U, parsed.DwParam2);
        Assert.Equal(0U, parsed.DwParam3);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyCoatStorageStartupCreatesEmptyGarmentAndMountBuckets()
    {
        var packets = LegacyCoatStoragePacket.CreateLegacyStartup(PlayerItemSnapshot.Empty);

        Assert.Equal(2, packets.Count);
        Assert.All(packets, packet => Assert.Empty(packet.Items));
        Assert.All(
            packets,
            packet => Assert.Equal("0800ac0808002000", Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant()));
    }

    [Fact]
    public void LegacyCoatStorageStartupSendsGarmentsBeforeMounts()
    {
        var garment = new PlayerItemSnapshotItem
        {
            Uid = 10,
            ItemId = 199185,
            Position = 0,
            WarehouseId = 100,
            MaximumDurability = 222,
            Plus = 3,
            Bless = 1,
        };
        var mount = new PlayerItemSnapshotItem
        {
            Uid = 20,
            ItemId = 200614,
            Position = 0,
            WarehouseId = 100,
            MaximumDurability = 333,
        };
        var snapshot = new PlayerItemSnapshot([], [], [], [], [], [mount, garment]);

        var packets = LegacyCoatStoragePacket.CreateLegacyStartup(snapshot);

        Assert.Equal(2, packets.Count);
        var parsedGarment = Assert.Single(packets[0].Items);
        Assert.Equal(garment.Uid, parsedGarment.ItemUid);
        Assert.Equal(garment.ItemId, parsedGarment.ItemId);
        Assert.Equal(3U, parsedGarment.Plus);
        Assert.Equal(1U, parsedGarment.Bless);
        Assert.Equal(222U, parsedGarment.MinDurability);
        Assert.Equal(222U, parsedGarment.MaxDurability);
        Assert.Equal(3U, parsedGarment.Type1);
        Assert.Equal(3U, parsedGarment.Type2);

        var parsedMount = Assert.Single(packets[1].Items);
        Assert.Equal(mount.Uid, parsedMount.ItemUid);
        Assert.Equal(mount.ItemId, parsedMount.ItemId);
        Assert.Equal(333U, parsedMount.MinDurability);
        Assert.Equal(333U, parsedMount.MaxDurability);
    }

    [Fact]
    public void LegacyCoatStorageStartupSendsBucketInReverseSnapshotOrder()
    {
        var first = new PlayerItemSnapshotItem
        {
            Uid = 5157344,
            ItemId = 200743,
            Position = 0,
            WarehouseId = 100,
            MaximumDurability = ushort.MaxValue,
        };
        var second = new PlayerItemSnapshotItem
        {
            Uid = 5157345,
            ItemId = 200765,
            Position = 0,
            WarehouseId = 100,
            MaximumDurability = ushort.MaxValue,
        };
        var third = new PlayerItemSnapshotItem
        {
            Uid = 5157346,
            ItemId = 200614,
            Position = 0,
            WarehouseId = 100,
            MaximumDurability = ushort.MaxValue,
        };
        var fourth = new PlayerItemSnapshotItem
        {
            Uid = 5193797,
            ItemId = 205003,
            Position = 0,
            WarehouseId = 100,
            MaximumDurability = ushort.MaxValue,
        };
        var fifth = new PlayerItemSnapshotItem
        {
            Uid = 5251335,
            ItemId = 200629,
            Position = 0,
            WarehouseId = 100,
            MaximumDurability = ushort.MaxValue,
        };

        var packets = LegacyCoatStoragePacket.CreateLegacyStartup([first, second, third, fourth, fifth]);

        var packet = Assert.Single(packets, candidate => candidate.Items.Count > 0);
        Assert.Equal(
            [5251335U, 5193797U, 5157346U, 5157345U, 5157344U],
            packet.Items.Select(item => item.ItemUid).ToArray());
    }

    [Fact]
    public void LegacyCoatStorageRejectsMissingRequiredDefaultFields()
    {
        var writer = new PacketWriter(LegacyCoatStoragePacket.Opcode, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyCoatStoragePacket.TryParse(frame, out _));
    }

    [Theory]
    [InlineData(new byte[] { 0x20, 0x00 })]
    [InlineData(new byte[] { 0x08, 0x05, 0x18, 0x09 })]
    public void LegacyCoatStorageRejectsMissingRequiredFields(byte[] payload)
    {
        var frame = ReadPayloadFrame(payload);

        Assert.False(LegacyCoatStoragePacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyCoatStorageRejectsIncompleteItemStorageRows()
    {
        var payload = CreatePayload((1, 0), (4, 0));
        WriteBytes(payload, fieldNumber: 5, CreatePayload((1, 1001)));
        var frame = ReadPayloadFrame(payload);

        Assert.False(LegacyCoatStoragePacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static PacketFrame ReadPayloadFrame(IReadOnlyCollection<byte> payload)
    {
        return ReadFrame(CreateFrameBytes(payload));
    }

    private static byte[] CreateFrameBytes(IReadOnlyCollection<byte> payload)
    {
        var writer = new PacketWriter(LegacyCoatStoragePacket.Opcode, payload.Count);
        payload.ToArray().CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static List<byte> CreatePayload(params (int FieldNumber, uint Value)[] fields)
    {
        var payload = new List<byte>();
        foreach (var (fieldNumber, value) in fields)
        {
            WriteVarintField(payload, fieldNumber, value);
        }

        return payload;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | 0);
        WriteVarint(payload, value);
    }

    private static void WriteBytes(List<byte> payload, int fieldNumber, IReadOnlyCollection<byte> value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | 2);
        WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
    }

    private static void WriteVarint(List<byte> payload, ulong value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            payload.Add(next);
        }
        while (value != 0);
    }
}
