using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyPlayerReceiveResponseRecoveryRoll10Attribute = "LegacyPlayerReceiveResponseRecoveryRoll10";
    private const string RuntimeLegacyPlayerReceiveResponseRecoveryRoll5Attribute = "LegacyPlayerReceiveResponseRecoveryRoll5";
    private const string RuntimeLegacyPlayerReceiveResponseAegisRoll10Attribute = "LegacyPlayerReceiveResponseAegisRoll10";
    private const string RuntimeLegacyPlayerReceiveResponseAegisRoll5Attribute = "LegacyPlayerReceiveResponseAegisRoll5";

    private readonly record struct LegacyPlayerReceiveResponseEffectsResult(
        bool RecoveryEffect,
        bool AegisEffect)
    {
        public bool IsActive => RecoveryEffect || AegisEffect;
    }

    private static LegacyPlayerReceiveResponseEffectsResult TryApplyLegacyPlayerReceiveResponseEffects(
        CharacterSnapshot target)
    {
        var recovery = HasLegacyPlayerReceiveSupportTortoiseGem(target) &&
            CheckLegacyPercentRoll(target.Attributes, RuntimeLegacyPlayerReceiveResponseRecoveryRoll10Attribute, 10) &&
            CheckLegacyPercentRoll(target.Attributes, RuntimeLegacyPlayerReceiveResponseRecoveryRoll5Attribute, 5);
        var aegis = HasLegacyPlayerReceiveBless(target) &&
            CheckLegacyPercentRoll(target.Attributes, RuntimeLegacyPlayerReceiveResponseAegisRoll10Attribute, 10) &&
            CheckLegacyPercentRoll(target.Attributes, RuntimeLegacyPlayerReceiveResponseAegisRoll5Attribute, 5);

        return new LegacyPlayerReceiveResponseEffectsResult(recovery, aegis);
    }

    private static bool HasLegacyPlayerReceiveSupportTortoiseGem(CharacterSnapshot target)
    {
        return ReadAttributeBool(
            target.Attributes,
            "LegacySuprtTortoiseGem",
            "SuprtTortoiseGem",
            "SupportTortoiseGem",
            "Equipment.SuprtTortoiseGem",
            "Equipment.SupportTortoiseGem");
    }

    private static bool HasLegacyPlayerReceiveBless(CharacterSnapshot target)
    {
        return ReadAttributeBool(
            target.Attributes,
            "LegacyHaveBless",
            "HaveBless",
            "Equipment.HaveBless");
    }

    private static async ValueTask SendLegacyPlayerReceiveResponseEffectsAsync(
        WorldPacketHandlerContext context,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        if (!result.ResponseEffectsResult.IsActive)
        {
            return;
        }

        if (result.ResponseEffectsResult.RecoveryEffect)
        {
            await SendLegacyPlayerReceiveResponseEffectAsync(
                    context,
                    result,
                    "recovery",
                    "SuprtTortoiseGem",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.ResponseEffectsResult.AegisEffect)
        {
            await SendLegacyPlayerReceiveResponseEffectAsync(
                    context,
                    result,
                    "Aegis4",
                    "HaveBless",
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendLegacyPlayerReceiveResponseEffectAsync(
        WorldPacketHandlerContext context,
        LegacyPlayerMagicDamageResult result,
        string effectName,
        string sourceGate,
        CancellationToken cancellationToken)
    {
        var targetSession = result.UpdatedSession;
        var targetUid = CheckedUInt32(targetSession.Character.Uid, nameof(targetSession.Character.Uid));
        var effect = LegacyStringPacket.PlayerEffect(targetUid, effectName).ToFrameBytes();
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                result.ActiveSession.Connection,
                targetSession,
                effect,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy player receive response effect " +
                $"{FormatSession(context with { Session = targetSession })} targetUid={targetUid} " +
                $"effect={effectName} gate={sourceGate}; " +
                "source=CheckGemEffects.CheckRespouseDamage.")
            .ConfigureAwait(false);
    }
}
