using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgAttackPacketTests
{
    [Theory]
    [InlineData(0, MsgAttackType.None)]
    [InlineData(2, MsgAttackType.Physical)]
    [InlineData(24, MsgAttackType.Magic)]
    [InlineData(28, MsgAttackType.Archer)]
    [InlineData(8, MsgAttackType.RequestMarriage)]
    [InlineData(9, MsgAttackType.AcceptMarriage)]
    [InlineData(14, MsgAttackType.Death)]
    [InlineData(26, MsgAttackType.Reflect)]
    [InlineData(27, MsgAttackType.Dash)]
    [InlineData(36, MsgAttackType.UpdateHunterJar)]
    [InlineData(44, MsgAttackType.CounterKillSwitch)]
    [InlineData(43, MsgAttackType.Scapegoat)]
    [InlineData(45, MsgAttackType.FatalStrike)]
    [InlineData(46, MsgAttackType.InteractionRequest)]
    [InlineData(47, MsgAttackType.InteractionAccept)]
    [InlineData(48, MsgAttackType.InteractionRefuse)]
    [InlineData(49, MsgAttackType.InteractionEffect)]
    [InlineData(50, MsgAttackType.InteractionStopEffect)]
    [InlineData(53, MsgAttackType.InMoveSpell)]
    [InlineData(55, MsgAttackType.BlueDamage)]
    [InlineData(57, MsgAttackType.BackFire)]
    public void MsgAttackTypeDefinesLegacyNumericValues(int expected, MsgAttackType type)
    {
        Assert.Equal((uint)expected, (uint)type);
    }

    [Fact]
    public void MsgAttackEffectDefinesLegacyNumericValues()
    {
        Assert.Equal(0U, (uint)MsgAttackEffect.None);
        Assert.Equal(1U, (uint)MsgAttackEffect.Block);
        Assert.Equal(2U, (uint)MsgAttackEffect.Penetration);
        Assert.Equal(4U, (uint)MsgAttackEffect.CriticalStrike);
        Assert.Equal(8U, (uint)MsgAttackEffect.Imunity);
        Assert.Equal(0xAU, (uint)MsgAttackEffect.Break);
        Assert.Equal(0x10U, (uint)MsgAttackEffect.ResistMetal);
        Assert.Equal(0x20U, (uint)MsgAttackEffect.ResistWood);
        Assert.Equal(0x40U, (uint)MsgAttackEffect.ResistWater);
        Assert.Equal(0x80U, (uint)MsgAttackEffect.ResistFire);
        Assert.Equal(0x100U, (uint)MsgAttackEffect.ResistEarth);
        Assert.Equal(0x200U, (uint)MsgAttackEffect.AddStudyPoint);
        Assert.Equal(0x400U, (uint)MsgAttackEffect.LuckyStrike);
        Assert.Equal(0x2000U, (uint)MsgAttackEffect.NatureShield);
        Assert.Equal(0x4000U, (uint)MsgAttackEffect.Assasssin);
        Assert.Equal(0x8000U, (uint)MsgAttackEffect.BurningSky);
        Assert.Equal(0x10000U, (uint)MsgAttackEffect.FireCurse);
        Assert.Equal(0x40000U, (uint)MsgAttackEffect.TailedBeast);
        Assert.Equal(0x1000000U, (uint)MsgAttackEffect.Destroy);
        Assert.Equal(0x4000000U, (uint)MsgAttackEffect.BonePulseDestroy);
        Assert.Equal(0x8000000U, (uint)MsgAttackEffect.Dash);
    }

    [Fact]
    public void MsgAttackOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2135, WorldPacketOpcodes.MsgAttack);
        Assert.Equal(WorldPacketOpcodes.MsgAttack, MsgAttackPacket.Opcode);
    }

    [Fact]
    public void MsgAttackPacketWritesLegacyProtobufFrameShape()
    {
        var packet = new MsgAttackPacket(new MsgAttackQuery(
            Uid: 900_001,
            OpponentUid: 800_002,
            X: 123,
            Y: 456,
            AttackType: MsgAttackType.Physical,
            Damage: 12_345,
            KilledMonster: 0,
            ResponseDamage: 12_345,
            Effect: MsgAttackEffect.Block | MsgAttackEffect.CriticalStrike,
            Unknown: 0));

        Assert.Equal(
        [
            0x23, 0x00, 0x57, 0x08, 0x20, 0xA1, 0xF7, 0x36,
            0x28, 0x82, 0xEA, 0x30, 0x38, 0x7B, 0x40, 0xC8,
            0x03, 0x68, 0x02, 0x78, 0xB9, 0x60, 0x88, 0x01,
            0x00, 0xA8, 0x01, 0xB9, 0x60, 0xC8, 0x01, 0x05,
            0xD0, 0x01, 0x00,
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void MsgAttackPacketRoundTripsQuery()
    {
        var packet = new MsgAttackPacket(new MsgAttackQuery(
            Uid: 900_001,
            OpponentUid: 800_002,
            X: 123,
            Y: 456,
            AttackType: MsgAttackType.Magic,
            Damage: 98_765,
            KilledMonster: 1,
            ResponseDamage: 3_210,
            Effect: MsgAttackEffect.CriticalStrike | MsgAttackEffect.LuckyStrike,
            Unknown: 77,
            CombatGear: 0x0102_0304_0506_0708,
            SpellLevel: 4,
            SpellId: 1045));
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgAttackPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void MsgAttackPacketParsesSyntheticPhysicalAttackWithDefaultResponseFields()
    {
        var frame = ReadFrame(CreateSyntheticFrame(
            (4, 900_001), (5, 800_002), (7, 123), (8, 456), (13, 2), (15, 321)));

        Assert.True(MsgAttackPacket.TryParse(frame, out var parsed));
        Assert.Equal(900_001U, parsed.Query.Uid);
        Assert.Equal(800_002U, parsed.Query.OpponentUid);
        Assert.Equal(123, parsed.Query.X);
        Assert.Equal(456, parsed.Query.Y);
        Assert.Equal(MsgAttackType.Physical, parsed.Query.AttackType);
        Assert.Equal(321, parsed.Query.Damage);
        Assert.Equal(0, parsed.Query.KilledMonster);
        Assert.Equal(0U, parsed.Query.ResponseDamage);
        Assert.Equal(MsgAttackEffect.None, parsed.Query.Effect);
        Assert.Equal(0U, parsed.Query.Unknown);
    }

    [Theory]
    [InlineData(2080720235U, (ushort)1219, (ushort)22125, (ushort)46334, 800002U, (ushort)123, (ushort)456, (ushort)1234)]
    [InlineData(4205086057U, (ushort)1024, (ushort)3699, (ushort)28744, 0U, (ushort)1, (ushort)65535, (ushort)60000)]
    public void MsgAttackPacketDecodesIndependentSyntheticMagicVectors(
        uint encodedOpponentUid,
        ushort encodedX,
        ushort encodedY,
        ushort encodedSpellId,
        uint expectedOpponentUid,
        ushort expectedX,
        ushort expectedY,
        ushort expectedSpellId)
    {
        // Constants were calculated from synthetic inputs independently of the packet codec.
        var frame = ReadFrame(CreateSyntheticFrame(
            (4, 900_001), (5, encodedOpponentUid), (7, encodedX), (8, encodedY),
            (13, 24), (19, encodedSpellId)));
        Assert.True(MsgAttackPacket.TryParse(frame, out var parsed));
        Assert.Equal(900_001U, parsed.Query.Uid);
        Assert.Equal(MsgAttackType.Magic, parsed.Query.AttackType);
        Assert.Equal(encodedSpellId, parsed.Query.SpellId);
        Assert.Equal(encodedOpponentUid, parsed.Query.OpponentUid);
        Assert.Equal(encodedX, parsed.Query.X);
        Assert.Equal(encodedY, parsed.Query.Y);

        var decoded = MsgAttackPacket.DecodeMagicAttack(parsed.Query);
        Assert.Equal(expectedSpellId, decoded.SpellId);
        Assert.Equal(expectedOpponentUid, decoded.OpponentUid);
        Assert.Equal(expectedX, decoded.X);
        Assert.Equal(expectedY, decoded.Y);
    }

    [Fact]
    public void MsgAttackPacketRejectsWrongOpcode()
    {
        var bytes = new MsgAttackPacket(CreatePhysicalQuery()).ToFrameBytes();
        bytes[2] = 0x58;
        var frame = ReadFrame(bytes);

        Assert.False(MsgAttackPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgAttackPacketRejectsTruncatedVarintPayload()
    {
        var frame = ReadFrame(
        [
            0x06, 0x00, 0x57, 0x08,
            0x20, 0x80
        ]);

        Assert.False(MsgAttackPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgAttackPacketRejectsOverlongVarintPayload()
    {
        var payload = new List<byte>
        {
            0xF8, 0x01,
            0x80, 0x80, 0x80, 0x80, 0x80,
            0x80, 0x80, 0x80, 0x80, 0x02
        };
        payload.AddRange(new MsgAttackPacket(CreatePhysicalQuery()).ToFrameBytes().Skip(4));
        var writer = new PacketWriter(MsgAttackPacket.Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgAttackPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgAttackPacketRejectsUnsupportedUnknownWireType()
    {
        var payload = new List<byte>
        {
            0xFB, 0x01
        };
        payload.AddRange(new MsgAttackPacket(CreatePhysicalQuery()).ToFrameBytes().Skip(4));
        var writer = new PacketWriter(MsgAttackPacket.Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgAttackPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgAttackPacketPreservesUnknownAttackTypeAndEffectValues()
    {
        const uint unknownType = 0xFFEE;
        const uint unknownEffect = 0xA5A5_0000;
        var packet = new MsgAttackPacket(CreatePhysicalQuery() with
        {
            AttackType = (MsgAttackType)unknownType,
            Effect = (MsgAttackEffect)unknownEffect
        });
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgAttackPacket.TryParse(frame, out var parsed));

        Assert.Equal(unknownType, (uint)parsed.Query.AttackType);
        Assert.Equal(unknownEffect, (uint)parsed.Query.Effect);
    }

    [Fact]
    public void MsgAttackPacketOmitsDefaultOptionalFieldsAndReadsThemAsZero()
    {
        var frame = ReadFrame(new MsgAttackPacket(CreatePhysicalQuery()).ToFrameBytes());

        Assert.True(MsgAttackPacket.TryParse(frame, out var parsed));

        Assert.Equal(0UL, parsed.Query.CombatGear);
        Assert.Equal(0, parsed.Query.SpellLevel);
        Assert.Equal(0, parsed.Query.SpellId);
        Assert.DoesNotContain(frame.Payload.ToArray(), b => b is 0x80 or 0x90 or 0x98);
    }

    [Fact]
    public void MsgAttackPacketSkipsSupportedUnknownProtobufFields()
    {
        var bytes = new byte[]
        {
            0x27, 0x00, 0x57, 0x08, 0x20, 0xA1, 0xF7, 0x36,
            0x28, 0x82, 0xEA, 0x30, 0x38, 0x7B, 0x40, 0xC8,
            0x03, 0x68, 0x02, 0x78, 0xB9, 0x60, 0x88, 0x01,
            0x00, 0xA8, 0x01, 0xB9, 0x60, 0xC8, 0x01, 0x05,
            0xD0, 0x01, 0x00, 0xD8, 0x06, 0xB9, 0x60,
        };

        Assert.True(MsgAttackPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(CreatePhysicalQuery(), parsed.Query);
    }

    [Fact]
    public void EncodeMagicAttackMatchesSyntheticVector()
    {
        var encoded = MsgAttackPacket.EncodeMagicAttack(CreatePhysicalQuery() with
        {
            AttackType = MsgAttackType.Magic,
            Damage = 1045
        });

        Assert.Equal(924964630, encoded.Damage);
        Assert.Equal(2080720235U, encoded.OpponentUid);
        Assert.Equal(1219, encoded.X);
        Assert.Equal(22125, encoded.Y);
    }

    [Fact]
    public void DecodeMagicAttackMatchesSyntheticVector()
    {
        var decoded = MsgAttackPacket.DecodeMagicAttack(CreatePhysicalQuery() with
        {
            AttackType = MsgAttackType.Magic,
            OpponentUid = 2080720235,
            X = 1219,
            Y = 22125,
            SpellId = 55062
        });

        Assert.Equal(1045, decoded.SpellId);
        Assert.Equal(800_002U, decoded.OpponentUid);
        Assert.Equal(123, decoded.X);
        Assert.Equal(456, decoded.Y);
    }

    [Fact]
    public void MagicEncodeAndDecodeAreInverseForLegacyLowerWordFields()
    {
        var original = CreatePhysicalQuery() with
        {
            AttackType = MsgAttackType.Magic,
            Damage = 1045
        };

        var encoded = MsgAttackPacket.EncodeMagicAttack(original);
        var decoded = MsgAttackPacket.DecodeMagicAttack(encoded with
        {
            SpellId = unchecked((ushort)encoded.Damage)
        });

        Assert.Equal((ushort)original.Damage, decoded.SpellId);
        Assert.Equal(original.OpponentUid, decoded.OpponentUid);
        Assert.Equal(original.X, decoded.X);
        Assert.Equal(original.Y, decoded.Y);
    }

    private static MsgAttackQuery CreatePhysicalQuery()
    {
        return new MsgAttackQuery(
            Uid: 900_001,
            OpponentUid: 800_002,
            X: 123,
            Y: 456,
            AttackType: MsgAttackType.Physical,
            Damage: 12_345,
            KilledMonster: 0,
            ResponseDamage: 12_345,
            Effect: MsgAttackEffect.Block | MsgAttackEffect.CriticalStrike,
            Unknown: 0);
    }

    private static byte[] CreateSyntheticFrame(params (int Field, ulong Value)[] fields)
    {
        // Wire construction intentionally does not invoke MsgAttackPacket.ToFrameBytes.
        var payload = new List<byte>();
        foreach (var (field, value) in fields)
        {
            Append((ulong)field << 3);
            Append(value);
        }

        var writer = new PacketWriter(MsgAttackPacket.Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();

        void Append(ulong value)
        {
            while (value >= 128)
            {
                payload.Add((byte)((value % 128) | 128));
                value /= 128;
            }

            payload.Add((byte)value);
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
