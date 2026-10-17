using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyChainBoltGameplayEvidence =
        @"matching-decompile=DecompiledSource\GameServer\MrRayzo.Game.MsgServer.AttackHandler\ChainBolt.cs; " +
        @"dispatch=DecompiledSource\GameServer\MrRayzo.Game.MsgServer\MsgAttackPacket.cs MagicSort.ChainBolt->ChainBolt.Execute; " +
        @"magic=GameServer\DBServer\magictype.txt rows 10309/10343-10346 id=10309 sort=37 ranges=10/12/14/15/15 duration=60; " +
        @"status=DecompiledSource\GameServer\MrRayzo.Game.MsgServer\MsgUpdate.cs XPList=4 ChaintBolt=0x5C";

    private enum LegacyChainBoltPrimaryTargetKind
    {
        None,
        Monster,
        Player,
        SobNpc,
    }

    private static async ValueTask<bool> TryHandleLegacyChainBoltNonXpMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Id != LegacyChainBoltSpellId ||
            spell.Sort != LegacyMagicSort.ChainBolt)
        {
            return false;
        }

        if (TryFindVisibleMonsterTargetByUid(context, resourceSession.Character, query.OpponentUid, out var monsterPrimary))
        {
            if (context.MonsterStates is not { } monsterStates)
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            MonsterRuntimeStateResource,
                            "chainbolt-non-xplist-monster",
                            "ChainBolt primary monster target was found, but monster runtime state is not loaded",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("map", monsterPrimary.Monster.MapId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monsterPrimary.Monster.MonsterId),
                                LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                                LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                LegacyParityGapDiagnostics.Fact("targetKind", "monster"),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                LegacyParityGapDiagnostics.Fact("targetKind", "monster"),
                            ]))
                    .ConfigureAwait(false);
                return true;
            }

            await ExecuteLegacyChainBoltMonsterBranchAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    monsterStates,
                    monsterPrimary,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetVisibleSessionByUid(context, resourceSession, query.OpponentUid, out var playerPrimary))
        {
            await ExecuteLegacyChainBoltPlayerBranchAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    playerPrimary,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryFindVisibleSobNpcByUid(context, resourceSession.Character, query.OpponentUid, out var sobNpcPrimary))
        {
            if (context.SobNpcStates is not { } sobNpcStates)
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            SobNpcRuntimeStateResource,
                            "chainbolt-non-xplist-sob",
                            "ChainBolt primary SOB target was found, but SOB runtime state is not loaded",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("map", sobNpcPrimary.MapId),
                                LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                                LegacyParityGapDiagnostics.Fact("sobNpcKind", sobNpcPrimary.Kind),
                                LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                            ]))
                    .ConfigureAwait(false);
                return true;
            }

            await ExecuteLegacyChainBoltSobNpcBranchAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    sobNpcStates,
                    sobNpcPrimary,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy ChainBolt non-XPList with no visible primary target " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"opponentUid={query.OpponentUid}; gameplayEvidence={LegacyChainBoltGameplayEvidence}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask ExecuteLegacyChainBoltMonsterBranchAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        MonsterRuntimeStateStore monsterStates,
        LegacyPlayerMonsterMagicTarget primary,
        CancellationToken cancellationToken)
    {
        var updatedSession = resourceSession;
        var targets = SelectLegacyChainBoltMonsterTargets(context, updatedSession, spell, primary);
        foreach (var target in targets)
        {
            if (IsLegacyGuardMonsterFamily(target.Target.Monster, target.Target.MonsterFamily))
            {
                updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                        context,
                        updatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedSession };
                break;
            }
        }

        var statusFields = CalculateLegacyRuntimeStatusFields(updatedSession, context.WorldData);
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var results = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint RequestedDamage,
            bool CountsForProgress)>();
        var now = DateTimeOffset.UtcNow;

        foreach (var target in targets)
        {
            var damage = CalculateLegacyPlayerMonsterVisibleMagicDamageWithEffects(
                updatedSession,
                context.WorldData!,
                target.Target,
                spell,
                statusFields);
            updatedSession = await TryConsumeLegacySlayerAsync(
                    context,
                    updatedSession,
                    damage.ConsumesSlayer,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            var requestedDamage = damage.Damage.Damage;
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                target.Target.Monster,
                requestedDamage,
                now,
                updatedSession);
            results.Add((target.Target, result, requestedDamage, target.CountsForProgress));
            if (result.AlreadyDead)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                target.Target.Monster.Uid,
                GetLegacyPlayerMonsterMagicAnimationDamage(spell, result.RequestedDamage),
                Hit: result.RequestedDamage > 0,
                damage.Damage.Effect));
        }

        if (animationTargets.Count != 0)
        {
            var animation = LegacySpellAnimationPacket.ForPlayerMagic(
                CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                CheckedUInt16(primary.Monster.X, nameof(primary.Monster.X)),
                CheckedUInt16(primary.Monster.Y, nameof(primary.Monster.Y)),
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                animationTargets);
            await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
                .ConfigureAwait(false);
        }

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, requestedDamage, countsForProgress) in results)
        {
            if (result.AlreadyDead)
            {
                continue;
            }

            if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
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
                context = context with { Session = updatedSession };
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                firstRetaliationTarget ??= target;
            }

            if (countsForProgress)
            {
                updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        requestedDamage,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedSession };
            }
        }

        if (firstRetaliationTarget is { } retaliationTarget)
        {
            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    updatedSession,
                    retaliationTarget.Monster,
                    retaliationTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var progressDamage = results
            .Where(result => result.CountsForProgress && !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.RequestedDamage);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "ChainBolt non-XPList weapon proficiency experience")
            .ConfigureAwait(false);
        await LogLegacyChainBoltHandledAsync(
                context with { Session = updatedSession },
                spell,
                playerSpell,
                LegacyChainBoltPrimaryTargetKind.Monster,
                animationTargets.Count,
                progressDamage,
                results
                    .Where(result => !result.Result.AlreadyDead)
                    .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied))
            .ConfigureAwait(false);
    }

    private static async ValueTask ExecuteLegacyChainBoltPlayerBranchAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldActiveSession primary,
        CancellationToken cancellationToken)
    {
        var updatedSession = resourceSession;
        var targets = await SelectLegacyChainBoltPlayerTargetsAsync(
                context,
                updatedSession,
                spell,
                primary,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                targets.Select(target => target.Target).ToList(),
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var results = new List<LegacyPlayerMagicDamageResult>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        foreach (var target in targets)
        {
            var activeTarget = target.Target.ActiveSession;
            (activeTarget, var wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        context,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            var damage = wildPhoenixDefended
                ? 1U
                : CalculateLegacyPlayerPlayerMagicDamage(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData);
            if (!wildPhoenixDefended)
            {
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    spell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.Player);
            }

            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                activeTarget,
                updatedSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                updatedSession = currentReceiveAttacker;
                context = context with { Session = updatedSession };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }
            if (!HasLegacyChainBoltPlayerAnimationResult(result, wildPhoenixDefended))
            {
                continue;
            }

            if (result.UpdatedAttackerSession is not null)
            {
                updatedSession = result.UpdatedAttackerSession;
                context = context with { Session = updatedSession };
            }

            results.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));
        }

        if (animationTargets.Count != 0)
        {
            foreach (var result in results)
            {
                await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                        context,
                        updatedSession,
                        result,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var animation = LegacySpellAnimationPacket.ForPlayerMagic(
                CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                CheckedUInt16(primary.Session.Character.X, nameof(primary.Session.Character.X)),
                CheckedUInt16(primary.Session.Character.Y, nameof(primary.Session.Character.Y)),
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                animationTargets);
            await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
                .ConfigureAwait(false);

            foreach (var result in results)
            {
                await SendLegacyPlayerMagicDamageUpdatesAsync(
                        context,
                        updatedSession,
                        result,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await TryApplyLegacyPlayerMagicChillingSnowAsync(
                    context,
                    updatedSession,
                    results,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await LogLegacyChainBoltHandledAsync(
                context with { Session = updatedSession },
                spell,
                playerSpell,
                LegacyChainBoltPrimaryTargetKind.Player,
                animationTargets.Count,
                progressDamage: 0,
                results.Aggregate(0UL, (total, result) => total + result.DamageApplied))
            .ConfigureAwait(false);
    }

    private static async ValueTask ExecuteLegacyChainBoltSobNpcBranchAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        SobNpcRuntimeStateStore sobNpcStates,
        SobNpc primary,
        CancellationToken cancellationToken)
    {
        var updatedSession = resourceSession;
        var targets = SelectLegacyChainBoltSobNpcTargets(context, updatedSession, spell, primary);
        var statusFields = CalculateLegacyRuntimeStatusFields(updatedSession, context.WorldData);
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var results = new List<(SobNpc SobNpc, SobNpcDamageResult Result, uint RequestedDamage)>();
        var killedTrainingTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var now = DateTimeOffset.UtcNow;

        foreach (var target in targets)
        {
            var damage = LegacySobNpcMagicDamageCalculator.CalculateDamage(
                statusFields[LegacyPlayerStatusPacket.MagicAttackIndex],
                statusFields[LegacyPlayerStatusPacket.MagicPercentIndex],
                statusFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex],
                spell);
            damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                updatedSession,
                spell,
                damage,
                LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);

            var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.Target.SobNpc, context.WorldData?.StaticStatues);
            var receiveGearOutcome = await TrySendLegacySobNpcReceiveCombatGearUpdateAsync(
                    context with { Session = updatedSession },
                    updatedSession,
                    target.Target.SobNpc,
                    cancellationToken)
                .ConfigureAwait(false);
            if (receiveGearOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return;
            }

            updatedSession = receiveGearOutcome.Session;
            context = context with { Session = updatedSession };
            (updatedSession, _) = await TrySendLegacySobNpcReceiveNinjaSageModeAsync(
                    context with { Session = updatedSession },
                    updatedSession,
                    context.WorldData,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = WithLegacySobNpcReceiveHitAttributes(context, updatedSession, target.Target.SobNpc);
            context = context with { Session = updatedSession };
            damage = ApplyLegacySobNpcReceiveDamageModifiers(updatedSession, damage);

            var result = sobNpcStates.ApplyDamage(
                target.Target.SobNpc,
                damage,
                now,
                deathBehavior);
            results.Add((target.Target.SobNpc, result, damage));
            if (result.AlreadyDead || result.DamageApplied == 0)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(target.Target.SobNpc.Id, nameof(target.Target.SobNpc.Id)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));
            if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(target.Target.SobNpc))
            {
                killedTrainingTargets.Add((target.Target.SobNpc, result));
            }
            else if (!LegacySobNpcEventRules.IsTrainingSob(target.Target.SobNpc))
            {
                eventSideEffectTargets.Add((target.Target.SobNpc, result, deathBehavior));
            }
        }

        if (animationTargets.Count != 0)
        {
            var animation = LegacySpellAnimationPacket.ForPlayerMagic(
                CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                CheckedUInt16(primary.X, nameof(primary.X)),
                CheckedUInt16(primary.Y, nameof(primary.Y)),
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                animationTargets);
            await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
                .ConfigureAwait(false);

            foreach (var killed in killedTrainingTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        killed.SobNpc,
                        sobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var eventTarget in eventSideEffectTargets)
            {
                updatedSession = await ApplyLegacySobNpcEventSideEffectsAsync(
                        context,
                        updatedSession,
                        eventTarget.SobNpc,
                        eventTarget.Result,
                        eventTarget.DeathBehavior,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedSession };
            }
        }

        var progressDamage = results.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        updatedSession = await ApplyLegacyDirectSobNpcIncreaseExperienceAsync(
                context,
                updatedSession,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "ChainBolt non-XPList SOB weapon proficiency experience")
            .ConfigureAwait(false);
        await LogLegacyChainBoltHandledAsync(
                context with { Session = updatedSession },
                spell,
                playerSpell,
                LegacyChainBoltPrimaryTargetKind.SobNpc,
                animationTargets.Count,
                progressDamage,
                results.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied))
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<(LegacyPlayerMonsterMagicTarget Target, bool CountsForProgress)>
        SelectLegacyChainBoltMonsterTargets(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyMagicRuntimeSpell spell,
            LegacyPlayerMonsterMagicTarget primary)
    {
        var selected = new List<(LegacyPlayerMonsterMagicTarget Target, bool CountsForProgress)>();
        if (CanLegacyPlayerAttackMonster(session.Character, primary.Monster, primary.MonsterFamily, out _))
        {
            selected.Add((primary, CountsForProgress: true));
        }

        var chainRange = spell.Range / 2;
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (candidate.Monster.Uid == primary.Monster.Uid ||
                LegacyGetDistance(primary.Monster.X, primary.Monster.Y, candidate.Monster.X, candidate.Monster.Y) >= chainRange ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            selected.Add((candidate, CountsForProgress: false));
        }

        return selected;
    }

    private static async ValueTask<IReadOnlyList<(LegacyPlayerMagicTarget Target, bool CountsForProgress)>>
        SelectLegacyChainBoltPlayerTargetsAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyMagicRuntimeSpell spell,
            WorldActiveSession primary,
            CancellationToken cancellationToken)
    {
        var selected = new List<(LegacyPlayerMagicTarget Target, bool CountsForProgress)>();
        if (CanLegacyPlayerMagicDamageTarget(
                context,
                session,
                primary.Session,
                spell,
                out _,
                out var failureEffect))
        {
            selected.Add((new LegacyPlayerMagicTarget(primary), CountsForProgress: false));
        }
        else if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
        {
            await SendScalarUpdateAsync(
                    context,
                    session.Character,
                    LegacyMsgUpdateDataType.ForbiddenPirate,
                    1,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
        {
            await TrySendLegacyPlayerHellVortexCounterAsync(context, session, primary, cancellationToken)
                .ConfigureAwait(false);
        }

        var chainRange = spell.Range / 3;
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (character.Uid == primary.Session.Character.Uid ||
                LegacyGetDistance(
                    primary.Session.Character.X,
                    primary.Session.Character.Y,
                    character.X,
                    character.Y) >= chainRange)
            {
                continue;
            }

            selected.Add((candidate, CountsForProgress: false));
        }

        return selected;
    }

    private static IReadOnlyList<(LegacyPlayerSobNpcMagicTarget Target, bool CountsForProgress)>
        SelectLegacyChainBoltSobNpcTargets(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyMagicRuntimeSpell spell,
            SobNpc primary)
    {
        var selected = new List<(LegacyPlayerSobNpcMagicTarget Target, bool CountsForProgress)>();
        if (CanLegacySobNpcMagicDamageTarget(context, session.Character, primary, out _))
        {
            selected.Add((new LegacyPlayerSobNpcMagicTarget(primary), CountsForProgress: true));
        }

        var chainRange = spell.Range / 2;
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if ((candidate.SobNpc.Id == primary.Id &&
                    candidate.SobNpc.MapId == primary.MapId &&
                    candidate.SobNpc.SourceLine == primary.SourceLine) ||
                LegacyGetDistance(primary.X, primary.Y, candidate.SobNpc.X, candidate.SobNpc.Y) >= chainRange ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            selected.Add((candidate, CountsForProgress: true));
        }

        return selected;
    }

    private static bool HasLegacyChainBoltPlayerAnimationResult(
        LegacyPlayerMagicDamageResult result,
        bool wildPhoenixDefended)
    {
        return wildPhoenixDefended ||
            result.DamageApplied != 0 ||
            result.HawksAmbitionConsumed ||
            result.DuelCleanupApplied ||
            result.AttackerAbsolutionConsumed ||
            result.TargetAbsolutionConsumed ||
            result.BloodTideActivated ||
            result.ReverseMagicApplied ||
            result.CapriceStaminaTransferred ||
            result.CapriceXpListRemoved ||
            result.XpBoosterApplied ||
            result.XpKillerApplied ||
            result.TidalWaveApplied ||
            result.QuenchApplied ||
            result.AttackHitApplied ||
            result.TournamentOutcome.IsActive ||
            result.MirrorOfSinResult.IsActive ||
            result.LightOfStaminaResult.IsActive ||
            result.CalmWindResult.IsActive ||
            result.DivineGuardResult.IsActive ||
            result.KillingFlashResult.IsActive ||
            result.DrainingTouchResult.IsActive ||
            result.BloodSpawnResult.IsActive ||
            result.ResponseEffectsResult.IsActive ||
            result.InfiniteMistResult.IsActive ||
            result.LightningShieldActivationLeft is not null ||
            result.LightningShieldDrainLeft is not null;
    }

    private static async ValueTask LogLegacyChainBoltHandledAsync(
        WorldPacketHandlerContext context,
        LegacyMagicRuntimeSpell spell,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyChainBoltPrimaryTargetKind primaryTargetKind,
        int animationTargetCount,
        ulong progressDamage,
        ulong totalAppliedDamage)
    {
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy ChainBolt non-XPList " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} spellLevel={playerSpell.Level} " +
                $"primaryTargetKind={primaryTargetKind} targets={animationTargetCount} progressDamage={progressDamage} " +
                $"totalAppliedDamage={totalAppliedDamage}; gameplayEvidence={LegacyChainBoltGameplayEvidence}.")
            .ConfigureAwait(false);
    }
}
