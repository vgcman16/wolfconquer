using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySlowPaceRuneItemBase = 4_036_300;
    private const uint LegacySlowPaceRuneItemMax = 4_036_309;
    private const string RuntimeLegacySlowPaceRollAttribute = "LegacySlowPaceRoll";
    private const string RuntimeLegacyDodgeAttribute = "Dodge";

    private static void TryApplyLegacyPlayerReceiveSlowPace(
        CharacterSnapshot attacker,
        CharacterSnapshot target,
        WorldSession? attackerSession,
        WorldSession targetSession,
        WorldDataCatalog? worldData,
        out CharacterSnapshot updatedTarget)
    {
        updatedTarget = target;
        if (attackerSession is null ||
            !TryGetEquippedLegacySlowPaceLevel(attackerSession.PlayerItems, out var slowPaceLevel) ||
            !CheckLegacySlowPaceRateRoll(attacker, slowPaceLevel))
        {
            return;
        }

        var targetStatusSession = targetSession with { Character = target };
        var statusFields = CalculateLegacyRuntimeStatusFields(targetStatusSession, worldData);
        var currentDodge = statusFields[LegacyPlayerStatusPacket.DodgeIndex];
        if (currentDodge == 0)
        {
            return;
        }

        var reducedDodge = currentDodge - (currentDodge * 50 / 100);
        updatedTarget = WithCharacterAttribute(updatedTarget, RuntimeLegacyDodgeAttribute, reducedDodge);
    }

    private static bool TryGetEquippedLegacySlowPaceLevel(
        PlayerItemSnapshot playerItems,
        out byte runeLevel)
    {
        return TryGetEquippedLegacyRuneLevel(
            playerItems,
            LegacySlowPaceRuneItemBase,
            LegacySlowPaceRuneItemMax,
            out runeLevel);
    }

    private static bool CheckLegacySlowPaceRateRoll(CharacterSnapshot attacker, byte runeLevel)
    {
        var chance = runeLevel switch
        {
            1 => 5,
            2 => 10,
            3 => 15,
            4 => 20,
            5 => 25,
            6 => 30,
            7 => 35,
            8 => 40,
            9 => 50,
            _ => 0,
        };
        return CheckLegacyPercentRoll(attacker.Attributes, RuntimeLegacySlowPaceRollAttribute, chance);
    }

    private static void ApplyLegacyRuntimeStatusFieldOverrides(
        CharacterSnapshot character,
        uint[] fields)
    {
        if (TryReadUInt32Attribute(character.Attributes, out var dodge, RuntimeLegacyDodgeAttribute))
        {
            fields[LegacyPlayerStatusPacket.DodgeIndex] = dodge;
        }
    }
}
