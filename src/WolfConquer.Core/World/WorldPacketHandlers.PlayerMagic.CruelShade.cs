using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCruelShadeSpellId = 3_050;
    private const ushort LegacyGapingWoundsSpellId = 11_230;
    private const int LegacyCruelShadeSortValue = 26;
    private const string LegacyCruelShadeSource = "DecLife.cs";
    private const string LegacyCruelShadeToxinEraserGap = "cruelshade-toxin-eraser-damage";
    private const string LegacyGapingWoundsToxinEraserGap = "gapingwounds-toxin-eraser-damage";
    private const string RuntimeLegacyGapingWoundsToxinEraserRollAttribute = "LegacyGapingWoundsToxinEraserRoll";

    private static async ValueTask<bool> HandleLegacyPlayerCruelShadeMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if ((int)spell.Sort != LegacyCruelShadeSortValue)
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

        if (spell.Id != LegacyCruelShadeSpellId &&
            spell.Id != LegacyGapingWoundsSpellId)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        reason:
                            "DecLife handling is currently modeled only for CruelShade spell 3050 and GapingWounds spell 11230 after the source CanUseSpell.Verified resource/cooldown gate."))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy DecLife magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    "unsupported DecLife spell id after CanUseSpell.Verified.")
                .ConfigureAwait(false);
            return true;
        }

        context = context with { Session = resourceSession };
        var updatedSession = resourceSession;
        var now = DateTimeOffset.UtcNow;
        var spellName = GetLegacyDecLifeSpellName(spell.Id);
        var selectedTargetKind = "none";
        var skippedReason = "opponent was not visible as monster, player, or SOB NPC";
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint RequestedDamage,
            ulong SpellProgressDamage)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();

        if (TryFindVisibleMonsterTargetByUid(context, updatedSession.Character, query.OpponentUid, out var monsterTarget))
        {
            selectedTargetKind = "monster";
            var distance = LegacyGetDistance(
                updatedSession.Character.X,
                updatedSession.Character.Y,
                monsterTarget.Monster.X,
                monsterTarget.Monster.Y);
            if (distance >= spell.Range)
            {
                skippedReason = $"monsterUid={monsterTarget.Monster.Uid} distance={distance} range={spell.Range}";
            }
            else if (!CanLegacyPlayerAttackMonster(
                         updatedSession.Character,
                         monsterTarget.Monster,
                         monsterTarget.MonsterFamily,
                         out var failureDiagnostic))
            {
                skippedReason = failureDiagnostic;
                await context.Log.WriteLineAsync(
                        $"WorldServer rejected legacy {spellName} monster magic " +
                        $"{FormatSession(context)} spellId={spell.Id} targetUid={monsterTarget.Monster.Uid}; " +
                        failureDiagnostic + ".")
                    .ConfigureAwait(false);
            }
            else if (IsLegacyGapingWoundsAnimationOnlyBossMonster(spell, monsterTarget.Monster))
            {
                var requestedDamage = CalculateLegacyDecLifeMonsterDamage(monsterTarget.Monster, spell);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    monsterTarget.Monster.Uid,
                    requestedDamage,
                    Hit: requestedDamage > 0,
                    MsgAttackEffect.None));
                skippedReason = string.Empty;
            }
            else if (context.MonsterStates is not { } monsterStates)
            {
                skippedReason = "monster runtime state is not loaded";
                await LogLegacyCruelShadeRuntimeStateGapAsync(
                        context,
                        spell,
                        targetKind: "monster",
                        mapId: monsterTarget.Monster.MapId,
                        targetUid: monsterTarget.Monster.Uid,
                        resource: MonsterRuntimeStateResource,
                        operation: $"{GetLegacyDecLifeDiagnosticKey(spell.Id)}-monster-magic")
                    .ConfigureAwait(false);
            }
            else
            {
                if (IsLegacyGuardMonsterFamily(monsterTarget.Monster, monsterTarget.MonsterFamily))
                {
                    updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                            context,
                            updatedSession,
                            cancellationToken)
                        .ConfigureAwait(false);
                    context = context with { Session = updatedSession };
                }

                var requestedDamage = CalculateLegacyDecLifeMonsterDamage(monsterTarget.Monster, spell);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    monsterTarget.Monster,
                    requestedDamage,
                    now,
                    updatedSession);
                monsterResults.Add((
                    monsterTarget,
                    result,
                    requestedDamage,
                    CalculateLegacyDecLifeMonsterSpellProgressDamage(
                        updatedSession,
                        monsterTarget,
                        requestedDamage)));

                if (!result.AlreadyDead)
                {
                    animationTargets.Add(new LegacySpellAnimationTarget(
                        monsterTarget.Monster.Uid,
                        result.RequestedDamage,
                        Hit: result.RequestedDamage > 0,
                        MsgAttackEffect.None));
                    skippedReason = string.Empty;
                }
            }
        }
        else if (TryGetVisibleSessionByUid(context, updatedSession, query.OpponentUid, out var playerTarget))
        {
            selectedTargetKind = "player";
            var distance = LegacyGetDistance(
                updatedSession.Character.X,
                updatedSession.Character.Y,
                playerTarget.Session.Character.X,
                playerTarget.Session.Character.Y);
            if (distance >= spell.Range)
            {
                skippedReason = $"playerUid={query.OpponentUid} distance={distance} range={spell.Range}";
            }
            else if (!CanLegacyPlayerMagicDamageTarget(
                         context,
                         updatedSession,
                         playerTarget.Session,
                         spell,
                         out var failureDiagnostic,
                         out var failureEffect))
            {
                skippedReason = failureDiagnostic;
                if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
                {
                    await SendScalarUpdateAsync(
                            context,
                            updatedSession.Character,
                            LegacyMsgUpdateDataType.ForbiddenPirate,
                            1,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
                {
                    await TrySendLegacyPlayerHellVortexCounterAsync(
                            context,
                            updatedSession,
                            playerTarget,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await context.Log.WriteLineAsync(
                        $"WorldServer rejected legacy {spellName} player magic " +
                        $"{FormatSession(context)} spellId={spell.Id} targetUid={playerTarget.Session.Character.Uid}; " +
                        failureDiagnostic + ".")
                    .ConfigureAwait(false);
            }
            else if (spell.Id == LegacyGapingWoundsSpellId &&
                HasLegacyGapingWoundsToxinEraserAvoidance(playerTarget.Session.Character))
            {
                skippedReason =
                    $"playerUid={playerTarget.Session.Character.Uid} active ToxinEraser";
                await context.Log.WriteLineAsync(
                        "WorldServer skipped legacy GapingWounds player magic " +
                        $"{FormatSession(context)} spellId={spell.Id} targetUid={playerTarget.Session.Character.Uid}; " +
                        "source PerfectionEffects.ToxinEraser rejected target.")
                    .ConfigureAwait(false);
            }
            else
            {
                updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                        context,
                        updatedSession,
                        [new LegacyPlayerMagicTarget(playerTarget)],
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedSession };

                double toxinEraser = 0;
                var toxinEraserModeled = true;
                var requestedDamage = spell.Id == LegacyGapingWoundsSpellId
                    ? CalculateLegacyGapingWoundsPlayerDamage(playerTarget.Session)
                    : CalculateLegacyCruelShadePlayerDamage(
                        playerTarget.Session,
                        out toxinEraser,
                        out toxinEraserModeled);
                if (spell.Id != LegacyGapingWoundsSpellId && !toxinEraserModeled)
                {
                    await LogLegacyCruelShadeToxinEraserGapAsync(
                            context,
                            spell,
                            playerTarget.Session,
                            toxinEraser)
                        .ConfigureAwait(false);
                }

                var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    playerTarget,
                    updatedSession.Character,
                    requestedDamage,
                    cancellationToken)
                .ConfigureAwait(false);
                if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
                {
                    updatedSession = currentReceiveAttacker;
                    context = context with { Session = updatedSession };
                }

                if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                {
                    return true;
                }

                if (receiveOutcome.Result is not { } result)
                {
                    goto PlayerReceiveComplete;
                }
                if (result.UpdatedAttackerSession is not null)
                {
                    updatedSession = result.UpdatedAttackerSession;
                    context = context with { Session = updatedSession };
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                    result.DamageApplied,
                    Hit: result.DamageApplied > 0,
                    MsgAttackEffect.None));
                skippedReason = string.Empty;

                if (result.DamageApplied > 0)
                {
                    playerResults.Add(result);
                }
                PlayerReceiveComplete:
                    ;
            }
        }
        else if (TryFindVisibleSobNpcByUid(context, updatedSession.Character, query.OpponentUid, out var sobNpcTarget))
        {
            selectedTargetKind = "sob-npc";
            var distance = LegacyGetDistance(
                updatedSession.Character.X,
                updatedSession.Character.Y,
                sobNpcTarget.X,
                sobNpcTarget.Y);
            if (distance >= spell.Range)
            {
                skippedReason = $"sobNpcUid={sobNpcTarget.Id} distance={distance} range={spell.Range}";
            }
            else if (!CanLegacySobNpcMagicDamageTarget(context, updatedSession.Character, sobNpcTarget, out var failureDiagnostic))
            {
                skippedReason = failureDiagnostic;
                if (TryCreateLegacySobNpcMagicGateDialog(
                        updatedSession.Character,
                        sobNpcTarget,
                        failureDiagnostic,
                        out var gateDialog))
                {
                    await context.Connection.SendAsync(gateDialog.ToFrameBytes(), cancellationToken)
                        .ConfigureAwait(false);
                }

                await context.Log.WriteLineAsync(
                        $"WorldServer rejected legacy {spellName} SOB magic " +
                        $"{FormatSession(context)} spellId={spell.Id} targetUid={sobNpcTarget.Id}; " +
                        failureDiagnostic + ".")
                    .ConfigureAwait(false);
            }
            else if (context.SobNpcStates is not { } sobNpcStates)
            {
                skippedReason = "SOB runtime state is not loaded";
                await LogLegacyCruelShadeRuntimeStateGapAsync(
                        context,
                        spell,
                        targetKind: "sob-npc",
                        mapId: sobNpcTarget.MapId,
                        targetUid: CheckedUInt32(sobNpcTarget.Id, nameof(sobNpcTarget.Id)),
                        resource: SobNpcRuntimeStateResource,
                        operation: $"{GetLegacyDecLifeDiagnosticKey(spell.Id)}-sob-magic")
                    .ConfigureAwait(false);
            }
            else
            {
                var visibleSobNpc = sobNpcStates.GetVisible(sobNpcTarget);
                var requestedDamage = CalculateLegacyDecLifeSobNpcDamage(visibleSobNpc, spell);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpcTarget);
                var result = sobNpcStates.ApplyDamage(sobNpcTarget, requestedDamage, now, deathBehavior);
                sobNpcResults.Add((sobNpcTarget, result, deathBehavior));

                if (!result.AlreadyDead)
                {
                    animationTargets.Add(new LegacySpellAnimationTarget(
                        CheckedUInt32(sobNpcTarget.Id, nameof(sobNpcTarget.Id)),
                        result.DamageApplied,
                        Hit: result.DamageApplied > 0,
                        MsgAttackEffect.None));
                    skippedReason = string.Empty;
                }

                if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(sobNpcTarget))
                {
                    killedTrainingSobNpcTargets.Add((sobNpcTarget, result));
                }
                else if (!LegacySobNpcEventRules.IsTrainingSob(sobNpcTarget) && !result.AlreadyDead)
                {
                    eventSobNpcSideEffectTargets.Add((sobNpcTarget, result, deathBehavior));
                }
            }
        }

        var animation = new LegacySpellAnimationPacket(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.OpponentUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, requestedDamage, _) in monsterResults)
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
                if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
                {
                    var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                    foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                    {
                        await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                if (result.DamageApplied > 0)
                {
                    firstRetaliationTarget ??= target;
                }
            }

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

        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var (sobNpc, _) in killedTrainingSobNpcTargets)
        {
            if (context.SobNpcStates is { } sobNpcStates)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        sobNpc,
                        sobNpcStates.GetVisible(sobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var (sobNpc, result, _) in eventSobNpcSideEffectTargets)
        {
            await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, result, spell)
                .ConfigureAwait(false);
        }

        var totalMonsterProgressDamage = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.SpellProgressDamage);
        var totalSobNpcExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var totalAppliedDamage =
            monsterResults.Where(result => !result.Result.AlreadyDead).Aggregate(
                0UL,
                (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied) +
            sobNpcResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);

        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalMonsterProgressDamage + totalSobNpcExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer handled legacy {spellName} magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targetKind={selectedTargetKind} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"spellProgressDamage={totalMonsterProgressDamage + totalSobNpcExperienceDamage} " +
                $"totalAppliedDamage={totalAppliedDamage}" +
                (string.IsNullOrEmpty(skippedReason) ? string.Empty : $" skippedReason=\"{skippedReason}\"") +
                $"; source={LegacyCruelShadeSource} spellId={spell.Id} direct OpponentUID runtime slice.")
            .ConfigureAwait(false);
        return true;
    }

    private static string GetLegacyDecLifeSpellName(ushort spellId)
    {
        return spellId == LegacyGapingWoundsSpellId ? "GapingWounds" : "CruelShade";
    }

    private static string GetLegacyDecLifeDiagnosticKey(ushort spellId)
    {
        return spellId == LegacyGapingWoundsSpellId ? "gapingwounds" : "cruelshade";
    }

    private static string GetLegacyDecLifeToxinEraserGapKey(ushort spellId)
    {
        return spellId == LegacyGapingWoundsSpellId
            ? LegacyGapingWoundsToxinEraserGap
            : LegacyCruelShadeToxinEraserGap;
    }

    private static bool IsLegacyGapingWoundsAnimationOnlyBossMonster(
        LegacyMagicRuntimeSpell spell,
        LegacyMonsterInstance monster)
    {
        return spell.Id == LegacyGapingWoundsSpellId && monster.Boss;
    }

    private static uint CalculateLegacyDecLifeMonsterDamage(
        LegacyMonsterInstance monster,
        LegacyMagicRuntimeSpell spell)
    {
        if (monster.Boss)
        {
            return 1;
        }

        if (monster.Hitpoints <= 1)
        {
            return 0;
        }

        return CalculateLegacyDecLifeCurrentHitpointDamage(monster.Hitpoints, spell);
    }

    private static uint CalculateLegacyDecLifeCurrentHitpointDamage(
        uint currentHitpoints,
        LegacyMagicRuntimeSpell spell)
    {
        var percent = spell.Damage >= 100 ? 0 : 100 - spell.Damage;
        return MulDivToUInt32(currentHitpoints, percent, 100);
    }

    private static ulong CalculateLegacyDecLifeMonsterSpellProgressDamage(
        WorldSession session,
        LegacyPlayerMonsterMagicTarget target,
        uint requestedDamage)
    {
        if (requestedDamage == 0 ||
            LegacyMonsterDamageRules.IsLegacyGuardFamily(target.MonsterFamily))
        {
            return 0;
        }

        return checked((ulong)AdjustLegacyMonsterExperience(
            checked((int)Math.Min(requestedDamage, target.Monster.MaxLife)),
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            target.Monster.Level));
    }

    private static uint CalculateLegacyCruelShadePlayerDamage(
        WorldSession targetSession,
        out double toxinEraser,
        out bool toxinEraserModeled)
    {
        return CalculateLegacyDecLifePlayerDamage(
            targetSession,
            defaultMissingToxinEraser: false,
            out toxinEraser,
            out toxinEraserModeled);
    }

    private static uint CalculateLegacyGapingWoundsPlayerDamage(WorldSession targetSession)
    {
        return CalculateLegacyDecLifePlayerDamage(
            targetSession,
            defaultMissingToxinEraser: true,
            out _,
            out _);
    }

    private static uint CalculateLegacyDecLifePlayerDamage(
        WorldSession targetSession,
        bool defaultMissingToxinEraser,
        out double toxinEraser,
        out bool toxinEraserModeled)
    {
        var currentHitpoints = CheckedUInt32(
            targetSession.Character.MinHitPoints,
            nameof(targetSession.Character.MinHitPoints));
        if (currentHitpoints <= 1)
        {
            toxinEraser = 0;
            toxinEraserModeled = true;
            return 0;
        }

        toxinEraserModeled = TryReadLegacyCruelShadeToxinEraser(targetSession.Character, out toxinEraser);
        if (!toxinEraserModeled && defaultMissingToxinEraser)
        {
            toxinEraser = 0;
            toxinEraserModeled = true;
        }

        var reduction = Math.Min(99.0, toxinEraser);
        if (reduction < 0)
        {
            reduction = 0;
        }

        var percent = checked((uint)Math.Clamp((int)(100.0 - reduction), 0, 100));
        return MulDivToUInt32(currentHitpoints, percent, 100);
    }

    private static bool TryReadLegacyCruelShadeToxinEraser(
        CharacterSnapshot character,
        out double toxinEraser)
    {
        return TryReadDoubleAttribute(
            character.Attributes,
            out toxinEraser,
            "PerfectionStatus.ToxinEraser",
            "LegacyPerfectionStatus.ToxinEraser",
            "ToxinEraser",
            "PerfectionToxinEraser",
            "ToxinEraserPercent",
            "LegacyToxinEraser");
    }

    private static bool HasLegacyGapingWoundsToxinEraserAvoidance(CharacterSnapshot character)
    {
        if (TryReadLegacyGapingWoundsToxinEraserActive(
                character.Attributes,
                out var active,
                "LegacyGapingWoundsToxinEraser",
                "GapingWoundsToxinEraser",
                "ToxinEraserAvoidsGapingWounds",
                "LegacyToxinEraserAvoidsGapingWounds",
                "PerfectionEffects.ToxinEraser",
                "LegacyPerfectionEffects.ToxinEraser",
                "ToxinEraserActive",
                "LegacyToxinEraserActive",
                "PerfectionToxinEraserActive"))
        {
            return active;
        }

        if (!TryReadLegacyCruelShadeToxinEraser(character, out var toxinEraser) ||
            toxinEraser <= 0)
        {
            return false;
        }

        if (toxinEraser >= 100)
        {
            return true;
        }

        var roll = TryReadUInt32Attribute(
                character.Attributes,
                out var forcedRoll,
                RuntimeLegacyGapingWoundsToxinEraserRollAttribute,
                "GapingWoundsToxinEraserRoll",
                "LegacyToxinEraserRoll",
                "ToxinEraserRoll")
            ? forcedRoll % 100
            : Random.Shared.NextDouble() * 100.0;
        return roll < toxinEraser;
    }

    private static bool TryReadLegacyGapingWoundsToxinEraserActive(
        IReadOnlyDictionary<string, string> attributes,
        out bool active,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!attributes.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                raw == "1")
            {
                active = true;
                return true;
            }

            if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                raw == "0")
            {
                active = false;
                return true;
            }
        }

        active = false;
        return false;
    }

    private static uint CalculateLegacyDecLifeSobNpcDamage(
        SobNpc visibleSobNpc,
        LegacyMagicRuntimeSpell spell)
    {
        var currentHitpoints = CheckedUInt32(visibleSobNpc.Life, nameof(visibleSobNpc.Life));
        if (currentHitpoints <= 1)
        {
            return 0;
        }

        if (spell.Id == LegacyGapingWoundsSpellId)
        {
            return CalculateLegacyDecLifeCurrentHitpointDamage(currentHitpoints, spell);
        }

        return MulDivToUInt32(currentHitpoints, spell.Damage % 100, 100);
    }

    private static async ValueTask LogLegacyCruelShadeRuntimeStateGapAsync(
        WorldPacketHandlerContext context,
        LegacyMagicRuntimeSpell spell,
        string targetKind,
        int mapId,
        uint targetUid,
        string resource,
        string operation)
    {
        var spellName = GetLegacyDecLifeSpellName(spell.Id);
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    resource,
                    operation,
                    $"{spellName} target was found and passed source range/attack gates, but runtime state is not loaded",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", mapId),
                        LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                        LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                        LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        LegacyParityGapDiagnostics.Fact("targetKind", targetKind),
                        LegacyParityGapDiagnostics.Fact("targetUid", targetUid),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                        LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        LegacyParityGapDiagnostics.Fact("targetKind", targetKind),
                    ]))
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"PARITY-GAP {spellName} runtime damage skipped " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetKind={targetKind} targetUid={targetUid} resource={resource}; " +
                $"source={LegacyCruelShadeSource} requires mutable target HP.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyCruelShadeToxinEraserGapAsync(
        WorldPacketHandlerContext context,
        LegacyMagicRuntimeSpell spell,
        WorldSession targetSession,
        double toxinEraser)
    {
        var spellName = GetLegacyDecLifeSpellName(spell.Id);
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.Create(
                    LegacyParityGapCategory.Magic,
                    GetLegacyDecLifeToxinEraserGapKey(spell.Id),
                    $"{spellName} source overwrites player target damage with target.Owner.PerfectionStatus.ToxinEraser; no local target perfection attribute was present, so the source default 0 percent reduction was used.",
                    severity: LegacyParityGapSeverity.Info,
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                        LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                        LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        LegacyParityGapDiagnostics.Fact("targetKind", "player"),
                        LegacyParityGapDiagnostics.Fact("targetUid", targetSession.Character.Uid),
                        LegacyParityGapDiagnostics.Fact("toxinEraser", toxinEraser),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        LegacyParityGapDiagnostics.Fact("targetKind", "player"),
                    ]))
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"PARITY-GAP {spellName} player ToxinEraser missing " +
                $"{FormatSession(context)} spellId={spell.Id} targetUid={targetSession.Character.Uid}; " +
                $"source={LegacyCruelShadeSource} used local default toxinEraser=0.")
            .ConfigureAwait(false);
    }

}
