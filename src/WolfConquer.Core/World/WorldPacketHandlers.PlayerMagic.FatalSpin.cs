using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const byte LegacyFatalSpinSort = 73;
    private const ushort LegacyFatalSpinSpellId = 12_110;
    private const ushort LegacyDragonPunchSpellId = 12_240;
    private const ushort LegacyShadowFistSpellId = 15_410;
    private const byte LegacyFatalSpinLineWidth = 2;
    private const byte LegacyDragonPunchLineWidth = 0;
    private const byte LegacyShadowFistPlayerRange = 4;
    private const string LegacyShadowFistRuneName = "shadowfist";

    private readonly record struct LegacyFatalSpinPassProfile(
        byte LineWidth,
        long PlayerDistanceLimit,
        bool PlayerDistanceInclusive,
        uint PlayerDamageNumerator,
        uint PlayerDamageDenominator,
        uint SobNpcDamageNumerator,
        uint SobNpcDamageDenominator,
        bool IncludeSobNpcTargets,
        bool ApplyPlayerCanAttackSideEffects,
        bool TrackFollowUpAnchor,
        bool SendAnimationWhenEmpty);

    private readonly record struct LegacyFatalSpinFollowUpAnchor(ushort X, ushort Y);

    private sealed record LegacyFatalSpinPassResult(
        WorldSession Session,
        IReadOnlyList<LegacySpellAnimationTarget> AnimationTargets,
        uint ProgressDamage,
        int MonsterTargetCount,
        int PlayerTargetCount,
        int SobNpcTargetCount,
        ulong TotalAppliedDamage,
        int CounterHitShoots,
        LegacyFatalSpinFollowUpAnchor? FollowUpAnchor);

    private sealed record LegacyFatalSpinMonsterPassResult(
        WorldSession Session,
        IReadOnlyList<LegacySpellAnimationTarget> AnimationTargets,
        uint ProgressDamage,
        ulong TotalAppliedDamage);

    private sealed record LegacyFatalSpinPlayerPassResult(
        WorldSession Session,
        IReadOnlyList<LegacySpellAnimationTarget> AnimationTargets,
        ulong TotalAppliedDamage,
        int CounterHitShoots);

    private sealed record LegacyFatalSpinSobNpcPassResult(
        WorldSession Session,
        IReadOnlyList<LegacySpellAnimationTarget> AnimationTargets,
        uint ProgressDamage,
        ulong TotalAppliedDamage);

    private static async ValueTask<bool> HandleLegacyPlayerFatalSpinMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if ((byte)spell.Sort != LegacyFatalSpinSort)
        {
            return false;
        }

        // FatalSpin.Execute calls CanUseSpell.Verified before switching on 12110/12240.
        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        switch (spell.Id)
        {
            case LegacyFatalSpinSpellId:
                await HandleLegacyFatalSpin12110Async(
                        context,
                        resourceSession,
                        query,
                        playerSpell,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case LegacyDragonPunchSpellId:
                await HandleLegacyFatalSpin12240Async(
                        context,
                        resourceSession,
                        query,
                        playerSpell,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                await context.Log.WriteLineAsync(
                        "WorldServer source-backed returned legacy FatalSpin magic after resource prep " +
                        $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                        $"sort={(int)spell.Sort}; source=FatalSpin.cs CanUseSpell before unsupported ID switch.")
                    .ConfigureAwait(false);
                return true;
        }
    }

    private static async ValueTask HandleLegacyFatalSpin12110Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var profile = new LegacyFatalSpinPassProfile(
            LegacyFatalSpinLineWidth,
            PlayerDistanceLimit: 1,
            PlayerDistanceInclusive: true,
            PlayerDamageNumerator: 3,
            PlayerDamageDenominator: 9,
            SobNpcDamageNumerator: 30,
            SobNpcDamageDenominator: 100,
            IncludeSobNpcTargets: true,
            ApplyPlayerCanAttackSideEffects: true,
            TrackFollowUpAnchor: false,
            SendAnimationWhenEmpty: false);
        var result = await ExecuteLegacyFatalSpinPrimaryCastAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                profile,
                cancellationToken)
            .ConfigureAwait(false);
        if (result is null)
        {
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy FatalSpin magic " +
                $"{FormatSession(context with { Session = result.Session })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={spell.Range} width={profile.LineWidth} " +
                $"playerRange={profile.PlayerDistanceLimit} playerRangeInclusive={profile.PlayerDistanceInclusive} " +
                $"targets={result.AnimationTargets.Count} monsterTargets={result.MonsterTargetCount} " +
                $"playerTargets={result.PlayerTargetCount} sobNpcTargets={result.SobNpcTargetCount} " +
                $"sourceProgress={result.ProgressDamage} totalAppliedDamage={result.TotalAppliedDamage} " +
                $"counterHitShoots={result.CounterHitShoots} animationSent={result.AnimationTargets.Count > 0}; " +
                "source=FatalSpin.cs case 12110.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyFatalSpin12240Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var profile = new LegacyFatalSpinPassProfile(
            LegacyDragonPunchLineWidth,
            PlayerDistanceLimit: spell.Range,
            PlayerDistanceInclusive: false,
            PlayerDamageNumerator: 12,
            PlayerDamageDenominator: 40,
            SobNpcDamageNumerator: 1,
            SobNpcDamageDenominator: 1,
            IncludeSobNpcTargets: true,
            ApplyPlayerCanAttackSideEffects: true,
            TrackFollowUpAnchor: true,
            SendAnimationWhenEmpty: true);
        var initial = await ExecuteLegacyFatalSpinPrimaryCastAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                profile,
                cancellationToken)
            .ConfigureAwait(false);
        if (initial is null)
        {
            return;
        }

        var followUpOutcome = await TryExecuteLegacyShadowFistFollowUpAsync(
                context with { Session = initial.Session },
                initial,
                query,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (followUpOutcome is not { } followUp)
        {
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy FatalSpin DragonPunch magic " +
                $"{FormatSession(context with { Session = followUp.Session })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={spell.Range} width={profile.LineWidth} " +
                $"targets={initial.AnimationTargets.Count} monsterTargets={initial.MonsterTargetCount} " +
                $"playerTargets={initial.PlayerTargetCount} sobNpcTargets={initial.SobNpcTargetCount} " +
                $"sourceProgress={initial.ProgressDamage} totalAppliedDamage={initial.TotalAppliedDamage} " +
                $"shadowFistTriggered={followUp.Triggered} shadowFistTargets={followUp.TargetCount} " +
                $"shadowFistProgress={followUp.FollowUpProgress} sourceCumulativeProgress={followUp.CumulativeProgress}; " +
                "source=FatalSpin.cs case 12240 and conditional ShadowFist 15410 tail.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<LegacyFatalSpinPassResult?> ExecuteLegacyFatalSpinPrimaryCastAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        LegacyFatalSpinPassProfile profile,
        CancellationToken cancellationToken)
    {
        await SendLegacyFatalSpinInteractionEchoAsync(context, session, query, cancellationToken)
            .ConfigureAwait(false);

        var pass = await ExecuteLegacyFatalSpinDamagePassAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                lineRange: spell.Range,
                profile,
                cancellationToken)
            .ConfigureAwait(false);
        if (pass is null)
        {
            return null;
        }
        var updatedSession = await ApplyLegacyFatalSpinIncreaseExperienceAsync(
                context with { Session = pass.Session },
                pass.Session,
                pass.ProgressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                pass.ProgressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(pass.ProgressDamage),
                cancellationToken,
                activity: $"FatalSpin {spell.Id} magic weapon proficiency experience")
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        if (profile.SendAnimationWhenEmpty || pass.AnimationTargets.Count > 0)
        {
            await SendLegacyFatalSpinAnimationAsync(
                    context,
                    updatedSession,
                    query.X,
                    query.Y,
                    playerSpell,
                    spell,
                    pass.AnimationTargets,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return pass with { Session = updatedSession };
    }

}
