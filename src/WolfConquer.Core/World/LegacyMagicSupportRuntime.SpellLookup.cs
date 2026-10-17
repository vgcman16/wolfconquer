using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyMagicSupportRuntime
{
    private const int TypeIdField = 1;
    private const int SortField = 2;
    private const int NameField = 3;
    private const int LevelField = 8;
    private const int UseManaField = 9;
    private const int DamageField = 10;
    private const int RateField = 12;
    private const int DurationField = 13;
    private const int MaxTargetsField = 14;
    private const int RangeField = 15;
    private const int StatusField = 16;
    private const int ExperienceField = 18;
    private const int NeedLevelField = 20;
    private const int WeaponTypeField = 22;
    private const int UseStaminaField = 29;
    private const int CoolDownField = 33;
    private const int ColdTimeField = 47;
    private const int MinimumFieldCount = ColdTimeField + 1;

    public static bool TryResolveSupportSpell(
        WorldDataCatalog worldData,
        ushort spellId,
        ushort spellLevel,
        out LegacyMagicSupportSpell spell)
    {
        ArgumentNullException.ThrowIfNull(worldData);

        spell = default!;
        if (!worldData.MagicByTypeId.TryGetValue(spellId, out var rows))
        {
            return false;
        }

        MagicType? fallback = null;
        foreach (var row in rows)
        {
            if (TryReadByte(row.Fields, LevelField, out var rowLevel) && rowLevel == spellLevel)
            {
                return TryCreateSupportSpell(row, out spell);
            }

            fallback ??= row;
        }

        return fallback is not null && TryCreateSupportSpell(fallback, out spell);
    }

    public static bool TryCreateSupportSpell(MagicType row, out LegacyMagicSupportSpell spell)
    {
        ArgumentNullException.ThrowIfNull(row);

        spell = default!;
        if (row.Fields.Count < MinimumFieldCount ||
            !TryReadUInt16(row.Fields, TypeIdField, out var id) ||
            !TryReadByte(row.Fields, SortField, out var sort) ||
            !TryReadByte(row.Fields, LevelField, out var level) ||
            !TryReadUInt16(row.Fields, UseManaField, out var useMana) ||
            !TryReadUInt32(row.Fields, DamageField, out var rawDamage) ||
            !TryReadInt32(row.Fields, RateField, out var rate) ||
            !TryReadUInt32(row.Fields, DurationField, out var duration) ||
            !TryReadUInt32(row.Fields, ExperienceField, out var experience) ||
            !TryReadByte(row.Fields, MaxTargetsField, out var maxTargets) ||
            !TryReadByte(row.Fields, RangeField, out var range) ||
            !TryReadInt32(row.Fields, StatusField, out var status) ||
            !TryReadUInt16(row.Fields, NeedLevelField, out var needLevel) ||
            !TryReadUInt32(row.Fields, WeaponTypeField, out var weaponType) ||
            !TryReadByte(row.Fields, UseStaminaField, out var useStamina) ||
            !TryReadUInt16(row.Fields, CoolDownField, out var coolDown) ||
            !TryReadInt32(row.Fields, ColdTimeField, out var coldTime))
        {
            return false;
        }

        var supportSort = (LegacyMagicSort)sort;
        spell = new LegacyMagicSupportSpell(
            id,
            supportSort,
            row.Fields.Count > NameField ? row.Fields[NameField] : row.Name,
            level,
            useMana,
            rawDamage,
            NormalizeLegacySupportPower(supportSort, id, level, rawDamage),
            rate,
            duration,
            experience,
            maxTargets,
            range,
            status,
            needLevel,
            weaponType,
            useStamina,
            coolDown,
            coldTime);
        return true;
    }

    private static uint NormalizeLegacySupportPower(LegacyMagicSort sort, ushort spellId, byte level, uint rawDamage)
    {
        if (spellId == 1_195 && level == 2)
        {
            return 1_020;
        }

        if (rawDamage == 0)
        {
            return 100;
        }

        if (spellId is 13_030 or 15_560 or 14_320 or 12_200 or 15_440 or 15_470 or 14_500)
        {
            return rawDamage;
        }

        if ((((int)sort == 1 || (int)sort == 2 || (int)sort == 20) && rawDamage < 10_000) ||
            (int)sort == 51)
        {
            return rawDamage;
        }

        var normalized = Math.Min(rawDamage % 1_000, 500);
        if (spellId is 12_580 or 12_590 or 12_600)
        {
            normalized = 100;
        }

        if (normalized == 0)
        {
            normalized = 100;
        }

        return spellId switch
        {
            11_140 or 10_490 => 150,
            3_306 => 1,
            12_380 => 5_000,
            12_070 => 104,
            _ => normalized,
        };
    }

    private static bool TryReadUInt16(IReadOnlyList<string> fields, int index, out ushort value)
    {
        value = 0;
        return index < fields.Count &&
            ushort.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt32(IReadOnlyList<string> fields, int index, out uint value)
    {
        value = 0;
        return index < fields.Count &&
            uint.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadByte(IReadOnlyList<string> fields, int index, out byte value)
    {
        value = 0;
        return index < fields.Count &&
            byte.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadInt32(IReadOnlyList<string> fields, int index, out int value)
    {
        value = 0;
        return index < fields.Count &&
            int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
