using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterSpawnPacketTests
{
    [Fact]
    public void LegacyMonsterSpawnOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal(WorldPacketOpcodes.LegacyRoleSpawn, LegacyMonsterSpawnPacket.Opcode);
    }

    [Fact]
    public void LegacyMonsterSpawnPacketWritesLegacyMonsterRoleProtobufSlice()
    {
        var packet = LegacyMonsterSpawnPacket.FromMonsterRole(
            uid: 200,
            mesh: 51700,
            level: 72,
            hitpoints: 1234,
            maxLife: 1500,
            x: 10,
            y: 20,
            action: 100,
            monsterId: 1,
            name: "TestMobA");

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyMonsterSpawnPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0xF4, 0x93, 0x03,
            0x10, 0xC8, 0x01,
            0x18, 0x00,
            0x20, 0x00,
            0x28, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x40, 0x00,
            0x48, 0x00,
            0x50, 0x00,
            0x58, 0x00,
            0x60, 0x00,
            0xA8, 0x01, 0xD2, 0x09,
            0xB8, 0x01, 0xDC, 0x0B,
            0xD0, 0x01, 0x48,
            0xD8, 0x01, 0x0A,
            0xE0, 0x01, 0x14,
            0xE8, 0x01, 0x00,
            0xF0, 0x01, 0x00,
            0xF8, 0x01, 0x64,
            0x90, 0x02, 0x00,
            0x98, 0x02, 0x48,
            0xA0, 0x02, 0x00,
            0xA8, 0x02, 0x00,
            0xB0, 0x02, 0x00,
            0xD0, 0x02, 0x00,
            0xD8, 0x02, 0x00,
            0xF8, 0x03, 0x00,
            0x80, 0x04, 0x00,
            0x88, 0x04, 0x00,
            0x90, 0x04, 0x00,
            0x98, 0x04, 0x00,
            0xA0, 0x04, 0x00,
            0xA8, 0x04, 0x00,
            0xF8, 0x04, 0x00,
            0xDA, 0x06, 0x08, 0x54, 0x65, 0x73, 0x74, 0x4D, 0x6F, 0x62, 0x41,
            0xDA, 0x06, 0x00,
            0xDA, 0x06, 0x00,
            0xDA, 0x06, 0x00,
            0xE0, 0x06, 0x00,
            0xF0, 0x06, 0x00,
            0xF8, 0x06, 0x01,
            0x80, 0x07, 0x00,
            0x88, 0x07, 0x00,
            0xC8, 0x07, 0x00,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyMonsterSpawnPacketRoundTrips()
    {
        var packet = LegacyMonsterSpawnPacket.FromMonsterRole(
            uid: 200,
            mesh: 51700,
            level: 72,
            hitpoints: 1234,
            maxLife: 1500,
            x: 10,
            y: 20,
            action: 100,
            monsterId: 1,
            name: "TestMobA");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMonsterSpawnPacket.TryParse(frame, out var parsed));

        Assert.Equal(51700U, parsed.Mesh);
        Assert.Equal(200U, parsed.Uid);
        Assert.Equal(1234U, parsed.Hitpoints);
        Assert.Equal(1500U, parsed.MaxLife);
        Assert.Equal(72, parsed.MonsterLevel);
        Assert.Equal(10, parsed.X);
        Assert.Equal(20, parsed.Y);
        Assert.Equal(100, parsed.Action);
        Assert.Equal(72, parsed.Level);
        Assert.False(parsed.Boss);
        Assert.Equal(1U, parsed.MonsterId);
        Assert.Equal(["TestMobA", string.Empty, string.Empty, string.Empty], parsed.Names);
        Assert.Equal(new ulong[10], parsed.StatusFlags);
    }

    [Fact]
    public void LegacyMonsterSpawnPacketOmitsOptionalZeroHitpointFieldsLikeSpawnPacketProto()
    {
        var packet = LegacyMonsterSpawnPacket.FromMonsterRole(
            uid: 201,
            mesh: 37,
            level: 1,
            hitpoints: 0,
            maxLife: 0,
            x: 10,
            y: 20,
            action: 0,
            monsterId: 37,
            name: "TestMob2");
        var frame = ReadFrame(packet.ToFrameBytes());

        var fields = ReadPayloadFieldNumbers(frame.Payload.ToArray());

        Assert.DoesNotContain(21, fields);
        Assert.DoesNotContain(23, fields);
        Assert.True(LegacyMonsterSpawnPacket.TryParse(frame, out var parsed));
        Assert.Equal(0U, parsed.Hitpoints);
        Assert.Equal(0U, parsed.MaxLife);
    }

    [Fact]
    public void LegacyMonsterSpawnPacketRoundTripsOwnerPetProjection()
    {
        var packet = LegacyMonsterSpawnPacket.FromOwnerPet(
            uid: 700_101,
            mesh: 138,
            level: 73,
            hitpoints: 4321,
            maxLife: 5000,
            x: 841,
            y: 589,
            ownerUid: 900_001,
            ownerFacing: 5,
            ownerGuildId: 1370,
            ownerPet: 9,
            ownerPet1: 15,
            monsterId: 9001,
            name: "TestMob3");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMonsterSpawnPacket.TryParse(frame, out var parsed));

        Assert.Equal(138U, parsed.Mesh);
        Assert.Equal(700_101U, parsed.Uid);
        Assert.Equal(73, parsed.MonsterLevel);
        Assert.Equal(4321U, parsed.Hitpoints);
        Assert.Equal(5000U, parsed.MaxLife);
        Assert.Equal(841, parsed.X);
        Assert.Equal(589, parsed.Y);
        Assert.Equal(900_001U, parsed.OwnerUid);
        Assert.Equal(9U, parsed.OwnerPet);
        Assert.Equal(15U, parsed.OwnerPet1);
        Assert.Equal(5, parsed.Facing);
        Assert.Equal(1370U, parsed.GuildId);
        Assert.Equal(9001U, parsed.MonsterId);
        Assert.Equal(["TestMob3", string.Empty, string.Empty, string.Empty], parsed.Names);
    }

    [Fact]
    public void LegacyMonsterSpawnPacketMapsOwnerPetMeshForNewerClient()
    {
        var packet = LegacyMonsterSpawnPacket.FromOwnerPet(
            uid: 700_556,
            mesh: 556,
            level: 48,
            hitpoints: 1578,
            maxLife: 1578,
            x: 467,
            y: 483,
            ownerUid: 900_001,
            ownerFacing: 5,
            ownerGuildId: 1370,
            ownerPet: 9,
            ownerPet1: 15,
            monsterId: 69,
            name: "TestMob4",
            clientCompatibility: LegacyClientCompatibilityOptions.NewerClient);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMonsterSpawnPacket.TryParse(frame, out var parsed));
        Assert.Equal(557U, parsed.Mesh);
        Assert.Equal(69U, parsed.MonsterId);
    }

    [Fact]
    public void LegacyMonsterSpawnPacketProjectsMesh980OwnerPetConstants()
    {
        var packet = LegacyMonsterSpawnPacket.FromOwnerPet(
            uid: 700_980,
            mesh: 980,
            level: 90,
            hitpoints: 6500,
            maxLife: 7000,
            x: 100,
            y: 200,
            ownerUid: 900_001,
            ownerFacing: 6,
            ownerGuildId: 1370,
            ownerPet: 99,
            ownerPet1: 15,
            monsterId: 9999,
            name: "TestMob5");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMonsterSpawnPacket.TryParse(frame, out var parsed));

        Assert.Equal(980U, parsed.Mesh);
        Assert.Equal(3U, parsed.OwnerPet);
        Assert.Equal(15U, parsed.OwnerPet1);
        Assert.Equal(4264U, parsed.MonsterId);
        Assert.Equal(900_001U, parsed.OwnerUid);
        Assert.Equal(6, parsed.Facing);
        Assert.Equal(1370U, parsed.GuildId);
        Assert.Equal(["TestMob5", string.Empty, string.Empty, string.Empty], parsed.Names);
    }

    [Fact]
    public void LegacyMonsterSpawnPacketScalesBossHitpointsLikeMonsterRoleGetArray()
    {
        var packet = LegacyMonsterSpawnPacket.FromMonsterRole(
            uid: 300,
            mesh: 4212,
            level: 120,
            hitpoints: 50000,
            maxLife: 100000,
            x: 30,
            y: 40,
            action: 0,
            monsterId: 4212,
            name: "TestMob6",
            boss: true);

        Assert.True(packet.Boss);
        Assert.Equal(5000U, packet.Hitpoints);
    }

    [Theory]
    [InlineData(17U, 900_001U, 127U, 128U, (byte)3, (ushort)10, (ushort)20, 501U, "SyntheticA")]
    [InlineData(129U, 900_002U, 16_383U, 16_384U, (byte)25, (ushort)127, (ushort)128, 502U, "SyntheticB")]
    [InlineData(16_385U, 900_003U, 0U, 0U, (byte)1, (ushort)0, (ushort)65_535, 503U, "SyntheticC")]
    [InlineData(257U, 900_004U, 65_535U, 65_536U, (byte)255, (ushort)256, (ushort)257, 504U, "SyntheticD")]
    public void LegacyMonsterSpawnPacketParsesIndependentSyntheticRolePayloads(
        uint expectedMesh,
        uint expectedUid,
        uint expectedHitpoints,
        uint expectedMaxLife,
        byte expectedLevel,
        ushort expectedX,
        ushort expectedY,
        uint expectedMonsterId,
        string expectedName)
    {
        // Construct the wire fields directly, without using the production serializer.
        var payload = new List<byte>();
        int[] zeroFields = [3, 4, 5, 8, 9, 10, 11, 12, 29, 30, 31, 34, 36, 37, 38,
            42, 43, 63, 64, 65, 66, 67, 68, 69, 79, 108, 110, 112, 113, 121];
        foreach (var field in zeroFields)
        {
            AddSyntheticVarint(payload, (ulong)field << 3);
            AddSyntheticVarint(payload, 0);
        }

        (int Field, ulong Value)[] populatedFields =
        [
            (1, expectedMesh), (2, expectedUid), (21, expectedHitpoints),
            (23, expectedMaxLife), (26, expectedLevel), (27, expectedX),
            (28, expectedY), (35, expectedLevel), (111, expectedMonsterId)
        ];
        foreach (var (field, value) in populatedFields)
        {
            AddSyntheticVarint(payload, (ulong)field << 3);
            AddSyntheticVarint(payload, value);
        }

        for (var index = 0; index < 10; index++)
        {
            AddSyntheticVarint(payload, 6UL << 3);
            AddSyntheticVarint(payload, 0);
        }

        foreach (var name in new[] { expectedName, string.Empty, string.Empty, string.Empty })
        {
            var encoded = System.Text.Encoding.UTF8.GetBytes(name);
            AddSyntheticVarint(payload, (107UL << 3) | 2);
            AddSyntheticVarint(payload, (ulong)encoded.Length);
            payload.AddRange(encoded);
        }

        var writer = new PacketWriter(LegacyMonsterSpawnPacket.Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        Assert.True(LegacyMonsterSpawnPacket.TryParse(ReadFrame(writer.ToArray()), out var parsed));
        Assert.Equal(expectedMesh, parsed.Mesh);
        Assert.Equal(expectedUid, parsed.Uid);
        Assert.Equal(expectedHitpoints, parsed.Hitpoints);
        Assert.Equal(expectedMaxLife, parsed.MaxLife);
        Assert.Equal(expectedLevel, parsed.MonsterLevel);
        Assert.Equal(expectedLevel, parsed.Level);
        Assert.Equal(expectedX, parsed.X);
        Assert.Equal(expectedY, parsed.Y);
        Assert.Equal(expectedMonsterId, parsed.MonsterId);
        Assert.Equal([expectedName, string.Empty, string.Empty, string.Empty], parsed.Names);
        Assert.Equal(new ulong[10], parsed.StatusFlags);
    }

    [Fact]
    public void LegacyMonsterSpawnPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyMonsterSpawnPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyMonsterSpawnPacket.TryParse(frame, out _));
    }

    private static void AddSyntheticVarint(List<byte> payload, ulong value)
    {
        while (value >= 128)
        {
            payload.Add((byte)((value % 128) | 128));
            value /= 128;
        }

        payload.Add((byte)value);
    }

    private static IReadOnlyList<int> ReadPayloadFieldNumbers(byte[] payload)
    {
        var fields = new List<int>();
        var offset = 0;
        while (offset < payload.Length)
        {
            var tag = ReadVarint(payload, ref offset);
            fields.Add((int)(tag >> 3));

            switch (tag & 0x07)
            {
                case 0:
                    _ = ReadVarint(payload, ref offset);
                    break;
                case 2:
                    var length = ReadVarint(payload, ref offset);
                    Assert.True(length <= int.MaxValue);
                    Assert.True(offset <= payload.Length - (int)length);
                    offset += (int)length;
                    break;
                default:
                    Assert.Fail($"Unexpected wire type {tag & 0x07}.");
                    break;
            }
        }

        return fields;
    }

    private static ulong ReadVarint(byte[] payload, ref int offset)
    {
        var value = 0UL;
        for (var shift = 0; shift < 64; shift += 7)
        {
            Assert.True(offset < payload.Length);
            var current = payload[offset++];
            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return value;
            }
        }

        Assert.Fail("Malformed varint.");
        return 0;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
