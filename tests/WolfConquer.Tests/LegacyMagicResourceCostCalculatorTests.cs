using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicResourceCostCalculatorTests
{
    [Theory]
    [InlineData(1, 10, 90)]
    [InlineData(2, 15, 85)]
    [InlineData(3, 25, 75)]
    [InlineData(7, 75, 25)]
    [InlineData(8, 85, 15)]
    [InlineData(9, 90, 10)]
    public void MpMasterRuneLevelsReduceManaUsingLegacySourceTable(
        byte runeLevel,
        ushort expectedReductionPercent,
        ushort expectedUseMana)
    {
        var runeItemId = CreateMpMasterRuneItemId(runeLevel);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 103,
            }),
            CreateItemCatalog((int)runeItemId, $"MPMaster(+{runeLevel})"),
            CreateLegacyRuneEligibleAttributes());

        Assert.Equal(expectedUseMana, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.True(cost.HasMpMaster);
        Assert.Equal(runeLevel, cost.MpMasterLevel);
        Assert.Equal(expectedReductionPercent, cost.MpMasterReductionPercent);
    }

    [Fact]
    public void MpMasterAlternateRuneDoesNotReduceManaWhileNormalRuneSetIsActive()
    {
        var runeItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 123,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+9)"),
            CreateLegacyRuneEligibleAttributes());

        Assert.Equal(100, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.False(cost.HasMpMaster);
        Assert.Null(cost.MpMasterLevel);
        Assert.Equal(0, cost.MpMasterReductionPercent);
    }

    [Fact]
    public void MpMasterAlternateRuneReducesManaWhileAlternateRuneSetIsActive()
    {
        var runeItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 123,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+9)"),
            CreateLegacyRuneEligibleAttributes(),
            alternateEquipmentActive: true);

        Assert.Equal(10, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.True(cost.HasMpMaster);
        Assert.Equal((byte)9, cost.MpMasterLevel);
        Assert.Equal(90, cost.MpMasterReductionPercent);
    }

    [Fact]
    public void MpMasterAlternateRuneSetFallsBackToNormalRuneWithoutAlternateCounterpart()
    {
        var runeItemId = CreateMpMasterRuneItemId(3);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 103,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+3)"),
            CreateLegacyRuneEligibleAttributes(),
            alternateEquipmentActive: true);

        Assert.Equal(75, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.True(cost.HasMpMaster);
        Assert.Equal((byte)3, cost.MpMasterLevel);
        Assert.Equal(25, cost.MpMasterReductionPercent);
    }

    [Fact]
    public void MpMasterAlternateRuneSetUsesAlternateCounterpartOverNormalRune()
    {
        var normalRuneItemId = CreateMpMasterRuneItemId(1);
        var alternateRuneItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(
                new PlayerItemSnapshotItem
                {
                    ItemId = normalRuneItemId,
                    Position = 103,
                },
                new PlayerItemSnapshotItem
                {
                    ItemId = alternateRuneItemId,
                    Position = 123,
                }),
            CreateItemCatalog(
                ((int)normalRuneItemId, "MPMaster(+1)"),
                ((int)alternateRuneItemId, "MPMaster(+9)")),
            CreateLegacyRuneEligibleAttributes(),
            alternateEquipmentActive: true);

        Assert.Equal(10, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.True(cost.HasMpMaster);
        Assert.Equal((byte)9, cost.MpMasterLevel);
        Assert.Equal(90, cost.MpMasterReductionPercent);
    }

    [Fact]
    public void MpMasterRuneDoesNotReduceManaWhenCharacterClassFailsLegacyRuneJobRequirement()
    {
        var runeItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 103,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+9)", requiredClass: 16000),
            CreateLegacyRuneEligibleAttributes(),
            characterClass: 7051);

        Assert.Equal(100, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.False(cost.HasMpMaster);
        Assert.Null(cost.MpMasterLevel);
        Assert.Equal(0, cost.MpMasterReductionPercent);
    }

    [Fact]
    public void MpMasterRuneReducesManaWhenCharacterClassPassesLegacyRuneJobRequirement()
    {
        var runeItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 103,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+9)", requiredClass: 16000),
            CreateLegacyRuneEligibleAttributes(),
            characterClass: 16051);

        Assert.Equal(10, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.True(cost.HasMpMaster);
        Assert.Equal((byte)9, cost.MpMasterLevel);
        Assert.Equal(90, cost.MpMasterReductionPercent);
    }

    [Theory]
    [InlineData(1000, 1051)]
    [InlineData(2000, 2051)]
    [InlineData(4000, 4051)]
    [InlineData(5000, 5051)]
    [InlineData(6000, 6051)]
    [InlineData(7000, 7051)]
    [InlineData(8000, 8049)]
    [InlineData(9000, 9051)]
    [InlineData(13002, 13051)]
    [InlineData(14002, 14051)]
    [InlineData(16000, 16051)]
    public void MpMasterRuneReducesManaForMatchingSourceUpperPromotedClassFamily(
        int requiredClass,
        long characterClass)
    {
        var runeItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 103,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+9)", checked((uint)requiredClass)),
            CreateLegacyRuneEligibleAttributes(),
            characterClass: characterClass);

        Assert.Equal(10, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.True(cost.HasMpMaster);
        Assert.Equal((byte)9, cost.MpMasterLevel);
        Assert.Equal(90, cost.MpMasterReductionPercent);
    }

    [Theory]
    [InlineData(10000, 16057)]
    [InlineData(19000, 16057)]
    public void MpMasterRuneGenericTaoistRequirementMatchesLegacyItemTypePass(
        int requiredClass,
        long characterClass)
    {
        var runeItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 103,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+9)", checked((uint)requiredClass)),
            CreateLegacyRuneEligibleAttributes(),
            characterClass: characterClass);

        Assert.Equal(10, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.True(cost.HasMpMaster);
        Assert.Equal((byte)9, cost.MpMasterLevel);
        Assert.Equal(90, cost.MpMasterReductionPercent);
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(2, 14)]
    public void MpMasterRuneDoesNotReduceManaBeforeLegacyRuneEligibility(long reborn, long level)
    {
        var runeItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 103,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+9)"),
            CreateLegacyCharacterAttributes(reborn, level));

        Assert.Equal(100, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.False(cost.HasMpMaster);
        Assert.Null(cost.MpMasterLevel);
        Assert.Equal(0, cost.MpMasterReductionPercent);
    }

    [Fact]
    public void NonRuneMpMasterTextDoesNotReduceMana()
    {
        const uint itemId = 723_744;
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = itemId,
                Position = 103,
            }),
            CreateItemCatalog((int)itemId, "MPMaster(+9)Pack"),
            CreateLegacyRuneEligibleAttributes());

        Assert.Equal(100, cost.UseMana);
        Assert.Equal(0, cost.UseStamina);
        Assert.False(cost.HasMpMaster);
        Assert.Null(cost.MpMasterLevel);
        Assert.Equal(0, cost.MpMasterReductionPercent);
    }

    [Fact]
    public void NoMpMasterRuneKeepsOriginalManaCost()
    {
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 123, useStamina: 7),
            PlayerItemSnapshot.Empty,
            CreateItemCatalog(4_036_109, "MPMaster(+9)"));

        Assert.Equal(123, cost.UseMana);
        Assert.Equal(7, cost.UseStamina);
        Assert.False(cost.HasMpMaster);
        Assert.Null(cost.MpMasterLevel);
        Assert.Equal(0, cost.MpMasterReductionPercent);
    }

    [Fact]
    public void ScurvyBombStatusAddsLegacyStaminaSurcharge()
    {
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 0, useStamina: 5),
            PlayerItemSnapshot.Empty,
            catalog: null,
            characterAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyMagicResourceCostCalculator.ScurvyBombStatusFlag),
            });

        Assert.Equal(0, cost.UseMana);
        Assert.Equal(11, cost.UseStamina);
        Assert.False(cost.HasMpMaster);
        Assert.Equal(6, cost.ScurvyBombStaminaSurcharge);
    }

    [Theory]
    [InlineData(LegacyMagicResourceCostCalculator.IronbonePassiveSpellId)]
    [InlineData(LegacyMagicResourceCostCalculator.WarSuit201NormalAttack1SpellId)]
    [InlineData(LegacyMagicResourceCostCalculator.WarSuit201NormalAttack2SpellId)]
    [InlineData(LegacyMagicResourceCostCalculator.WarSuit201NormalAttack3SpellId)]
    [InlineData(LegacyMagicResourceCostCalculator.TripleAttackDragonhowlSpellId)]
    public void SpellIdsWithoutSourceExemptionPayRowCostsAndScurvySurcharge(ushort spellId)
    {
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(spellId, useMana: 100, useStamina: 5),
            PlayerItemSnapshot.Empty,
            catalog: null,
            characterAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyMagicResourceCostCalculator.ScurvyBombStatusFlag),
            });

        Assert.False(LegacyMagicResourceCostCalculator.IsLegacyResourceFreeSpell(spellId));
        Assert.Equal(100, cost.UseMana);
        Assert.Equal(11, cost.UseStamina);
        Assert.False(cost.HasMpMaster);
        Assert.Equal(6, cost.ScurvyBombStaminaSurcharge);
    }

    private static LegacyMagicRuntimeSpell CreateSpell(ushort useMana, byte useStamina = 0)
    {
        return CreateSpell(spellId: 1_000, useMana, useStamina);
    }

    private static LegacyMagicRuntimeSpell CreateSpell(ushort spellId, ushort useMana, byte useStamina = 0)
    {
        return new LegacyMagicRuntimeSpell(
            Id: spellId,
            Sort: LegacyMagicSort.Attack,
            Name: "Thunder",
            Level: 0,
            UseMana: useMana,
            Damage: 120,
            Experience: 100,
            MaxTargets: 0,
            Range: 9,
            NeedLevel: 1,
            WeaponType: 0,
            UseStamina: useStamina,
            CoolDown: 100,
            ColdTime: 1_400);
    }

    private static PlayerItemSnapshot CreatePlayerItems(params PlayerItemSnapshotItem[] runes)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems: runes);
    }

    private static uint CreateMpMasterRuneItemId(byte runeLevel)
    {
        return 4_030_800u + runeLevel;
    }

    private static IReadOnlyDictionary<string, string> CreateLegacyRuneEligibleAttributes()
    {
        return CreateLegacyCharacterAttributes(reborn: 2, level: 15);
    }

    private static IReadOnlyDictionary<string, string> CreateLegacyCharacterAttributes(long reborn, long level)
    {
        return new Dictionary<string, string>
        {
            ["Reborn"] = reborn.ToString(),
            ["Level"] = level.ToString(),
        };
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<ItemType>> CreateItemCatalog(int itemId, string name)
    {
        return CreateItemCatalog((itemId, name));
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<ItemType>> CreateItemCatalog(
        int itemId,
        string name,
        uint requiredClass)
    {
        return new Dictionary<int, IReadOnlyList<ItemType>>
        {
            [itemId] = [new ItemType(itemId, name, CreateItemFields(itemId, name, requiredClass))],
        };
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<ItemType>> CreateItemCatalog(
        params (int ItemId, string Name)[] items)
    {
        return items.ToDictionary(
            item => item.ItemId,
            item => (IReadOnlyList<ItemType>)[new ItemType(
                item.ItemId,
                item.Name,
                [item.ItemId.ToString(), item.Name])]);
    }

    private static IReadOnlyList<string> CreateItemFields(int itemId, string name, uint requiredClass)
    {
        var fields = Enumerable.Repeat("0", ItemType.RequiredClassFieldIndex + 1).ToArray();
        fields[0] = itemId.ToString();
        fields[1] = name;
        fields[ItemType.RequiredClassFieldIndex] = requiredClass.ToString();
        return fields;
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[16];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words);
    }
}
