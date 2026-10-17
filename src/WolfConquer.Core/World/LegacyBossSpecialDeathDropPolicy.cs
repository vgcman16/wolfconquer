using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public readonly record struct LegacyBossSpecialDeathDropItem(
    uint ItemId,
    int Count,
    byte ScatterRange,
    string Source);

public readonly record struct LegacyBossSpecialGeneratedDropItem(
    uint ItemId,
    ushort Durability,
    ushort MaximumDurability,
    byte ScatterRange,
    uint Attribute1,
    uint Attribute2,
    uint Attribute3,
    uint Attribute4,
    uint Attribute5,
    string Source);

public sealed record LegacyBossSpecialDeathDropPlan(
    uint FamilyId,
    uint MapId,
    bool Boss,
    string SourceBranch,
    IReadOnlyList<LegacyBossSpecialDeathDropItem> FixedItems,
    IReadOnlyList<LegacyBossSpecialGeneratedDropItem> GeneratedItems,
    IReadOnlyList<string> PendingSourceBehaviors)
{
    public int TotalFixedItemCount => FixedItems.Sum(static item => item.Count);

    public int TotalGeneratedItemCount => GeneratedItems.Count;

    public int TotalItemCount => TotalFixedItemCount + TotalGeneratedItemCount;
}

public readonly record struct LegacyBossSpecialDeathDropRolls(
    bool Family4212RareDrop = false,
    bool Family4171RareDrop = false,
    bool DynamicInstanceReward = false,
    bool DragonIslandFamily4220RareDrop = false,
    bool Family20055Add711679 = false,
    bool Family20055Add711188 = false,
    int? DeityLandPrimaryDropIndex = null,
    int? DeityLandSecondaryDropIndex = null,
    int? DeityLandRelicItemIndex = null,
    int? DeityLandRelicDurabilityRoll = null,
    int? DeityLandRelicAttributeCount = null,
    IReadOnlyList<int>? DeityLandRelicAttributeIndexes = null,
    IReadOnlyList<int>? DeityLandRelicAttributeValues = null,
    int? QueenOfEvilDragonBallCount = null,
    IReadOnlyList<int>? QueenOfEvilTalismanIndexes = null,
    int? SoulsPurificationRollModulo = null,
    IReadOnlyList<int>? SoulsPurificationIndexes = null,
    int? SoulsAccessoryRollModulo = null,
    IReadOnlyList<int>? SoulsAccessoryIndexes = null);

public sealed record LegacyBossSpecialDeathDropCatalog(
    IReadOnlyDictionary<ushort, IReadOnlyList<uint>> PurificationItemsByLevel,
    IReadOnlyList<uint> AccessoryItems,
    IReadOnlyList<uint> QueenOfEvilTalismanItems,
    IReadOnlyDictionary<int, IReadOnlyList<XuanbaoRandomAttribute>> RelicRandomAttributesByItemTypeId)
{
    public static LegacyBossSpecialDeathDropCatalog Empty { get; } = new(
        new Dictionary<ushort, IReadOnlyList<uint>>(),
        [],
        [],
        new Dictionary<int, IReadOnlyList<XuanbaoRandomAttribute>>());

    public static LegacyBossSpecialDeathDropCatalog FromWorldData(WorldDataCatalog worldData)
    {
        ArgumentNullException.ThrowIfNull(worldData);
        return FromItemsById(
            worldData.ItemsById,
            worldData.XuanbaoRandomAttributesByItemTypeId);
    }

    public static LegacyBossSpecialDeathDropCatalog FromItemsById(
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>> itemsById)
    {
        return FromItemsById(
            itemsById,
            new Dictionary<int, IReadOnlyList<XuanbaoRandomAttribute>>());
    }

    private static LegacyBossSpecialDeathDropCatalog FromItemsById(
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>> itemsById,
        IReadOnlyDictionary<int, IReadOnlyList<XuanbaoRandomAttribute>> relicRandomAttributesByItemTypeId)
    {
        var purificationItems = new Dictionary<ushort, List<uint>>();
        var seenPurificationItems = new HashSet<uint>();
        var accessoryItems = new List<uint>();
        var seenAccessoryItems = new HashSet<uint>();
        var queenOfEvilTalismanItems = new List<uint>();
        var seenQueenOfEvilTalismanItems = new HashSet<uint>();

        foreach (var item in itemsById
            .OrderBy(static pair => pair.Key)
            .SelectMany(static pair => pair.Value))
        {
            if (item.Id < 0)
            {
                continue;
            }

            var itemId = checked((uint)item.Id);
            var purificationLevel = item.PurificationLevel;
            if (purificationLevel is > 0 and <= ushort.MaxValue &&
                itemId is not 729_305U and not 727_465U &&
                seenPurificationItems.Add(itemId))
            {
                var level = checked((ushort)purificationLevel);
                if (!purificationItems.TryGetValue(level, out var items))
                {
                    items = [];
                    purificationItems.Add(level, items);
                }

                items.Add(itemId);
            }

            if (item.IsLegacyAccessory && seenAccessoryItems.Add(itemId))
            {
                accessoryItems.Add(itemId);
            }

            if (ItemType.GetLegacyEquipmentPosition(itemId) == 103 &&
                itemId % 100 == 1 &&
                seenQueenOfEvilTalismanItems.Add(itemId))
            {
                queenOfEvilTalismanItems.Add(itemId);
            }
        }

        return new LegacyBossSpecialDeathDropCatalog(
            purificationItems.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<uint>)pair.Value.ToArray()),
            accessoryItems,
            queenOfEvilTalismanItems,
            relicRandomAttributesByItemTypeId);
    }
}

