using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPlayerStatusPacketTests
{
    [Fact]
    public void LegacyPlayerStatusOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2301, WorldPacketOpcodes.LegacyPlayerStatus);
        Assert.Equal(WorldPacketOpcodes.LegacyPlayerStatus, LegacyPlayerStatusPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyPlayerStatus, LegacyPlayerStatusQueryPacket.Opcode);
    }

    [Fact]
    public void LegacyPlayerStatusQueryRoundTripsTargetUid()
    {
        var packet = new LegacyPlayerStatusQueryPacket(900_001);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal((ushort)8, frame.Header.Length);
        Assert.Equal(WorldPacketOpcodes.LegacyPlayerStatus, frame.Header.Opcode);
        Assert.Equal(LegacyPlayerStatusQueryPacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyPlayerStatusQueryPacket.TryParse(frame, out var parsed));
        Assert.Equal(900_001U, parsed.Uid);
    }

    [Fact]
    public void LegacyPlayerStatusQueryAcceptsLegacyTrailingBytes()
    {
        var writer = new PacketWriter(LegacyPlayerStatusQueryPacket.Opcode, payloadLength: 8);
        writer.WriteUInt32(0, 900_001);
        writer.WriteUInt32(4, 42);
        var frame = ReadFrame(writer.ToArray());

        Assert.True(LegacyPlayerStatusQueryPacket.TryParse(frame, out var parsed));
        Assert.Equal(900_001U, parsed.Uid);
    }

    [Fact]
    public void LegacyPlayerStatusQueryResolvesZeroUidToCurrentSession()
    {
        var snapshot = CharacterSnapshotParser.ParseText(
            """
            [Character]
            UID=900001
            Name=SyntheticStatus
            Body=1003
            Face=12
            Haire=31
            Class=1001
            FirstClass=0
            SecoundeClass=1000
            Reborn=1
            Map=1000
            X=123
            Y=456
            Level=42
            Experience=12345
            PMap=1000
            PMapX=10
            PMapY=20
            Strength=10
            Agility=20
            Vitaliti=30
            Spirit=40
            Atributes=5
            MinHitPoints=1234
            MinMana=567
            Money=0
            ConquerPoints=0
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=0
            """);
        var session = new WorldSession(
            2,
            "2",
            snapshot,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);

        var query = new LegacyPlayerStatusQueryPacket(0);

        Assert.Equal(900_001U, query.ResolveTargetUid(session));
    }

    [Fact]
    public void LegacyPlayerStatusParsesIndependentLittleEndianPrefix()
    {
        var bytes = new byte[164];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0), 164);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2301);
        uint[] prefix = [0x0102_0304, 257, 258, 259, 260, 261, 262];
        for (var index = 0; index < prefix.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + index * 4), prefix[index]);
        }

        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(0x0102_0304U, packet.Uid);
        Assert.Equal(257U, packet.MaxHP);
        Assert.Equal(258U, packet.MaxMana);
        Assert.Equal(259U, packet.MaxAttack);
        Assert.Equal(260U, packet.MinAttack);
        Assert.Equal(261U, packet.Defence);
        Assert.Equal(262U, packet.MagicAttack);
        Assert.Equal(40, packet.Fields.Count);
        Assert.All(packet.Fields.Skip(7), field => Assert.Equal(0U, field));
    }

    [Fact]
    public void LegacyPlayerStatusWritesLegacyFixedFortyFieldFrame()
    {
        var fields = CreateSequentialFields();
        var packet = new LegacyPlayerStatusPacket(fields);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(164, bytes.Length);
        Assert.Equal((ushort)164, frame.Header.Length);
        Assert.Equal((ushort)2301, frame.Header.Opcode);
        Assert.Equal(LegacyPlayerStatusPacket.PayloadLength, frame.Payload.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        for (var index = 0; index < LegacyPlayerStatusPacket.FieldCount; index++)
        {
            Assert.Equal((uint)(index + 1), reader.ReadUInt32(index * sizeof(uint)));
        }
    }

    [Fact]
    public void LegacyPlayerStatusRoundTripsAllFields()
    {
        var fields = CreateSequentialFields();
        var packet = new LegacyPlayerStatusPacket(fields);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyPlayerStatusPacket.TryParse(frame, out var parsed));

        Assert.Equal(fields, parsed.Fields);
        Assert.Equal(1U, parsed.Uid);
        Assert.Equal(40U, parsed.Fields[^1]);
    }

    [Fact]
    public void LegacyPlayerStatusFactoryWritesDistinctSyntheticFieldsInOrder()
    {
        var packet = LegacyPlayerStatusPacket.Create(
            uid: 1,
            maxHp: 2,
            maxMana: 3,
            maxAttack: 4,
            minAttack: 5,
            defence: 6,
            magicAttack: 7,
            magicDefence: 8,
            dodge: 9,
            agilityAttack: 10,
            accuracy: 11,
            physicalPercent: 12,
            magicPercent: 13,
            mDefence: 14,
            damage: 15,
            itemBless: 16,
            criticalStrike: 17,
            skillCStrike: 18,
            immunity: 19,
            penetration: 20,
            block: 21,
            breakthrough: 22,
            counteraction: 23,
            detoxication: 24,
            physicalDamageIncrease: 25,
            magicDamageIncrease: 26,
            physicalDamageDecrease: 27,
            magicDamageDecrease: 28,
            metalResistance: 29,
            woodResistance: 30,
            waterResistance: 31,
            fireResistance: 32,
            earthResistance: 33,
            prestigeLevel: 34,
            runeScore: 35,
            dashRate: 36,
            luckyStrikePercent: 37,
            strikeLockPercent: 38,
            dodgeRate: 40);

        var expected = new byte[164];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0), 164);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2301);
        for (var index = 0; index < 40; index++)
        {
            var value = index == 38 ? 0U : (uint)(index + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + index * 4), value);
        }

        Assert.Equal(expected, packet.ToFrameBytes());
        Assert.Equal(40, packet.Fields.Count);
        Assert.Equal(8U, packet.MagicDefence);
        Assert.Equal(34U, packet.PrestigeLevel);
        Assert.Equal(0U, packet.ReservedZero);
        Assert.Equal(40U, packet.DodgeRate);
    }

    private static uint[] CreateSequentialFields()
    {
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        for (var index = 0; index < fields.Length; index++)
        {
            fields[index] = (uint)(index + 1);
        }

        return fields;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
