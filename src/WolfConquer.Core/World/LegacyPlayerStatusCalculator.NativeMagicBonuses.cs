using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyPlayerStatusCalculator
{
    // Four-field source-query contributions, not a second full-status projection.
    // Raw table rows must already satisfy the source unsigned loader domain.
    internal static LegacyNativeMagicQueryFields NativeMagicBeastAndProfession(
        CharacterSnapshot character, WorldDataCatalog? data)
    {
        uint attack = 0, critical = 0, increase = 0;
        unchecked
        {
            if (data is not null && data.BeastAttributesByLevel.Count != 0 &&
                ReadBooleanAttribute(character, "TailedBeastActivated") &&
                TryReadUIntAttribute(character, "TailedBeastTotalPoints", out var points))
            {
                var level = CalculateLegacyBeastLevel(points, data.BeastAttributesByLevel);
                if (data.BeastAttributesByLevel.TryGetValue(level, out var beast))
                {
                    attack += NativeMagicUnsignedRow(beast.Fields, 4);
                    // Equip1735 explicitly narrows FinalMDmgDealt, NOT MAttack.
                    increase += (ushort)NativeMagicUnsignedRow(beast.Fields, 8);
                }
            }
            if (data is not null && character.Class is >= 0 and <= int.MaxValue &&
                data.ProfessionLevelBenefitsByProfessionCode.TryGetValue((int)character.Class, out var profession))
            {
                // Source loader takes exactly FOUR values; field7 is prestige, not a fifth benefit.
                for (var at = 3; at < 7; at++)
                {
                    var encoded = NativeMagicUnsignedRow(profession.Fields, at);
                    switch (encoded / 10_000)
                    {
                        case 8: attack += encoded % 10_000; break;
                        case 2: critical += encoded % 10_000; break;
                    }
                }
            }
        }
        return new(attack, 0, critical, increase);
    }

    internal static LegacyNativeMagicQueryFields NativeMagicMythsoulRows(
        PlayerItemSnapshot items, WorldDataCatalog? data)
    {
        uint attack = 0, critical = 0, increase = 0;
        if (data is null) return default;
        unchecked
        {
            foreach (var item in items.MythsoulBagItems)
            {
                if (item.ItemId > int.MaxValue ||
                    !data.SpecialEffectAttributesById.TryGetValue((int)item.ItemId, out var row)) continue;
                var type = NativeMagicUnsignedRow(row.Fields, 1);
                var value = NativeMagicUnsignedRow(row.Fields, 2);
                switch (type)
                {
                    case 4: attack += value; break;
                    case 8: increase += value; break;
                    case 11: critical += value; break;
                }
            }
        }
        return new(attack, 0, critical, increase);
    }

    internal static LegacyNativeMagicQueryFields NativeMagicRuneStorageRows(
        PlayerItemSnapshot items, WorldDataCatalog? data)
    {
        if (data is null) return default;
        uint score = 0, attack = 0, critical = 0;
        unchecked
        {
            foreach (var item in items.RuneItems)
                if (item.Position is not (0 or 211))
                    score += 500U + (item.ItemId / 10_000 != 401 ? 300U * (item.ItemId % 100) : 0U);
            foreach (var row in data.RuneStorageAttributes)
            {
                if (NativeMagicUnsignedRow(row.Fields, 1) > score) continue;
                // Source RuneTable.AttributeValue is exactly three byte-type/uint-value pairs.
                for (var at = 2; at <= 6; at += 2)
                {
                    var type = NativeMagicUnsignedRow(row.Fields, at);
                    if (type > byte.MaxValue) throw new FormatException("Native RuneAttribute is a byte.");
                    var value = NativeMagicUnsignedRow(row.Fields, at + 1);
                    switch (type)
                    {
                        case 3: attack += value; break;
                        case 11: critical += value; break;
                    }
                }
            }
        }
        return new(attack, 0, critical, 0);
    }

    private static uint NativeMagicUnsignedRow(IReadOnlyList<string> fields, int at) =>
        at < fields.Count && uint.TryParse(fields[at], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var value) ? value :
            throw new FormatException($"Native unsigned equipment contribution field {at} is invalid.");
}