public static partial class LegacyBossSpecialDeathDropPolicy
{
    public const uint ThrillingSpookFamilyId = 4_212;
    public const uint StudyPointBossFamilyId = 4_171;
    public const uint StudyPointSpecialFamilyId = 20_060;
    public const uint DeityLandSpecialFamilyId = 8_311;
    public const uint DragonIslandMapId = 10_137;
    public const uint DeityLandBossDropMapId = 10_250;
    public const uint DragonIslandFamilyId = 4_220;
    public const uint QueenOfEvilFamilyId = 3_970;
    public const uint StudyPointTeamRewardFamilyId = 20_055;
    public const uint SoulBoxFamilyId = 6_643;

    private static readonly (uint ItemId, ushort Count)[] DeityLandBossPrimaryDropPool =
    [
        (3_003_124, 2),
        (1_088_000, 4),
        (1_088_001, 10),
        (3_303_484, 1),
        (3_003_519, 5),
        (3_008_186, 10),
        (3_008_187, 6),
        (3_008_188, 2),
        (3_008_189, 4),
        (3_008_190, 1),
        (3_008_193, 1),
        (3_008_198, 1),
        (3_008_197, 1),
        (3_008_196, 1),
        (3_008_195, 1),
        (3_008_194, 1),
        (3_335_127, 1),
        (3_330_770, 1),
        (3_316_793, 1),
    ];

    private static readonly (uint ItemId, ushort Count)[] DeityLandBossSecondaryDropPool =
    [
        (3_002_030, 7),
        (3_002_926, 3),
        (3_303_483, 30),
        (723_727, 5),
        (725_067, 5),
        (725_068, 5),
        (3_002_029, 5),
        (3_008_956, 4),
        (3_008_957, 4),
        (3_008_958, 3),
        (3_008_959, 2),
        (3_008_960, 2),
        (3_335_125, 10),
        (3_335_126, 10),
    ];

    private static readonly uint[] DeityLandRelicItemPool =
    [
        4_100_001,
        4_100_002,
        4_100_003,
        4_100_004,
        4_100_005,
    ];

    public static bool TryCreateFixedDropPlan(
        uint familyId,
        uint mapId,
        bool boss,
        LegacyBossSpecialDeathDropRolls rolls,
        out LegacyBossSpecialDeathDropPlan plan)
    {
        return TryCreateFixedDropPlan(
            familyId,
            mapId,
            boss,
            rolls,
            catalog: null,
            out plan);
    }

    public static bool TryCreateFixedDropPlan(
        uint familyId,
        uint mapId,
        bool boss,
        LegacyBossSpecialDeathDropRolls rolls,
        LegacyBossSpecialDeathDropCatalog? catalog,
        out LegacyBossSpecialDeathDropPlan plan)
    {
        return TryCreateFixedDropPlan(
            familyId,
            mapId,
            boss,
            familyName: null,
            rolls,
            catalog,
            out plan);
    }

