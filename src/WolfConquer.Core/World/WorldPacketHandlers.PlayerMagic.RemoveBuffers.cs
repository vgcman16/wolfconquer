using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacySerenitySpellId = 10_400;
    private const ushort LegacyTranquilitySpellId = 10_425;
    private const ushort LegacyCompassionSpellId = 10_430;
    private const uint LegacyRemoveBuffersSpellProgressDamage = 1_000;

    private static async ValueTask<bool> HandleLegacyPlayerRemoveBuffersMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!LegacyMagicSortCapabilities.IsSupportedRemoveBuffersRuntimeSpell(spell.Sort, spell.Id))
        {
            return false;
        }

        // RemoveBuffers.Execute calls CanUseSpell.Verified before its spell-ID switch.
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

        context = context with { Session = resourceSession };
        switch (spell.Id)
        {
            case LegacySerenitySpellId:
                await HandleLegacySerenityRemoveBuffersAsync(
                        context,
                        resourceSession,
                        query,
                        playerSpell,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case LegacyTranquilitySpellId:
                await HandleLegacyTranquilityRemoveBuffersAsync(
                        context,
                        resourceSession,
                        query,
                        playerSpell,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case LegacyCompassionSpellId:
                await HandleLegacyCompassionRemoveBuffersAsync(
                        context,
                        resourceSession,
                        query,
                        playerSpell,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
        }

        return true;
    }

    private static async ValueTask HandleLegacySerenityRemoveBuffersAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var cleanup = await CleanLegacyRemoveBuffersSingleTargetAsync(
                context,
                resourceSession,
                query.OpponentUid,
                LegacyRemoveBuffersCoreStatusSet,
                LegacyRemoveBuffersCoreStatusSet,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedSession = cleanup.CasterSession;
        context = context with { Session = updatedSession };

        await SendLegacyRemoveBuffersAnimationAsync(
                context,
                updatedSession,
                query,
                playerSpell,
                spell,
                cleanup.AnimationTargets,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await ApplyLegacyRemoveBuffersSpellProgressAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled matching legacy Serenity RemoveBuffers magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} " +
                $"sort={(int)spell.Sort} targetUid={query.OpponentUid} " +
                $"resolvedTargets={cleanup.ResolvedTargets} stateUpdates={cleanup.StateUpdates} " +
                $"commitFailures={cleanup.CommitFailures} progressDamage={LegacyRemoveBuffersSpellProgressDamage}; " +
                "order=animation-before-progress; " +
                "source=matching DecompiledSource RemoveBuffers.cs case 10400.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyTranquilityRemoveBuffersAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var baseCleanup = await CleanLegacyRemoveBuffersSingleTargetAsync(
                context,
                resourceSession,
                query.OpponentUid,
                LegacyRemoveBuffersCoreStatusSet,
                LegacyTranquilityExternalStatusSet,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedSession = baseCleanup.CasterSession;
        context = context with { Session = updatedSession };

        await SendLegacyRemoveBuffersAnimationAsync(
                context,
                updatedSession,
                query,
                playerSpell,
                spell,
                baseCleanup.AnimationTargets,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await ApplyLegacyRemoveBuffersSpellProgressAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var graceFollowUp = false;
        var graceCleanup = LegacyRemoveBuffersCleanupResult.Empty(updatedSession);
        if (context.WorldData is { } worldData &&
            context.TeamStates?.HasTeam(context.Connection) == true &&
            LegacyMagicRuntime.TryResolveLearnedSpell(
                updatedSession.PlayerSpells,
                worldData,
                LegacyGraceOfHeavenSpellId,
                out var gracePlayerSpell,
                out var graceSpell))
        {
            graceFollowUp = true;
            graceCleanup = await CleanLegacyRemoveBuffersTeamAsync(
                    context,
                    updatedSession,
                    LegacyRemoveBuffersCoreStatusSet,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = graceCleanup.CasterSession;
            context = context with { Session = updatedSession };

            await SendLegacyRemoveBuffersAnimationAsync(
                    context,
                    updatedSession,
                    query,
                    gracePlayerSpell,
                    graceSpell,
                    graceCleanup.AnimationTargets,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await ApplyLegacyRemoveBuffersSpellProgressAsync(
                    context,
                    updatedSession,
                    gracePlayerSpell,
                    graceSpell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled matching legacy Tranquility RemoveBuffers magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} " +
                $"sort={(int)spell.Sort} targetUid={query.OpponentUid} " +
                $"baseResolvedTargets={baseCleanup.ResolvedTargets} baseStateUpdates={baseCleanup.StateUpdates} " +
                $"baseCommitFailures={baseCleanup.CommitFailures} graceFollowUp={graceFollowUp} " +
                $"graceTargets={graceCleanup.ResolvedTargets} graceStateUpdates={graceCleanup.StateUpdates} " +
                $"graceCommitFailures={graceCleanup.CommitFailures} " +
                $"baseProgressDamage={LegacyRemoveBuffersSpellProgressDamage} " +
                $"graceProgressDamage={(graceFollowUp ? LegacyRemoveBuffersSpellProgressDamage : 0)}; " +
                "order=base-animation-before-progress-before-grace-animation-before-progress; " +
                "source=matching DecompiledSource RemoveBuffers.cs case 10425 and Team.cs owner-inclusive Members.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyCompassionRemoveBuffersAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var cleanup = await CleanLegacyRemoveBuffersTeamAsync(
                context,
                resourceSession,
                LegacyCompassionStatusSet,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedSession = cleanup.CasterSession;
        context = context with { Session = updatedSession };

        await SendLegacyRemoveBuffersAnimationAsync(
                context,
                updatedSession,
                query,
                playerSpell,
                spell,
                cleanup.AnimationTargets,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await ApplyLegacyRemoveBuffersSpellProgressAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled matching legacy Compassion RemoveBuffers magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} " +
                $"sort={(int)spell.Sort} resolvedTargets={cleanup.ResolvedTargets} " +
                $"stateUpdates={cleanup.StateUpdates} commitFailures={cleanup.CommitFailures} " +
                $"progressDamage={LegacyRemoveBuffersSpellProgressDamage}; " +
                "order=animation-before-progress; " +
                "source=matching DecompiledSource RemoveBuffers.cs case 10430 and Team.cs owner-inclusive Members.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyRemoveBuffersAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacySpellAnimationTarget> targets,
        CancellationToken cancellationToken)
    {
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            targets);
        await SendPlayerSpellAnimationToVisibleAsync(
                context,
                session,
                animation,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> ApplyLegacyRemoveBuffersSpellProgressAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        return await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                session,
                playerSpell,
                spell,
                LegacyRemoveBuffersSpellProgressDamage,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
