using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyMudWallSpellId = 16_440;
    private const int LegacyMudWallFlag = 274;
    private const int LegacyNinjaMudSigilImpeccableType = 301;
    private const int LegacyMudSigilImpeccablePowerValueIndex = 5;
    private const string RuntimeLegacyMudSigilImpeccableLevelAttribute = "LegacyMudSigilImpeccableLevel";
    private const string RuntimeLegacyMudSigilImpeccablePowerAttribute = "LegacyMudSigilImpeccablePower";

    private static uint ApplyLegacyPlayerReceiveMudWall(
        WorldSession targetSession,
        WorldDataCatalog? worldData,
        uint damage)
    {
        if (!HasLegacyStatusFlag(targetSession.Character, LegacyMudWallFlag) ||
            !TryGetLegacyMudSigilImpeccablePower(targetSession, worldData, out var mudSigilPower))
        {
            return damage;
        }

        return SubtractLegacyMudWallReduction(damage, mudSigilPower / 100);
    }

    private static bool TryGetLegacyMudSigilImpeccablePower(
        WorldSession session,
        WorldDataCatalog? worldData,
        out uint power)
    {
        power = 0;
        if (!IsLegacyNinjaClass(session.Character.Class))
        {
            return false;
        }

        if (session.Character.Attributes.TryGetValue("MsgNinja", out var raw) &&
            TryReadLegacyMsgNinjaEquippedItemLevel(
                raw,
                LegacyNinjaMudSigilImpeccableType,
                session.AlternateEquipmentActive,
                out var liveLevel) &&
            TryGetLegacyMudSigilImpeccableGouyuPower(worldData, liveLevel, out power))
        {
            return true;
        }

        if (TryReadUInt32Attribute(
                session.Character.Attributes,
                out var fallbackLevel,
                RuntimeLegacyMudSigilImpeccableLevelAttribute,
                "MudSigilImpeccableLevel",
                "NinjaMudSigilImpeccableLevel") &&
            fallbackLevel != 0 &&
            TryGetLegacyMudSigilImpeccableGouyuPower(worldData, fallbackLevel, out power))
        {
            return true;
        }

        return TryReadUInt32Attribute(
                session.Character.Attributes,
                out power,
                RuntimeLegacyMudSigilImpeccablePowerAttribute,
                "MudSigilImpeccablePower",
                "NinjaMudSigilImpeccablePower") &&
            power != 0;
    }

    private static bool TryGetLegacyMudSigilImpeccableGouyuPower(
        WorldDataCatalog? worldData,
        uint level,
        out uint power)
    {
        power = 0;
        if (level > int.MaxValue ||
            worldData is null ||
            !worldData.GouyuTypesByType.TryGetValue(LegacyNinjaMudSigilImpeccableType, out var rows))
        {
            return false;
        }

        foreach (var row in rows)
        {
            if (row.Level != (int)level ||
                row.Values.Count <= LegacyMudSigilImpeccablePowerValueIndex ||
                row.Values[LegacyMudSigilImpeccablePowerValueIndex] <= 0)
            {
                continue;
            }

            power = checked((uint)row.Values[LegacyMudSigilImpeccablePowerValueIndex]);
            return true;
        }

        return false;
    }

    private static uint SubtractLegacyMudWallReduction(uint damage, uint percent)
    {
        var reduction = MulDivToUInt32(damage, percent, 100);
        return reduction >= damage ? 0 : damage - reduction;
    }
}