    public static bool TryCreateFixedDropPlan(
        uint familyId,
        uint mapId,
        bool boss,
        string? familyName,
        LegacyBossSpecialDeathDropRolls rolls,
        LegacyBossSpecialDeathDropCatalog? catalog,
        out LegacyBossSpecialDeathDropPlan plan)
    {
        var catalogValue = catalog ?? LegacyBossSpecialDeathDropCatalog.Empty;
        var plans = new List<LegacyBossSpecialDeathDropPlan>();

        if (IsDeityLandBossDropBranch(mapId, boss))
        {
            plans.Add(CreateDeityLandBossDropPlan(familyId, mapId, boss, rolls, catalogValue));
        }

        if (IsDragonIslandSpecialBranch(familyId, mapId))
        {
            plans.Add(CreateDragonIslandFamily4220Plan(familyId, mapId, boss, rolls));
        }

        if (IsQueenOfEvilSpecialBranch(familyId, familyName))
        {
            plans.Add(CreateQueenOfEvilPlan(familyId, mapId, boss, rolls, catalogValue));
        }

        if (IsStudyPointSpecialBranch(familyId, boss))
        {
            plans.Add(CreateStudyPointSpecialPlan(familyId, mapId, boss, rolls, catalogValue));
        }
        else if (boss && familyId == ThrillingSpookFamilyId)
        {
            plans.Add(CreateThrillingSpookPlan(mapId, boss, rolls, catalogValue));
        }
        else if (boss && familyId == StudyPointTeamRewardFamilyId)
        {
            plans.Add(CreateStudyPointTeamRewardPlan(familyId, mapId, boss, rolls, catalogValue));
        }
        else if (boss && familyId == SoulBoxFamilyId)
        {
            plans.Add(CreateSoulBoxFamily6643Plan(familyId, mapId, boss, rolls, catalogValue));
        }
        else if (boss && plans.Count == 0)
        {
            plans.Add(CreateGenerateBossFamilySourceSilentPlan(familyId, mapId, boss));
        }

        if (plans.Count > 0)
        {
            plan = plans.Count == 1
                ? plans[0]
                : CombineFixedDropPlans(familyId, mapId, boss, plans);
            return true;
        }

        plan = default!;
        return false;
    }

    public static bool IsStudyPointSpecialBranch(uint familyId, bool boss)
    {
        return boss && familyId == StudyPointBossFamilyId ||
            familyId is StudyPointSpecialFamilyId or DeityLandSpecialFamilyId;
    }

    public static bool IsDragonIslandSpecialBranch(uint familyId, uint mapId)
    {
        return mapId == DragonIslandMapId && familyId == DragonIslandFamilyId;
    }

    public static bool IsDeityLandBossDropBranch(uint mapId, bool boss)
    {
        return boss && mapId == DeityLandBossDropMapId;
    }

    public static bool IsQueenOfEvilSpecialBranch(uint familyId, string? familyName = null)
    {
        return familyId == QueenOfEvilFamilyId ||
            string.Equals(familyName, "QueenofEvil", StringComparison.Ordinal);
    }

    public static bool IsSourceSpecialDropBranch(uint familyId, uint mapId, bool boss)
    {
        return IsSourceSpecialDropBranch(familyId, mapId, boss, familyName: null);
    }

    public static bool IsSourceSpecialDropBranch(uint familyId, uint mapId, bool boss, string? familyName)
    {
        return IsDragonIslandSpecialBranch(familyId, mapId) ||
            IsDeityLandBossDropBranch(mapId, boss) ||
            IsQueenOfEvilSpecialBranch(familyId, familyName) ||
            IsStudyPointSpecialBranch(familyId, boss) ||
            boss && familyId == ThrillingSpookFamilyId ||
            boss && familyId is StudyPointTeamRewardFamilyId or SoulBoxFamilyId ||
            boss;
    }

    private static LegacyBossSpecialDeathDropPlan CombineFixedDropPlans(
        uint familyId,
        uint mapId,
        bool boss,
        IReadOnlyList<LegacyBossSpecialDeathDropPlan> plans)
    {
        return new LegacyBossSpecialDeathDropPlan(
            familyId,
            mapId,
            boss,
            string.Join(" + ", plans.Select(static plan => plan.SourceBranch)),
            plans.SelectMany(static plan => plan.FixedItems).ToArray(),
            plans.SelectMany(static plan => plan.GeneratedItems).ToArray(),
            plans.SelectMany(static plan => plan.PendingSourceBehaviors).ToArray());
    }

