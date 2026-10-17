using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyHellVortexFanoutRadius = 6;
    private const int LegacyHellVortexCounterCascadeLimit = 16;

    private static async ValueTask<bool> HandleLegacyPlayerHellVortexMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken,
        HashSet<uint>? activeHellVortexCasters = null)
    {
        if (spell.Sort != LegacyMagicSort.HellVortex)
        {
            return false;
        }

        // HellVortex.Execute calls CanUseSpell.Verified before its exact spell-id switch.
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

        if (spell.Id != LegacyHellVortexSpellId)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-backed returned legacy HellVortex-sort magic after resource preparation " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"sort={(int)spell.Sort}; exact matching handler id is {LegacyHellVortexSpellId}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = resourceSession;
        context = context with { Session = updatedSession };
        activeHellVortexCasters ??= new HashSet<uint>();
        activeHellVortexCasters.Add(CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)));
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyHellVortexMonsterTargets(context, updatedSession, query.OpponentUid)
            : [];

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var now = DateTimeOffset.UtcNow;

        if (context.MonsterStates is { } monsterStates)
        {
            foreach (var candidate in monsterTargets)
            {
                if (!TryRefreshLegacyHellVortexMonsterRecipient(
                        context, updatedSession, query.OpponentUid, candidate, out var target))
                {
                    continue;
                }

                if (IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
                {
                    updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                            context,
                            updatedSession,
                            cancellationToken)
                        .ConfigureAwait(false);
                    context = context with { Session = updatedSession };
                }

                var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                requestedDamage = CalculateLegacyHellVortexSoulDamage(
                    requestedDamage,
                    playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    requestedDamage,
                    now,
                    updatedSession);
                monsterResults.Add((target, result));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    result.RequestedDamage,
                    Hit: result.RequestedDamage > 0,
                    MsgAttackEffect.None));
            }
        }

        // The matching handler completes its monster pass before CanAttackPlayer can countercast.
        var playerTargets = SelectLegacyHellVortexPlayerTargets(
                context,
                updatedSession,
                query.OpponentUid);

        foreach (var target in playerTargets)
        {
            var hit = await TryReceiveLegacyHellVortexPlayerHitAsync(
                    context,
                    updatedSession,
                    target,
                    query.OpponentUid,
                    playerSpell,
                    spell,
                    activeHellVortexCasters,
                    cancellationToken)
                .ConfigureAwait(false);
            if (hit.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            updatedSession = hit.Session;
            context = context with { Session = updatedSession };
            if (hit.Result is not { } result || hit.Animation is not { } animationTarget)
            {
                continue;
            }

            playerResults.Add(result);
            animationTargets.Add(animationTarget);
        }

        foreach (var (target, result) in monsterResults)
        {
            if (result.AlreadyDead)
            {
                continue;
            }

            if (result.Killed)
            {
                var death = new MsgAttackPacket(new MsgAttackQuery(
                    Uid: CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                    OpponentUid: target.Monster.Uid,
                    X: target.Monster.X,
                    Y: target.Monster.Y,
                    AttackType: MsgAttackType.Death,
                    Damage: 0,
                    KilledMonster: 1,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0));
                await SendMonsterCombatFrameToVisibleAsync(context, updatedSession, target.Monster, death, cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var totalMonsterDamage = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        var totalPlayerDamage = playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy HellVortex magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} totalDamage={totalMonsterDamage + totalPlayerDamage}; " +
                $"selectedOpponentUid={query.OpponentUid} radius={LegacyHellVortexFanoutRadius} " +
                "mixedTargetCategories=False pushback=False bombFlag=0 " +
                "increaseExperienceUp=False updateSpellCheckUpdate=False; " +
                "source=matching DecompiledSource HellVortex.cs.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask TrySendLegacyPlayerHellVortexCounterAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        WorldActiveSession counterCaster,
        CancellationToken cancellationToken,
        HashSet<uint>? activeHellVortexCasters = null)
    {
        if (context.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                counterCaster.Session.PlayerSpells,
                worldData,
                LegacyHellVortexSpellId,
                out var playerSpell,
                out var spell) ||
            !IsLegacyHellVortexSpell(spell))
        {
            return;
        }

        activeHellVortexCasters ??= new HashSet<uint>();
        var counterCasterUid = CheckedUInt32(counterCaster.Session.Character.Uid, nameof(counterCaster.Session.Character.Uid));
        if (activeHellVortexCasters.Contains(counterCasterUid))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player HellVortex counter cascade " +
                    $"{FormatSession(context)} counterUid={counterCaster.Session.Character.Uid} " +
                    $"targetUid={attacker.Character.Uid}; source=CanAttackPlayer.Verified HellVortex recursive caster already active.")
                .ConfigureAwait(false);
            return;
        }

        if (activeHellVortexCasters.Count >= LegacyHellVortexCounterCascadeLimit)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player HellVortex counter cascade " +
                    $"{FormatSession(context)} counterUid={counterCaster.Session.Character.Uid} " +
                    $"targetUid={attacker.Character.Uid}; source=CanAttackPlayer.Verified HellVortex cascade limit.")
                .ConfigureAwait(false);
            return;
        }

        activeHellVortexCasters.Add(counterCasterUid);
        var counterContext = context with
        {
            Connection = counterCaster.Connection,
            Session = counterCaster.Session,
        };
        var counterQuery = new MsgAttackQuery(
            Uid: counterCasterUid,
            OpponentUid: CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid)),
            X: 0,
            Y: 0,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: LegacyHellVortexSpellId);

        if (!await HandleLegacyPlayerHellVortexMagicAsync(
                counterContext,
                counterCaster.Session,
                counterQuery,
                playerSpell,
                spell,
                cancellationToken,
                activeHellVortexCasters)
            .ConfigureAwait(false))
        {
            return;
        }

        if (counterContext.Sessions?.TryGet(counterContext.Connection, out var updatedCounterSession) == true &&
            updatedCounterSession is not null)
        {
            counterContext = counterContext with { Session = updatedCounterSession };
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy player HellVortex counter " +
                $"{FormatSession(counterContext)} " +
                $"spellId={LegacyHellVortexSpellId} targetUid={attacker.Character.Uid} " +
                "source=CanAttackPlayer.Verified HellVortex -> ProcescMagic(ignoreStamp) -> HellVortex.Execute.")
            .ConfigureAwait(false);
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyHellVortexMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint opponentUid)
    {
        var selected = FindLegacyHellVortexSelectedMonster(context, session, opponentUid);
        if (selected is null)
        {
            return [];
        }

        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    selected.X,
                    selected.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) <= LegacyHellVortexFanoutRadius &&
                CanLegacyPlayerAttackMonster(
                    session.Character,
                    candidate.Monster,
                    candidate.MonsterFamily,
                    out _))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyHellVortexPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint opponentUid)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        if (context.Sessions is not { } sessions)
        {
            return targets;
        }

        var activeSessions = sessions.Snapshot();
        var selected = FindLegacyMapBlockPlayer(sessions, session, opponentUid);
        if (selected is null)
        {
            return targets;
        }

        foreach (var active in activeSessions)
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeePlayer(session.Character, active.Session.Character) ||
                LegacyGetDistance(
                    selected.Session.Character.X,
                    selected.Session.Character.Y,
                    active.Session.Character.X,
                    active.Session.Character.Y) > LegacyHellVortexFanoutRadius)
            {
                continue;
            }

            targets.Add(new LegacyPlayerMagicTarget(active));
        }

        return targets;
    }

    private static uint CalculateLegacyHellVortexSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged.
        _ = useSpellSoul;
        return damage;
    }
}
