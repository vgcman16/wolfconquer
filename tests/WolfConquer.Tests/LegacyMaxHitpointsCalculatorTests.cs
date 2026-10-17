using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMaxHitpointsCalculatorTests
{
    [Theory]
    [InlineData(7005, 10, 20, 30, 40, 930)]
    [InlineData(1001, 10, 20, 30, 40, 976)]
    [InlineData(1005, 10, 20, 30, 40, 1069)]
    [InlineData(1058, 10, 20, 30, 40, 1069)]
    public void AttributeHitpointsMatchLegacyGameClientFormula(
        long classCode,
        long strength,
        long agility,
        long vitality,
        long spirit,
        uint expected)
    {
        var hitpoints = LegacyMaxHitpointsCalculator.CalculateAttributeHitpoints(
            classCode,
            strength,
            agility,
            vitality,
            spirit);

        Assert.Equal(expected, hitpoints);
    }

    [Fact]
    public void BeastHitpointsUseLegacyTotalPointsLevel()
    {
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["TailedBeastActivated"] = "True",
                ["TailedBeastTotalPoints"] = "350",
            });
        var beastAttributes = new Dictionary<int, BeastAttribute>
        {
            [1] = CreateBeastAttribute(level: 1, requiredPoints: 100, hitpoints: 10),
            [2] = CreateBeastAttribute(level: 2, requiredPoints: 200, hitpoints: 20),
            [3] = CreateBeastAttribute(level: 3, requiredPoints: 300, hitpoints: 30),
        };

        var hitpoints = LegacyMaxHitpointsCalculator.CalculateBeastHitpoints(snapshot, beastAttributes);

        Assert.Equal(30U, hitpoints);
    }

    [Fact]
    public void ChiHitpointsUseHpAddAttributesAndPerfectDragonBonus()
    {
        var state = new LegacyChiState(
            ChiPoints: 0,
            [
                LegacyChiPowerState.Parse(
                    LegacyChiPowerType.Dragon,
                    "1/0/8/2500/0/3/200/0/6/3500/0/2/200/0/"),
                LegacyChiPowerState.Parse(
                    LegacyChiPowerType.Phoenix,
                    "1/0/6/1234/0/0/0/0/0/0/0/0/0/0/0/"),
            ]);

        var hitpoints = LegacyMaxHitpointsCalculator.CalculateChiHitpoints(state);

        Assert.Equal(9_734U, hitpoints);
    }

    [Fact]
    public void SnapshotBreakdownIncludesChiHitpointsWhenChiStateIsPresent()
    {
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Dragon"] = "1/0/8/2500/0/3/200/0/6/3500/0/2/200/0/",
                ["Pheonix"] = "1/0/6/1234/0/0/0/0/0/0/0/0/0/0/0/",
            });

        var breakdown = LegacyMaxHitpointsCalculator.Calculate(
            snapshot,
            PlayerItemSnapshot.Empty,
            CombatGearSnapshot.Empty,
            catalog: null);

        Assert.Equal(9_734U, breakdown.ExternalHitpoints);
        Assert.Equal(9_734U, breakdown.Total);
        Assert.DoesNotContain("Chi.MaxLife", breakdown.MissingInputs);
        Assert.Contains("Chi.FateExperience", breakdown.MissingInputs);
        Assert.Contains("InnerPower.MaxLife", breakdown.MissingInputs);
    }

    [Fact]
    public void ProfessionHitpointsDecodeLegacyHpAddBenefitValues()
    {
        var benefits = new Dictionary<int, ProfessionLevelBenefit>
        {
            [7051] = new(
                Id: 561,
                ProfessionCode: 7051,
                Rank: 5,
                BonusValues: [62_600, 71_200, 0, 0, 2_600],
                SourceFile: "prof_lev_benefit.txt",
                LineNumber: 561,
                RawLine: "561@@7051@@5@@62600@@71200@@0@@0@@2600@@",
                Fields: ["561", "7051", "5", "62600", "71200", "0", "0", "2600", ""]),
        };

        var hitpoints = LegacyMaxHitpointsCalculator.CalculateProfessionHitpoints(7051, benefits);

        Assert.Equal(2_600U, hitpoints);
    }

    [Fact]
    public void EquipmentHitpointsUseItemTypeEnchantPurificationAndItemAddSources()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 1,
                    ItemId = 111003,
                    Durability = 1,
                    Position = 1,
                    Plus = 2,
                    Enchant = 7,
                    PurificationItemId = 800001,
                    AnimaItemId = 900001,
                },
                new PlayerItemSnapshotItem
                {
                    Uid = 2,
                    ItemId = 300000,
                    Durability = 1,
                    Position = 12,
                    Plus = 2,
                    Enchant = 99,
                },
                new PlayerItemSnapshotItem
                {
                    Uid = 3,
                    ItemId = 120001,
                    Durability = 0,
                    Position = 2,
                    Plus = 2,
                    Enchant = 50,
                },
            ],
            RuneItems: []);
        var itemsById = new Dictionary<int, IReadOnlyList<ItemType>>
        {
            [111003] = [CreateItemType(111003, itemHitpoints: 100)],
            [800001] = [CreateItemType(800001, itemHitpoints: 25)],
            [900001] = [CreateItemType(900001, itemHitpoints: 200)],
            [300000] = [CreateItemType(300000, itemHitpoints: 40)],
        };
        var itemAdd = new Dictionary<(int ItemTypeId, int Level), ItemAddBonus>
        {
            [(111000, 2)] = new(111000, 2, [50, 0, 0, 0, 0, 0, 0, 0], 1, "111000 2 50 0 0 0 0 0 0 0"),
            [(300000, 2)] = new(300000, 2, [5, 0, 0, 0, 0, 0, 0, 0], 2, "300000 2 5 0 0 0 0 0 0 0"),
        };

        var hitpoints = LegacyMaxHitpointsCalculator.CalculateEquipmentHitpoints(
            playerItems,
            itemsById,
            itemAdd);

        Assert.Equal(228U, hitpoints);
    }

    [Fact]
    public void EquipmentHitpointsSelectCurrentEquipmentPage()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 1,
                    ItemId = 111003,
                    Durability = 1,
                    Position = 1,
                },
                new PlayerItemSnapshotItem
                {
                    Uid = 2,
                    ItemId = 112003,
                    Durability = 1,
                    Position = 51,
                },
            ],
            RuneItems: []);
        var itemsById = new Dictionary<int, IReadOnlyList<ItemType>>
        {
            [111003] = [CreateItemType(111003, itemHitpoints: 100)],
            [112003] = [CreateItemType(112003, itemHitpoints: 900)],
        };

        var inactiveHitpoints = LegacyMaxHitpointsCalculator.CalculateEquipmentHitpoints(
            playerItems,
            itemsById,
            itemAddBonusByItemTypeIdAndLevel: null);
        var activeHitpoints = LegacyMaxHitpointsCalculator.CalculateEquipmentHitpoints(
            playerItems,
            itemsById,
            itemAddBonusByItemTypeIdAndLevel: null,
            alternateEquipmentActive: true);

        Assert.Equal(100U, inactiveHitpoints);
        Assert.Equal(900U, activeHitpoints);
    }

    [Fact]
    public void CircleOfLifeEnchantHitpointsUseLegacyRuneLevelScaleAndCurrentEquipmentPage()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = 111003,
                    Durability = 1,
                    Position = 1,
                    Enchant = 100,
                },
                new PlayerItemSnapshotItem
                {
                    ItemId = 112003,
                    Durability = 1,
                    Position = 51,
                    Enchant = 50,
                },
                new PlayerItemSnapshotItem
                {
                    ItemId = 300000,
                    Durability = 1,
                    Position = 12,
                    Enchant = 100,
                },
            ],
            RuneItems: []);

        var inactiveHitpoints = LegacyMaxHitpointsCalculator.CalculateCircleOfLifeEnchantHitpoints(
            playerItems,
            runeLevel: 8);
        var activeHitpoints = LegacyMaxHitpointsCalculator.CalculateCircleOfLifeEnchantHitpoints(
            playerItems,
            runeLevel: 8,
            alternateEquipmentActive: true);

        Assert.Equal(180U, inactiveHitpoints);
        Assert.Equal(90U, activeHitpoints);
    }

    [Fact]
    public void CircleOfLifeEnchantHitpointsCanDiscoverEquippedRuneByName()
    {
        var snapshot = CreateSnapshot(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = 111003,
                    Durability = 1,
                    Position = 1,
                    Enchant = 100,
                },
            ],
            RuneItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = 4036008,
                    Position = 101,
                },
            ]);
        var itemsById = new Dictionary<int, IReadOnlyList<ItemType>>
        {
            [4036008] = [CreateItemType(4036008, itemHitpoints: 0, name: "Circle of Life (+8)")],
        };

        var hitpoints = LegacyMaxHitpointsCalculator.CalculateCircleOfLifeEnchantHitpoints(
            snapshot,
            playerItems,
            itemsById);

        Assert.Equal(180U, hitpoints);
    }

    [Fact]
    public void SnapshotBreakdownIncludesCircleOfLifeEnchantBonusFromExplicitRuneLevel()
    {
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CircleOfLifeRuneLevel"] = "9",
            });
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = 111003,
                    Durability = 1,
                    Position = 1,
                    Enchant = 100,
                },
            ],
            RuneItems: []);

        var breakdown = LegacyMaxHitpointsCalculator.Calculate(
            snapshot,
            playerItems,
            CombatGearSnapshot.Empty,
            catalog: null);

        Assert.Equal(100U, breakdown.EquipmentHitpoints);
        Assert.Equal(200U, breakdown.ExternalHitpoints);
        Assert.Equal(300U, breakdown.Total);
    }

    [Fact]
    public void RuntimeMaxHitpointsKeepsComputedHpWhenStoredSnapshotMaxIsStale()
    {
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100",
            });
        var boostedSnapshot = snapshot with { Vitality = 30 };

        var hitpoints = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
            boostedSnapshot,
            currentHitpoints: 1,
            PlayerItemSnapshot.Empty,
            CombatGearSnapshot.Empty,
            catalog: null);

        Assert.Equal(720U, hitpoints);
    }

    [Fact]
    public void CombatGearStatusHitpointsUseSplashFlagMagic()
    {
        const int splashFlag = 0x196;
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(splashFlag),
        };
        var magicByTypeId = new Dictionary<int, IReadOnlyList<MagicType>>
        {
            [21800] = [MagicWithGDamage(21800, "Splash", gDamage: 444)],
        };

        var hitpoints = LegacyMaxHitpointsCalculator.CalculateCombatGearStatusHitpoints(
            attributes,
            magicByTypeId);

        Assert.Equal(444U, hitpoints);
    }

    [Fact]
    public void CombatGearHitpointsUseEquippedLifeFivePercentOtherLifeAndBlackJadeMaxHp()
    {
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 2,
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
                    BlackJades: [16907]),
                new CombatGearSnapshotItem(
                    ItemID: 702,
                    Level: 4,
                    Exp: 0,
                    Equip: 0,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
        var gearByTypeAndLevel = new Dictionary<(int GearTypeId, int Level), CombatGear>
        {
            [(701, 4)] = CreateCombatGear(701, life: 1_000),
            [(702, 4)] = CreateCombatGear(702, life: 500),
        };
        var blackJades = new Dictionary<int, DaoqiDictType>
        {
            [16907] = new(
                Id: 16907,
                Type: 0,
                Level: 0,
                Values: [],
                SourceFile: "cq_daoqi_dict_type.txt",
                LineNumber: 1,
                RawLine: "16907@@0@@1@@300@@2@@10@@",
                Fields: ["16907", "0", "1", "300", "2", "10", ""]),
        };

        var hitpoints = LegacyMaxHitpointsCalculator.CalculateCombatGearHitpoints(
            classCode: 7005,
            combatGear,
            gearByTypeAndLevel,
            blackJades);

        Assert.Equal(1_325U, hitpoints);
    }

    [Fact]
    public void CombatGearHitpointsAddPirateNutBookMaxHpFromBlackJadeBagScore()
    {
        var blackJadeIds = Enumerable.Range(13, 14)
            .Select(index => (uint)((index * 1000) + 907))
            .ToArray();
        var combatGear = new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    ItemID: 703,
                    Level: 4,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            new CombatGearBlackJadeBagSnapshot(
                Count: blackJadeIds.Length,
                BlackJades: blackJadeIds));
        var blackJades = blackJadeIds
            .Select((id, index) => CreateDaoqiDictType(
                (int)id,
                score: 600,
                typeRune: ((index + 13) * 1000) + 7,
                typeCombatGear: 0))
            .ToDictionary(blackJade => blackJade.Id);
        var collectionTiers = new Dictionary<int, IReadOnlyList<CollectionRewardTier>>
        {
            [700] =
            [
                CreateCollectionTier(
                    collectionId: 700,
                    minInclusive: 8_400,
                    maxInclusive: int.MaxValue,
                    rewardType: 1,
                    rewardValue: 1_500),
            ],
        };

        var hitpoints = LegacyMaxHitpointsCalculator.CalculateCombatGearHitpoints(
            classCode: 7051,
            combatGear,
            combatGearByGearTypeAndLevel: new Dictionary<(int GearTypeId, int Level), CombatGear>
            {
                [(703, 4)] = CreateCombatGear(703, life: 0, score: 800),
            },
            daoqiDictTypesById: blackJades,
            collectionRewardTiersByCollectionId: collectionTiers);

        Assert.Equal(1_500U, hitpoints);
    }

    [Fact]
    public void FullBreakdownDocumentsUnloadedLegacyStatusSources()
    {
        var breakdown = LegacyMaxHitpointsCalculator.Calculate(
            classCode: 7005,
            strength: 10,
            agility: 20,
            vitality: 30,
            spirit: 40,
            PlayerItemSnapshot.Empty,
            CombatGearSnapshot.Empty,
            itemsById: null,
            itemAddBonusByItemTypeIdAndLevel: null,
            combatGearByGearTypeAndLevel: null);

        Assert.Equal(930U, breakdown.Total);
        Assert.Contains("Chi.MaxLife", breakdown.MissingInputs);
        Assert.Contains("InnerPower.MaxLife", breakdown.MissingInputs);
    }

    private static ItemType CreateItemType(int id, uint itemHitpoints, string name = "TestItem")
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = id.ToString();
        fields[1] = name;
        fields[18] = itemHitpoints.ToString();
        return new ItemType(id, name, fields);
    }

    private static CombatGear CreateCombatGear(int gearTypeId, int life, int score = 0)
    {
        return new CombatGear(
            Id: gearTypeId,
            GearTypeId: gearTypeId,
            Level: 4,
            UpExperience: 0,
            Life: life,
            Attack: 0,
            Defense: 0,
            UnknownStat0: 0,
            MagicDefense: 0,
            MagicTypeIds: [],
            Score: score,
            Unknown0: 0,
            Unknown1: 0,
            Unknown2: 0,
            SourceFile: "combat_gear.txt",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }

    private static DaoqiDictType CreateDaoqiDictType(int id, int score, int typeRune, int typeCombatGear)
    {
        var fields = Enumerable.Repeat("0", 16).ToArray();
        fields[0] = id.ToString();
        fields[1] = "1";
        fields[2] = "24";
        fields[6] = score.ToString();
        fields[9] = typeRune.ToString();
        fields[14] = typeCombatGear.ToString();
        fields[15] = "";
        return new DaoqiDictType(
            id,
            Type: 1,
            Level: 24,
            Values: [],
            SourceFile: "daoqi_dict_type.txt",
            LineNumber: 1,
            RawLine: "",
            Fields: fields);
    }

    private static CollectionRewardTier CreateCollectionTier(
        int collectionId,
        int minInclusive,
        int maxInclusive,
        int rewardType,
        int rewardValue)
    {
        return new CollectionRewardTier(
            Id: collectionId,
            CollectionId: collectionId,
            MinInclusive: minInclusive,
            MaxInclusive: maxInclusive,
            RewardType: rewardType,
            RewardValue: rewardValue,
            RewardAmount: 0,
            SourceFile: "dict_collection.txt",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }

    private static BeastAttribute CreateBeastAttribute(int level, int requiredPoints, int hitpoints)
    {
        return new BeastAttribute(
            level,
            [requiredPoints, hitpoints, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            "beasts_attr.txt",
            level,
            "",
            []);
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

    private static CharacterSnapshot CreateSnapshot(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1,
            Name: "Tester",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 7005,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 1,
            Experience: 0,
            MapId: 1002,
            X: 0,
            Y: 0,
            PreviousMapId: 1002,
            PreviousMapX: 0,
            PreviousMapY: 0,
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
            SourceFile: "Tester.ini",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }
}
