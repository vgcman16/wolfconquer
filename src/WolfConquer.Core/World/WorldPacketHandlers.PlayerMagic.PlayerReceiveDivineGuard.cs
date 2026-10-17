using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyDivineGuardFlag = 0xB1;
    private const int LegacyDivineGuardStatusSeconds = 5;
    private const uint LegacyDivineGuardAzureShieldTime = 15;
    private const double LegacyDivineGuardStatusRatePercent = 6.0;
    private const string RuntimeLegacyDivineGuardRollAttribute = "LegacyDivineGuardRoll";
    private const string RuntimeLegacyDivineGuardStatusRollAttribute = "LegacyDivineGuardStatusRoll";
    internal const string RuntimeLegacyDivineGuardExpiresAtUtcAttribute = "LegacyDivineGuardExpiresAtUtc";

    private readonly record struct LegacyPlayerReceiveDivineGuardResult(
        bool Applied,
        bool StatusOpened)
    {
        public bool IsActive => Applied;
    }

    private static LegacyPlayerReceiveDivineGuardResult TryApplyLegacyPlayerReceiveDivineGuard(
        CharacterSnapshot attacker,
        CharacterSnapshot target,
        WorldSession? attackerSession,
        out CharacterSnapshot updatedAttacker)
    {
        updatedAttacker = attacker;
        var ratePercent = ReadLegacyPlayerReceiveDivineGuardPercent(attacker);
        if (ratePercent <= 0 ||
            ReadLegacyPlayerReceivePrestigeLevel(target) < ReadLegacyPlayerReceivePrestigeLevel(attacker) ||
            !CheckLegacyPlayerReceiveDivineGuardRateRoll(
                attacker.Attributes,
                RuntimeLegacyDivineGuardRollAttribute,
                ratePercent))
        {
            return default;
        }

        var statusOpened = false;
        if (attackerSession is not null &&
            !HasLegacyStatusFlag(updatedAttacker, LegacyDivineGuardFlag) &&
            CheckLegacyPlayerReceiveDivineGuardRateRoll(
                attacker.Attributes,
                RuntimeLegacyDivineGuardStatusRollAttribute,
                LegacyDivineGuardStatusRatePercent))
        {
            var expiresAt = DateTimeOffset.UtcNow.ToUniversalTime()
                .AddSeconds(LegacyDivineGuardStatusSeconds)
                .ToString("O", CultureInfo.InvariantCulture);
            updatedAttacker = WithCharacterAttribute(
                WithLegacyStatusFlag(updatedAttacker, LegacyDivineGuardFlag),
                RuntimeLegacyDivineGuardExpiresAtUtcAttribute,
                expiresAt);
            statusOpened = true;
        }

        return new LegacyPlayerReceiveDivineGuardResult(Applied: true, statusOpened);
    }

    private static double ReadLegacyPlayerReceiveDivineGuardPercent(CharacterSnapshot attacker)
    {
        return TryReadDoubleAttribute(
                attacker.Attributes,
                out var ratePercent,
                "PerfectionStatus.DivineGuard",
                "LegacyPerfectionStatus.DivineGuard",
                "DivineGuard")
            ? ratePercent
            : 0.0;
    }

    private static bool CheckLegacyPlayerReceiveDivineGuardRateRoll(
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

    private static async ValueTask SendLegacyDivineGuardAzureShieldUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AzureShield,
                    Value: 0,
                    Flag: LegacyDivineGuardFlag,
                    Time: LegacyDivineGuardAzureShieldTime,
                    Damage: 0,
                    SpellLevel: 0),
            ]);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
    }
}
