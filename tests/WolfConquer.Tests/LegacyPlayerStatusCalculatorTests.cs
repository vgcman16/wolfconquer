using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPlayerStatusCalculatorTests
{
    [Fact]
    public void CalculatorReportsTortoiseGemPercentAsDamageStatusField()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(
                    itemId: 120269,
                    position: 3,
                    socketOne: 73,
                    socketTwo: 72),
            ],
            RuneItems: []);

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            playerItems,
            catalog: null,
            maxHitpoints: 100,
            prestigeLevel: 0);

        Assert.Equal(10U, fields[LegacyPlayerStatusPacket.DamageIndex]);
        Assert.Equal(110U, LegacyCombatDamageCalculator.CalculateAdjustedPhysicalDefense(
            physicalDefense: 100,
            tortoiseGemDamagePercent: fields[LegacyPlayerStatusPacket.DamageIndex]));
    }

    [Fact]
    public void CalculatorSelectsOnlyCurrentEquipmentPageForTortoiseDamageGems()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 120269, position: 3, socketOne: 73),
                Equipment(itemId: 120269, position: 53, socketOne: 71),
            ],
            RuneItems: []);

        var inactiveFields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            playerItems,
            catalog: null,
            maxHitpoints: 100,
            prestigeLevel: 0);
        var activeFields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            playerItems,
            catalog: null,
            maxHitpoints: 100,
            prestigeLevel: 0,
            alternateEquipmentActive: true);

        Assert.Equal(6U, inactiveFields[LegacyPlayerStatusPacket.DamageIndex]);
        Assert.Equal(2U, activeFields[LegacyPlayerStatusPacket.DamageIndex]);
    }

    [Fact]
    public void CalculatorCapsTortoiseDamageStatusAtFirstEightEquippedGems()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 120269, position: 1, socketOne: 73, socketTwo: 73),
                Equipment(itemId: 150269, position: 2, socketOne: 73, socketTwo: 73),
                Equipment(itemId: 160249, position: 3, socketOne: 73, socketTwo: 73),
                Equipment(itemId: 500329, position: 4, socketOne: 73, socketTwo: 73),
                Equipment(itemId: 421339, position: 5, socketOne: 73, socketTwo: 73),
            ],
            RuneItems: []);

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            playerItems,
            catalog: null,
            maxHitpoints: 100,
            prestigeLevel: 0);

        Assert.Equal(48U, fields[LegacyPlayerStatusPacket.DamageIndex]);
    }

    [Fact]
    public void CalculatorIgnoresTortoiseDamageGemsFromClearedOrRestrictedSocketPositions()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 120269, position: 3, socketOne: 71),
                Equipment(itemId: 421339, position: 10, socketOne: 73, socketTwo: 73),
                Equipment(itemId: 900109, position: 11, socketOne: 73, socketTwo: 73),
                Equipment(itemId: 203009, position: 13, socketOne: 73, socketTwo: 73),
            ],
            RuneItems: []);

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            playerItems,
            catalog: null,
            maxHitpoints: 100,
            prestigeLevel: 0);

        Assert.Equal(2U, fields[LegacyPlayerStatusPacket.DamageIndex]);
    }

    [Fact]
    public void CalculatorComposesCombatGearDefenseAndTortoiseDamageIndexForDamageInputs()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 120269, position: 3, socketOne: 73, socketTwo: 72),
            ],
            RuneItems: []);
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 4,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithCombatGear(
            CombatGearDefinition(gearTypeId: 701, level: 4, defense: 1_000));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            playerItems,
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);
        var adjustedDefense = LegacyCombatDamageCalculator.CalculateAdjustedPhysicalDefense(
            fields[LegacyPlayerStatusPacket.DefenceIndex],
            fields[LegacyPlayerStatusPacket.DamageIndex]);

        Assert.Equal(1_000U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(10U, fields[LegacyPlayerStatusPacket.DamageIndex]);
        Assert.Equal(1_100U, adjustedDefense);
    }

    [Fact]
    public void CalculatorAppliesTharnCutterElementalArrowSigilStatusBonuses()
    {
        const int prisonSigilFutilityCursedFlag = 0x11D | 0x20;
        const int whirlSigilAuraCursedFlag = 0x11E | 0x20;
        const int top8WeeklyHawksAmbitionFlag = 0x3F | 0x100;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    prisonSigilFutilityCursedFlag,
                    whirlSigilAuraCursedFlag,
                    top8WeeklyHawksAmbitionFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithMagic(ElementalArrowMagic());

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(21U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(20U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(1_000U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(200U, fields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
        Assert.Equal(5U, fields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        Assert.Equal(5U, fields[LegacyPlayerStatusPacket.CounteractionIndex]);
    }

    [Fact]
    public void CalculatorAppliesMudWallStatusDefenseBonusesFromLearnedSpell()
    {
        const int mudWallFlag = 0x112;

        var snapshot = CreateSnapshot() with
        {
            Class = 5051,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(mudWallFlag),
                ["MsgNinja"] = MsgNinjaWithEquippedItems(),
            },
        };
        var catalog = CreateCatalogWithMagic(
            MagicWithGDamage(16_440, "MudWall", gDamage: 123_456));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            playerSpells: PlayerSpells((16_440, 0)));

        Assert.Equal(12U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(456U, fields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CalculatorRequiresMudWallFlagAndLearnedSpellForMudWallStatusDefenseBonuses(
        bool hasMudWallFlag,
        bool hasLearnedMudWallSpell)
    {
        const int mudWallFlag = 0x112;

        var snapshot = CreateSnapshot() with
        {
            Class = 5051,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = hasMudWallFlag ? BuildStatusFlags(mudWallFlag) : BuildStatusFlags(),
                ["MsgNinja"] = MsgNinjaWithEquippedItems(),
            },
        };
        var catalog = CreateCatalogWithMagic(MagicWithGDamage(16_440, "MudWall", gDamage: 123_456));
        var playerSpells = hasLearnedMudWallSpell
            ? PlayerSpells((16_440, 0))
            : PlayerSpellSnapshot.Empty;

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            playerSpells: playerSpells);

        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
    }

    [Fact]
    public void CalculatorAppliesEquippedMudSigilSolidityStatusDefenseBonuses()
    {
        const int mudWallFlag = 0x112;
        const int mudSigilSolidityType = 302;
        const int mudSigilLevel = 4;

        var snapshot = CreateSnapshot() with
        {
            Class = 5051,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(mudWallFlag),
                ["MsgNinja"] = MsgNinjaWithEquippedItems(
                    (ItemType: (uint)mudSigilSolidityType, Level: (uint)mudSigilLevel, Position: 1U)),
            },
        };
        var catalog = CreateCatalog(
            items: [],
            combatGear: [],
            magic: [MagicWithGDamage(16_440, "MudWall", gDamage: 123_456)],
            gouyuTypes: [GouyuTypeWithPower(mudSigilSolidityType, mudSigilLevel, power: 78_901)]);

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            playerSpells: PlayerSpells((16_440, 0)));

        Assert.Equal(19U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(9_357U, fields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearStatusItemBlessBonusesWithLegacyCap()
    {
        const int holyProtectionFlag = 0x78 | 0x100;
        const int phoenixBlessingFlag = 0x3F | 0x40 | 0x100;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(holyProtectionFlag, phoenixBlessingFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithMagic(
            MagicWithDamageOnHuman(20350, "HolyProtection", damageOnHuman: 35),
            MagicWithDamageOnHuman(20300, "PhoenixBlessing", damageOnHuman: 40));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(60U, fields[LegacyPlayerStatusPacket.ItemBlessIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearNobleSpiritMagicDefenseAndPenetration()
    {
        const int nobleSpiritFlag = 0x134 | 0x40;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(nobleSpiritFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithMagic(MagicWithGDamage(20390, "NobleSpirit", gDamage: 12_345));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(123U, fields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
        Assert.Equal(4_500U, fields[LegacyPlayerStatusPacket.PenetrationIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearFinalDamageDecreaseStatusBonuses()
    {
        const int flameShieldFlag = 0x3C | 0x40 | 0x100;
        const int flowKnackFlag = 0x29 | 0x40 | 0x100;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(flameShieldFlag, flowKnackFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithMagic(
            MagicWithDamageOnHumanAndDamage2(20290, "FlameShield", damageOnHuman: 101, damage2: 202),
            MagicWithGDamageAndDamageOnMonster(20210, "FlowKnack", gDamage: 303, damageOnMonster: 404));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(404U, fields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
        Assert.Equal(606U, fields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearDodgeRateStatusBonuses()
    {
        const int divineEmptinessFlag = 0x5D | 0x120;
        const int wackeSpiritFlag = 0x76 | 0x100;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(divineEmptinessFlag, wackeSpiritFlag),
            },
        };
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 120269, position: 3),
            ],
            RuneItems: []);
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalog(
            [CreateItemType(120269, "DodgeArmor", dodge: 200)],
            combatGear: [],
            magic:
            [
                MagicWithDamageOnHuman(20260, "DivineEmptiness", damageOnHuman: 2_500),
                MagicWithGDamage(20310, "WackeSpirit", gDamage: 33),
            ]);

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            playerItems,
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(200U, fields[LegacyPlayerStatusPacket.DodgeIndex]);
        Assert.Equal(83U, fields[LegacyPlayerStatusPacket.DodgeRateIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearCrackMantraDefenseReductionStatusBonuses()
    {
        const int crackMantraPhysicalFlag = 0x17A;
        const int crackMantraMagicFlag = 0x17B;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(crackMantraPhysicalFlag, crackMantraMagicFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalog(
            items: [],
            combatGear: [CombatGearDefinition(701, 0, defense: 500, magicDefense: 50)],
            magic: [MagicWithDamageOnHumanAndDamage2(20380, "CrackMantra", damageOnHuman: 125, damage2: 80)]);

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(375U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearSenseAndDiabolizeDodgeRateStatusBonuses()
    {
        const int diabolizeFlag = 0x18A;
        const int senseFlag = 0x19C;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(diabolizeFlag, senseFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalog(
            items: [],
            combatGear: [],
            magic:
            [
                MagicWithGDamage(21630, "Diabolize", gDamage: 55),
                MagicWithDamageOnHumanAndDamage2(21720, "Sense", damageOnHuman: 77, damage2: 44),
            ]);

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(99U, fields[LegacyPlayerStatusPacket.DodgeRateIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearSailingBlockStatusBonus()
    {
        const int sailingFlag = 0x1A3;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(sailingFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithMagic(MagicWithGDamage(21810, "Sailing", gDamage: 7));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(700U, fields[LegacyPlayerStatusPacket.BlockIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearShellImmunityStatusBonus()
    {
        const int shellFlag = 0x197;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(shellFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithMagic(MagicWithGDamage(21740, "Shell", gDamage: 64));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(64U, fields[LegacyPlayerStatusPacket.ImmunityIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearDaoqiStyleStatusBonuses()
    {
        const int barrierFlag = 0x18D;
        const int stormFlag = 0x192;
        const int thrashFlag = 0x193;
        const int thunderFlag = 0x191;
        const int torrentFlag = 0x190;
        const int tideFlag = 0x18F;
        const int vastFlag = 0x18E;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    barrierFlag,
                    stormFlag,
                    thrashFlag,
                    thunderFlag,
                    torrentFlag,
                    tideFlag,
                    vastFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithMagic(
            MagicWithGDamage(21730, "Barrier", gDamage: 3),
            MagicWithGDamage(21750, "Storm", gDamage: 11),
            MagicWithGDamage(21760, "Thrash", gDamage: 13),
            MagicWithGDamage(21770, "Thunder", gDamage: 17),
            MagicWithGDamage(21780, "Torrent", gDamage: 19),
            MagicWithGDamage(21790, "Tide", gDamage: 23),
            MagicWithGDamage(21820, "Vast", gDamage: 29));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(20U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(19U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(23U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(30U, fields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
        Assert.Equal(11U, fields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
        Assert.Equal(29U, fields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        Assert.Equal(3U, fields[LegacyPlayerStatusPacket.CounteractionIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearColdBloodlineDefenseStatusBonus()
    {
        const int coldBloodlineFlag = 0x19A;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(coldBloodlineFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithMagic(MagicWithDamageOnHuman(21550, "ColdBloodline", damageOnHuman: 37));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(37U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
    }

    [Fact]
    public void CalculatorAppliesCombatGearSplashMaxHpStatusBonus()
    {
        const int splashFlag = 0x196;

        var snapshot = CreateSnapshot() with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(splashFlag),
            },
        };
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var catalog = CreateCatalogWithMagic(MagicWithGDamage(21800, "Splash", gDamage: 444));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            snapshot,
            EmptyPlayerItems(),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0,
            combatGear);

        Assert.Equal(544U, fields[LegacyPlayerStatusPacket.MaxHPIndex]);
    }

    [Fact]
    public void CalculatorAppliesTemperedGlaiveRuneToCurrentLeftWeaponBaseAttack()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 421339, position: 5),
            ],
            RuneItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = 4030008,
                    Position = 101,
                },
            ]);
        var catalog = CreateCatalogWithItems(
            CreateItemType(421339, "LeftWeapon", maxAttack: 900, minAttack: 450),
            CreateItemType(4030008, "Tempered Glaive"));

        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            playerItems,
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);

        Assert.Equal(1_051U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(675U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
    }

    [Fact]
    public void CalculatorProjectsLiveFinalPhysicalDamageRefineryIntoMonsterDamageAfterDefense2()
    {
        const uint refineryItemId = 724_111;

        var catalog = CreateCatalog(
            items: [CreateItemType(410339, "Blade", maxAttack: 1_001, minAttack: 1_000)],
            combatGear: [],
            refineryMaterials:
            [
                RefineryMaterial(
                    itemType: (int)refineryItemId,
                    primaryValue: 275,
                    effectType: 11),
            ]);
        var baseFields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            new PlayerItemSnapshot(
                InventoryItems: [],
                RelicFuseItems: [],
                MythsoulBagItems: [],
                EquipmentItems:
                [
                    Equipment(itemId: 410339, position: 4),
                ],
                RuneItems: []),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);
        var refinedFields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            new PlayerItemSnapshot(
                InventoryItems: [],
                RelicFuseItems: [],
                MythsoulBagItems: [],
                EquipmentItems:
                [
                    Equipment(itemId: 410339, position: 4, refineryEffectId: refineryItemId),
                ],
                RuneItems: []),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);

        var baseDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            baseFields[LegacyPlayerStatusPacket.MinAttackIndex],
            baseFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 0,
            monsterDefense2: 10_000,
            attackerLevel: 140,
            monsterLevel: 140,
            finalPhysicalDamageIncrease: baseFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            attackRoll: LegacyPlayerMonsterPhysicalAttackRoll.MinimumRoll);
        var refinedDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            refinedFields[LegacyPlayerStatusPacket.MinAttackIndex],
            refinedFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 0,
            monsterDefense2: 10_000,
            attackerLevel: 140,
            monsterLevel: 140,
            finalPhysicalDamageIncrease: refinedFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            attackRoll: LegacyPlayerMonsterPhysicalAttackRoll.MinimumRoll);

        Assert.Equal(0U, baseFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(275U, refinedFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(1_200U, baseDamage);
        // Source: Physical.OnMonster applies Base.CalculateExtraAttack after Defense2.
        Assert.Equal(1_475U, refinedDamage);
    }

    [Fact]
    public void CalculatorProjectsLiveFinalMagicAttackRefineryIntoMagicDamageIncrease()
    {
        const uint refineryItemId = 724_112;

        var catalog = CreateCatalog(
            items: [CreateItemType(410339, "Blade")],
            combatGear: [],
            refineryMaterials:
            [
                RefineryMaterial(
                    itemType: (int)refineryItemId,
                    primaryValue: 425,
                    effectType: 12),
            ]);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            new PlayerItemSnapshot(
                InventoryItems: [],
                RelicFuseItems: [],
                MythsoulBagItems: [],
                EquipmentItems:
                [
                    Equipment(itemId: 410339, position: 4, refineryEffectId: refineryItemId),
                ],
                RuneItems: []),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);

        // Source: Equip.IncreaseRifainaryStatus maps Rifinery FinalMAttack to Status.MagicDamageIncrease.
        Assert.Equal(425U, fields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex]);
    }

    private static PlayerItemSnapshotItem Equipment(
        uint itemId,
        ushort position,
        byte socketOne = 0,
        byte socketTwo = 0,
        uint refineryEffectId = 0)
    {
        return new PlayerItemSnapshotItem
        {
            ItemId = itemId,
            Durability = 100,
            Position = position,
            SocketOne = socketOne,
            SocketTwo = socketTwo,
            RefineryEffectId = refineryEffectId,
        };
    }

    private static CharacterSnapshot CreateSnapshot()
    {
        return new CharacterSnapshot(
            Uid: 1,
            Name: "TortoiseProof",
            Body: 1008,
            Face: 154,
            Hair: 27,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1015,
            X: 830,
            Y: 584,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 100,
            MinMana: 5_000,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 7,
            SourceFile: "tortoise-proof.ini",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static ItemType CreateItemType(
        int id,
        string name,
        uint maxAttack = 0,
        uint minAttack = 0,
        uint dodge = 0)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = id.ToString();
        fields[1] = name;
        fields[13] = maxAttack.ToString();
        fields[14] = minAttack.ToString();
        fields[17] = dodge.ToString();
            return new ItemType(id, name, fields);
    }

    private static PlayerItemSnapshot EmptyPlayerItems()
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems: []);
    }

    private static PlayerSpellSnapshot PlayerSpells(params (ushort Id, ushort Level)[] spells)
    {
        return new PlayerSpellSnapshot(
            spells.Select(spell => new PlayerSpellSnapshotEntry(
                spell.Id,
                spell.Level,
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0)).ToArray());
    }

    private static MagicType ElementalArrowMagic()
    {
        var fields = Enumerable.Repeat("0", 49).ToArray();
        fields[0] = "18160";
        fields[1] = "18160";
        fields[3] = "ElementalArrow";
        fields[8] = "0";
        fields[10] = "20";
        fields[13] = "10";
        fields[35] = "1000";
        fields[36] = "200";
        fields[37] = "5";
        fields[43] = "2";
        fields[44] = "20";
        fields[47] = "0";
        return new MagicType(18160, 18160, "ElementalArrow", fields);
    }

    private static MagicType MagicWithDamageOnHuman(int id, string name, int damageOnHuman)
    {
        var fields = Enumerable.Repeat("0", 49).ToArray();
        fields[0] = id.ToString();
        fields[1] = id.ToString();
        fields[3] = name;
        fields[8] = "0";
        fields[35] = damageOnHuman.ToString();
        return new MagicType(id, id, name, fields);
    }

    private static MagicType MagicWithDamageOnHumanAndDamage2(
        int id,
        string name,
        int damageOnHuman,
        int damage2)
    {
        var fields = Enumerable.Repeat("0", 49).ToArray();
        fields[0] = id.ToString();
        fields[1] = id.ToString();
        fields[3] = name;
        fields[8] = "0";
        fields[35] = damageOnHuman.ToString();
        fields[36] = damage2.ToString();
        return new MagicType(id, id, name, fields);
    }

    private static MagicType MagicWithGDamageAndDamageOnMonster(
        int id,
        string name,
        int gDamage,
        int damageOnMonster)
    {
        var fields = Enumerable.Repeat("0", 49).ToArray();
        fields[0] = id.ToString();
        fields[1] = id.ToString();
        fields[3] = name;
        fields[8] = "0";
        fields[10] = gDamage.ToString();
        fields[44] = damageOnMonster.ToString();
        return new MagicType(id, id, name, fields);
    }

    private static MagicType MagicWithGDamage(int id, string name, int gDamage)
    {
        var fields = Enumerable.Repeat("0", 49).ToArray();
        fields[0] = id.ToString();
        fields[1] = id.ToString();
        fields[3] = name;
        fields[8] = "0";
        fields[10] = gDamage.ToString();
        return new MagicType(id, id, name, fields);
    }

    private static GouyuType GouyuTypeWithPower(int type, int level, int power)
    {
        var values = Enumerable.Repeat(0, 10).ToArray();
        values[5] = power;
        return new GouyuType(
            Id: checked((type * 100) + level),
            Type: type,
            Level: level,
            Values: values,
            SourceFile: "fixture://gouyu_type.txt",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }



    private static WorldDataCatalog CreateCatalogWithItems(params ItemType[] items)
    {
        return CreateCatalog(items, combatGear: []);
    }

    private static WorldDataCatalog CreateCatalogWithCombatGear(params CombatGear[] combatGear)
    {
        return CreateCatalog(items: [], combatGear: combatGear);
    }

    private static WorldDataCatalog CreateCatalogWithMagic(params MagicType[] magic)
    {
        return CreateCatalog(items: [], combatGear: [], magic: magic);
    }

    private static WorldDataCatalog CreateCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<CombatGear> combatGear,
        IReadOnlyList<MagicType>? magic = null,
        IReadOnlyList<GouyuType>? gouyuTypes = null,
        IReadOnlyList<RefineryMaterial>? refineryMaterials = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-10T00:00:00Z"),
                "fixture://legacy-player-status",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: gouyuTypes ?? [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: refineryMaterials ?? [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: magic ?? [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: combatGear,
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static RefineryMaterial RefineryMaterial(int itemType, int primaryValue, int effectType)
    {
        return new RefineryMaterial(
            itemType,
            "FinalMDamage",
            primaryValue,
            effectType,
            [primaryValue, effectType, 0, 0],
            "1-Handed",
            "Source FinalMDamage refinery type that increases physical damage.",
            [],
            "Rifinery.txt",
            1,
            "",
            []);
    }

    private static CombatGear CombatGearDefinition(int gearTypeId, int level, int defense, int magicDefense = 0)
    {
        return new CombatGear(
            Id: gearTypeId,
            GearTypeId: gearTypeId,
            Level: level,
            UpExperience: 0,
            Life: 0,
            Attack: 0,
            Defense: defense,
            UnknownStat0: 0,
            MagicDefense: magicDefense,
            MagicTypeIds: [],
            Score: 0,
            Unknown0: 0,
            Unknown1: 0,
            Unknown2: 0,
            SourceFile: "combat_gear.txt",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var wordCount = Math.Max(
            10,
            flags
                .Where(flag => flag >= 0)
                .Select(flag => flag / 32 + 1)
                .DefaultIfEmpty(10)
                .Max());
        var words = new ulong[wordCount];
        foreach (var flag in flags)
        {
            if (flag < 0)
            {
                continue;
            }

            var wordIndex = flag / 32;
            var bitIndex = flag % 32;
            words[wordIndex] |= 1UL << bitIndex;
        }

        return string.Join('/', words) + "/";
    }

    private static string MsgNinjaWithEquippedItems(params (uint ItemType, uint Level, uint Position)[] items)
    {
        var fields = Enumerable.Repeat("0", 20).ToList();
        foreach (var item in items)
        {
            fields.Add(item.ItemType.ToString());
            fields.Add(item.Level.ToString());
            fields.Add(item.Position.ToString());
            fields.Add("0");
            fields.Add("0");
        }

        return string.Join('/', fields) + "/";
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