    private static LegacyBossSpecialDeathDropPlan CreateGenerateBossFamilySourceSilentPlan(
        uint familyId,
        uint mapId,
        bool boss)
    {
        return new LegacyBossSpecialDeathDropPlan(
            familyId,
            mapId,
            boss,
            "MonsterRole.cs GenerateBossFamily()",
            [],
            [],
            []);
    }

    private static LegacyBossSpecialDeathDropPlan CreateDeityLandBossDropPlan(
        uint familyId,
        uint mapId,
        bool boss,
        LegacyBossSpecialDeathDropRolls rolls,
        LegacyBossSpecialDeathDropCatalog catalog)
    {
        var items = new List<LegacyBossSpecialDeathDropItem>();
        AppendDeityLandBossPoolDrop(
            items,
            DeityLandBossPrimaryDropPool,
            rolls.DeityLandPrimaryDropIndex,
            "dictionary2",
            "Pool.GetRandom.Next(0, dictionary2.Count)");
        AppendDeityLandBossPoolDrop(
            items,
            DeityLandBossSecondaryDropPool,
            rolls.DeityLandSecondaryDropIndex,
            "dictionary3",
            "Pool.GetRandom.Next(0, dictionary3.Count)");

        items.AddRange(
        [
            DeityLandBossItem(3_005_127, 5, "for (num5 < 5) DropItemID(3005127, range 10).", scatterRange: 10),
            DeityLandBossItem(3_004_181, 1, "DropItemID(3004181, range 5).", scatterRange: 5),
            DeityLandBossItem(4_200_001, 1, "DropItemID(4200001, range 1).", scatterRange: 1),
            DeityLandBossItem(4_200_002, 1, "DropItemID(4200002, range 1).", scatterRange: 1),
            DeityLandBossItem(4_200_003, 1, "DropItemID(4200003, range 1).", scatterRange: 1),
            DeityLandBossItem(4_200_002, 1, "DropItemID(4200002, range 10).", scatterRange: 10),
            DeityLandBossItem(3_316_793, 1, "DropItemID(3316793, range 1).", scatterRange: 1),
        ]);

        var generatedItems = new List<LegacyBossSpecialGeneratedDropItem>();
        var pending = new List<string>();
        if (TryCreateDeityLandRandomRelicDrop(catalog, rolls, out var relicDrop, out var missingReason))
        {
            generatedItems.Add(relicDrop);
        }
        else
        {
            pending.Add(missingReason);
        }

        return new LegacyBossSpecialDeathDropPlan(
            familyId,
            mapId,
            boss,
            "MonsterRole.cs killer.Player.Map == 10250 && Boss > 0",
            items,
            generatedItems,
            pending);
    }

    private static void AppendDeityLandBossPoolDrop(
        List<LegacyBossSpecialDeathDropItem> items,
        IReadOnlyList<(uint ItemId, ushort Count)> pool,
        int? forcedIndex,
        string poolName,
        string source)
    {
        var poolIndex = SelectPoolIndex(pool.Count, forcedIndex);
        var (itemId, count) = pool[poolIndex];
        items.Add(DeityLandBossItem(
            itemId,
            count,
            $"{source}; {poolName}[{poolIndex}] DropItemID({itemId}, range 3) repeated {count} times.",
            scatterRange: 3));
    }

