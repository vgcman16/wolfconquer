using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyShadowFistFollowUpResult?> TryExecuteLegacyShadowFistFollowUpAsync(
        WorldPacketHandlerContext context,
        LegacyFatalSpinPassResult initial,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell initialSpell,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !HasLegacyEquippedRune(initial.Session.PlayerItems, worldData, LegacyShadowFistRuneName) ||
            !TryResolveLegacyPlayerMagicRuntimeSpell(
                initial.Session,
                worldData,
                LegacyShadowFistSpellId,
                out var followUpPlayerSpell,
                out var followUpSpell) ||
            initial.FollowUpAnchor is not { } anchor)
        {
            return new LegacyShadowFistFollowUpResult(
                initial.Session,
                Triggered: false,
                TargetCount: 0,
                FollowUpProgress: 0,
                CumulativeProgress: initial.ProgressDamage);
        }

        var profile = new LegacyFatalSpinPassProfile(
            LegacyDragonPunchLineWidth,
            PlayerDistanceLimit: LegacyShadowFistPlayerRange,
            PlayerDistanceInclusive: false,
            PlayerDamageNumerator: 1,
            PlayerDamageDenominator: 2,
            SobNpcDamageNumerator: 1,
            SobNpcDamageDenominator: 1,
            IncludeSobNpcTargets: false,
            ApplyPlayerCanAttackSideEffects: false,
            TrackFollowUpAnchor: false,
            SendAnimationWhenEmpty: true);

        // The source omits CanAttackPlayer in this tail. Enumeration remains fenced through
        // WolfConquer's existing visible, attackable player target infrastructure.
        var followUp = await ExecuteLegacyFatalSpinDamagePassAsync(
                context,
                initial.Session,
                query,
                followUpPlayerSpell,
                followUpSpell,
                lineRange: initialSpell.Range,
                profile,
                cancellationToken)
            .ConfigureAwait(false);
        if (followUp is null)
        {
            return null;
        }
        var cumulativeProgress = unchecked(initial.ProgressDamage + followUp.ProgressDamage);
        var updatedSession = await ApplyLegacyFatalSpinIncreaseExperienceAsync(
                context with { Session = followUp.Session },
                followUp.Session,
                cumulativeProgress,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        await SendLegacyFatalSpinAnimationAsync(
                context,
                updatedSession,
                anchor.X,
                anchor.Y,
                followUpPlayerSpell,
                followUpSpell,
                followUp.AnimationTargets,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy ShadowFist follow-up " +
                $"{FormatSession(context)} spellId={followUpSpell.Id} anchorX={anchor.X} anchorY={anchor.Y} " +
                $"lineRange={initialSpell.Range} width={profile.LineWidth} targets={followUp.AnimationTargets.Count} " +
                $"monsterTargets={followUp.MonsterTargetCount} playerTargets={followUp.PlayerTargetCount} " +
                $"followUpProgress={followUp.ProgressDamage} cumulativeProgress={cumulativeProgress} " +
                "playerSafetyFence=CanLegacyPlayerMagicDamageTarget; source=FatalSpin.cs ShadowFist 15410 tail.")
            .ConfigureAwait(false);
        return new LegacyShadowFistFollowUpResult(
            updatedSession,
            Triggered: true,
            TargetCount: followUp.AnimationTargets.Count,
            FollowUpProgress: followUp.ProgressDamage,
            CumulativeProgress: cumulativeProgress);
    }

    private readonly record struct LegacyShadowFistFollowUpResult(
        WorldSession Session,
        bool Triggered,
        int TargetCount,
        uint FollowUpProgress,
        uint CumulativeProgress);
}
