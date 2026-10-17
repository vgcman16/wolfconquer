using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyNinjaDustSigilStunnLevelAttribute = "LegacyNinjaDustSigilStunnLevel";

    private const string RuntimeLegacyNinjaFlameSigilRapidLevelAttribute = "LegacyNinjaFlameSigilRapidLevel";

    private const int LegacyNinjaFlameSigilRapidType = 104;

    private const int LegacyNinjaDustSigilStunnType = 305;

    private const int LegacyNinjaFlameSigilRapidPowerValueIndex = 5;

    private static uint AddLegacyFlameSigilRapidMonsterDamage(
        WorldSession session,
        WorldDataCatalog worldData,
        uint damage)
    {
        return TryGetLegacyNinjaFlameSigilRapidPower(session, worldData, out var power)
            ? AddLegacySaturating(damage, power)
            : damage;
    }

    private static bool TryGetLegacyNinjaFlameSigilRapidPower(
        WorldSession session,
        WorldDataCatalog worldData,
        out uint power)
    {
        power = 0;
        return TryGetLegacyNinjaFlameSigilRapidLevel(
                session.Character,
                session.AlternateEquipmentActive,
                out var level) &&
            TryGetLegacyNinjaFlameSigilRapidGouyuPower(worldData, level, out power);
    }

    private static bool TryGetLegacyNinjaFlameSigilRapidGouyuPower(
        WorldDataCatalog worldData,
        uint level,
        out uint power)
    {
        power = 0;
        if (level > int.MaxValue ||
            !worldData.GouyuTypesByType.TryGetValue(LegacyNinjaFlameSigilRapidType, out var rows))
        {
            return false;
        }

        foreach (var row in rows)
        {
            if (row.Level != (int)level ||
                row.Values.Count <= LegacyNinjaFlameSigilRapidPowerValueIndex ||
                row.Values[LegacyNinjaFlameSigilRapidPowerValueIndex] <= 0)
            {
                continue;
            }

            power = checked((uint)row.Values[LegacyNinjaFlameSigilRapidPowerValueIndex]);
            return true;
        }

        return false;
    }

    private static bool TryGetLegacyNinjaFlameSigilRapidLevel(
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
                RuntimeLegacyNinjaFlameSigilRapidLevelAttribute,
                "NinjaFlameSigilRapidLevel",
                "FlameSigilRapidLevel") &&
            level != 0)
        {
            return true;
        }

        return character.Attributes.TryGetValue("MsgNinja", out var raw) &&
            TryReadLegacyMsgNinjaEquippedItemLevel(
                raw,
                LegacyNinjaFlameSigilRapidType,
                alternateEquipmentActive,
                out level);
    }

    private static bool HasLegacyNinjaDustSigilStunn(WorldSession session)
    {
        return TryGetLegacyNinjaDustSigilStunnLevel(
            session.Character,
            session.AlternateEquipmentActive,
            out _);
    }

    private static bool TryGetLegacyNinjaDustSigilStunnLevel(
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
                RuntimeLegacyNinjaDustSigilStunnLevelAttribute,
                "NinjaDustSigilStunnLevel",
                "DustSigilStunnLevel",
                "LegacyNinjaDustSigilStunnEquipped",
                "NinjaDustSigilStunnEquipped",
                "DustSigilStunnEquipped") &&
            level != 0)
        {
            return true;
        }

        return character.Attributes.TryGetValue("MsgNinja", out var raw) &&
            TryReadLegacyMsgNinjaEquippedItemLevel(
                raw,
                LegacyNinjaDustSigilStunnType,
                alternateEquipmentActive,
                out level);
    }
}