    private static LegacyBossSpecialDeathDropPlan CreateQueenOfEvilPlan(
        uint familyId,
        uint mapId,
        bool boss,
        LegacyBossSpecialDeathDropRolls rolls,
        LegacyBossSpecialDeathDropCatalog catalog)
    {
        var pending = new List<string>
        {
            "QueenofEvil source branch falls through to later MonsterRole.cs boss/generic drop logic; GenerateBossFamily() remains a separate pending parity seam.",
        };

        var items = new List<LegacyBossSpecialDeathDropItem>
        {
            QueenOfEvilItem(3_328_253, 3, "for (b6 < 3) DropItemID(3328253, range 10)."),
            QueenOfEvilItem(3_328_254, 3, "for (b6 < 3) DropItemID(3328254, range 10)."),
            QueenOfEvilItem(3_330_770, 20, "for (b7 < 20) DropItemID(3330770, range 10)."),
            QueenOfEvilItem(3_316_793, 20, "for (b7 < 20) DropItemID(3316793, range 10)."),
            QueenOfEvilItem(1_088_000, GetLegacyQueenOfEvilDragonBallCount(rolls), "Pool.GetRandom.Next(1, 8) red DragonBall ground items.", scatterRange: 3),
            QueenOfEvilItem(3_335_128, 5, "for (num18 < 5) DropItemID(3335128, range 10)."),
        };

        if (catalog.QueenOfEvilTalismanItems.Count > 0)
        {
            for (var selectionIndex = 0; selectionIndex < 5; selectionIndex++)
            {
                var poolIndex = SelectPoolIndex(
                    catalog.QueenOfEvilTalismanItems.Count,
                    selectionIndex,
                    rolls.QueenOfEvilTalismanIndexes);
                items.Add(QueenOfEvilItem(
                    catalog.QueenOfEvilTalismanItems[poolIndex],
                    1,
                    $"for (num18 < 5) random ItemType.ItemPosition == 103 && ID % 100 == 1 talisman poolIndex={poolIndex}."));
            }
        }
        else
        {
            pending.Add("QueenofEvil random talisman drops need ItemType.ItemPosition == 103 && ID % 100 == 1 pool, but no matching itemtype rows were loaded.");
        }

        return new LegacyBossSpecialDeathDropPlan(
            familyId,
            mapId,
            boss,
            "MonsterRole.cs Name == \"QueenofEvil\" || Family.ID == 3970",
            items,
            [],
            pending);
    }

    private static int GetLegacyQueenOfEvilDragonBallCount(LegacyBossSpecialDeathDropRolls rolls)
    {
        if (rolls.QueenOfEvilDragonBallCount is { } forcedCount)
        {
            return Math.Clamp(forcedCount, 1, 7);
        }

        return Random.Shared.Next(1, 8);
    }

    private static LegacyBossSpecialDeathDropPlan CreateDragonIslandFamily4220Plan(
        uint familyId,
        uint mapId,
        bool boss,
        LegacyBossSpecialDeathDropRolls rolls)
    {
        var items = new List<LegacyBossSpecialDeathDropItem>();
        if (rolls.DragonIslandFamily4220RareDrop)
        {
            items.Add(DragonIslandItem(420_009, 1, "Core.Rate(20) DropItemID(420009, range 2).", scatterRange: 2));
        }
        else
        {
            items.Add(DragonIslandItem(3_330_609, 1, "else DropItemID(3330609, range 2).", scatterRange: 2));
            items.Add(DragonIslandItem(3_333_402, 1, "else DropItemID(3333402, range 2).", scatterRange: 2));
        }

        items.AddRange(
        [
            DragonIslandItem(3_330_770, 10, "for (b3 < 10) DropItemID(3330770, range 10)."),
            DragonIslandItem(3_316_793, 10, "for (b3 < 10) DropItemID(3316793, range 10)."),
            DragonIslandItem(3_328_253, 3, "for (b4 < 3) DropItemID(3328253, range 10)."),
            DragonIslandItem(3_328_254, 3, "for (b4 < 3) DropItemID(3328254, range 10)."),
            DragonIslandItem(3_306_368, 8, "for (b5 < 2) four DropItemID(3306368, range 10)."),
            DragonIslandItem(3_009_104, 2, "for (b5 < 2) DropItemID(3009104, range 2).", scatterRange: 2),
            DragonIslandItem(3_009_002, 2, "for (b5 < 2) DropItemID(3009002, range 10)."),
            DragonIslandItem(3_005_360, 2, "for (b5 < 2) DropItemID(3005360, range 10)."),
            DragonIslandItem(3_004_181, 2, "for (b5 < 2) DropItemID(3004181, range 3).", scatterRange: 3),
            DragonIslandItem(3_335_128, 2, "for (b5 < 2) DropItemID(3335128, range 10)."),
            DragonIslandItem(3_310_995, 2, "for (b5 < 2) DropItemID(3310995, range 10)."),
        ]);

        return new LegacyBossSpecialDeathDropPlan(
            familyId,
            mapId,
            boss,
            "MonsterRole.cs Map == 10137 && Family.ID == 4220",
            items,
            [],
            []);
    }

