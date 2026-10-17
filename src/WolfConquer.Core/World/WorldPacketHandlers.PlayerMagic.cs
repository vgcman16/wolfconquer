using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ulong ReadLegacyRuntimeStamina(CharacterSnapshot character)
    {
        if (TryReadUInt64(character.Attributes, out var stamina, "Stamina"))
        {
            return stamina;
        }

        if (TryReadUInt64(character.Attributes, out var heavenBlessing, "HeavenBlessing", "HeavensBlessing") &&
            heavenBlessing > 0)
        {
            return 150;
        }

        return 100;
    }

    private static bool TryResolveLegacyPlayerMagicRuntimeSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        ushort spellId,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        if (LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, spellId, out playerSpell, out spell))
        {
            return true;
        }

        return TryResolveLegacyCombatGearCatalogSpell(session, worldData, spellId, out playerSpell, out spell);
    }

    private static bool TryReadDaoqiField(DaoqiDictType blackJade, int fieldIndex, out int value)
    {
        value = 0;
        return fieldIndex >= 0 &&
            fieldIndex < blackJade.Fields.Count &&
            int.TryParse(blackJade.Fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsLegacyFireTaoistClass(long classCode)
    {
        return classCode is >= 14002 and <= 14057;
    }

    private static bool IsLegacyWaterTaoistClass(long classCode)
    {
        return classCode is >= 13002 and <= 13057;
    }
}
