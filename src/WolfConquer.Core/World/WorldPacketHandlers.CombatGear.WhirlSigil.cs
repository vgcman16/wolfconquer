using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyNinjaWhirlSigilSageLevelAttribute = "LegacyNinjaWhirlSigilSageLevel";

    private const ushort LegacyWhirlSigilSagePhysicalSpellId = 16_420;

    private const ushort LegacyWhirlSigilSageAlternatePhysicalSpellId = 16_430;

    private const int LegacyNinjaWhirlSigilSageType = 404;


    private static uint GetLegacyWhirlSigilSageDamagePercent(
        WorldSession session,
        WorldDataCatalog? worldData,
        ushort spellId)
    {
        if (spellId is not (LegacyWhirlSigilSagePhysicalSpellId or LegacyWhirlSigilSageAlternatePhysicalSpellId))
        {
            return 0;
        }

        if (!TryGetLegacyNinjaWhirlSigilSageLevel(
                session.Character,
                session.AlternateEquipmentActive,
                out var level))
        {
            return 0;
        }

        return TryGetLegacyGouyuTypeDamagePercent(
            worldData,
            LegacyNinjaWhirlSigilSageType,
            level,
            out var damagePercent)
            ? damagePercent
            : 0;
    }


    private static bool TryGetLegacyNinjaWhirlSigilSageLevel(
        CharacterSnapshot character,
        bool alternateEquipmentActive,
        out uint level)
    {
        level = 0;
        if (!IsLegacyNinjaClass(character.Class))
        {
            return false;
        }

        if (TryReadUInt32Attribute(
                character.Attributes,
                out level,
                RuntimeLegacyNinjaWhirlSigilSageLevelAttribute,
                "NinjaWhirlSigilSageLevel",
                "WhirlSigilSageLevel") &&
            level != 0)
        {
            return true;
        }

        return character.Attributes.TryGetValue("MsgNinja", out var raw) &&
            TryReadLegacyMsgNinjaEquippedItemLevel(
                raw,
                LegacyNinjaWhirlSigilSageType,
                alternateEquipmentActive,
                out level);
    }
}
