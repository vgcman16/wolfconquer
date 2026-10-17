using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public readonly record struct LegacyMagicResourceCost(
    ushort UseMana,
    ushort UseStamina,
    byte? MpMasterLevel,
    ushort MpMasterReductionPercent,
    ushort ScurvyBombStaminaSurcharge,
    ushort DragonPierceStaminaSurcharge)
{
    public bool HasMpMaster => MpMasterLevel is not null;
}

public static partial class LegacyMagicResourceCostCalculator
{
    public const int ScurvyBombStatusFlag = 0x60;
    public const ushort ScurvyBombStaminaSurcharge = 6;
    public const int DragonPierceStaminaStatusFlag = 0x136;
    public const ushort IronbonePassiveSpellId = 17_650;
    public const ushort WarSuit201NormalAttack1SpellId = 17_460;
    public const ushort WarSuit201NormalAttack2SpellId = 17_470;
    public const ushort WarSuit201NormalAttack3SpellId = 17_480;
    public const ushort TripleAttackDragonhowlSpellId = 17_490;

    private const string DragonPierceStaminaAttribute = "DragonPierceStamina";
    private const string MpMasterRuneName = "MPMaster";
    private const string RebornAttribute = "Reborn";
    private const string LevelAttribute = "Level";
    private const string ClassAttribute = "Class";
    private const long LegacyRuneMinimumReborn = 2;
    private const long LegacyRuneMinimumLevel = 15;

    public static LegacyMagicResourceCost Calculate(
        LegacyMagicRuntimeSpell spell,
        PlayerItemSnapshot playerItems,
        WorldDataCatalog? catalog,
        IReadOnlyDictionary<string, string>? characterAttributes = null,
        bool alternateEquipmentActive = false,
        long? characterClass = null)
    {
        return Calculate(
            spell,
            playerItems,
            catalog?.ItemsById,
            characterAttributes,
            alternateEquipmentActive,
            characterClass);
    }

    public static LegacyMagicResourceCost Calculate(
        LegacyMagicRuntimeSpell spell,
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        IReadOnlyDictionary<string, string>? characterAttributes = null,
        bool alternateEquipmentActive = false,
        long? characterClass = null)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(playerItems);

        var mana = spell.UseMana;
        byte? mpMasterLevel = null;
        var mpMasterReductionPercent = 0;
        if (CanUseLegacyRuneEffects(characterAttributes) &&
            TryGetEquippedRuneLevel(
                playerItems,
                itemsById,
                MpMasterRuneName,
                alternateEquipmentActive,
                characterAttributes,
                characterClass,
                out var runeLevel))
        {
            mpMasterLevel = runeLevel;
            mpMasterReductionPercent = GetMpMasterManaReductionPercent(runeLevel);
            mana = ReduceMana(mana, mpMasterReductionPercent);
        }

        var stamina = (ushort)spell.UseStamina;
        var scurvyBombStaminaSurcharge = HasScurvyBombStatus(characterAttributes)
            ? ScurvyBombStaminaSurcharge
            : (ushort)0;
        stamina = AddSaturating(stamina, scurvyBombStaminaSurcharge);
        var dragonPierceStaminaSurcharge = GetDragonPierceStaminaSurcharge(characterAttributes);
        stamina = AddSaturating(stamina, dragonPierceStaminaSurcharge);

