using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyMirrorOfSinXpListSeconds = 20;
    private const int LegacyMirrorOfSinXpListRatePercent = 10;
    private const string RuntimeLegacyMirrorOfSinRollAttribute = "LegacyMirrorOfSinRoll";
    private const string RuntimeLegacyMirrorOfSinXpListRollAttribute = "LegacyMirrorOfSinXPListRoll";
    internal const string RuntimeLegacyMirrorOfSinXpListExpiresAtUtcAttribute = "LegacyMirrorOfSinXpListExpiresAtUtc";

    private readonly record struct LegacyPlayerReceiveMirrorOfSinResult(
        bool Applied,
        bool XpListOpened)
    {
        public bool IsActive => Applied;
    }

    private static LegacyPlayerReceiveMirrorOfSinResult TryApplyLegacyPlayerReceiveMirrorOfSin(
        CharacterSnapshot attacker,
        WorldSession? attackerSession,
        out CharacterSnapshot updatedAttacker)
    {
        updatedAttacker = attacker;
        var ratePercent = ReadLegacyPlayerReceiveMirrorOfSinPercent(attacker);
        if (ratePercent <= 0 ||
            !CheckLegacyPlayerReceiveMirrorOfSinRateRoll(
                attacker.Attributes,
                RuntimeLegacyMirrorOfSinRollAttribute,
                ratePercent))
        {
            return default;
        }

        var xpListOpened = false;
        if (attackerSession is not null &&
            !IsLegacyPlayerOnXpSkill(updatedAttacker) &&
            !HasLegacyStatusFlag(updatedAttacker, LegacyRuntimeUpdateTicker.XpListFlag) &&
            CheckLegacyPlayerReceiveMirrorOfSinRateRoll(
                attacker.Attributes,
                RuntimeLegacyMirrorOfSinXpListRollAttribute,
                LegacyMirrorOfSinXpListRatePercent))
        {
            var expiresAt = DateTimeOffset.UtcNow.ToUniversalTime()
                .AddSeconds(LegacyMirrorOfSinXpListSeconds)
                .ToString("O", CultureInfo.InvariantCulture);
            updatedAttacker = WithCharacterAttribute(
                WithLegacyStatusFlag(updatedAttacker, LegacyRuntimeUpdateTicker.XpListFlag),
                RuntimeLegacyMirrorOfSinXpListExpiresAtUtcAttribute,
                expiresAt);
            xpListOpened = true;
        }

        return new LegacyPlayerReceiveMirrorOfSinResult(Applied: true, xpListOpened);
    }

    private static double ReadLegacyPlayerReceiveMirrorOfSinPercent(CharacterSnapshot attacker)
    {
        return TryReadDoubleAttribute(
                attacker.Attributes,
                out var ratePercent,
                "PerfectionStatus.MirrorOfSin",
                "LegacyPerfectionStatus.MirrorOfSin",
                "MirrorOfSin")
            ? ratePercent
            : 0.0;
    }

    private static bool CheckLegacyPlayerReceiveMirrorOfSinRateRoll(
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
