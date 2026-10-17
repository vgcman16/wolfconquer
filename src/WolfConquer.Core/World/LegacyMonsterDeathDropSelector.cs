using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyMonsterDeathDropSelector
{
    private const byte LegacyBossFamilyGeneratedDropCount = 4;

    private static readonly ushort[] LegacyGeneratedNecklaceTypes = [120, 121];
    private static readonly ushort[] LegacyGeneratedRingTypes = [150, 151];
    private static readonly ushort[] LegacyGeneratedArmetTypes = [111, 112, 113, 114, 117, 118];
    private static readonly ushort[] LegacyGeneratedArmorTypes = [130, 131, 132, 133, 134];
    private static readonly ushort[] LegacyGeneratedOneHanderTypes = [410, 420, 421, 430, 440, 450, 460, 480, 481, 490, 500, 601];
    private static readonly ushort[] LegacyGeneratedTwoHanderTypes = [510, 530, 560, 561, 580, 900];

    public static LegacyMonsterDeathDropSelection Select(
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMonsterDeathDropRolls rolls,
        LegacyMonsterDeathDropContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(monsterFamily);
        ArgumentNullException.ThrowIfNull(rolls);

        context ??= LegacyMonsterDeathDropContext.Default;
        var roll = PositiveModulo(rolls.DropRoll, 1000);
        if (!context.DropsOnDeathEnabled)
        {
            return NoDrop("Source MonsterRole skips drops when DropItemsOnDeath is not enabled.");
        }

        if (IsLegacyMonsterDropBlockedMap(monster.MapId))
        {
            return NoDrop($"Source MonsterRole blocks monster drops on map {monster.MapId}.");
        }

        if (IsLegacyBossFamilyMonster(monster))
        {
            return SelectBossFamilyDrop(monsterFamily, rolls, context);
        }

        if (monster.MapId == 11127)
        {
            return NoDrop("Map 11127 has source fixed-item drop branches before normal drops; that map-specific branch is pending.");
        }

        if (roll is > 300 and < 340)
        {
            return NoDrop("Source MonsterRole returns without a normal drop for rolls 301..339 outside map 11127.");
        }

        var dropCount = GetLegacyDropCount(roll, rolls.DropCountRateRoll);
        if (roll is > 40 and < 60)
        {
            return SelectMoneyDrop(rolls.MoneyAmount, dropCount, context);
        }

        if (roll is > 500 and < 600)
        {
            return SelectFixedItemDrop(monsterFamily, "drop_hp", "HP item roll 501..599", dropCount, context);
        }

        if (roll is > 600 and < 700)
        {
            return SelectFixedItemDrop(monsterFamily, "drop_mp", "MP item roll 601..699", dropCount, context);
        }

        if (roll <= 700)
        {
            return NoDrop($"Source roll {roll} is outside the modeled normal drop bands.");
        }

        if (TrySelectSpecialDrop(monsterFamily, rolls, dropCount, context, out var specialDrop))
        {
            return specialDrop;
        }

        return SelectGeneratedItemDrop(monsterFamily, rolls, dropCount, context);
    }

    private static LegacyMonsterDeathDropSelection SelectBossFamilyDrop(
        Monster monsterFamily,
        LegacyMonsterDeathDropRolls rolls,
        LegacyMonsterDeathDropContext context)
    {
        if (rolls.GeneratedSort is not { } forcedSort)
        {
            return NoDrop("Source GenerateBossFamily hit, but no deterministic generated item sort was supplied.");
        }

        if (rolls.GeneratedBaseLevel is not { } forcedLevel)
        {
            return NoDrop("Source GenerateBossFamily hit, but no deterministic generated item level was supplied.");
        }

        var level = AlterGeneratedItemLevel(forcedLevel, forcedSort, rolls);
        if (level > 99)
        {
            return NoDrop($"Generated boss-family equipment level {level} is outside the local item-id level range.");
        }

        var quality = rolls.GeneratedQuality ?? (byte)3;
        if (quality > 9)
        {
            return NoDrop($"Generated boss-family equipment quality {quality} is outside the source item-id quality digit range.");
        }

        var candidate = checked((uint)forcedSort * 1000U + level * 10U + quality);
        if (!TryUseKnownItemId(candidate, context, out var reason))
        {
            return NoDrop(reason);
        }

        return new LegacyMonsterDeathDropSelection(
            LegacyMonsterDeathDropKind.Item,
            "Source MonsterRole routes boss-family deaths through MobItemGenerator.GenerateBossFamily; the deterministic generated item hook models the four source loop candidates.",
            LegacyBossFamilyGeneratedDropCount,
            candidate,
            MoneyAmount: 0,
            forcedSort,
            level,
            quality);
    }

    private static LegacyMonsterDeathDropSelection SelectMoneyDrop(
        uint moneyAmount,
        byte dropCount,
        LegacyMonsterDeathDropContext context)
    {
        if (moneyAmount == 0)
        {
            return NoDrop("Money roll hit, but the source GenerateGold amount roll was not supplied.");
        }

        var itemId = GetLegacyMoneyItemId(moneyAmount);
        if (!TryUseKnownItemId(itemId, context, out var reason))
        {
            return NoDrop(reason);
        }

        return new LegacyMonsterDeathDropSelection(
            LegacyMonsterDeathDropKind.Money,
            "Source roll 41..59 maps GenerateGold amount to a money item id.",
            dropCount,
            itemId,
            moneyAmount);
    }

    private static LegacyMonsterDeathDropSelection SelectFixedItemDrop(
        Monster monsterFamily,
        string attributeName,
        string branchName,
        byte dropCount,
        LegacyMonsterDeathDropContext context)
    {
        if (!TryReadUInt32Attribute(monsterFamily.Attributes, out var itemId, attributeName))
        {
            return NoDrop($"{branchName} hit, but monster family attribute '{attributeName}' is missing or invalid.");
        }

        if (!TryUseKnownItemId(itemId, context, out var reason))
        {
            return NoDrop(reason);
        }

        return new LegacyMonsterDeathDropSelection(
            LegacyMonsterDeathDropKind.Item,
            $"Source {branchName} maps to monster family '{attributeName}'.",
            dropCount,
            itemId,
            MoneyAmount: 0);
    }

    private static bool TrySelectSpecialDrop(
        Monster monsterFamily,
        LegacyMonsterDeathDropRolls rolls,
        byte dropCount,
        LegacyMonsterDeathDropContext context,
        out LegacyMonsterDeathDropSelection selection)
    {
        if (monsterFamily.SpecialDrops.Count == 0 || !rolls.SpecialDropRateHit)
        {
            selection = default!;
            return false;
        }

        var index = PositiveModulo(rolls.SpecialDropIndex, monsterFamily.SpecialDrops.Count);
        var specialDrop = monsterFamily.SpecialDrops[index];
        if (specialDrop.ItemTypeId <= 0)
        {
            selection = NoDrop("Source special drop watcher hit, but the local SpecialDrop item id is not positive.");
            return true;
        }

        var itemId = checked((uint)specialDrop.ItemTypeId);
        if (!TryUseKnownItemId(itemId, context, out var reason))
        {
            selection = NoDrop(reason);
            return true;
        }

        selection = new LegacyMonsterDeathDropSelection(
            LegacyMonsterDeathDropKind.SpecialItem,
            "Source MobItemGenerator checks DropSpecials before generated equipment; supplied watcher hit selected a local SpecialDrop.",
            dropCount,
            itemId,
            MoneyAmount: 0,
            ItemSort: null,
            ItemLevel: null,
            ItemQuality: checked((byte)(itemId % 10)),
            SpecialDropChanceOrCount: specialDrop.ChanceOrCount);
        return true;
    }

    private static LegacyMonsterDeathDropSelection SelectGeneratedItemDrop(
        Monster monsterFamily,
        LegacyMonsterDeathDropRolls rolls,
        byte dropCount,
        LegacyMonsterDeathDropContext context)
    {
        if (!TrySelectGeneratedItemBase(monsterFamily, rolls, out var itemBase, out var reason))
        {
            return NoDrop(reason);
        }

        if (itemBase.Level == 99)
        {
            return NoDrop("Source MobItemGenerator suppresses generated equipment when the selected drop level is 99.");
        }

        var level = AlterGeneratedItemLevel(itemBase.Level, itemBase.Sort, rolls);
        if (level > 99)
        {
            return NoDrop($"Generated equipment level {level} is outside the local item-id level range.");
        }

        var quality = rolls.GeneratedQuality ?? (byte)3;
        if (quality > 9)
        {
            return NoDrop($"Generated equipment quality {quality} is outside the source item-id quality digit range.");
        }

        var candidate = checked((uint)itemBase.Sort * 1000U + level * 10U + quality);
        if (!TryUseKnownItemId(candidate, context, out reason))
        {
            return NoDrop(reason);
        }

        return new LegacyMonsterDeathDropSelection(
            LegacyMonsterDeathDropKind.Item,
            "Source roll >700 maps through MobItemGenerator generated equipment sort/level/quality.",
            dropCount,
            candidate,
            MoneyAmount: 0,
            itemBase.Sort,
            level,
            quality);
    }

    private static bool TrySelectGeneratedItemBase(
        Monster monsterFamily,
        LegacyMonsterDeathDropRolls rolls,
        out LegacyMonsterGeneratedItemBase itemBase,
        out string reason)
    {
        if (rolls.GeneratedSort is { } forcedSort)
        {
            if (rolls.GeneratedBaseLevel is { } forcedLevel)
            {
                itemBase = new LegacyMonsterGeneratedItemBase(forcedSort, forcedLevel);
                reason = "";
                return true;
            }

            if (TryReadGeneratedItemLevelForSort(monsterFamily, forcedSort, out var level))
            {
                itemBase = new LegacyMonsterGeneratedItemBase(forcedSort, level);
                reason = "";
                return true;
            }

            itemBase = default;
            reason = $"Generated sort {forcedSort} was supplied, but no source drop_* level attribute maps to that sort.";
            return false;
        }

        if (rolls.GeneratedCategoryRoll is not { } categoryRoll)
        {
            itemBase = default;
            reason = "Generated equipment roll hit, but no item category roll was supplied.";
            return false;
        }

        var normalizedCategoryRoll = PositiveModulo(categoryRoll, 1200);
        if (normalizedCategoryRoll < 20)
        {
            return TrySelectGeneratedItemBase(monsterFamily, [160], rolls, out itemBase, out reason, "drop_shoes", "drop_boots");
        }

        if (normalizedCategoryRoll < 50)
        {
            return TrySelectGeneratedItemBase(monsterFamily, LegacyGeneratedNecklaceTypes, rolls, out itemBase, out reason, "drop_necklace");
        }

        if (normalizedCategoryRoll < 100)
        {
            return TrySelectGeneratedItemBase(monsterFamily, LegacyGeneratedRingTypes, rolls, out itemBase, out reason, "drop_ring");
        }

        if (normalizedCategoryRoll < 400)
        {
            return TrySelectGeneratedItemBase(monsterFamily, LegacyGeneratedArmetTypes, rolls, out itemBase, out reason, "drop_armet");
        }

        if (normalizedCategoryRoll < 700)
        {
            return TrySelectGeneratedItemBase(monsterFamily, LegacyGeneratedArmorTypes, rolls, out itemBase, out reason, "drop_armor");
        }

        if (rolls.GeneratedWeaponRoll is not { } weaponRoll)
        {
            itemBase = default;
            reason = "Generated weapon branch hit, but no weapon-category roll was supplied.";
            return false;
        }

        var normalizedWeaponRoll = PositiveModulo(weaponRoll, 100);
        if (normalizedWeaponRoll < 20)
        {
            return TrySelectGeneratedItemBase(monsterFamily, [900], rolls, out itemBase, out reason, "drop_shield");
        }

        if (normalizedWeaponRoll >= 40 && normalizedWeaponRoll < 80)
        {
            return TrySelectGeneratedItemBase(monsterFamily, LegacyGeneratedOneHanderTypes, rolls, out itemBase, out reason, "drop_weapon");
        }

        if (normalizedWeaponRoll >= 80)
        {
            return TrySelectGeneratedItemBase(monsterFamily, LegacyGeneratedTwoHanderTypes, rolls, out itemBase, out reason, "drop_weapon");
        }

        itemBase = default;
        reason = "Source generated weapon roll 20..39 does not assign a generated equipment sort.";
        return false;
    }

    private static bool TrySelectGeneratedItemBase(
        Monster monsterFamily,
        IReadOnlyList<ushort> sorts,
        LegacyMonsterDeathDropRolls rolls,
        out LegacyMonsterGeneratedItemBase itemBase,
        out string reason,
        params string[] levelAttributeNames)
    {
        if (!TryReadUInt32Attribute(monsterFamily.Attributes, out var level, levelAttributeNames))
        {
            itemBase = default;
            reason = $"Generated equipment branch hit, but none of [{string.Join(", ", levelAttributeNames)}] is available.";
            return false;
        }

        if (sorts.Count == 0)
        {
            itemBase = default;
            reason = "Generated equipment branch has no source sort table.";
            return false;
        }

        var index = PositiveModulo(rolls.GeneratedSortIndexRoll, sorts.Count);
        itemBase = new LegacyMonsterGeneratedItemBase(sorts[index], level);
        reason = "";
        return true;
    }

    private static bool TryReadGeneratedItemLevelForSort(Monster monsterFamily, ushort sort, out uint level)
    {
        if (sort == 160)
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_shoes", "drop_boots");
        }

        if (LegacyGeneratedNecklaceTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_necklace");
        }

        if (LegacyGeneratedRingTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_ring");
        }

        if (LegacyGeneratedArmetTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_armet");
        }

        if (LegacyGeneratedArmorTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_armor");
        }

        if (sort == 900)
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_shield");
        }

        if (LegacyGeneratedOneHanderTypes.Contains(sort) ||
            LegacyGeneratedTwoHanderTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_weapon");
        }

        level = 0;
        return false;
    }

    private static uint AlterGeneratedItemLevel(uint level, ushort sort, LegacyMonsterDeathDropRolls rolls)
    {
        var alterRoll = PositiveModulo(rolls.GeneratedLevelAlterRoll, 100);
        if (alterRoll < 50)
        {
            var maxExclusive = checked((int)Math.Min(int.MaxValue, level / 2U + level / 3U));
            var lowerRoll = PositiveModulo(rolls.GeneratedLowerLevelRoll, Math.Max(1, maxExclusive));
            var altered = checked((uint)lowerRoll);
            return altered > 1 ? altered - 1U : altered;
        }

        if (alterRoll > 80)
        {
            return Math.Min(level + 1U, IsLegacyGeneratedShortLevelSort(sort) ? 9U : 23U);
        }

        return level;
    }

    private static bool IsLegacyGeneratedShortLevelSort(ushort sort)
    {
        return sort is >= 110 and <= 114 or >= 130 and <= 134 or >= 900 and <= 999;
    }

    private static byte GetLegacyDropCount(int roll, int rateRoll)
    {
        var count = (byte)(roll % 5);
        if (count == 0 || PositiveModulo(rateRoll, 100) < 80)
        {
            return 1;
        }

        return count;
    }

    private static uint GetLegacyMoneyItemId(uint amount)
    {
        if (amount < 100)
        {
            return 1_090_000;
        }

        if (amount < 399)
        {
            return 1_090_010;
        }

        if (amount < 5_099)
        {
            return 1_090_020;
        }

        if (amount < 8_099)
        {
            return 1_091_000;
        }

        if (amount < 12_099)
        {
            return 1_091_010;
        }

        return 1_091_020;
    }

    private static bool TryReadUInt32Attribute(
        IReadOnlyDictionary<string, string> attributes,
        out uint value,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (attributes.TryGetValue(name, out var raw) &&
                uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryUseKnownItemId(uint candidate, LegacyMonsterDeathDropContext context, out string reason)
    {
        if (candidate == 0 || candidate > int.MaxValue)
        {
            reason = $"Candidate drop item id {candidate} is outside the local item catalog id range.";
            return false;
        }

        if (context.ItemExists is null)
        {
            reason = $"Candidate drop item id {candidate} needs WorldData item validation before runtime can spawn it.";
            return false;
        }

        if (!context.ItemExists(candidate))
        {
            reason = $"Candidate drop item id {candidate} is not present in the supplied WorldData item catalog.";
            return false;
        }

        reason = "";
        return true;
    }

    internal static bool IsLegacyMonsterDropBlockedMap(int mapId)
    {
        return mapId is 10_250 or 3_846 or 2_351 or 2_352 or 2_353;
    }

    private static bool IsLegacyBossFamilyMonster(LegacyMonsterInstance monster)
    {
        return monster.Boss || monster.MaxLife is > 100_000 and < 7_000_000;
    }

    private static int PositiveModulo(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static LegacyMonsterDeathDropSelection NoDrop(string reason)
    {
        return new LegacyMonsterDeathDropSelection(LegacyMonsterDeathDropKind.None, reason);
    }

    private readonly record struct LegacyMonsterGeneratedItemBase(ushort Sort, uint Level);
}

public sealed record LegacyMonsterDeathDropRolls(
    int DropRoll,
    uint MoneyAmount = 0,
    int DropCountRateRoll = 0,
    bool SpecialDropRateHit = false,
    int SpecialDropIndex = 0,
    ushort? GeneratedSort = null,
    uint? GeneratedBaseLevel = null,
    byte? GeneratedQuality = null,
    int? GeneratedCategoryRoll = null,
    int? GeneratedWeaponRoll = null,
    int GeneratedSortIndexRoll = 0,
    int GeneratedLevelAlterRoll = 50,
    int GeneratedLowerLevelRoll = 0);

public sealed record LegacyMonsterDeathDropContext(
    Func<uint, bool>? ItemExists = null,
    bool DropsOnDeathEnabled = true,
    byte KillerLevel = 0,
    byte KillerVipLevel = 0,
    bool KillerOnAutoHunt = false)
{
    public static LegacyMonsterDeathDropContext Default { get; } = new();

    public static LegacyMonsterDeathDropContext FromWorldData(
        WorldDataCatalog worldData,
        bool dropsOnDeathEnabled = true,
        byte killerLevel = 0,
        byte killerVipLevel = 0,
        bool killerOnAutoHunt = false)
    {
        ArgumentNullException.ThrowIfNull(worldData);

        return new LegacyMonsterDeathDropContext(
            itemId => itemId <= int.MaxValue &&
                worldData.ItemsById.TryGetValue(checked((int)itemId), out var items) &&
                items.Count > 0,
            dropsOnDeathEnabled,
            killerLevel,
            killerVipLevel,
            killerOnAutoHunt);
    }
}

public sealed record LegacyMonsterDeathDropSelection(
    LegacyMonsterDeathDropKind Kind,
    string Reason,
    byte DropCount = 0,
    uint ItemId = 0,
    uint MoneyAmount = 0,
    ushort? ItemSort = null,
    uint? ItemLevel = null,
    byte? ItemQuality = null,
    int? SpecialDropChanceOrCount = null);

public enum LegacyMonsterDeathDropKind
{
    None,
    Money,
    Item,
    SpecialItem,
}