    private static LegacyBossSpecialDeathDropPlan CreateStudyPointSpecialPlan(
        uint familyId,
        uint mapId,
        bool boss,
        LegacyBossSpecialDeathDropRolls rolls,
        LegacyBossSpecialDeathDropCatalog catalog)
    {
        var items = new List<LegacyBossSpecialDeathDropItem>();
        if (rolls.Family4171RareDrop)
        {
            items.Add(new LegacyBossSpecialDeathDropItem(
                4_200_010,
                Count: 1,
                ScatterRange: 10,
                "MonsterRole.cs Family.ID 4171/20060/8311 Core.Rate(10) DropItemID(4200010, range 10)."));
        }
        else
        {
            items.Add(new LegacyBossSpecialDeathDropItem(
                4_200_007,
                Count: 1,
                ScatterRange: 10,
                "MonsterRole.cs Family.ID 4171/20060/8311 else DropItemID(4200007, range 10)."));
            items.Add(new LegacyBossSpecialDeathDropItem(
                700_132,
                Count: 1,
                ScatterRange: 10,
                "MonsterRole.cs Family.ID 4171/20060/8311 else DropItemID(700132, range 10)."));
        }

        items.AddRange(
        [
            StudyPointItem(3_005_127, 3, "for (num19 < 3) DropItemID(3005127, range 10)."),
            StudyPointItem(3_335_128, 3, "for (num19 < 3) DropItemID(3335128, range 10)."),
            StudyPointItem(3_328_253, 3, "for (b8 < 3) DropItemID(3328253, range 10)."),
            StudyPointItem(3_328_254, 3, "for (b8 < 3) DropItemID(3328254, range 10)."),
        ]);

        var pending = new List<string>();
        AppendGenerateSoulsItems(items, pending, catalog, rolls, level: 7, scatterRange: 10, "Family.ID 4171/20060/8311");

        if (rolls.DynamicInstanceReward)
        {
            items.AddRange(
            [
                StudyPointItem(3_333_403, 3, "DynamicID != UID for (num20 < 3) DropItemID(3333403, range 3).", scatterRange: 3),
                StudyPointItem(729_463, 1, "DynamicID != UID DropItemID(729463, range 3).", scatterRange: 3),
                StudyPointItem(3_306_885, 1, "DynamicID != UID DropItemID(3306885, range 3).", scatterRange: 3),
            ]);
        }

        items.AddRange(
        [
            StudyPointItem(3_330_770, 10, "for (b9 < 10) DropItemID(3330770, range 10)."),
            StudyPointItem(3_316_793, 10, "for (b9 < 10) DropItemID(3316793, range 10)."),
        ]);

        return new LegacyBossSpecialDeathDropPlan(
            familyId,
            mapId,
            boss,
            "MonsterRole.cs (Boss > 0 && Family.ID == 4171) || Family.ID == 20060 || Family.ID == 8311",
            items,
            [],
            pending);
    }

    private static LegacyBossSpecialDeathDropPlan CreateThrillingSpookPlan(
        uint mapId,
        bool boss,
        LegacyBossSpecialDeathDropRolls rolls,
        LegacyBossSpecialDeathDropCatalog catalog)
    {
        var chanceItem = rolls.Family4212RareDrop ? 4_200_010U : 3_321_805U;
        var chanceSource = rolls.Family4212RareDrop
            ? "MonsterRole.cs Family.ID == 4212 Core.Rate(10) DropItemID(4200010, range 10)."
            : "MonsterRole.cs Family.ID == 4212 else DropItemID(3321805, range 10).";
        var items = new List<LegacyBossSpecialDeathDropItem>
        {
            Item(3_335_128, 5, "for (num22 < 5) DropItemID(3335128, range 10)."),
            Item(3_005_127, 1, "DropItemID(3005127, range 10)."),
            Item(3_311_633, 1, "for (num23 < 1) DropItemID(3311633, range 10)."),
        };
        var pending = new List<string>();
        AppendGenerateSoulsItems(items, pending, catalog, rolls, level: 7, scatterRange: 10, "Family.ID == 4212");

        items.AddRange(
        [
            Item(3_335_125, 1, "DropItemID(3335125, range 10)."),
            Item(3_328_253, 3, "for (b10 < 3) DropItemID(3328253, range 10)."),
            Item(3_328_254, 3, "for (b10 < 3) DropItemID(3328254, range 10)."),
            Item(3_330_770, 10, "for (b11 < 10) DropItemID(3330770, range 10)."),
            Item(3_316_793, 10, "for (b11 < 10) DropItemID(3316793, range 10)."),
            new LegacyBossSpecialDeathDropItem(chanceItem, 1, 10, chanceSource),
        ]);

        return new LegacyBossSpecialDeathDropPlan(
            ThrillingSpookFamilyId,
            mapId,
            boss,
            "MonsterRole.cs Boss > 0 && Family.ID == 4212",
            items,
            [],
            pending);
    }

