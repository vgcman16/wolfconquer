using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyCalmWindRollAttribute = "LegacyCalmWindRoll";

    private readonly record struct LegacyPlayerReceiveCalmWindResult(bool Applied)
    {
        public bool IsActive => Applied;
    }

    private static LegacyPlayerReceiveCalmWindResult TryApplyLegacyPlayerReceiveCalmWind(
        CharacterSnapshot attacker)
    {
        var ratePercent = ReadLegacyPlayerReceiveCalmWindPercent(attacker);
        if (ratePercent <= 0 ||
            !CheckLegacyPlayerReceiveCalmWindRateRoll(attacker, ratePercent))
        {
            return default;
        }

        return new LegacyPlayerReceiveCalmWindResult(Applied: true);
    }

    private static double ReadLegacyPlayerReceiveCalmWindPercent(CharacterSnapshot attacker)
    {
        return TryReadDoubleAttribute(
                attacker.Attributes,
                out var ratePercent,
                "PerfectionStatus.CalmWind",
                "LegacyPerfectionStatus.CalmWind",
                "CalmWind")
            ? ratePercent
            : 0.0;
    }

    private static bool CheckLegacyPlayerReceiveCalmWindRateRoll(
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
                RuntimeLegacyCalmWindRollAttribute)
            ? forcedRoll
            : Random.Shared.NextDouble() * 100.0;
        return roll < chance;
    }
}
