using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCombatGearPacketTests
{
    [Fact]
    public void CombatGearLoadMatchesLegacyRequiredProtobufShape()
    {
        var packet = new LegacyCombatGearPacket(
            LegacyCombatGearPacketType.Load,
            1_293_600,
            [
                new LegacyCombatGearItem(
                    Id: 701,
                    Level: 4,
                    Exp: 0,
                    Equip: 0,
                    Transform: 0,
                    Data1: 16_907,
                    Data2: 1_907,
                    Data3: 19_907,
                    Data4: 14_907,
                    Data5: 3_907,
                    Data6: 2_907,
                    MasteryScore: 928_000)
            ]);

        var bytes = packet.ToFrameBytes();

        Assert.Equal(
            "2f007409080010a0fa4e1a2308bd051004180020002800308b840138f30e40c39b0148bb7450c31e58db166080d238",
            Convert.ToHexString(bytes).ToLowerInvariant());
        Assert.True(LegacyCombatGearPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.Uid, parsed.Uid);
        Assert.Equal(packet.Items, parsed.Items);
    }

    [Fact]
    public void AirPowerInfoMatchesLegacyRequiredProtobufShape()
    {
        var packet = new LegacyAirPowerUpPacket(
            LegacyAirPowerUpType.Info,
            UserId: 1_293_600,
            PowerType: 0,
            [
                new LegacyAirPowerUpPower(
                    Id: 0,
                    SenseLevel: 9,
                    SenseAipPower: 10_000,
                    ArmedLevel: 9,
                    ArmedAipPower: 10_000,
                    OverLoadLevel: 9,
                    OverLoadAipPower: 10_000,
                    Points: 92_305)
            ]);

        var bytes = packet.ToFrameBytes();

        Assert.Equal(
            "2300a009080010a0fa4e180022150800100918904e200928904e300938904e4091d105",
            Convert.ToHexString(bytes).ToLowerInvariant());
        Assert.True(LegacyAirPowerUpPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.UserId, parsed.UserId);
        Assert.Equal(packet.PowerType, parsed.PowerType);
        Assert.Equal(packet.Powers, parsed.Powers);
    }

    [Fact]
    public void StartupFiltersCombatGearToLegacyPirateItems()
    {
        var snapshot = CreateSnapshot(classCode: 7051);
        var combatGear = CreateCombatGear(
            [
                CreateItem(701, data1: 16_907, masteryScore: 928_000),
                CreateItem(401, data1: 1_111, masteryScore: 2_222),
                CreateItem(703, equip: 1, data1: 9_907, masteryScore: 344),
            ]);

        Assert.True(LegacyCombatGearPacket.TryCreateStartup(snapshot, combatGear, out var packet));

        Assert.Equal([701U, 703U], packet.Items.Select(item => item.Id));
        Assert.Equal(16_907U, packet.Items[0].Data1);
        Assert.Equal(344U, packet.Items[1].MasteryScore);
    }

    [Fact]
    public void StartupSkipsUnsupportedClass()
    {
        var snapshot = CreateSnapshot(classCode: 1005);
        var combatGear = CreateCombatGear([CreateItem(701)]);

        Assert.False(LegacyCombatGearPacket.TryCreateStartup(snapshot, combatGear, out _));
    }

    [Fact]
    public void PirateStartupSpellsUseCombatGearSkillsAndFixedLegacySpells()
    {
        var snapshot = CreateSnapshot(classCode: 7051);
        var combatGear = CreateCombatGear(
            [
                CreateItem(701),
                CreateItem(702),
            ]);
        var sourceGear = new Dictionary<(int GearTypeId, int Level), CombatGear>
        {
            [(701, 4)] = CreateSourceGear(701, 4, 22_050_003),
            [(702, 4)] = CreateSourceGear(702, 4, 22_060_003),
        };

        var spells = LegacyCombatGearPacket.CreatePirateStartupSpells(snapshot, combatGear, sourceGear);

        Assert.Equal(
            [22_050, 21_540, 21_920, 22_060, 21_540, 21_920],
            spells.Select(spell => spell.Id));
        Assert.Equal([3, 0, 0, 3, 0, 0], spells.Select(spell => spell.Level));
    }

    [Fact]
    public void PirateStartupSpellsAddBlackJadeSpellAttributesBeforeCombatGearSkills()
    {
        var snapshot = CreateSnapshot(classCode: 7051);
        var combatGear = CreateCombatGear([CreateItem(701, data1: 16_907)]);
        var sourceGear = new Dictionary<(int GearTypeId, int Level), CombatGear>
        {
            [(701, 4)] = CreateSourceGear(701, 4, 22_050_003),
        };
        var daoqi = new Dictionary<int, DaoqiDictType>
        {
            [16_907] = CreateDaoqiSpellEntry(16_907, 21_660_009, 21_700_009),
            [1_907] = CreateDaoqiSpellEntry(1_907, 21_510_009, 21_670_009),
            [19_907] = CreateDaoqiSpellEntry(19_907, 21_750_009, 0),
        };

        var spells = LegacyCombatGearPacket.CreatePirateStartupSpells(
            snapshot,
            combatGear,
            sourceGear,
            daoqi);

        Assert.Equal(
            [21_660, 21_700, 21_510, 21_670, 21_750, 22_050, 21_540, 21_920],
            spells.Select(spell => spell.Id));
        Assert.Equal([9, 9, 9, 9, 9, 3, 0, 0], spells.Select(spell => spell.Level));
    }

    [Fact]
    public void PirateStartupSpellsStillEmitFixedSpellsWithoutCombatGearCatalog()
    {
        var snapshot = CreateSnapshot(classCode: 7051);
        var combatGear = CreateCombatGear([CreateItem(701)]);

        var spells = LegacyCombatGearPacket.CreatePirateStartupSpells(snapshot, combatGear, null);

        Assert.Equal([21_540, 21_920], spells.Select(spell => spell.Id));
        Assert.All(spells, spell => Assert.Equal(0, spell.Level));
    }

    private static CharacterSnapshot CreateSnapshot(long classCode)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
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
            X: 430,
            Y: 378,
            PreviousMapId: 1002,
            PreviousMapX: 430,
            PreviousMapY: 378,
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
            SourceFile: "",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static CombatGearSnapshot CreateCombatGear(IReadOnlyList<CombatGearSnapshotItem> items)
    {
        return new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: items.Count,
            new CombatGearAirPowersSnapshot(
                SenseLevel: 9,
                ArmedLevel: 9,
                OverLoadLevel: 9,
                Points: 92_305,
                SenseAipPower: 10_000,
                ArmedAipPower: 10_000,
                OverLoadAipPower: 10_000),
            items,
            CombatGearBlackJadeBagSnapshot.Empty);
    }

    private static CombatGearSnapshotItem CreateItem(
        uint itemId,
        uint equip = 0,
        uint data1 = 0,
        uint masteryScore = 0)
    {
        return new CombatGearSnapshotItem(
            itemId,
            Level: 4,
            Exp: 0,
            Equip: equip,
            Transform: 0,
            MasteryScore: masteryScore,
            Animas: [[], []],
            BlackJades: [data1, 1_907, 19_907, 14_907, 3_907, 2_907]);
    }

    private static CombatGear CreateSourceGear(int gearTypeId, int level, int skill)
    {
        var fields = new[]
        {
            "1",
            gearTypeId.ToString(),
            level.ToString(),
            "0",
            "0",
            "0",
            "0",
            "0",
            "0",
            skill.ToString(),
            "0",
            "0",
            "0",
            "0",
            "0",
            "0",
            "0",
            "0",
            "0",
            "0",
            "0",
            "0",
            "",
        };

        return new CombatGear(
            Id: 1,
            gearTypeId,
            level,
            UpExperience: 0,
            Life: 0,
            Attack: 0,
            Defense: 0,
            UnknownStat0: 0,
            MagicDefense: 0,
            MagicTypeIds: [skill],
            Score: 0,
            Unknown0: 0,
            Unknown1: 0,
            Unknown2: 0,
            SourceFile: "combat_gear.txt",
            LineNumber: 1,
            RawLine: string.Join("@@", fields),
            Fields: fields);
    }

    private static DaoqiDictType CreateDaoqiSpellEntry(int id, int attrValue1, int attrValue2)
    {
        var fields = new[]
        {
            id.ToString(),
            "2",
            "24",
            attrValue1.ToString(),
            attrValue2 == 0 ? "0" : "24",
            attrValue2.ToString(),
            "600",
            "0",
            "0",
            "0",
            "10",
            "0",
            "0",
            "10",
            "701",
            "",
        };

        return new DaoqiDictType(
            id,
            Type: 2,
            Level: 24,
            Values: fields.Skip(3).Take(12).Select(int.Parse).ToArray(),
            SourceFile: "daoqi_dict_type.txt",
            LineNumber: 1,
            RawLine: string.Join("@@", fields),
            Fields: fields);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