        return new LegacyMagicResourceCost(
            mana,
            stamina,
            mpMasterLevel,
            (ushort)mpMasterReductionPercent,
            scurvyBombStaminaSurcharge,
            dragonPierceStaminaSurcharge);
    }

    public static bool IsLegacyResourceFreeSpell(ushort spellId)
    {
        // Matching CanUseSpell.Verified has no spell-ID exemption. The map 1039
        // bypass belongs to cast preparation, not to the spell's row costs.
        _ = spellId;
        return false;
    }

    public static ushort GetMpMasterManaReductionPercent(byte runeLevel)
    {
        return runeLevel switch
        {
            2 => 15,
            3 => 25,
            7 => 75,
            8 => 85,
            9 => 90,
            _ => checked((ushort)(runeLevel * 10)),
        };
    }

    private static ushort ReduceMana(ushort useMana, int reductionPercent)
    {
        if (useMana == 0 || reductionPercent <= 0)
        {
            return useMana;
        }

        var reduction = (uint)useMana * (uint)reductionPercent / 100;
        return reduction >= useMana
            ? (ushort)0
            : (ushort)(useMana - reduction);
    }

    private static ushort AddSaturating(ushort value, ushort addition)
    {
        var sum = value + (uint)addition;
        return sum > ushort.MaxValue ? ushort.MaxValue : (ushort)sum;
    }

    private static bool HasScurvyBombStatus(IReadOnlyDictionary<string, string>? characterAttributes)
    {
        return characterAttributes is not null &&
            LegacyPlayerDeathState.HasStatusFlag(characterAttributes, ScurvyBombStatusFlag);
    }

    private static ushort GetDragonPierceStaminaSurcharge(
        IReadOnlyDictionary<string, string>? characterAttributes)
    {
        if (characterAttributes is null ||
            !LegacyPlayerDeathState.HasStatusFlag(characterAttributes, DragonPierceStaminaStatusFlag) ||
            !TryReadUInt64(characterAttributes, DragonPierceStaminaAttribute, out var surcharge))
        {
            return 0;
        }

        return surcharge > ushort.MaxValue
            ? ushort.MaxValue
            : (ushort)surcharge;
    }

    internal static bool TryGetEquippedRuneLevel(
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        string runeName,
        bool alternateEquipmentActive,
        IReadOnlyDictionary<string, string>? characterAttributes,
        long? characterClass,
        out byte level)
    {
        level = 0;
        if (!TryGetEquippedRuneItem(playerItems, itemsById, runeName,
                alternateEquipmentActive, characterAttributes, characterClass, out var rune))
        {
            return false;
        }

        level = (byte)(rune!.ItemId % 100);
        return true;
    }

    private static bool PassesLegacyRuneJobRequirement(
        ItemType itemType,
        IReadOnlyDictionary<string, string>? characterAttributes,
        long? characterClass)
    {
        var requiredClass = itemType.RequiredClass;
        if (requiredClass == 0)
        {
            return true;
        }

        var classCode = characterClass;
        if (classCode is null &&
            characterAttributes is not null &&
            TryReadInt64(characterAttributes, ClassAttribute, out var attributeClass))
        {
            classCode = attributeClass;
        }

        if (classCode is null)
        {
            return true;
        }

        return requiredClass switch
        {
            >= 1000 and <= 1005 => classCode >= requiredClass && classCode <= 1051,
            >= 2000 and <= 2005 => classCode >= requiredClass && classCode <= 2051,
            >= 4000 and <= 4005 => classCode >= requiredClass && classCode <= 4051,
            >= 5000 and <= 5005 => classCode >= requiredClass && classCode <= 5051,
            >= 6000 and <= 6005 => classCode >= requiredClass && classCode <= 6051,
            >= 7000 and <= 7005 => classCode >= requiredClass && classCode <= 7051,
            >= 8000 and <= 8005 => classCode >= requiredClass && classCode <= 8049,
            >= 9000 and <= 9005 => classCode >= requiredClass && classCode <= 9051,
            >= 13000 and <= 13005 => classCode >= requiredClass && classCode <= 13051,
            >= 14000 and <= 14005 => classCode >= requiredClass && classCode <= 14051,
            10000 or 19000 => classCode >= 10000,
            >= 16000 and <= 16005 => classCode >= requiredClass && classCode <= 16051,
            _ => false,
        };
    }

    private static bool CanUseLegacyRuneEffects(IReadOnlyDictionary<string, string>? characterAttributes)
    {
        if (characterAttributes is null ||
            !TryReadInt64(characterAttributes, RebornAttribute, out var reborn) ||
            !TryReadInt64(characterAttributes, LevelAttribute, out var level))
        {
            return true;
        }

        return reborn >= LegacyRuneMinimumReborn &&
            level >= LegacyRuneMinimumLevel;
    }

    private static bool TryReadInt64(
        IReadOnlyDictionary<string, string> values,
        string key,
        out long value)
    {
        value = 0;
        return values.TryGetValue(key, out var raw) &&
            long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt64(
        IReadOnlyDictionary<string, string> values,
        string key,
        out ulong value)
    {
        value = 0;
        return values.TryGetValue(key, out var raw) &&
            ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsLegacyRuneItem(uint itemId)
    {
        var family = itemId / 10000;
        return family is >= 401 and <= 403;
    }

    private static bool TryGetItemType(
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>> itemsById,
        uint itemId,
        out ItemType itemType)
    {
        itemType = default!;
        if (itemId > int.MaxValue ||
            !itemsById.TryGetValue((int)itemId, out var rows) ||
            rows.Count == 0)
        {
            return false;
        }

        itemType = rows[0];
        return true;
    }

    private static bool ContainsRuneName(ItemType itemType, string runeName)
    {
        return itemType.Name.Contains(runeName, StringComparison.OrdinalIgnoreCase) ||
            (itemType.Fields.Count > 1 &&
                itemType.Fields[1].Contains(runeName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLegacyWornRunePosition(
        ushort position,
        IReadOnlyList<PlayerItemSnapshotItem> runeItems,
        bool alternateEquipmentActive)
    {
        if (position is < 101 or > 134)
        {
            return false;
        }

        if (!alternateEquipmentActive)
        {
            return position <= 120;
        }

        return position >= 120 || !HasLegacyAlternateRuneCounterpart(runeItems, position);
    }

    private static bool HasLegacyAlternateRuneCounterpart(
        IReadOnlyList<PlayerItemSnapshotItem> runeItems,
        ushort normalPosition)
    {
        if (normalPosition >= 120)
        {
            return false;
        }

        var alternatePosition = normalPosition + 20;
        foreach (var rune in runeItems)
        {
            if (rune.Position == alternatePosition)
            {
                return true;
            }
        }

        return false;
    }
}
