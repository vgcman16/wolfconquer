using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCombatGearStartupEffectPacketTests
{
    [Fact]
    public void PirateRevelatorStartupFramesMatchSyntheticExpectedLayouts()
    {
        var snapshot = CreateSnapshot(classCode: 7051, x: 29, y: 43);
        var playerSpells = new PlayerSpellSnapshot(
            [new PlayerSpellSnapshotEntry(21_520, Level: 3, Experience: 0, PreviousLevel: 0, SoulLevel: 0, UseSpellSoul: 2)]);
        Assert.True(LegacyCombatGearStartupEffectPacket.TryCreatePirateRevelatorStartupFrames(
            snapshot, playerSpells, out var frames));
        Assert.Equal(4, frames.Count);

        // Independent tagged-field fixtures with an invented player and position.
        Assert.Equal(Frame(2135, Fields(
            (4, 73), (5, 73), (7, 29), (8, 43), (13, 0), (15, 0),
            (17, 0), (18, 3), (19, 21_520), (21, 0), (25, 0), (26, 0))), frames[0]);
        var statusFields = new List<(int Field, ulong Value)> { (1, 25) };
        for (var index = 0; index < 18; index++)
        {
            statusFields.Add((3, index == 12 ? 1UL << 24 : 0UL));
        }
        Assert.Equal(Frame(2176, [.. Fields((1, 73)), .. Nested(2, Fields(statusFields.ToArray()))]), frames[1]);
        Assert.Equal(Frame(2176, [.. Fields((1, 73)), .. Nested(2, Fields((1, 120), (4, 408), (6, 500)))]), frames[2]);
        var target = Fields((1, 73), (2, 0), (3, 1), (4, 0), (6, 0), (7, 0), (8, 0), (9, 0));
        Assert.Equal(Frame(2239,
            [.. Fields((1, 73), (2, 73), (5, 21_520), (6, 3), (7, 0), (8, 2), (9, 0)), .. Nested(10, target)]), frames[3]);
    }

    [Fact]
    public void PirateRevelatorStartupFramesRequirePirateAndSpell()
    {
        var playerSpells = new PlayerSpellSnapshot(
            [new PlayerSpellSnapshotEntry(21_520, Level: 9, Experience: 0, PreviousLevel: 0, SoulLevel: 0, UseSpellSoul: 0)]);

        Assert.False(LegacyCombatGearStartupEffectPacket.TryCreatePirateRevelatorStartupFrames(
            CreateSnapshot(classCode: 4051, x: 29, y: 43),
            playerSpells,
            out _));
        Assert.False(LegacyCombatGearStartupEffectPacket.TryCreatePirateRevelatorStartupFrames(
            CreateSnapshot(classCode: 7051, x: 29, y: 43),
            PlayerSpellSnapshot.Empty,
            out _));
    }

    private static byte[] Frame(ushort opcode, byte[] payload)
    {
        var bytes = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode);
        payload.CopyTo(bytes, 4);
        return bytes;
    }

    private static byte[] Fields(params (int Field, ulong Value)[] fields)
    {
        var result = new List<byte>();
        foreach (var (field, value) in fields)
        {
            AddVarint(result, (ulong)(field << 3));
            AddVarint(result, value);
        }
        return result.ToArray();
    }

    private static byte[] Nested(int field, byte[] payload)
    {
        var result = new List<byte>();
        AddVarint(result, (ulong)((field << 3) | 2));
        AddVarint(result, (ulong)payload.Length);
        result.AddRange(payload);
        return result.ToArray();
    }

    private static void AddVarint(List<byte> result, ulong value)
    {
        while (value >= 128)
        {
            result.Add((byte)((value & 127) | 128));
            value >>= 7;
        }
        result.Add((byte)value);
    }

    private static CharacterSnapshot CreateSnapshot(long classCode, long x, long y)
    {
        return new CharacterSnapshot(
            Uid: 73,
            Name: "FixturePirate",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: classCode,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: x,
            Y: y,
            PreviousMapId: 1002,
            PreviousMapX: x,
            PreviousMapY: y,
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
            SourceFile: "synthetic-pirate.ini",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
