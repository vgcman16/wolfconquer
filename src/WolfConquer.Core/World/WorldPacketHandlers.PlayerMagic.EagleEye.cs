using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyEagleEyeConsumedTargetDamage = 11_135;
    internal const string RuntimeLegacyEagleEyeExpiresAtUtcAttribute = "LegacyEagleEyeExpiresAtUtc";
    private static readonly TimeSpan LegacyEagleEyeCountdownDuration = TimeSpan.FromSeconds(20);
    private readonly record struct LegacyEagleEyePlayerHit(
        LegacyPlayerMagicReceiveOutcome Receive,
        uint RequestedDamage,
        MsgAttackEffect Effect);

    private static async ValueTask<bool> HandleLegacyPlayerEagleEyeMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.DirectAttack ||
            spell.Id != LegacyEagleEyeSpellId)
        {
            return false;
        }

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
        if (TryFindVisibleMonsterTargetByUid(context, resourceSession.Character, query.OpponentUid, out var monsterTarget))
        {
            await HandleLegacyEagleEyeMonsterAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    monsterTarget,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetVisibleSessionByUid(context, resourceSession, query.OpponentUid, out var playerTarget))
        {
            await HandleLegacyEagleEyePlayerAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    playerTarget,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryFindVisibleSobNpcByUid(context, resourceSession.Character, query.OpponentUid, out var sobNpcTarget))
        {
            await HandleLegacyEagleEyeSobNpcAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    sobNpcTarget,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    private static async ValueTask HandleLegacyEagleEyeMonsterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        LegacyPlayerMonsterMagicTarget target,
        CancellationToken cancellationToken,
        Func<double, bool>? family8418Rate = null,
        Func<int, int, int>? family8418Next = null)
    {
        if (!CanLegacyPlayerAttackMonster(session.Character, target.Monster, target.MonsterFamily, out _))
        {
            return;
        }

        if (context.MonsterStates is not { } monsterStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        MonsterRuntimeStateResource,
                        "eagle-eye-monster-magic",
                        "EagleEye monster target was found, but monster runtime state is not loaded"))
                .ConfigureAwait(false);
            return;
        }

        if (IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
        {
            session = await TryApplyLegacyGuardAttackFlashingNameAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = session };
        }

        var now = DateTimeOffset.UtcNow;
        var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
            session,
            context.WorldData,
            target.Monster,
            target.MonsterFamily,
            spell);
        var receive = await ApplyLegacyMonsterNativeReceiveAsync(
                context, session, session, target.Monster, target.MonsterFamily,
                requestedDamage, now, cancellationToken, family8418Rate, family8418Next)
            .ConfigureAwait(false);
        var result = receive.Damage;
        session = receive.Session;
        context = context with { Session = session };
        if (result.AlreadyDead)
        {
            return;
        }

        if (receive.NativeDeadHandled && result.Killed)
        {
            // ReceiveAttack.Monster shares team experience AFTER Dead, BEFORE EagleEye resumes.
            await TryApplyLegacyMonsterTeamExperienceAsync(context, session, target.Monster, cancellationToken)
                .ConfigureAwait(false);
        }

        if (monsterStates.IsBlackSpotted(target.Monster, now))
        {
            session = await TryConsumeLegacyEagleEyeCountdownAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            session = SetLegacyEagleEyeCountdown(session, active: true);
        }

        if (receive.NativeDeadHandled)
        {
            // Native UpdateSpell precedes this converted path's animation (Attack.cs496..498).
            session = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context, session, playerSpell, spell, result.DamageApplied, cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = session };
        }

        await SendLegacyEagleEyeAnimationAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                target.Monster.Uid,
                result.RequestedDamage,
                MsgAttackEffect.None,
                useCountdownTargetSelection: true,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = session;
        if (result.Killed)
        {
            if (!receive.NativeDeadHandled)
            {
                var death = new MsgAttackPacket(new MsgAttackQuery(
                    Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                    OpponentUid: target.Monster.Uid,
                    X: target.Monster.X,
                    Y: target.Monster.Y,
                    AttackType: MsgAttackType.Death,
                    Damage: 0,
                    KilledMonster: 1,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0));
                await SendMonsterCombatFrameToVisibleAsync(context, session, target.Monster, death, cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(context, session, target.Monster, target.MonsterFamily, cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TryApplyLegacyMonsterTeamExperienceAsync(context, updatedSession, target.Monster, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }

            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                context,
                updatedSession,
                target.Monster,
                target.MonsterFamily,
                result.DamageApplied,
                cancellationToken)
            .ConfigureAwait(false);
        if (!receive.NativeDeadHandled)
        {
            updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                    context,
                    updatedSession,
                    playerSpell,
                    spell,
                    result.DamageApplied,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        context.Sessions?.Set(context.Connection, updatedSession);
    }

    private static async ValueTask HandleLegacyEagleEyePlayerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldActiveSession target,
        CancellationToken cancellationToken)
    {
        if (!CanLegacyPlayerMagicDamageTarget(context, session, target.Session, spell, out _))
        {
            return;
        }

        var damageResults = new List<LegacyPlayerMagicDamageResult>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var primaryHit = await ApplyLegacyEagleEyePlayerHitAsync(
                context,
                session,
                target,
                spell,
                applyPrimaryScale: true,
                cancellationToken)
            .ConfigureAwait(false);
        if (primaryHit.Receive.AttackerSession is { } primaryAttacker)
        {
            session = primaryAttacker;
            context = context with { Session = session };
        }
        if (primaryHit.Receive.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return;
        }
        if (primaryHit.Receive.Result is { } primaryResult)
        {
            damageResults.Add(primaryResult);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(
                    primaryResult.UpdatedSession.Character.Uid,
                    nameof(primaryResult.UpdatedSession.Character.Uid)),
                primaryHit.RequestedDamage,
                Hit: true,
                primaryHit.Effect));

            var activeTarget = primaryResult.ActiveSession;
            if (IsLegacyPlayerBlackSpotActive(activeTarget.Session.Character, DateTimeOffset.UtcNow))
            {
                session = await TryConsumeLegacyEagleEyeCountdownAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                session = SetLegacyEagleEyeCountdown(session, active: true);
            }

            context.Sessions?.Set(context.Connection, session);
        }
        context = context with { Session = session };
        foreach (var secondaryTarget in SelectLegacyEagleEyeSecondaryPlayerTargets(
                     context,
                     session,
                     target,
                     query,
                     spell))
        {
            var secondaryHit = await ApplyLegacyEagleEyePlayerHitAsync(
                    context,
                    session,
                    secondaryTarget,
                    spell,
                    applyPrimaryScale: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (secondaryHit.Receive.AttackerSession is { } secondaryAttacker)
            {
                session = secondaryAttacker;
                context = context with { Session = session };
            }
            if (secondaryHit.Receive.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return;
            }
            if (secondaryHit.Receive.Result is not { } secondaryResult)
            {
                continue;
            }

            damageResults.Add(secondaryResult);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(
                    secondaryResult.UpdatedSession.Character.Uid,
                    nameof(secondaryResult.UpdatedSession.Character.Uid)),
                secondaryHit.RequestedDamage,
                Hit: true,
                secondaryHit.Effect));
        }

        await SendLegacyEagleEyeAnimationTargetsAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                animationTargets,
                useCountdownTargetSelection: true,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var result in damageResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(context, session, result, cancellationToken)
                .ConfigureAwait(false);
        }

        context.Sessions?.Set(context.Connection, session);
    }

    private static IReadOnlyList<WorldActiveSession> SelectLegacyEagleEyeSecondaryPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession primaryTarget,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var selected = new List<WorldActiveSession>();
        var primaryUid = primaryTarget.Session.Character.Uid;
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var activeSession = candidate.ActiveSession;
            var character = activeSession.Session.Character;
            if (character.Uid == primaryUid ||
                LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    character.X,
                    character.Y) > spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    spell.Range,
                    width: 0))
            {
                continue;
            }

            selected.Add(activeSession);
        }

        return selected;
    }

    private static async ValueTask<LegacyEagleEyePlayerHit> ApplyLegacyEagleEyePlayerHitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession target,
        LegacyMagicRuntimeSpell spell,
        bool applyPrimaryScale,
        CancellationToken cancellationToken)
    {
        var activeTarget = target;
        if (context.Sessions?.TryGet(target.Connection, out var refreshedTarget) == true &&
            refreshedTarget is not null)
        {
            activeTarget = target with { Session = refreshedTarget };
        }

        var statusDefended =
            HasLegacyStatusFlag(activeTarget.Session.Character, LegacyShurikenVortexFlag) ||
            HasLegacyStatusFlag(activeTarget.Session.Character, LegacyManiacDanceFlag);
        var wildPhoenixDefended = false;
        if (!statusDefended)
        {
            (activeTarget, wildPhoenixDefended) = await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                    context,
                    activeTarget,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var physicalDamage = statusDefended || wildPhoenixDefended
            ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
            : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                session,
                activeTarget.Session,
                spell,
                context.WorldData,
                damageDivisor: 1);
        var requestedDamage = applyPrimaryScale
            ? MulDivToUInt32(physicalDamage.Damage, 14, 22)
            : physicalDamage.Damage;
        var receive = statusDefended || wildPhoenixDefended
            ? await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    session.Character,
                    requestedDamage,
                    cancellationToken)
                .ConfigureAwait(false)
            : await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
                    context,
                    activeTarget,
                    session.Character,
                    requestedDamage,
                    cancellationToken)
                .ConfigureAwait(false);
        return new LegacyEagleEyePlayerHit(receive, requestedDamage, physicalDamage.Effect);
    }

    private static async ValueTask HandleLegacyEagleEyeSobNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        SobNpc target,
        CancellationToken cancellationToken)
    {
        if (!CanLegacySobNpcMagicDamageTarget(context, session.Character, target, out _) ||
            context.SobNpcStates is not { } sobNpcStates)
        {
            return;
        }

        var requestedDamage = CalculateLegacySobNpcPhysicalSpellDamage(
            session,
            spell,
            context.WorldData,
            damageMultiplier: 1);
        var result = sobNpcStates.ApplyDamage(
            target,
            requestedDamage,
            DateTimeOffset.UtcNow,
            LegacySobNpcEventRules.GetDeathBehavior(target));
        if (result.AlreadyDead || result.DamageApplied == 0)
        {
            return;
        }

        await SendLegacyEagleEyeAnimationAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                CheckedUInt32(target.Id, nameof(target.Id)),
                requestedDamage,
                MsgAttackEffect.None,
                useCountdownTargetSelection: false,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(target))
        {
            await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                    context,
                    session,
                    target,
                    sobNpcStates.GetVisible(target),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!LegacySobNpcEventRules.IsTrainingSob(target))
        {
            await LogLegacySobNpcEventSideEffectGapAsync(context, target, result, spell).ConfigureAwait(false);
        }

        var progressDamage = CalculateLegacySobNpcExperienceDamage(target, result);
        session = await ApplyLegacyDirectSobNpcIncreaseExperienceAsync(
                context,
                session,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = session };
        session = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                session,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context.Sessions?.Set(context.Connection, session);
    }

    private static async ValueTask<WorldSession> TryConsumeLegacyEagleEyeCountdownAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !IsLegacyEagleEyeCountDownActive(session.Character) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyEagleEyeCountdownSpellId,
                out var playerSpell,
                out _) ||
            !TryReadLegacyMagicByteField(
                worldData,
                LegacyEagleEyeCountdownSpellId,
                playerSpell.Level,
                LegacyMagicRateField,
                out var rate) ||
            !CheckLegacyEagleEyeResponseRateRoll(session.Character, rate))
        {
            return session;
        }

        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var response = new LegacySpellAnimationPacket(
            uid,
            AttackedUid: uid,
            X: 0,
            Y: 0,
            LegacyEagleEyeCountdownSpellId,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(uid, LegacyEagleEyeCountdownSpellId, Hit: false, MsgAttackEffect.None)]);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(LegacyMagicColdTimePacket.ZeroEagleEye().ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = SetLegacyEagleEyeCountdown(session, active: false);
        context.Sessions?.Set(context.Connection, updatedSession);
        return updatedSession;
    }

    private static WorldSession SetLegacyEagleEyeCountdown(WorldSession session, bool active)
    {
        var character = WithoutCharacterAttribute(
            WithoutCharacterAttribute(
                WithoutCharacterAttribute(
                    WithoutCharacterAttribute(session.Character, "EagleEyeCountDown"),
                    "EagleEyeCountdown"),
                RuntimeLegacyEagleEyeCountDownAttribute),
            RuntimeLegacyEagleEyeExpiresAtUtcAttribute);
        character = WithCharacterAttribute(
            character,
            RuntimeLegacyEagleEyeCountDownAttribute,
            active ? "1" : "0");
        if (active)
        {
            character = WithCharacterAttribute(
                character,
                RuntimeLegacyEagleEyeExpiresAtUtcAttribute,
                (DateTimeOffset.UtcNow.ToUniversalTime() + LegacyEagleEyeCountdownDuration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return session with { Character = character };
    }

    private static async ValueTask SendLegacyEagleEyeAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        uint targetUid,
        uint targetDamage,
        MsgAttackEffect effect,
        bool useCountdownTargetSelection,
        CancellationToken cancellationToken)
    {
        await SendLegacyEagleEyeAnimationTargetsAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                [new LegacySpellAnimationTarget(targetUid, targetDamage, Hit: true, effect)],
                useCountdownTargetSelection,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyEagleEyeAnimationTargetsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacySpellAnimationTarget> targets,
        bool useCountdownTargetSelection,
        CancellationToken cancellationToken)
    {
        var countdownActive = useCountdownTargetSelection &&
            IsLegacyEagleEyeCountDownActive(session.Character);
        var casterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            !useCountdownTargetSelection || countdownActive
                ? targets
                : [new LegacySpellAnimationTarget(
                    casterUid,
                    LegacyEagleEyeConsumedTargetDamage,
                    Hit: false,
                    MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken).ConfigureAwait(false);
    }
}