    private static LegacyBossSpecialDeathDropPlan CreateStudyPointTeamRewardPlan(
        uint familyId,
        uint mapId,
        bool boss,
        LegacyBossSpecialDeathDropRolls rolls,
        LegacyBossSpecialDeathDropCatalog catalog)
    {
        var items = new List<LegacyBossSpecialDeathDropItem>();
        var pending = new List<string>();
        AppendGenerateSoulsItems(items, pending, catalog, rolls, level: 6, scatterRange: 3, "Family.ID == 20055");

        if (rolls.Family20055Add711679)
        {
            items.Add(StudyPointTeamRewardItem(711_679, 1, "Core.Rate(30) list3.Add(711679)."));
        }

        if (rolls.Family20055Add711188)
        {
            items.Add(StudyPointTeamRewardItem(711_188, 1, "Core.Rate(30) list3.Add(711188)."));
        }

        return new LegacyBossSpecialDeathDropPlan(
            familyId,
            mapId,
            boss,
            "MonsterRole.cs Boss > 0 && Family.ID == 20055",
            items,
            [],
            pending);
    }

    private static LegacyBossSpecialDeathDropPlan CreateSoulBoxFamily6643Plan(
        uint familyId,
        uint mapId,
        bool boss,
        LegacyBossSpecialDeathDropRolls rolls,
        LegacyBossSpecialDeathDropCatalog catalog)
    {
        var items = new List<LegacyBossSpecialDeathDropItem>();
        var pending = new List<string>();
        AppendGenerateSoulsItems(items, pending, catalog, rolls, level: 6, scatterRange: 3, "Family.ID == 6643");

        return new LegacyBossSpecialDeathDropPlan(
            familyId,
            mapId,
            boss,
            "MonsterRole.cs Boss > 0 && Family.ID == 6643",
            items,
            [],
            pending);
    }

    private static void AppendGenerateSoulsItems(
        List<LegacyBossSpecialDeathDropItem> items,
        List<string> pending,
        LegacyBossSpecialDeathDropCatalog catalog,
        LegacyBossSpecialDeathDropRolls rolls,
        ushort level,
        byte scatterRange,
        string branchSource)
    {
        if (catalog.PurificationItemsByLevel.TryGetValue(level, out var purificationItems) &&
            purificationItems.Count > 0)
        {
            var count = GetLegacyGenerateSoulsPurificationCount(rolls);
            AppendRandomPoolItems(
                items,
                purificationItems,
                count,
                rolls.SoulsPurificationIndexes,
                level,
                scatterRange,
                branchSource,
                $"MobItemGenerator.GenerateSoulsItems({level}) ItemType.PurificationItems[{level}] random item.");
        }
        else
        {
            pending.Add($"MobItemGenerator.GenerateSoulsItems({level}) needs ItemType.PurificationItems[{level}], but the itemtype purification pool was not loaded.");
        }

        if (catalog.AccessoryItems.Count > 0)
        {
            var count = GetLegacyGenerateSoulsAccessoryCount(rolls);
            AppendRandomPoolItems(
                items,
                catalog.AccessoryItems,
                count,
                rolls.SoulsAccessoryIndexes,
                level,
                scatterRange,
                branchSource,
                $"MobItemGenerator.GenerateSoulsItems({level}) ItemType.Accessorys random item.");
        }
        else
        {
            pending.Add($"MobItemGenerator.GenerateSoulsItems({level}) needs ItemType.Accessorys, but the itemtype accessory pool was not loaded.");
        }

        items.Add(SoulItem(729_463, level, scatterRange, branchSource, "always adds 729463 because level > 3."));
    }

