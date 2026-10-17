using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyMaxHitpointsCalculator
{
    private const int LegacyCombatGearSplashSpellId = 21_800;
    private const int LegacyMagicLevelFieldIndex = 8;
    private const int LegacyMagicGDamageFieldIndex = 10;
    private const int LegacySplashFlag = 0x196;

    public static uint CalculateCombatGearStatusHitpoints(
        IReadOnlyDictionary<string, string> characterAttributes,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId)
    {
        ArgumentNullException.ThrowIfNull(characterAttributes);

        if (!HasLegacyStatusFlag(characterAttributes, LegacySplashFlag) ||
            !TryGetLegacyMagicRow(
                magicByTypeId,
                LegacyCombatGearSplashSpellId,
                spellLevel: 0,
                out var splash))
        {
            return 0;
        }

        return ReadUIntMagicField(splash, LegacyMagicGDamageFieldIndex);
    }

    private static bool TryGetLegacyMagicRow(
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicByTypeId,
        int spellId,
        int spellLevel,
        out MagicType magic)
    {
        magic = null!;
        if (magicByTypeId is null ||
            !magicByTypeId.TryGetValue(spellId, out var rows))
        {
            return false;
        }

        MagicType? fallback = null;
        foreach (var row in rows)
        {
            if (TryReadIntField(row.Fields, LegacyMagicLevelFieldIndex, out var rowLevel) &&
                rowLevel == spellLevel)
            {
                magic = row;
                return true;
            }

            fallback ??= row;
        }

        if (fallback is null)
        {
            return false;
        }

        magic = fallback;
        return true;
    }

    private static uint ReadUIntMagicField(MagicType magic, int fieldIndex)
    {
        return TryReadUIntField(magic.Fields, fieldIndex, out var value) ? value : 0;
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        if (flag < 0)
        {
            return false;
        }

        if (!attributes.TryGetValue("StatusFlags", out var raw) &&
            !attributes.TryGetValue("BitVector", out raw))
        {
            return false;
        }

        var parts = (raw ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordIndex = flag / 32;
        if (wordIndex >= parts.Length ||
            !ulong.TryParse(parts[wordIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word))
        {
            return false;
        }

        return (word & (1UL << (flag % 32))) != 0;
    }
}
