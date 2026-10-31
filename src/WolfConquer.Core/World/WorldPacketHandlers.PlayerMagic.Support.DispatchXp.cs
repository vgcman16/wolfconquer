using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerDispatchXpMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell runtimeSpell,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session ||
            context.WorldData is not { } worldData ||
            !LegacyMagicSupportRuntime.TryResolveSupportSpell(
                worldData,
                runtimeSpell.Id,
                playerSpell.Level,
                out var supportSpell))
        {
            return false;
        }

        var support = LegacyMagicSupportRuntime.Classify(supportSpell);
        if (!support.Supported ||
            support.Kind is not (LegacyMagicSupportKind.DispatchXp or LegacyMagicSupportKind.DragonRoarStamina))
        {
            return false;
        }

        var casterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var opponentUid = query.OpponentUid == 0 ? casterUid : query.OpponentUid;
        var casterActor = CreateLegacyMagicSupportActor(
            session,
            LegacyMagicSupportTargetKind.Caster,
            worldData);
        var casterHadXpList = HasLegacyStatusFlag(session.Character, LegacyRuntimeUpdateTicker.XpListFlag);
        var teamTargetsByUid = new Dictionary<uint, WorldActiveSession>();
        LegacyMagicSupportActor? primaryTarget = null;
        WorldActiveSession? primaryActive = null;
        var hasPrimary = false;
        if (opponentUid != casterUid &&
            LegacyMagicSupportTargetResolver.TryResolveVisiblePlayerTarget(
                context,
                session,
                opponentUid,
                out var resolvedTarget))
        {
            primaryActive = resolvedTarget.ActiveSession;
            hasPrimary = true;
            primaryTarget = CreateLegacyMagicSupportActor(
                    resolvedTarget.ActiveSession.Session,
                    resolvedTarget.IsTeamMember
                        ? LegacyMagicSupportTargetKind.TeamMember
                        : LegacyMagicSupportTargetKind.Player,
                    worldData)
                with
            {
                IsTeamMember = resolvedTarget.IsTeamMember,
            };
            teamTargetsByUid[
                    CheckedUInt32(
                        resolvedTarget.ActiveSession.Session.Character.Uid,
                        nameof(resolvedTarget.ActiveSession.Session.Character.Uid))] =
                resolvedTarget.ActiveSession;
        }

        var teamMembers = CreateLegacyMagicSupportTeamMembers(
            context,
            session,
            worldData,
            teamTargetsByUid);
        var casterHasTeam = context.TeamStates?.HasTeam(context.Connection) == true;
        var outcome = LegacyMagicSupportRuntime.CalculateDispatchXp(
            supportSpell,
            casterActor,
            opponentUid,
            primaryTarget,
            teamMembers,
            casterHadXpList,
            casterHasTeam);
        if (!outcome.HasObservableEffect &&
            !outcome.ConsumesXpList &&
            outcome.SpellExperienceDamage == 0 &&
            outcome.Kind != LegacyMagicSupportKind.DragonRoarStamina)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy DispatchXp magic " +
                    $"{FormatSession(context)} spellId={runtimeSpell.Id} targetUid={query.OpponentUid}; {outcome.Reason}")
                .ConfigureAwait(false);
            return true;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                runtimeSpell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var updatedCasterSession = resourceSession;
        if (outcome.ConsumesXpList)
        {
            updatedCasterSession = resourceSession with
            {
                Character = WithoutLegacyStatusFlag(
                    resourceSession.Character,
                    LegacyRuntimeUpdateTicker.XpListFlag),
            };
            context.Sessions?.Set(context.Connection, updatedCasterSession);
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeXPListUpdate(casterUid, value: 0)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context with { Session = updatedCasterSession },
                    updatedCasterSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        foreach (var supportTarget in outcome.Targets)
        {
            animationTargets.Add(new LegacySpellAnimationTarget(
                supportTarget.AnimationUid,
                supportTarget.Damage,
                supportTarget.Hit,
                MsgAttackEffect.None));

            if (supportTarget.Kind is LegacyMagicSupportTargetKind.Caster)
            {
                updatedCasterSession = ApplyLegacyXpCountToPlayer(
                        context,
                        context.Connection,
                        updatedCasterSession,
                        supportTarget.NewXpCount);
                continue;
            }

            if (supportTarget.Kind is not (LegacyMagicSupportTargetKind.Player or LegacyMagicSupportTargetKind.TeamMember))
            {
                continue;
            }

            if (!TryResolveLegacyDispatchXpActiveTarget(
                    hasPrimary,
                    primaryActive,
                    teamTargetsByUid,
                    supportTarget.Uid,
                    out var activeTarget))
            {
                continue;
            }

            var activeTargetSession = activeTarget.Session;
            if (supportTarget.StaminaDelta > 0 ||
                outcome.Kind == LegacyMagicSupportKind.DragonRoarStamina)
            {
                activeTargetSession = await ApplyLegacyStaminaToPlayerAsync(
                        context,
                        activeTarget.Connection,
                        activeTargetSession,
                        supportTarget.NewStamina,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (supportTarget.XpCountDelta > 0 ||
                outcome.Kind == LegacyMagicSupportKind.DispatchXp)
            {
                ApplyLegacyXpCountToPlayer(
                        context,
                        activeTarget.Connection,
                        activeTargetSession,
                        supportTarget.NewXpCount);
            }
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            runtimeSpell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = updatedCasterSession },
                updatedCasterSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedCasterSession,
                playerSpell,
                runtimeSpell,
                outcome.SpellExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy DispatchXp magic " +
                $"{FormatSession(context)} spellId={runtimeSpell.Id} sort={(int)runtimeSpell.Sort} " +
                $"kind={outcome.Kind} targets={animationTargets.Count} consumesXpList={outcome.ConsumesXpList}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryResolveLegacyDispatchXpActiveTarget(
        bool hasPrimary,
        WorldActiveSession? primaryActive,
        IReadOnlyDictionary<uint, WorldActiveSession> targetsByUid,
        uint uid,
        out WorldActiveSession activeTarget)
    {
        if (hasPrimary &&
            primaryActive is not null &&
            primaryActive.Session.Character.Uid == uid)
        {
            activeTarget = primaryActive;
            return true;
        }

        return targetsByUid.TryGetValue(uid, out activeTarget!);
    }

    private static WorldSession ApplyLegacyXpCountToPlayer(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        ushort newXpCount)
    {
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(session.Character, "XPCount", newXpCount),
        };
        context.Sessions?.Set(connection, updatedSession);
        return updatedSession;
    }
}