    private static int GetLegacyGenerateSoulsPurificationCount(LegacyBossSpecialDeathDropRolls rolls)
    {
        var modulo = GetLegacyGenerateSoulsModulo(rolls.SoulsPurificationRollModulo);
        return modulo == 0 ? 1 : modulo;
    }

    private static int GetLegacyGenerateSoulsAccessoryCount(LegacyBossSpecialDeathDropRolls rolls)
    {
        return GetLegacyGenerateSoulsModulo(rolls.SoulsAccessoryRollModulo);
    }

    private static int GetLegacyGenerateSoulsModulo(int? forcedModulo)
    {
        var value = forcedModulo ?? Random.Shared.Next(0, 1000);
        var modulo = value % 3;
        return modulo < 0 ? modulo + 3 : modulo;
    }

    private static void AppendRandomPoolItems(
        List<LegacyBossSpecialDeathDropItem> items,
        IReadOnlyList<uint> pool,
        int count,
        IReadOnlyList<int>? forcedIndexes,
        ushort level,
        byte scatterRange,
        string branchSource,
        string source)
    {
        for (var index = 0; index < count; index++)
        {
            var poolIndex = SelectPoolIndex(pool.Count, index, forcedIndexes);
            items.Add(SoulItem(pool[poolIndex], level, scatterRange, branchSource, $"{source} poolIndex={poolIndex}."));
        }
    }

    private static int SelectPoolIndex(
        int poolCount,
        int selectionIndex,
        IReadOnlyList<int>? forcedIndexes)
    {
        if (forcedIndexes is not null && selectionIndex < forcedIndexes.Count)
        {
            var forced = forcedIndexes[selectionIndex] % poolCount;
            return forced < 0 ? forced + poolCount : forced;
        }

        return Random.Shared.Next(0, poolCount);
    }

    private static int SelectPoolIndex(int poolCount, int? forcedIndex)
    {
        if (forcedIndex is { } forced)
        {
            var selected = forced % poolCount;
            return selected < 0 ? selected + poolCount : selected;
        }

        return Random.Shared.Next(0, poolCount);
    }

    private static LegacyBossSpecialDeathDropItem Item(uint itemId, int count, string source)
    {
        return new LegacyBossSpecialDeathDropItem(itemId, count, ScatterRange: 10, $"MonsterRole.cs Family.ID == 4212 {source}");
    }

    private static LegacyBossSpecialDeathDropItem StudyPointItem(
        uint itemId,
        int count,
        string source,
        byte scatterRange = 10)
    {
        return new LegacyBossSpecialDeathDropItem(itemId, count, scatterRange, $"MonsterRole.cs Family.ID 4171/20060/8311 {source}");
    }

    private static LegacyBossSpecialDeathDropItem DragonIslandItem(
        uint itemId,
        int count,
        string source,
        byte scatterRange = 10)
    {
        return new LegacyBossSpecialDeathDropItem(itemId, count, scatterRange, $"MonsterRole.cs Map == 10137 && Family.ID == 4220 {source}");
    }

    private static LegacyBossSpecialDeathDropItem QueenOfEvilItem(
        uint itemId,
        int count,
        string source,
        byte scatterRange = 10)
    {
        return new LegacyBossSpecialDeathDropItem(itemId, count, scatterRange, $"MonsterRole.cs QueenofEvil/Family.ID == 3970 {source}");
    }

    private static LegacyBossSpecialDeathDropItem DeityLandBossItem(
        uint itemId,
        int count,
        string source,
        byte scatterRange)
    {
        return new LegacyBossSpecialDeathDropItem(
            itemId,
            count,
            scatterRange,
            $"MonsterRole.cs killer.Player.Map == 10250 && Boss > 0 {source}");
    }

    private static LegacyBossSpecialDeathDropItem StudyPointTeamRewardItem(
        uint itemId,
        int count,
        string source)
    {
        return new LegacyBossSpecialDeathDropItem(itemId, count, ScatterRange: 3, $"MonsterRole.cs Family.ID == 20055 {source}");
    }

    private static LegacyBossSpecialDeathDropItem SoulItem(
        uint itemId,
        ushort level,
        byte scatterRange,
        string branchSource,
        string source)
    {
        return new LegacyBossSpecialDeathDropItem(
            itemId,
            Count: 1,
            scatterRange,
            $"MonsterRole.cs {branchSource} foreach GenerateSoulsItems({level}); MobItemGenerator.cs {source}");
    }
}
