using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerHealingMagicAsync(
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
        if (!support.Supported || support.Kind != LegacyMagicSupportKind.Healing)
        {
            return false;
        }

        var casterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var opponentUid = query.OpponentUid == 0 ? casterUid : query.OpponentUid;
        var casterActor = CreateLegacyMagicSupportActor(
            session,
            LegacyMagicSupportTargetKind.Caster,
            worldData);
        LegacyMagicSupportActor? primaryTarget = null;
        WorldActiveSession targetPlayer = default!;
        var hasTargetPlayer = false;
        LegacyPlayerMonsterMagicTarget targetMonster = default;
        var hasTargetMonster = false;

        if (opponentUid != casterUid)
        {
            if (TryGetVisibleSessionByUid(context, session, opponentUid, out targetPlayer))
            {
                primaryTarget = CreateLegacyMagicSupportActor(
                    targetPlayer.Session,
                    LegacyMagicSupportTargetKind.Player,
                    worldData);
                hasTargetPlayer = true;
            }
            else if (TryFindVisibleMonsterTargetByUid(context, session.Character, opponentUid, out targetMonster))
            {
                primaryTarget = new LegacyMagicSupportActor(
                    targetMonster.Monster.Uid,
                    LegacyMagicSupportTargetKind.Monster,
                    targetMonster.Monster.X,
                    targetMonster.Monster.Y,
                    targetMonster.Monster.Hitpoints,
                    targetMonster.Monster.MaxLife);
                hasTargetMonster = true;
            }
        }

        var teamTargetsByUid = new Dictionary<uint, WorldActiveSession>();
        var teamMembers = CreateLegacyMagicSupportTeamMembers(
            context,
            session,
            worldData,
            teamTargetsByUid);

        var outcome = LegacyMagicSupportRuntime.CalculateHealing(
            supportSpell,
            casterActor,
            opponentUid,
            primaryTarget,
            teamMembers);
        var resourceSession = await TryPrepareLegacyRecruitHealingMagicCastAsync(
                context,
                session,
                runtimeSpell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        if (outcome.Targets.Count == 0)
        {
            var emptyAnimation = LegacySpellAnimationPacket.ForPlayerMagic(
                casterUid,
                query.X,
                query.Y,
                runtimeSpell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                []);
            await SendPlayerSpellAnimationToVisibleAsync(
                    context with { Session = resourceSession },
                    resourceSession,
                    emptyAnimation,
                    cancellationToken)
                .ConfigureAwait(false);
            await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                    context,
                    resourceSession,
                    playerSpell,
                    runtimeSpell,
                    outcome.SpellExperienceDamage,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy healing magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={runtimeSpell.Id} sort={(int)runtimeSpell.Sort} " +
                    "targets=0 totalHealing=0.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedCasterSession = resourceSession;
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var totalHealing = 0UL;

        foreach (var target in outcome.Targets)
        {
            totalHealing += target.HitpointDelta;
            animationTargets.Add(new LegacySpellAnimationTarget(
                target.AnimationUid,
                target.HitpointDelta,
                target.Hit,
                MsgAttackEffect.None));

            if (target.Kind is LegacyMagicSupportTargetKind.Caster)
            {
                updatedCasterSession = await ApplyLegacyHealingToPlayerAsync(
                        context,
                        context.Connection,
                        updatedCasterSession,
                        target.NewHitpoints,
                        worldData,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (target.Kind is LegacyMagicSupportTargetKind.Player or LegacyMagicSupportTargetKind.TeamMember)
            {
                WorldActiveSession playerTarget;
                if (hasTargetPlayer && targetPlayer.Session.Character.Uid == target.Uid)
                {
                    playerTarget = targetPlayer;
                }
                else if (teamTargetsByUid.TryGetValue(target.Uid, out var teamTarget))
                {
                    playerTarget = teamTarget;
                }
                else
                {
                    continue;
                }

                await ApplyLegacyHealingToPlayerAsync(
                        context,
                        playerTarget.Connection,
                        playerTarget.Session,
                        target.NewHitpoints,
                        worldData,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (target.Kind is LegacyMagicSupportTargetKind.Monster && hasTargetMonster)
            {
                var requestedHeal = target.NewHitpoints > targetMonster.Monster.Hitpoints
                    ? target.NewHitpoints - targetMonster.Monster.Hitpoints
                    : target.HitpointDelta;
                var heal = context.MonsterStates?.ApplyHeal(
                    targetMonster.Monster,
                    requestedHeal,
                    DateTimeOffset.UtcNow);
                if (heal is { AlreadyDead: false } &&
                    LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(targetMonster.Monster))
                {
                    var healedMonster = targetMonster.Monster with { Hitpoints = heal.Value.RemainingHitpoints };
                    foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(healedMonster))
                    {
                        await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    }
                }
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

        updatedCasterSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedCasterSession,
                playerSpell,
                runtimeSpell,
                outcome.SpellExperienceDamage == 0 ? totalHealing : outcome.SpellExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy healing magic " +
                $"{FormatSession(context)} spellId={runtimeSpell.Id} sort={(int)runtimeSpell.Sort} " +
                $"targets={outcome.Targets.Count} totalHealing={totalHealing}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession?> TryPrepareLegacyRecruitHealingMagicCastAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var resourceResult = await TrySpendLegacyPlayerMagicResourcesAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceResult.Session is null)
        {
            return null;
        }

        return await TryStartLegacyPlayerMagicCooldownAsync(
                context,
                resourceResult.Session,
                spell,
                resourceResult.GraceOfHeaven,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<LegacyMagicSupportActor> CreateLegacyMagicSupportTeamMembers(
        WorldPacketHandlerContext context,
        WorldSession casterSession,
        WorldDataCatalog worldData,
        Dictionary<uint, WorldActiveSession> targetsByUid)
    {
        var casterUid = CheckedUInt32(casterSession.Character.Uid, nameof(casterSession.Character.Uid));
        var members = new List<LegacyMagicSupportActor>();
        foreach (var active in LegacyTeamSupportTargetResolver.ResolveActiveTeammates(context, casterSession))
        {
            var memberUid = CheckedUInt32(active.Session.Character.Uid, nameof(active.Session.Character.Uid));
            if (memberUid == casterUid || targetsByUid.ContainsKey(memberUid))
            {
                continue;
            }

            targetsByUid.Add(memberUid, active);
            members.Add(CreateLegacyMagicSupportActor(
                    active.Session,
                    LegacyMagicSupportTargetKind.TeamMember,
                    worldData)
                with
            {
                IsTeamMember = true,
            });
        }

        return members;
    }

    private static async ValueTask<bool> HandleLegacyPlayerReviveMagicAsync(
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
        if (!support.Supported || support.Kind != LegacyMagicSupportKind.Revive)
        {
            return false;
        }

        var casterActor = CreateLegacyMagicSupportActor(
            session,
            LegacyMagicSupportTargetKind.Caster,
            worldData);
        var hasTarget = LegacyMagicSupportTargetResolver.TryResolveVisiblePlayerTarget(
            context,
            session,
            query.OpponentUid,
            out var target);
        var targetActor = hasTarget
            ? CreateLegacyMagicSupportActor(
                target.ActiveSession.Session,
                target.IsTeamMember
                    ? LegacyMagicSupportTargetKind.TeamMember
                    : LegacyMagicSupportTargetKind.Player,
                worldData)
                with
            {
                HasSoulShackle = HasLegacyStatusFlag(
                        target.ActiveSession.Session.Character,
                        LegacySoulShackleStatusFlag),
                IsTeamMember = target.IsTeamMember,
            }
            : null;
        var casterHasTeam = context.TeamStates?.HasTeam(context.Connection) == true;
        var targetHasTeam = hasTarget &&
            context.TeamStates?.HasTeam(target.ActiveSession.Connection) == true;

        var outcome = LegacyMagicSupportRuntime.CalculateReviveSupport(
            supportSpell,
            casterActor,
            targetActor,
            casterIsWatching: false,
            mercyPrayerWeaponEligible: true,
            casterHasTeam,
            targetHasTeam);
        if (!outcome.HasObservableEffect &&
            !outcome.ConsumesXpList &&
            outcome.SpellExperienceDamage == 0 &&
            outcome.Kind != LegacyMagicSupportKind.DragonRoarStamina)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy revive magic " +
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

        if (outcome.ConsumesXpList)
        {
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeXPListUpdate(
                            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                            value: 0)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        if (hasTarget)
        {
            foreach (var reviveTarget in outcome.Targets)
            {
                if (reviveTarget.Uid != target.ActiveSession.Session.Character.Uid)
                {
                    continue;
                }

                await ApplyLegacyReviveToPlayerAsync(
                        context,
                        target.ActiveSession.Connection,
                        target.ActiveSession.Session,
                        worldData,
                        reviveTarget.NewXpCount,
                        cancellationToken)
                    .ConfigureAwait(false);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    reviveTarget.AnimationUid,
                    reviveTarget.Damage,
                    reviveTarget.Hit,
                    MsgAttackEffect.None));
            }
        }

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            runtimeSpell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = resourceSession },
                resourceSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                resourceSession,
                playerSpell,
                runtimeSpell,
                outcome.SpellExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy revive magic " +
                $"{FormatSession(context)} spellId={runtimeSpell.Id} sort={(int)runtimeSpell.Sort} " +
                $"targets={animationTargets.Count} consumesXpList={outcome.ConsumesXpList}.")
            .ConfigureAwait(false);
        return true;
    }

    private static LegacyMagicSupportActor CreateLegacyMagicSupportActor(
        WorldSession session,
        LegacyMagicSupportTargetKind kind,
        WorldDataCatalog? worldData)
    {
        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            session.AlternateEquipmentActive);
        return new LegacyMagicSupportActor(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            kind,
            CheckedUInt16(session.Character.X, nameof(session.Character.X)),
            CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
            currentHitpoints,
            maxHitpoints,
            (ushort)Math.Min(ushort.MaxValue, ReadLegacyRuntimeStamina(session.Character)),
            (ushort)Math.Min(ushort.MaxValue, ReadAttributeUInt32(session.Character.Attributes, "XPCount", "XpCount")),
            HasLegacyStatusFlag(session.Character, LegacyHeavenBlessingFlag),
            HasSoulShackle: false,
            HasLegacyStatusFlag(session.Character, LegacyFlyFlag),
            HasHeavensWrath: false,
            HasInfinity: false);
    }

    private static async ValueTask<WorldSession> ApplyLegacyHealingToPlayerAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        uint newHitpoints,
        WorldDataCatalog? worldData,
        CancellationToken cancellationToken)
    {
        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            Math.Max(currentHitpoints, newHitpoints),
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            session.AlternateEquipmentActive);
        var clampedHitpoints = Math.Min(newHitpoints, maxHitpoints);
        var updatedSession = session with
        {
            Character = session.Character with { MinHitPoints = clampedHitpoints },
        };
        context.Sessions?.Set(connection, updatedSession);

        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            maxHitpoints,
            clampedHitpoints))
        {
            await connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static async ValueTask<WorldSession> ApplyLegacyStaminaToPlayerAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        ushort newStamina,
        CancellationToken cancellationToken)
    {
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(session.Character, "Stamina", newStamina),
        };
        context.Sessions?.Set(connection, updatedSession);

        await connection.SendAsync(
                LegacyMsgUpdatePacket
                    .CreateLegacyRuntimeStaminaTickUpdate(
                        CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                        newStamina)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> ApplyLegacyReviveToPlayerAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        WorldDataCatalog? worldData,
        ushort newXpCount,
        CancellationToken cancellationToken)
    {
        // Capture the Player.Revive receiver before any target HP/XP/stamina publication awaits.
        var nativePlayerReceiver = session.NativePlayer;
        var hadXpList = HasLegacyStatusFlag(session.Character, LegacyRuntimeUpdateTicker.XpListFlag);
        var currentHitpoints = session.Character.MinHitPoints <= 0
            ? 1U
            : CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            session.AlternateEquipmentActive);
        var revive = LegacyPlayerDeathState.TryRevive(
            session.Character,
            DateTimeOffset.UtcNow,
            new LegacyPlayerReviveOptions
            {
                AllowAlive = true,
                IgnoreReviveDelay = true,
                ReviveDelay = TimeSpan.Zero,
                MaximumHitpoints = maxHitpoints,
                MaximumMana = session.Character.MaxMana,
            });
        if (!revive.Allowed)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy magic revive target " +
                    $"uid={session.Character.Uid} hitpoints={session.Character.MinHitPoints} reviveAllowedAt={revive.State.ReviveAllowedAt}.")
                .ConfigureAwait(false);
            return session;
        }

        var revivedCharacter = WithCharacterAttribute(revive.Character, "XPCount", newXpCount);
        var updatedSession = session with { Character = revivedCharacter };
        context.Sessions?.Set(connection, updatedSession);
        var uid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));

        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
            uid,
            maxHitpoints,
            maxHitpoints))
        {
            await connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        if (hadXpList)
        {
            await connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(uid, value: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeXPCircleTickUpdate(uid, newXpCount).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, 100).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                    uid,
                    LegacyNativePlayerFlag6Lifecycle.CaptureVector(nativePlayerReceiver,
                        CreateLegacyRuntimeStatusFlagVector(updatedSession.Character))).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeMeshUpdate(
                    uid,
                    BuildLegacyCharacterMesh(updatedSession.Character)).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMapStatusPacket.FromMap(updatedSession.Character.MapId, worldData?.MapsById).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await SendPlayerSpawnToVisiblePeersAsync(
                context with { Connection = connection, Session = updatedSession },
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }
}
