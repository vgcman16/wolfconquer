using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyKillingFlashXpListSeconds = 20;
    private const int LegacyKillingFlashXpListRatePercent = 10;
    private const string RuntimeLegacyKillingFlashRollAttribute = "LegacyKillingFlashRoll";
    private const string RuntimeLegacyKillingFlashXpListRollAttribute = "LegacyKillingFlashXPListRoll";
    internal const string RuntimeLegacyKillingFlashXpListExpiresAtUtcAttribute = "RuntimeLegacyKillingFlashXpListExpiresAtUtc";

    private readonly record struct LegacyPlayerReceiveKillingFlashResult(
        bool Applied,
        bool XpListOpened)
    {
        public bool IsActive => Applied;
    }

    private static LegacyPlayerReceiveKillingFlashResult TryApplyLegacyPlayerReceiveKillingFlash(
        CharacterSnapshot attacker,
        WorldSession? attackerSession,
        out CharacterSnapshot updatedAttacker)
    {
        updatedAttacker = attacker;
        var ratePercent = ReadLegacyPlayerReceiveKillingFlashPercent(attacker);
        if (ratePercent <= 0 ||
            !CheckLegacyPlayerReceiveKillingFlashRateRoll(
                attacker.Attributes,
                RuntimeLegacyKillingFlashRollAttribute,
                ratePercent))
        {
            return default;
        }

        var xpListOpened = false;
        if (attackerSession is not null &&
            !IsLegacyPlayerOnXpSkill(updatedAttacker) &&
            !HasLegacyStatusFlag(updatedAttacker, LegacyRuntimeUpdateTicker.XpListFlag) &&
            CheckLegacyPlayerReceiveKillingFlashRateRoll(
                attacker.Attributes,
                RuntimeLegacyKillingFlashXpListRollAttribute,
                LegacyKillingFlashXpListRatePercent))
        {
            var expiresAt = DateTimeOffset.UtcNow.ToUniversalTime()
                .AddSeconds(LegacyKillingFlashXpListSeconds)
                .ToString("O", CultureInfo.InvariantCulture);
            updatedAttacker = WithCharacterAttribute(
                WithLegacyStatusFlag(updatedAttacker, LegacyRuntimeUpdateTicker.XpListFlag),
                RuntimeLegacyKillingFlashXpListExpiresAtUtcAttribute,
                expiresAt);
            xpListOpened = true;
        }

        return new LegacyPlayerReceiveKillingFlashResult(Applied: true, xpListOpened);
    }

    private static double ReadLegacyPlayerReceiveKillingFlashPercent(CharacterSnapshot attacker)
    {
        return TryReadDoubleAttribute(
                attacker.Attributes,
                out var ratePercent,
                "PerfectionStatus.KillingFlash",
                "LegacyPerfectionStatus.KillingFlash",
                "KillingFlash")
            ? ratePercent
            : 0.0;
    }

    private static bool CheckLegacyPlayerReceiveKillingFlashRateRoll(
        IReadOnlyDictionary<string, string> attributes,
        string forcedRollAttribute,
        double chance)
    {
        if (chance <= 0)
        {
            return false;
        }

        var roll = TryReadDoubleAttribute(attributes, out var forcedRoll, forcedRollAttribute)
            ? forcedRoll
            : Random.Shared.NextDouble() * 100.0;
        return roll < chance;
    }
}
