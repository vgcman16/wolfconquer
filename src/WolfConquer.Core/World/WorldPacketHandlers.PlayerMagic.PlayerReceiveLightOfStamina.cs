using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyLightOfStaminaRollAttribute = "LegacyLightOfStaminaRoll";

    private readonly record struct LegacyPlayerReceiveLightOfStaminaResult(
        bool Applied,
        bool StaminaRestored)
    {
        public bool IsActive => Applied;
    }

    private static LegacyPlayerReceiveLightOfStaminaResult TryApplyLegacyPlayerReceiveLightOfStamina(
        CharacterSnapshot attacker,
        CharacterSnapshot target,
        WorldSession? attackerSession,
        out CharacterSnapshot updatedAttacker)
    {
        updatedAttacker = attacker;
        var ratePercent = ReadLegacyPlayerReceiveLightOfStaminaPercent(attacker);
        if (ratePercent <= 0 ||
            ReadLegacyPlayerReceivePrestigeLevel(target) < ReadLegacyPlayerReceivePrestigeLevel(attacker) ||
            !CheckLegacyPlayerReceiveLightOfStaminaRateRoll(attacker, ratePercent))
        {
            return default;
        }

        var stamina = 0UL;
        if (attackerSession is not null && !TryReadLegacyRuntimeStamina(attacker, out stamina))
        {
            return default;
        }

        var staminaRestored = false;
        if (attackerSession is not null && stamina < 100)
        {
            updatedAttacker = WithCharacterAttribute(updatedAttacker, "Stamina", 100UL);
            staminaRestored = true;
        }

        return new LegacyPlayerReceiveLightOfStaminaResult(Applied: true, staminaRestored);
    }

    private static double ReadLegacyPlayerReceiveLightOfStaminaPercent(CharacterSnapshot attacker)
    {
        return TryReadDoubleAttribute(
                attacker.Attributes,
                out var ratePercent,
                "PerfectionStatus.LightOfStamina",
                "LegacyPerfectionStatus.LightOfStamina",
                "LightOfStamina")
            ? ratePercent
            : 0.0;
    }

    private static uint ReadLegacyPlayerReceivePrestigeLevel(CharacterSnapshot character)
    {
        return ReadAttributeUInt32(
            character.Attributes,
            "PrestigeLevel",
            "Status.PrestigeLevel",
            "LegacyPrestigeLevel");
    }

    private static bool CheckLegacyPlayerReceiveLightOfStaminaRateRoll(
        CharacterSnapshot attacker,
        double chance)
    {
        if (chance <= 0)
        {
            return false;
        }

        var roll = TryReadDoubleAttribute(
                attacker.Attributes,
                out var forcedRoll,
                RuntimeLegacyLightOfStaminaRollAttribute)
            ? forcedRoll
            : Random.Shared.NextDouble() * 100.0;
        return roll < chance;
    }
}
